using Microsoft.Win32;
using System.Globalization;
using System.Text;

namespace UacMaintenanceTray;

internal static class UacPolicyManager
{
    internal const int DefaultMaintenanceHours = 6;

    private const string PolicySubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
    private const string StateSubKey = @"SOFTWARE\UacMaintenanceTray";
    private const string TaskName = "UacMaintenanceTray-AutoRestore";
    private const string ConsentValueName = "ConsentPromptBehaviorAdmin";
    private const string OriginalValueName = "OriginalConsentPromptBehaviorAdmin";
    private const string EnabledAtName = "EnabledAtUtc";
    private const string RestoreAtName = "RestoreAtUtc";
    private const string GenerationIdName = "GenerationId";

    internal static UacStatus GetStatus()
    {
        using var policyKey = Registry.LocalMachine.OpenSubKey(PolicySubKey, writable: false)
            ?? throw new InvalidOperationException("找不到 Windows UAC 策略注册表项。");

        var enableLua = Convert.ToInt32(policyKey.GetValue("EnableLUA", 1), CultureInfo.InvariantCulture);
        var consent = Convert.ToInt32(policyKey.GetValue(ConsentValueName, 5), CultureInfo.InvariantCulture);

        using var stateKey = Registry.LocalMachine.OpenSubKey(StateSubKey, writable: false);
        if (stateKey is null)
        {
            return new UacStatus(enableLua, consent, false, null, null, null, null);
        }

        var original = Convert.ToInt32(stateKey.GetValue(OriginalValueName, 5), CultureInfo.InvariantCulture);
        var enabledAt = ParseRegistryDate(stateKey.GetValue(EnabledAtName) as string);
        var restoreAt = ParseRegistryDate(stateKey.GetValue(RestoreAtName) as string);
        var generationId = stateKey.GetValue(GenerationIdName) as string;
        return new UacStatus(enableLua, consent, true, enabledAt, restoreAt, original, generationId);
    }

    internal static void EnableMaintenanceMode(int hours)
    {
        EnsureElevated();
        hours = Math.Clamp(hours, 1, 24);

        using var policyKey = Registry.LocalMachine.OpenSubKey(PolicySubKey, writable: true)
            ?? throw new InvalidOperationException("无法打开 Windows UAC 策略注册表项。");

        var enableLua = Convert.ToInt32(policyKey.GetValue("EnableLUA", 1), CultureInfo.InvariantCulture);
        if (enableLua != 1)
        {
            throw new InvalidOperationException("EnableLUA 不是 1。为避免覆盖非标准配置，操作已停止。");
        }

        var currentConsent = Convert.ToInt32(policyKey.GetValue(ConsentValueName, 5), CultureInfo.InvariantCulture);
        var existingStatus = GetStatus();
        if (existingStatus.HasManagedState)
        {
            throw new InvalidOperationException("已存在本工具创建的维护状态。请先恢复或清理该状态。");
        }

        if (currentConsent == 0)
        {
            throw new InvalidOperationException("当前已经是无提示提升状态，但并非由本工具管理；操作已停止。");
        }

        var originalConsent = currentConsent;

        var enabledAt = DateTimeOffset.UtcNow;
        var restoreAt = enabledAt.AddHours(hours);
        var generationId = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);
        var taskRegistered = false;

        try
        {
            using (var stateKey = Registry.LocalMachine.CreateSubKey(StateSubKey, writable: true)
                ?? throw new InvalidOperationException("无法创建维护状态注册表项。"))
            {
                stateKey.SetValue(OriginalValueName, originalConsent, RegistryValueKind.DWord);
                stateKey.SetValue(EnabledAtName, enabledAt.ToString("O", CultureInfo.InvariantCulture), RegistryValueKind.String);
                stateKey.SetValue(RestoreAtName, restoreAt.ToString("O", CultureInfo.InvariantCulture), RegistryValueKind.String);
                stateKey.SetValue(GenerationIdName, generationId, RegistryValueKind.String);
                stateKey.SetValue("Version", "1.1.0", RegistryValueKind.String);
            }

            RegisterAutoRestore(originalConsent, restoreAt, generationId);
            taskRegistered = true;
            policyKey.SetValue(ConsentValueName, 0, RegistryValueKind.DWord);
            AppLog.Info($"Maintenance mode enabled. Generation={generationId}; Original={originalConsent}; RestoreAt={restoreAt:O}");
        }
        catch
        {
            var taskDeleted = !taskRegistered || TryDeleteRestoreTask();
            if (taskDeleted)
            {
                Registry.LocalMachine.DeleteSubKeyTree(StateSubKey, throwOnMissingSubKey: false);
            }

            throw;
        }
    }

    internal static void RestoreOriginalPolicy()
    {
        EnsureElevated();
        var status = GetStatus();
        if (!status.HasManagedState)
        {
            throw new InvalidOperationException("没有本工具创建的维护状态，拒绝覆盖现有 UAC 策略。");
        }

        var original = status.OriginalConsentPromptBehaviorAdmin ?? 5;
        using var policyKey = Registry.LocalMachine.OpenSubKey(PolicySubKey, writable: true)
            ?? throw new InvalidOperationException("无法打开 Windows UAC 策略注册表项。");
        policyKey.SetValue(ConsentValueName, original, RegistryValueKind.DWord);

        if (!TryDeleteRestoreTask())
        {
            AppLog.Error("Policy was restored, but the auto-restore task could not be removed. Managed state was retained for safe cleanup.");
            throw new InvalidOperationException("UAC 策略已恢复，但自动恢复任务未能确认删除。状态记录已保留，请稍后重试。");
        }

        Registry.LocalMachine.DeleteSubKeyTree(StateSubKey, throwOnMissingSubKey: false);
        AppLog.Info($"UAC policy restored to {original}.");
    }

    private static void RegisterAutoRestore(int originalValue, DateTimeOffset restoreAt, string generationId)
    {
        var restoreScript = $$"""
            $ErrorActionPreference = 'Stop'
            $statePath = 'HKLM:\SOFTWARE\UacMaintenanceTray'
            $policyPath = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System'
            try {
                $state = Get-ItemProperty -LiteralPath $statePath -ErrorAction Stop
                if ($state.GenerationId -eq '{{generationId}}') {
                    $current = (Get-ItemProperty -LiteralPath $policyPath -Name 'ConsentPromptBehaviorAdmin' -ErrorAction Stop).ConsentPromptBehaviorAdmin
                    if ($current -eq 0) {
                        Set-ItemProperty -LiteralPath $policyPath -Name 'ConsentPromptBehaviorAdmin' -Type DWord -Value {{originalValue}}
                    }
                    Remove-Item -LiteralPath $statePath -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
            finally {
                Unregister-ScheduledTask -TaskName '{{TaskName}}' -Confirm:$false -ErrorAction SilentlyContinue
            }
            """;
        var encodedRestore = Convert.ToBase64String(Encoding.Unicode.GetBytes(restoreScript));
        var restoreAtText = restoreAt.LocalDateTime.ToString("O", CultureInfo.InvariantCulture);

        var registerScript = $$"""
            $ErrorActionPreference = 'Stop'
            $action = New-ScheduledTaskAction -Execute "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Argument '-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand {{encodedRestore}}'
            $trigger = New-ScheduledTaskTrigger -Once -At ([datetime]::Parse('{{restoreAtText}}', [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind))
            $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
            $settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Minutes 5)
            Register-ScheduledTask -TaskName '{{TaskName}}' -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
            """;

        PowerShellRunner.RunEncoded(registerScript);
    }

    private static bool TryDeleteRestoreTask()
    {
        try
        {
            var script = $$"""
                $task = Get-ScheduledTask -TaskName '{{TaskName}}' -ErrorAction SilentlyContinue
                if ($null -ne $task) {
                    Unregister-ScheduledTask -TaskName '{{TaskName}}' -Confirm:$false -ErrorAction Stop
                }
                if ($null -ne (Get-ScheduledTask -TaskName '{{TaskName}}' -ErrorAction SilentlyContinue)) {
                    throw 'The auto-restore task still exists after deletion.'
                }
                """;
            PowerShellRunner.RunEncoded(script);
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to confirm deletion of the auto-restore task.", ex);
            return false;
        }
    }

    private static DateTimeOffset? ParseRegistryDate(string? value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result)
            ? result
            : null;
    }

    private static void EnsureElevated()
    {
        if (!ElevationBroker.IsElevated())
        {
            throw new UnauthorizedAccessException("该操作必须由提升后的管理员进程执行。");
        }
    }
}

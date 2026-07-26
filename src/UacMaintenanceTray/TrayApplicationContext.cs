using System.Diagnostics;

namespace UacMaintenanceTray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly LocalizationManager _localization;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _enableItem;
    private readonly ToolStripMenuItem _restoreItem;
    private readonly ToolStripMenuItem _languageItem;
    private readonly ToolStripMenuItem _systemLanguageItem;
    private readonly ToolStripMenuItem _englishLanguageItem;
    private readonly ToolStripMenuItem _chineseLanguageItem;
    private readonly ToolStripMenuItem _logItem;
    private readonly ToolStripMenuItem _exitItem;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly Icon _appIcon;
    private bool _operationInProgress;

    internal TrayApplicationContext()
    {
        _localization = new LocalizationManager();
        _statusItem = new ToolStripMenuItem { Enabled = false };
        _enableItem = new ToolStripMenuItem();
        _restoreItem = new ToolStripMenuItem();
        _languageItem = new ToolStripMenuItem();
        _systemLanguageItem = new ToolStripMenuItem();
        _englishLanguageItem = new ToolStripMenuItem();
        _chineseLanguageItem = new ToolStripMenuItem();
        _logItem = new ToolStripMenuItem();
        _exitItem = new ToolStripMenuItem();

        _enableItem.Click += async (_, _) => await EnableMaintenanceModeAsync();
        _restoreItem.Click += async (_, _) => await RestorePolicyAsync();
        _systemLanguageItem.Click += (_, _) => ChangeLanguage(LocalizationManager.SystemLanguage);
        _englishLanguageItem.Click += (_, _) => ChangeLanguage(LocalizationManager.EnglishLanguage);
        _chineseLanguageItem.Click += (_, _) => ChangeLanguage(LocalizationManager.ChineseLanguage);
        _logItem.Click += (_, _) => OpenLogDirectory();
        _exitItem.Click += (_, _) => ExitApplication();

        _languageItem.DropDownItems.AddRange([
            _systemLanguageItem,
            new ToolStripSeparator(),
            _englishLanguageItem,
            _chineseLanguageItem
        ]);

        var menu = new ContextMenuStrip();
        menu.Items.AddRange([
            _statusItem,
            new ToolStripSeparator(),
            _enableItem,
            _restoreItem,
            new ToolStripSeparator(),
            _languageItem,
            _logItem,
            _exitItem
        ]);

        _appIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not read the application icon."))
            ?? throw new InvalidOperationException("The application has no usable icon.");

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = _appIcon,
            Visible = true
        };

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _refreshTimer.Tick += (_, _) => RefreshStatus();
        _refreshTimer.Start();

        ApplyLocalization();
        AppLog.Info($"Tray application started. Language={_localization.ActiveLanguage}; Preference={_localization.Preference}");
        RefreshStatus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _appIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task EnableMaintenanceModeAsync()
    {
        if (_operationInProgress)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            _localization.Get("ConfirmBody"),
            _localization.Get("ConfirmTitle"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        SetBusy(true, _localization.Get("BusyWaitingElevation"));
        var result = await Task.Run(() => ElevationBroker.RunElevated("--enable", UacPolicyManager.DefaultMaintenanceHours));
        SetBusy(false, null);

        if (!result.Succeeded)
        {
            ShowError(FormatElevationError(result, "EnableError"));
        }

        RefreshStatus();
    }

    private async Task RestorePolicyAsync()
    {
        if (_operationInProgress)
        {
            return;
        }

        SetBusy(true, _localization.Get("BusyRestoring"));
        var result = await Task.Run(() => ElevationBroker.RunElevated("--restore"));
        SetBusy(false, null);

        if (!result.Succeeded)
        {
            ShowError(FormatElevationError(result, "RestoreError"));
        }

        RefreshStatus();
    }

    private void ChangeLanguage(string language)
    {
        try
        {
            _localization.SetPreference(language);
            ApplyLocalization();
            AppLog.Info($"Language changed. Language={_localization.ActiveLanguage}; Preference={_localization.Preference}");
            RefreshStatus();
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to change language", ex);
            ShowError(ex.Message);
        }
    }

    private void ApplyLocalization()
    {
        _statusItem.Text = _localization.Get("StatusReading");
        _enableItem.Text = _localization.Get("EnableMaintenance");
        _restoreItem.Text = _localization.Get("RestorePolicy");
        _languageItem.Text = _localization.Get("Language");
        _systemLanguageItem.Text = _localization.Get("LanguageSystem");
        _englishLanguageItem.Text = _localization.Get("LanguageEnglishName");
        _chineseLanguageItem.Text = _localization.Get("LanguageChineseName");
        _logItem.Text = _localization.Get("OpenLogs");
        _exitItem.Text = _localization.Get("Exit");
        _systemLanguageItem.Checked = _localization.Preference == LocalizationManager.SystemLanguage;
        _englishLanguageItem.Checked = _localization.Preference == LocalizationManager.EnglishLanguage;
        _chineseLanguageItem.Checked = _localization.Preference == LocalizationManager.ChineseLanguage;
        _notifyIcon.Text = TruncateTooltip(_localization.Get("TrayStatus", _localization.Get("StatusReading")));
    }

    private void RefreshStatus()
    {
        if (_operationInProgress)
        {
            return;
        }

        try
        {
            var status = UacPolicyManager.GetStatus();
            var description = FormatStatus(status);
            _statusItem.Text = description;
            _notifyIcon.Text = TruncateTooltip(_localization.Get("TrayStatus", description));
            _notifyIcon.Icon = _appIcon;
            _enableItem.Enabled = status.EnableLua == 1 && status.ConsentPromptBehaviorAdmin != 0;
            _restoreItem.Enabled = status.HasManagedState;
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to refresh UAC status", ex);
            var failed = _localization.Get("StatusReadFailed");
            _statusItem.Text = failed;
            _notifyIcon.Text = TruncateTooltip(_localization.Get("TrayStatus", failed));
            _enableItem.Enabled = false;
            _restoreItem.Enabled = false;
        }
    }

    private string FormatStatus(UacStatus status)
    {
        if (status.IsMaintenanceActive)
        {
            if (status.RestoreAtUtc is { } restoreAt)
            {
                var remaining = restoreAt - DateTimeOffset.UtcNow;
                if (remaining > TimeSpan.Zero)
                {
                    var formatted = remaining.TotalHours >= 1
                        ? _localization.Get("RemainingHoursMinutes", (int)remaining.TotalHours, remaining.Minutes)
                        : _localization.Get("RemainingMinutes", Math.Max(1, remaining.Minutes));
                    return _localization.Get("StatusMaintenanceRemaining", formatted);
                }
            }

            return _localization.Get("StatusMaintenanceWaiting");
        }

        if (status.ConsentPromptBehaviorAdmin == 0)
        {
            return _localization.Get("StatusUnmanagedNoPrompt");
        }

        return status.HasManagedState
            ? _localization.Get("StatusRestoredPendingCleanup")
            : _localization.Get("StatusNormal");
    }

    private string FormatElevationError(ElevationResult result, string fallbackKey)
    {
        return result.Failure switch
        {
            ElevationFailure.Cancelled => _localization.Get("ElevationCancelled"),
            ElevationFailure.TimedOut => _localization.Get("ElevationTimedOut"),
            ElevationFailure.ChildFailed => _localization.Get("ElevationChildFailed", result.ExitCode),
            ElevationFailure.StartFailed => _localization.Get("ElevationStartFailed", result.Detail ?? fallbackKey),
            _ => _localization.Get(fallbackKey)
        };
    }

    private void SetBusy(bool busy, string? statusText)
    {
        _operationInProgress = busy;
        _enableItem.Enabled = !busy;
        _restoreItem.Enabled = !busy;
        _languageItem.Enabled = !busy;
        if (statusText is not null)
        {
            _statusItem.Text = statusText;
        }
    }

    private static string TruncateTooltip(string value) => value.Length <= 63 ? value : value[..63];

    private static void OpenLogDirectory()
    {
        var directory = Path.GetDirectoryName(AppLog.FilePath)!;
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo("explorer.exe", directory) { UseShellExecute = true });
    }

    private void ShowError(string message)
    {
        MessageBox.Show(message, _localization.Get("ApplicationTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void ExitApplication()
    {
        _notifyIcon.Visible = false;
        AppLog.Info("Tray application exited.");
        ExitThread();
    }
}

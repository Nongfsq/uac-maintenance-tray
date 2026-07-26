using System.Diagnostics;

namespace UacMaintenanceTray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _enableItem;
    private readonly ToolStripMenuItem _restoreItem;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly Icon _appIcon;
    private bool _operationInProgress;

    internal TrayApplicationContext()
    {
        _statusItem = new ToolStripMenuItem("状态：正在读取…") { Enabled = false };
        _enableItem = new ToolStripMenuItem("启用管理员维护模式（6 小时）");
        _restoreItem = new ToolStripMenuItem("立即恢复原 UAC 策略");
        var logItem = new ToolStripMenuItem("打开日志目录");
        var exitItem = new ToolStripMenuItem("退出托盘程序");

        _enableItem.Click += async (_, _) => await EnableMaintenanceModeAsync();
        _restoreItem.Click += async (_, _) => await RestorePolicyAsync();
        logItem.Click += (_, _) => OpenLogDirectory();
        exitItem.Click += (_, _) => ExitApplication();

        var menu = new ContextMenuStrip();
        menu.Items.AddRange([
            _statusItem,
            new ToolStripSeparator(),
            _enableItem,
            _restoreItem,
            new ToolStripSeparator(),
            logItem,
            exitItem
        ]);

        _appIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath
            ?? throw new InvalidOperationException("无法读取程序图标。"))
            ?? throw new InvalidOperationException("程序没有可用图标。");

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = _appIcon,
            Text = "UAC 维护模式：正在读取状态",
            Visible = true
        };

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _refreshTimer.Tick += (_, _) => RefreshStatus();
        _refreshTimer.Start();

        AppLog.Info("Tray application started.");
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
            "维护模式会临时取消管理员账户的 UAC 同意提示。此期间，任何以管理员权限启动的程序都可能直接获得高权限。\n\n程序会安排自动恢复，但意外关机或系统修改仍可能影响恢复。是否继续启用 6 小时？",
            "确认降低 UAC 保护",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        SetBusy(true, "状态：等待管理员授权…");
        var result = await Task.Run(() => ElevationBroker.RunElevated("--enable", UacPolicyManager.DefaultMaintenanceHours));
        SetBusy(false, null);

        if (!result.Succeeded)
        {
            ShowError(result.Error ?? "无法启用维护模式。");
        }

        RefreshStatus();
    }

    private async Task RestorePolicyAsync()
    {
        if (_operationInProgress)
        {
            return;
        }

        SetBusy(true, "状态：正在恢复…");
        var result = await Task.Run(() => ElevationBroker.RunElevated("--restore"));
        SetBusy(false, null);

        if (!result.Succeeded)
        {
            ShowError(result.Error ?? "无法恢复 UAC 策略。");
        }

        RefreshStatus();
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
            _statusItem.Text = $"状态：{status.Description}";
            _notifyIcon.Text = TruncateTooltip($"UAC 维护模式：{status.Description}");
            _notifyIcon.Icon = _appIcon;
            _enableItem.Enabled = status.EnableLua == 1 && status.ConsentPromptBehaviorAdmin != 0;
            _restoreItem.Enabled = status.HasManagedState;
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to refresh UAC status", ex);
            _statusItem.Text = "状态：读取失败";
            _notifyIcon.Text = "UAC 维护模式：状态读取失败";
            _enableItem.Enabled = false;
            _restoreItem.Enabled = false;
        }
    }

    private void SetBusy(bool busy, string? statusText)
    {
        _operationInProgress = busy;
        _enableItem.Enabled = !busy;
        _restoreItem.Enabled = !busy;
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

    private static void ShowError(string message)
    {
        MessageBox.Show(message, "UAC 维护模式", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void ExitApplication()
    {
        _notifyIcon.Visible = false;
        AppLog.Info("Tray application exited.");
        ExitThread();
    }
}

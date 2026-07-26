<p align="center">
  <img src="assets/uac-maintenance-tray.svg" width="144" alt="UAC Maintenance Tray shield logo">
</p>

<h1 align="center">UAC Maintenance Tray</h1>

<p align="center">A small, explicit, and reversible Windows tray control for temporary administrator maintenance.</p>

<p align="center"><a href="README.zh-CN.md">简体中文说明</a></p>

> [!WARNING]
> Maintenance mode reduces UAC protection for administrator accounts. While active, programs that request elevation may receive it without a consent prompt. Use it only for a short, supervised maintenance window.

## What it does

UAC Maintenance Tray changes `ConsentPromptBehaviorAdmin` to `0` for a bounded period while leaving `EnableLUA` enabled. Before changing anything, it records the original value and creates a SYSTEM scheduled task to restore it. The tray shows whether the tool owns the current maintenance state and offers immediate restoration.

The restore task is bound to a random generation token. It restores the saved value only when that token still matches and the policy is still the value set by this tool. A stale task therefore cannot overwrite a later UAC choice made by the user or another administrator.

This is not a UAC bypass. Enabling or restoring maintenance mode uses the normal Windows UAC elevation flow and may display one consent prompt.

## Design boundaries

- No Windows service, driver, telemetry, updater, or autostart entry.
- No change to `EnableLUA`, Microsoft Defender, the firewall, or execution policy configuration.
- No permanent background privilege: the tray runs as the signed-in user and launches a short elevated child process only for a requested policy change.
- A self-contained folder release keeps the .NET runtime beside the app. It is intentionally not a true single-file bundle, which can extract native framework files under `%TEMP%\.net`.

## Install and run

1. Download `UacMaintenanceTray-v1.0.0-win-x64.zip` and its `.sha256.txt` file from the GitHub Release.
2. Verify the checksum, then extract the complete folder to a writable location such as `D:\Tools\UacMaintenanceTray`.
3. Run `UacMaintenanceTray.exe`. The shield appears in the notification area.
4. Right-click the icon to enable a six-hour maintenance window, restore immediately, open logs, or exit.

The application is currently unsigned, so Windows SmartScreen may show an unrecognized-app warning. Verify the published SHA-256 checksum before running it.

## Build from source

Requirements: Windows 11, PowerShell 7, and the .NET 10 SDK selected by `global.json`.

```powershell
pwsh -NoProfile -File .\scripts\Test-PublicRepo.ps1
pwsh -NoProfile -File .\scripts\Build-Release.ps1 -Version 1.0.0
```

The build creates a self-contained `win-x64` folder, ZIP archive, checksum, and file manifest under `artifacts/`. Release binaries are GitHub Release assets and are not committed to Git history.

## Diagnostics

Because the executable is a Windows GUI application, write diagnostics to a file:

```powershell
.\UacMaintenanceTray.exe --diagnostics --file .\diagnostics.json
```

Logs are stored in `Logs\app.log` beside the executable. They contain timestamps and operational errors, but no telemetry is transmitted.

## Security and recovery

Read [Security model](docs/SECURITY-MODEL.md) before using the tool. To report a vulnerability, follow [SECURITY.md](SECURITY.md). Automated tests never change the host UAC policy; destructive policy testing belongs in an isolated Windows Sandbox or disposable VM.

If the tray is unavailable, restore the normal administrator prompt from an elevated PowerShell window:

```powershell
Set-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' -Name ConsentPromptBehaviorAdmin -Type DWord -Value 5
Unregister-ScheduledTask -TaskName 'UacMaintenanceTray-AutoRestore' -Confirm:$false -ErrorAction SilentlyContinue
Remove-Item -LiteralPath 'HKLM:\SOFTWARE\UacMaintenanceTray' -Recurse -Force -ErrorAction SilentlyContinue
```

Value `5` is the Windows default for administrators, but organizations may use a different policy. Restore your known original value when applicable.

## License and notices

Project source is available under the [MIT License](LICENSE). Self-contained release archives also include the applicable .NET runtime license and third-party notices.

UAC Maintenance Tray is an independent project and is not affiliated with or endorsed by Microsoft. Windows and Microsoft are trademarks of the Microsoft group of companies.

## References

- [.NET application publishing overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/)
- [.NET single-file deployment and extraction behavior](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
- [GitHub Releases documentation](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases)
- [.NET license information](https://github.com/dotnet/core/blob/main/license-information.md)

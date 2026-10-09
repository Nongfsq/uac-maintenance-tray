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

The tray UI supports **Follow Windows language**, **English**, and **Simplified Chinese**. Right-click the tray icon and open **Language** to switch immediately. The selection is stored per user. UTF-8 JSON language packs live in `languages/`; missing or invalid entries fall back to embedded English so recovery controls remain usable.

This is not a UAC bypass. Enabling or restoring maintenance mode uses the normal Windows UAC elevation flow and may display one consent prompt.

## Design boundaries

- No Windows service, driver, telemetry, updater, or autostart entry.
- No change to `EnableLUA`, Microsoft Defender, the firewall, or execution policy configuration.
- No permanent background privilege: the tray runs as the signed-in user and launches a short elevated child process only for a requested policy change.
- Both release variants contain one application EXE. Supporting documentation, licenses, checksums, and artwork are grouped into named subdirectories; no loose DLL or Markdown files are placed beside the EXE.
- The framework-dependent package is the recommended small download and uses the latest compatible .NET 10 Desktop Runtime installed on Windows.
- The self-contained package bundles its own .NET runtime. On first launch after a build change, .NET may extract native components under `%TEMP%\.net`; `DOTNET_BUNDLE_EXTRACT_BASE_DIR` can relocate that cache.

## Install and run

Choose one clearly named package:

| Release asset | Runtime requirement | When to choose it |
|---|---|---|
| `UacMaintenanceTray-v1.1.0-win-x64-framework-dependent.zip` | Install the latest **.NET 10 Desktop Runtime x64** | Recommended; approximately 150 KB as a ZIP and receives compatible runtime security patches independently |
| `UacMaintenanceTray-v1.1.0-win-x64-self-contained.zip` | None | Use when installing .NET is not possible; approximately 44 MB as a ZIP because the runtime is bundled |

For the recommended framework-dependent package, install the desktop runtime—not only the basic `.NET Runtime`:

```powershell
winget install --id Microsoft.DotNet.DesktopRuntime.10 --exact
```

You can also use Microsoft's [.NET 10 download page](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) and select **.NET Desktop Runtime → Windows x64**. The SDK also satisfies this requirement.

Then:

1. Download the selected ZIP and `UacMaintenanceTray-v1.1.0-SHA256SUMS.txt` from the same GitHub Release.
2. Verify the archive checksum, then extract the versioned folder to a writable location such as `D:\Tools\UacMaintenanceTray`.
3. Run `UacMaintenanceTray.exe`. The shield appears in the notification area.
4. Right-click the icon to enable a six-hour maintenance window, restore immediately, open logs, or exit.

The application is currently unsigned, so Windows SmartScreen may show an unrecognized-app warning. Verify the published SHA-256 checksum before running it.

## Build from source

Requirements: Windows 11, PowerShell 7, and the .NET 10 SDK selected by `global.json`. The SDK can be installed with `winget install --id Microsoft.DotNet.SDK.10 --exact`.

```powershell
pwsh -NoProfile -File .\scripts\Test-PublicRepo.ps1
pwsh -NoProfile -File .\scripts\Build-Release.ps1 -Version 1.1.0
```

The build creates both single-file `win-x64` variants, two ZIP archives, one shared checksum file, and per-package manifests under `artifacts/`. Release binaries are GitHub Release assets and are not committed to Git history.

The archive layout is intentionally product-oriented:

```text
UacMaintenanceTray-v1.1.0-win-x64-<deployment-mode>/
├── UacMaintenanceTray.exe
├── assets/
├── checksums/
├── docs/
├── languages/
└── licenses/
```

## Language packs

Each pack is a UTF-8 JSON object whose keys must match `languages/en-US.json`. Packs are data only: the application never executes code from them, ignores unknown keys, rejects files larger than 128 KiB, and falls back to embedded English for missing or invalid values. See [Localization architecture](docs/LOCALIZATION.md) before adding or translating a pack.

## Diagnostics

Because the executable is a Windows GUI application, write diagnostics to a file:

```powershell
.\UacMaintenanceTray.exe --diagnostics --file .\diagnostics.json
```

To test a language pack without changing the saved tray preference, append `--language en-US` or `--language zh-CN`.

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

## Support

If this project is useful to you, you can buy me a coffee.

<a href="https://buymeacoffee.com/frankmenger"><img src="https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png" alt="Buy Me a Coffee" height="44"></a>

## License and notices

Project source is available under the [MIT License](LICENSE). The self-contained archive also includes the applicable .NET runtime license and third-party notices.

UAC Maintenance Tray is an independent project and is not affiliated with or endorsed by Microsoft. Windows and Microsoft are trademarks of the Microsoft group of companies.

## References

- [.NET application publishing overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/)
- [Install .NET on Windows](https://learn.microsoft.com/en-us/dotnet/core/install/windows)
- [.NET single-file deployment and extraction behavior](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
- [GitHub Releases documentation](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases)
- [.NET license information](https://github.com/dotnet/core/blob/main/license-information.md)

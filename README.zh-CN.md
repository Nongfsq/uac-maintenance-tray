<p align="center">
  <img src="assets/uac-maintenance-tray.svg" width="144" alt="UAC Maintenance Tray 盾牌图标">
</p>

<h1 align="center">UAC Maintenance Tray</h1>

<p align="center">用于临时管理员维护的、明确且可恢复的 Windows 托盘开关。</p>

<p align="center"><a href="README.md">English README</a></p>

> [!WARNING]
> 维护模式会降低管理员账户的 UAC 保护。启用期间，请求提升的程序可能不再显示同意提示。请只在有人看管的短时维护窗口中使用。

## 功能

程序在保持 `EnableLUA` 开启的前提下，临时把 `ConsentPromptBehaviorAdmin` 设为 `0`。修改之前，它会保存原值，并创建一个以 SYSTEM 身份运行的计划任务用于自动恢复。托盘会显示当前状态，并允许随时立即恢复。

自动恢复任务与随机生成的代际令牌绑定。只有令牌仍然匹配、且策略仍是本工具设置的值时，它才会写回原值。因此，过期任务不会覆盖用户或其他管理员后来设置的 UAC 策略。

托盘界面支持**跟随 Windows 语言、English、简体中文**。右键托盘图标并打开“语言”即可立即切换，选择会按用户保存。UTF-8 JSON 语言包位于 `languages/`；语言包缺失、损坏或键不完整时会回退到程序内置英文，确保恢复功能始终可用。

这不是 UAC 绕过工具。启用和恢复仍使用 Windows 标准 UAC 提升流程，通常会出现一次同意提示。

## 设计边界

- 不创建 Windows 服务、驱动、遥测、更新器或开机启动项。
- 不修改 `EnableLUA`、Microsoft Defender、防火墙或系统执行策略。
- 托盘以普通用户身份运行，只在用户明确请求切换时启动短时管理员子进程。
- 两种发布包都只有一个应用程序 EXE。说明文档、许可证、校验清单和图形资源分别归入具名子目录，EXE 旁不会散落 DLL 或 Markdown 文件。
- 推荐使用体积较小的“依赖运行时版”，它会使用 Windows 中已安装的最新兼容 .NET 10 Desktop Runtime。
- “自包含版”内置完整 .NET 运行时。新构建首次运行时可能把原生组件解压到 `%TEMP%\.net`；可通过 `DOTNET_BUNDLE_EXTRACT_BASE_DIR` 转移该缓存。

## 安装与使用

请根据文件名选择一种发布包：

| 发布文件 | 运行环境 | 适用情况 |
|---|---|---|
| `UacMaintenanceTray-v1.1.0-win-x64-framework-dependent.zip` | 需要安装最新版 **.NET 10 Desktop Runtime x64** | 推荐；ZIP 约 150 KB，兼容的运行时安全补丁可独立更新 |
| `UacMaintenanceTray-v1.1.0-win-x64-self-contained.zip` | 无需安装 .NET | 无法安装运行时时使用；内置运行时，因此 ZIP 约 44 MB |

使用推荐的依赖运行时版时，应安装桌面运行时，而不只是基础的 `.NET Runtime`：

```powershell
winget install --id Microsoft.DotNet.DesktopRuntime.10 --exact
```

也可以打开微软的 [.NET 10 下载页](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)，选择 **.NET Desktop Runtime → Windows x64**。已经安装 .NET 10 SDK 的电脑无需重复安装。

然后：

1. 从同一个 GitHub Release 下载所选 ZIP 和 `UacMaintenanceTray-v1.1.0-SHA256SUMS.txt`。
2. 校验压缩包哈希后，把版本目录解压到可写位置，例如 `D:\Tools\UacMaintenanceTray`。
3. 运行 `UacMaintenanceTray.exe`，通知区域会出现盾牌图标。
4. 右键图标即可启用六小时维护模式、立即恢复、打开日志目录或退出。

程序目前没有商业代码签名，Windows SmartScreen 可能提示“无法识别的应用”。运行前请核对发布页提供的 SHA-256。

## 从源码构建

需要 Windows 11、PowerShell 7，以及由 `global.json` 指定的 .NET 10 SDK。可通过 `winget install --id Microsoft.DotNet.SDK.10 --exact` 安装 SDK。

```powershell
pwsh -NoProfile -File .\scripts\Test-PublicRepo.ps1
pwsh -NoProfile -File .\scripts\Build-Release.ps1 -Version 1.1.0
```

构建结果位于 `artifacts/`，包括两种 `win-x64` 单文件发布包、两个 ZIP、一个共用 SHA-256 文件和各自的文件清单。二进制包通过 GitHub Release 发布，不提交到 Git 历史。

发布包根目录经过整理：

```text
UacMaintenanceTray-v1.1.0-win-x64-<deployment-mode>/
├── UacMaintenanceTray.exe
├── assets/
├── checksums/
├── docs/
├── languages/
└── licenses/
```

## 语言包

每个语言包都是 UTF-8 JSON 对象，键集合必须与 `languages/en-US.json` 一致。语言包只包含数据：程序不会执行其中的代码，会忽略未知键，拒绝超过 128 KiB 的文件，并为缺失或无效内容回退到内置英文。新增或翻译语言前请阅读[本地化架构](docs/LOCALIZATION.md)。

## 诊断

程序是 Windows GUI 应用，请把诊断结果写入文件：

```powershell
.\UacMaintenanceTray.exe --diagnostics --file .\diagnostics.json
```

如需在不修改托盘保存设置的情况下测试语言包，可追加 `--language en-US` 或 `--language zh-CN`。

日志保存在程序旁的 `Logs\app.log`。日志包含时间和操作错误，不会向外发送遥测。

## 安全与恢复

使用前请阅读[安全模型](docs/SECURITY-MODEL.md)，漏洞报告方式见 [SECURITY.md](SECURITY.md)。自动化测试绝不会修改宿主机 UAC；真实策略切换测试应只在 Windows Sandbox 或一次性虚拟机中进行。

若托盘不可用，可在提升后的 PowerShell 中恢复：

```powershell
Set-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' -Name ConsentPromptBehaviorAdmin -Type DWord -Value 5
Unregister-ScheduledTask -TaskName 'UacMaintenanceTray-AutoRestore' -Confirm:$false -ErrorAction SilentlyContinue
Remove-Item -LiteralPath 'HKLM:\SOFTWARE\UacMaintenanceTray' -Recurse -Force -ErrorAction SilentlyContinue
```

数值 `5` 是 Windows 对管理员账户的默认设置，但组织策略可能不同；如已知原值，应恢复为实际原值。

## 许可证

项目源码采用 [MIT License](LICENSE)。自包含发布包同时提供适用的 .NET 运行时许可证和第三方声明。

本项目为独立项目，与 Microsoft 没有关联，也未获得其背书。Windows 和 Microsoft 是 Microsoft 集团公司的商标。

## 参考资料

- [.NET 应用发布概述](https://learn.microsoft.com/en-us/dotnet/core/deploying/)
- [在 Windows 上安装 .NET](https://learn.microsoft.com/zh-cn/dotnet/core/install/windows)
- [.NET 单文件部署与解压行为](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
- [GitHub Releases 文档](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases)
- [.NET 许可证信息](https://github.com/dotnet/core/blob/main/license-information.md)

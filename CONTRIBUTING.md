# Contributing

Contributions are welcome through focused issues and pull requests.

1. Keep policy-changing behavior explicit, reversible, and fail-safe.
2. Do not add services, autostart, telemetry, remote scripts, or silent elevation.
3. Run `pwsh -NoProfile -File .\scripts\Test-PublicRepo.ps1` before submitting.
4. Never exercise UAC mutation commands on CI or a contributor's normal workstation; use an isolated disposable VM.
5. Update both READMEs and every shipped language pack when user-visible text changes.
6. Do not commit build output, logs, diagnostics, registry exports, credentials, certificates, or release archives.

By contributing, you agree that your contribution is licensed under the MIT License.

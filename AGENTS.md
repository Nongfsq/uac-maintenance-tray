# Repository agent guidance

## Purpose

Maintain a small, auditable Windows tray utility that temporarily changes one UAC consent policy and restores it safely.

## Safety invariants

- Never disable `EnableLUA`, Defender, the firewall, or other Windows security controls.
- Never run `--enable` or `--restore` during automated tests, CI, or normal host validation.
- Preserve token-bound restoration: a stale task must never overwrite a later administrator policy choice.
- Do not add a service, driver, autostart entry, telemetry, remote download, or silent elevation path.
- Keep the tray process unelevated; privilege is limited to short, explicit child operations.

## Change workflow

- Keep English `README.md` canonical and update `README.zh-CN.md` for the same user-visible facts.
- Keep `languages/en-US.json`, `languages/zh-CN.json`, and the embedded English fallback key-compatible. User-visible tray text must not be hard-coded outside the fallback dictionary.
- Build releases only through `scripts/Build-Release.ps1`.
- Run `scripts/Test-PublicRepo.ps1` before every release or publication.
- Keep release archives in GitHub Releases, never Git history.
- Preserve MIT licensing and include exact .NET runtime notices in self-contained packages.

## Files and privacy

- Never commit `bin/`, `obj/`, `artifacts/`, `publish/`, logs, diagnostics, registry exports, secrets, certificates, or user-specific IDE files.
- Reject authored files containing user-profile paths, private email addresses, tokens, or machine-specific cache/tool locations.
- Treat generated files as disposable; regenerate them from tracked source.

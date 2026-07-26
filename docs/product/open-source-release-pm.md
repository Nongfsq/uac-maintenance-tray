# Open-source release brief

## Essential promise

UAC Maintenance Tray gives a Windows administrator an explicit, reversible tray control for temporarily changing the administrator consent prompt, with automatic restoration and visible safety state.

## Users

- Windows 11 power users who need a short maintenance window without repeated consent prompts.
- Administrators who understand that disabling the prompt reduces protection and want the original policy restored automatically.

## Scope

- A portable Windows tray application with no service, telemetry, updater, or autostart entry.
- Explicit confirmation before enabling the reduced-prompt maintenance mode.
- A token-bound automatic restore task that cannot overwrite a later unrelated UAC choice.
- English primary documentation and a complete Simplified Chinese guide.
- Reproducible `win-x64` self-contained folder release packaged as ZIP with checksum and runtime notices.
- Source, build validation, privacy checks, CI, and a GitHub Release workflow.

## Non-goals

- Bypassing Windows elevation or silently granting an unelevated process administrator rights.
- Permanently disabling UAC, changing Defender, or running as a Windows service.
- A true single-file executable that extracts framework files into the system temporary directory.
- Automatic publication, code-signing certificate acquisition, or package-manager distribution in this phase.

## Current reality

The prototype works as a tray application and preserves the original UAC value, but its automatic restore task is not generation-bound. If task deletion fails, an old task could later restore a stale value. The prototype also uses stock system icons, has no public-repository governance, and its generated build directories contain local absolute paths.

## Release decision

Use an MIT license. Distribute a self-contained folder ZIP rather than a true single-file bundle: this keeps the runtime beside the application on the selected drive and avoids native single-file extraction under `%TEMP%\.net`.

## Success criteria

- A clean canonical repository exists without copied `bin`, `obj`, logs, registry exports, credentials, or local absolute paths in tracked files.
- The approved SVG is the canonical brand asset and generates the multi-resolution EXE/tray ICO.
- Safety-sensitive state transitions are token-bound, confirmed, logged, and covered by non-mutating tests.
- `scripts/Build-Release.ps1` produces a versioned ZIP, SHA-256 sidecar, manifest, MIT license, both READMEs, SVG, and applicable .NET notices.
- CI builds and tests without mutating the host UAC policy; release publication is tag-gated.
- Local Git history is ready for review. GitHub repository creation and push remain deferred until the owner explicitly authorizes publication.

## Risks and mitigations

- **Reduced UAC protection:** show an explicit warning and keep the active state visible in the tray.
- **Stale restore task:** bind tasks to a random generation token and require matching state plus the managed policy value before restoration.
- **Unsigned executable warnings:** document Windows SmartScreen behavior and publish SHA-256 checksums.
- **Runtime security servicing:** rebuild releases when the bundled .NET runtime receives security updates.
- **Privacy leakage:** generate from clean sources, reject sensitive filenames/content and absolute user paths, and never commit release binaries.

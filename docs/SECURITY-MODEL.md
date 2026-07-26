# Security model

## Protected boundary

The tray process runs without elevation. A requested change launches the same executable with the standard `runas` verb. The elevated process accepts only the built-in `--enable` and `--restore` operations and then exits.

## State ownership

Owned state lives under `HKLM\SOFTWARE\UacMaintenanceTray` and includes the original consent value, timestamps, and a random generation ID. The automatic restore task embeds that ID. It writes the original policy only when:

1. the owned state still exists;
2. its generation ID matches the task;
3. `ConsentPromptBehaviorAdmin` is still `0`, the value set by this application.

If any condition fails, the task does not overwrite the policy. A matching task may remove its own stale state and unregister itself.

## Failure behavior

- Enabling stops if `EnableLUA` is not `1`, the consent value is already `0`, or owned state already exists.
- Manual restoration writes the saved value first. Owned state is removed only after scheduled-task deletion is confirmed.
- Process invocations have finite timeouts, and output streams are drained concurrently to avoid deadlock.
- Logging failures never block restoration.

## Trust and distribution

The release is unsigned. The SHA-256 sidecar detects accidental or malicious archive replacement only when users obtain the checksum from a trusted release page. Source review and reproducible local builds remain the strongest available verification until code signing is introduced.

The self-contained single-file executable includes Microsoft .NET runtime components. Runtime licenses and third-party notices are copied from the exact restored runtime packs. Bundled runtimes must be rebuilt when relevant security updates are released. Native runtime files can be extracted by the .NET bundle host under `%TEMP%\.net`; users can relocate this cache with `DOTNET_BUNDLE_EXTRACT_BASE_DIR` before starting the process.

The framework-dependent single-file executable does not contain the .NET runtime. It requires the latest compatible .NET 10 Desktop Runtime x64 and can receive compatible runtime security patches independently of an application release.

## Testing policy

CI and normal local validation may read UAC status but must not invoke `--enable` or `--restore`. End-to-end mutation testing is permitted only in an isolated Windows Sandbox or disposable VM with a documented baseline and recovery snapshot.

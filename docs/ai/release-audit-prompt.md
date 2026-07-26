# Release audit prompt

You are auditing a Windows security-adjacent tray utility before an open-source release. Work read-only. Inspect the complete repository, build scripts, workflow files, authored documentation, and the exact staged release archive.

Verify all of the following:

1. No tracked candidate or release file exposes credentials, private email addresses, user-profile paths, local cache/tool paths, registry exports, logs, diagnostics, certificates, or generated `bin`/`obj` metadata.
2. The tray stays unelevated and elevation occurs only through an explicit user action using the standard Windows UAC flow.
3. The application never disables `EnableLUA`, Defender, the firewall, or execution-policy configuration.
4. Automatic restoration is bound to a unique generation token and writes the saved value only when the token matches and the current value is still the value set by this tool.
5. Manual restoration cannot silently discard recovery state when scheduled-task deletion fails.
6. All child processes have bounded waits; redirected output cannot deadlock.
7. CI never invokes policy-mutating commands. Release publication is tag-gated and uses least-privilege GitHub permissions.
8. The ZIP contains one versioned top-level folder, the EXE and complete self-contained runtime, both READMEs, MIT license, approved SVG, .NET licenses/notices, and a file manifest. Its SHA-256 sidecar matches.
9. English and Chinese documentation agree about risks, installation, diagnostics, recovery, unsigned SmartScreen behavior, and the no-service/no-telemetry design.
10. The approved SVG produces the EXE and tray icon at Windows notification-area sizes.

Do not change UAC policy, create or delete scheduled tasks, launch the elevated commands, publish to GitHub, or modify files. Report findings first, ordered by severity, with exact file and line references. Separate confirmed facts from inference. If no blocking issue exists, state that explicitly and list remaining release risks and untested assumptions.

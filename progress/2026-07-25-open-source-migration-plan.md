# Open-source migration and release plan

Date: 2026-07-25
Owner: Nongfsq
Status: Local release complete; publication deferred

Revision: the release provides clearly named self-contained and framework-dependent single-file EXEs. Each root contains only the executable; all non-executable materials are grouped into named subdirectories.

## Work items

| ID | Work item | Evidence of completion | Status |
|---|---|---|---|
| OPEN-001 | Create the canonical repository and governance baseline | Repository tree, `AGENTS.md`, Git hygiene files | Complete |
| OPEN-002 | Migrate only authored source and the approved SVG | Clean `src/` and `assets/`; no generated/local files | Complete |
| OPEN-003 | Harden policy restoration and diagnostics | Token-bound recovery, confirmation, timeouts, file diagnostics | Complete |
| OPEN-004 | Integrate the approved visual identity | SVG, multi-resolution ICO, application and tray icon | Complete |
| OPEN-005 | Create public documentation and licensing | English README, Chinese guide, security docs, MIT license | Complete |
| OPEN-006 | Add deterministic build and privacy validation | Release and repository-audit scripts | Complete |
| OPEN-007 | Add CI and tag-gated release automation | GitHub Actions workflows | Complete |
| OPEN-008 | Validate and stage the local release | Tests, clean build, ZIP/checksum, local Git repository, refreshed D-drive install | Complete |
| OPEN-009 | Publish source and release assets to GitHub | Public repository and multilingual v1.1.0 release | In progress; explicitly authorized |

## Validation gates

1. Unit tests and build run without changing the current machine's UAC policy.
2. Privacy audit passes against tracked candidates and packaged output.
3. Release contents are inspected and checksums are regenerated from the final archive.
4. The installed tray process starts from `D:\Tools\UacMaintenanceTray` and uses the new icon.
5. No GitHub remote write occurs during OPEN-001 through OPEN-008.

## Rollback

- Source migration is additive; the prototype remains untouched under the original workspace.
- The installed application is refreshed only after a successful package build. Its existing configuration and UAC state are checked first.
- UAC policy mutation is never exercised as part of automated validation on the host.

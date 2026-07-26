# Localization architecture

## Decision

UAC Maintenance Tray uses external UTF-8 JSON string packs rather than compiled `.resx` satellite assemblies. Satellite assemblies are the conventional WinForms mechanism, but they add culture-specific resource DLLs and require recompilation. JSON packs preserve the single-EXE product layout, are easy to audit and translate, and contain no executable code.

The executable embeds a complete English fallback dictionary. External packs can override only recognized keys. Missing files, malformed JSON, blank values, oversized files, and unknown keys cannot remove the recovery controls or prevent startup.

## Supported preferences

- `system`: Simplified Chinese for Windows UI cultures whose two-letter code is `zh`; English for all others.
- `en-US`: English.
- `zh-CN`: Simplified Chinese.

The preference is stored as the current user's `Language` string under `HKCU\SOFTWARE\UacMaintenanceTray`. No administrator rights are required to change it.

## Adding a language

1. Copy `languages/en-US.json` to `languages/<culture>.json` using a standard culture tag.
2. Translate values without changing keys or numbered placeholders such as `{0}`.
3. Add the culture to `LocalizationManager` and the tray language menu.
4. Update `scripts/Test-PublicRepo.ps1` and `scripts/Build-Release.ps1` so key parity and packaging are enforced.
5. Test normal, active-maintenance, failure, confirmation, and restore states in the new language.

Use trusted, reviewed translations for safety-sensitive labels. Language packs can change displayed meaning even though they cannot execute code.

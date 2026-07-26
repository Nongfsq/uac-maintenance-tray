namespace UacMaintenanceTray;

internal sealed record UacStatus(
    int EnableLua,
    int ConsentPromptBehaviorAdmin,
    bool HasManagedState,
    DateTimeOffset? EnabledAtUtc,
    DateTimeOffset? RestoreAtUtc,
    int? OriginalConsentPromptBehaviorAdmin,
    string? GenerationId)
{
    internal bool IsMaintenanceActive => HasManagedState && ConsentPromptBehaviorAdmin == 0;
}

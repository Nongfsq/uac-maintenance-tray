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

    internal string Description
    {
        get
        {
            if (IsMaintenanceActive)
            {
                if (RestoreAtUtc is { } restoreAt)
                {
                    var remaining = restoreAt - DateTimeOffset.UtcNow;
                    if (remaining > TimeSpan.Zero)
                    {
                        return $"维护模式（剩余 {FormatRemaining(remaining)}）";
                    }
                }

                return "维护模式（等待自动恢复）";
            }

            if (ConsentPromptBehaviorAdmin == 0)
            {
                return "无提示提升（非本工具管理）";
            }

            if (HasManagedState)
            {
                return "UAC 已恢复，存在待清理状态";
            }

            return "正常保护";
        }
    }

    private static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining.TotalHours >= 1)
        {
            return $"{(int)remaining.TotalHours}小时{remaining.Minutes}分钟";
        }

        return $"{Math.Max(1, remaining.Minutes)}分钟";
    }
}

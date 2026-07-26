using Microsoft.Win32;
using System.Globalization;
using System.Text.Json;

namespace UacMaintenanceTray;

internal sealed class LocalizationManager
{
    internal const string SystemLanguage = "system";
    internal const string EnglishLanguage = "en-US";
    internal const string ChineseLanguage = "zh-CN";

    private const string SettingsSubKey = @"SOFTWARE\UacMaintenanceTray";
    private const string LanguageValueName = "Language";
    private const int MaximumLanguagePackBytes = 128 * 1024;

    private static readonly IReadOnlyDictionary<string, string> EnglishFallback =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ApplicationTitle"] = "UAC Maintenance Tray",
            ["StatusReading"] = "Reading status…",
            ["StatusMaintenanceRemaining"] = "Maintenance mode ({0} remaining)",
            ["StatusMaintenanceWaiting"] = "Maintenance mode (waiting for automatic restore)",
            ["StatusUnmanagedNoPrompt"] = "Elevation without prompt (not managed by this tool)",
            ["StatusRestoredPendingCleanup"] = "UAC restored; cleanup is pending",
            ["StatusNormal"] = "Normal protection",
            ["StatusReadFailed"] = "Status unavailable",
            ["RemainingHoursMinutes"] = "{0}h {1}m",
            ["RemainingMinutes"] = "{0}m",
            ["TrayStatus"] = "UAC maintenance: {0}",
            ["EnableMaintenance"] = "Enable administrator maintenance mode (6 hours)",
            ["RestorePolicy"] = "Restore the original UAC policy now",
            ["Language"] = "Language",
            ["LanguageSystem"] = "Follow Windows language",
            ["LanguageEnglishName"] = "English",
            ["LanguageChineseName"] = "Simplified Chinese",
            ["OpenLogs"] = "Open log directory",
            ["Exit"] = "Exit tray application",
            ["BusyWaitingElevation"] = "Waiting for administrator approval…",
            ["BusyRestoring"] = "Restoring the policy…",
            ["ConfirmTitle"] = "Confirm reduced UAC protection",
            ["ConfirmBody"] = "Maintenance mode temporarily removes the UAC consent prompt for administrator accounts. During this time, any program requesting elevation may receive high privileges directly.\n\nThe application schedules automatic restoration, but shutdowns or system changes can still affect recovery. Enable it for 6 hours?",
            ["EnableError"] = "Could not enable maintenance mode.",
            ["RestoreError"] = "Could not restore the UAC policy.",
            ["ElevationCancelled"] = "Administrator approval was cancelled.",
            ["ElevationTimedOut"] = "The administrator operation timed out and was terminated.",
            ["ElevationChildFailed"] = "The administrator operation failed with exit code {0}.",
            ["ElevationStartFailed"] = "Could not start the administrator operation: {0}"
        };

    private IReadOnlyDictionary<string, string> _strings = EnglishFallback;
    private readonly string _systemResolvedLanguage =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase)
            ? ChineseLanguage
            : EnglishLanguage;

    internal string Preference { get; private set; } = SystemLanguage;
    internal string ActiveLanguage { get; private set; } = EnglishLanguage;
    internal CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo(EnglishLanguage);
    internal bool ExternalLanguagePackLoaded { get; private set; }

    internal LocalizationManager(string? preferenceOverride = null)
    {
        Preference = preferenceOverride is SystemLanguage or EnglishLanguage or ChineseLanguage
            ? preferenceOverride
            : ReadPreference();
        LoadActiveLanguage();
    }

    internal string Get(string key, params object[] arguments)
    {
        var value = _strings.TryGetValue(key, out var translated)
            ? translated
            : EnglishFallback.TryGetValue(key, out var fallback)
                ? fallback
                : key;
        return arguments.Length == 0 ? value : string.Format(Culture, value, arguments);
    }

    internal void SetPreference(string language)
    {
        if (language is not (SystemLanguage or EnglishLanguage or ChineseLanguage))
        {
            throw new ArgumentOutOfRangeException(nameof(language), language, "Unsupported language preference.");
        }

        using var key = Registry.CurrentUser.CreateSubKey(SettingsSubKey, writable: true)
            ?? throw new InvalidOperationException("Could not open the per-user application settings.");
        key.SetValue(LanguageValueName, language, RegistryValueKind.String);
        Preference = language;
        LoadActiveLanguage();
    }

    private void LoadActiveLanguage()
    {
        ActiveLanguage = Preference == SystemLanguage ? _systemResolvedLanguage : Preference;
        Culture = CultureInfo.GetCultureInfo(ActiveLanguage);
        (_strings, ExternalLanguagePackLoaded) = LoadLanguagePack(ActiveLanguage);
        CultureInfo.CurrentUICulture = Culture;
    }

    private static string ReadPreference()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsSubKey, writable: false);
        var value = key?.GetValue(LanguageValueName) as string;
        return value is SystemLanguage or EnglishLanguage or ChineseLanguage
            ? value
            : SystemLanguage;
    }

    private static (IReadOnlyDictionary<string, string> Strings, bool Loaded) LoadLanguagePack(string language)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "languages", $"{language}.json");
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return (EnglishFallback, false);
            }
            if (info.Length > MaximumLanguagePackBytes)
            {
                throw new InvalidDataException("Language pack exceeds the maximum allowed size.");
            }

            var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
                ?? throw new InvalidDataException("Language pack is empty.");
            var merged = new Dictionary<string, string>(EnglishFallback, StringComparer.Ordinal);
            foreach (var pair in loaded)
            {
                if (EnglishFallback.ContainsKey(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                {
                    merged[pair.Key] = pair.Value;
                }
            }
            return (merged, true);
        }
        catch (Exception ex)
        {
            AppLog.Error($"Failed to load language pack '{language}'; using embedded English.", ex);
            return (EnglishFallback, false);
        }
    }
}

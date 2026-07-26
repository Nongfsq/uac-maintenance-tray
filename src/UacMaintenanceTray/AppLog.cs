namespace UacMaintenanceTray;

internal static class AppLog
{
    private static readonly object Sync = new();

    internal static string FilePath { get; } = Path.Combine(
        AppContext.BaseDirectory,
        "Logs",
        "app.log");

    internal static void Info(string message) => Write("INFO", message, null);

    internal static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var line = $"{DateTimeOffset.Now:O} [{level}] {message}";
                if (exception is not null)
                {
                    line += $"{Environment.NewLine}{exception}";
                }

                File.AppendAllText(FilePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never prevent policy restoration or application exit.
        }
    }
}

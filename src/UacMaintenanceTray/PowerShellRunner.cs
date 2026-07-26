using System.Diagnostics;
using System.Text;

namespace UacMaintenanceTray;

internal static class PowerShellRunner
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(30);

    private static string WindowsPowerShellPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        "System32",
        "WindowsPowerShell",
        "v1.0",
        "powershell.exe");

    internal static void RunEncoded(string script)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var startInfo = new ProcessStartInfo
        {
            FileName = WindowsPowerShellPath,
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动 Windows PowerShell。");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit((int)OperationTimeout.TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("PowerShell operation timed out and was terminated.");
        }

        Task.WaitAll(outputTask, errorTask);
        var output = outputTask.Result;
        var error = errorTask.Result;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"PowerShell 操作失败（{process.ExitCode}）：{error}{output}");
        }
    }
}

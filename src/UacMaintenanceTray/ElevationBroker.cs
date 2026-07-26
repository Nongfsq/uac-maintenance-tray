using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace UacMaintenanceTray;

internal static class ElevationBroker
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(2);

    internal static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    internal static ElevationResult RunElevated(string command, int? hours = null)
    {
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("无法确定当前程序路径。");

        var arguments = hours is null ? command : $"{command} {hours.Value}";
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("无法启动管理员操作进程。");
            if (!process.WaitForExit((int)OperationTimeout.TotalMilliseconds))
            {
                process.Kill(entireProcessTree: true);
                return ElevationResult.Failure("管理员操作超时，已终止。", 1460);
            }

            return process.ExitCode == 0
                ? ElevationResult.Success()
                : ElevationResult.Failure($"管理员操作失败，退出码 {process.ExitCode}。", process.ExitCode);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return ElevationResult.Failure("已取消 UAC 授权。", 1223);
        }
        catch (Exception ex)
        {
            AppLog.Error("Elevated operation failed", ex);
            return ElevationResult.Failure(ex.Message, 1);
        }
    }
}

internal sealed record ElevationResult(bool Succeeded, string? Error, int ExitCode)
{
    internal static ElevationResult Success() => new(true, null, 0);
    internal static ElevationResult Failure(string error, int exitCode) => new(false, error, exitCode);
}

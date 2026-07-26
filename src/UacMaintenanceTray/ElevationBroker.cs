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
                return ElevationResult.Failed(ElevationFailure.TimedOut, 1460);
            }

            return process.ExitCode == 0
                ? ElevationResult.Success()
                : ElevationResult.Failed(ElevationFailure.ChildFailed, process.ExitCode);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return ElevationResult.Failed(ElevationFailure.Cancelled, 1223);
        }
        catch (Exception ex)
        {
            AppLog.Error("Elevated operation failed", ex);
            return ElevationResult.Failed(ElevationFailure.StartFailed, 1, ex.Message);
        }
    }
}

internal enum ElevationFailure
{
    None,
    Cancelled,
    TimedOut,
    ChildFailed,
    StartFailed
}

internal sealed record ElevationResult(bool Succeeded, ElevationFailure Failure, int ExitCode, string? Detail)
{
    internal static ElevationResult Success() => new(true, ElevationFailure.None, 0, null);
    internal static ElevationResult Failed(ElevationFailure failure, int exitCode, string? detail = null) =>
        new(false, failure, exitCode, detail);
}

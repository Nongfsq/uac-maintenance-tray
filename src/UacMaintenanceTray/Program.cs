using System.Text.Json;

namespace UacMaintenanceTray;

internal static class Program
{
    private const string MutexName = @"Local\UacMaintenanceTray-7D58BD2D-54A7-43EA-864D-64CC3D41F8DF";

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0)
            {
                return RunCommand(args);
            }

            using var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
            if (!createdNew)
            {
                return 0;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());
            return 0;
        }
        catch (Exception ex)
        {
            AppLog.Error("Unhandled application error", ex);
            return 1;
        }
    }

    private static int RunCommand(string[] args)
    {
        var command = args[0].ToLowerInvariant();
        return command switch
        {
            "--enable" => RunElevatedEnable(args),
            "--restore" => RunElevatedRestore(),
            "--diagnostics" => RunDiagnostics(args),
            _ => 2
        };
    }

    private static int RunElevatedEnable(string[] args)
    {
        if (!ElevationBroker.IsElevated())
        {
            return 5;
        }

        var hours = UacPolicyManager.DefaultMaintenanceHours;
        if (args.Length > 1 && int.TryParse(args[1], out var requestedHours))
        {
            hours = Math.Clamp(requestedHours, 1, 24);
        }

        UacPolicyManager.EnableMaintenanceMode(hours);
        return 0;
    }

    private static int RunElevatedRestore()
    {
        if (!ElevationBroker.IsElevated())
        {
            return 5;
        }

        UacPolicyManager.RestoreOriginalPolicy();
        return 0;
    }

    private static int RunDiagnostics(string[] args)
    {
        var status = UacPolicyManager.GetStatus();
        var result = new
        {
            status.EnableLua,
            status.ConsentPromptBehaviorAdmin,
            status.HasManagedState,
            status.IsMaintenanceActive,
            status.RestoreAtUtc,
            IsElevated = ElevationBroker.IsElevated(),
            Executable = Environment.ProcessPath,
            LogFile = AppLog.FilePath
        };

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        if (args.Length >= 3 && args[1].Equals("--file", StringComparison.OrdinalIgnoreCase))
        {
            var path = Path.GetFullPath(args[2]);
            Directory.CreateDirectory(Path.GetDirectoryName(path)
                ?? throw new InvalidOperationException("Diagnostics path has no parent directory."));
            File.WriteAllText(path, json);
        }
        else
        {
            Console.WriteLine(json);
        }

        return status.EnableLua == 1 ? 0 : 3;
    }
}

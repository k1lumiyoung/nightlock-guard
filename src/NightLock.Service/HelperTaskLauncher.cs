using System.Diagnostics;
using NightLock.Core;

namespace NightLock.Service;

/// <summary>
/// @spec spec://modules/core/INFRA-001-windows-runtime-baseline#session-helper
/// </summary>
internal static class HelperTaskLauncher
{
    public static void TryRun(FileLogger? logger)
    {
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = "/run /tn NightLockGuardHelper",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using var process = Process.Start(start);
            if (process is null)
            {
                logger?.Warn("Requested helper scheduled task start, but no process handle was returned.");
                return;
            }

            if (process.WaitForExit(5_000))
            {
                logger?.Info($"Requested helper scheduled task start. ExitCode={process.ExitCode}");
            }
            else
            {
                logger?.Info("Requested helper scheduled task start; schtasks.exe still running after 5s.");
            }
        }
        catch (Exception ex)
        {
            logger?.Error("Failed to start helper scheduled task", ex);
        }
    }
}

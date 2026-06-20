using System.Runtime.InteropServices;

namespace NightLock.Core;

/// <summary>
/// @spec spec://modules/core/INFRA-001-windows-runtime-baseline#install-layout
/// </summary>
public sealed class AppPaths
{
    public const string AppName = "NightLockGuard";

    public AppPaths(string dataDirectory)
    {
        DataDirectory = dataDirectory;
        ConfigPath = Path.Combine(dataDirectory, "config.json");
        LogDirectory = Path.Combine(dataDirectory, "logs");
        LogPath = Path.Combine(LogDirectory, "nightlock.log");
    }

    public string DataDirectory { get; }
    public string ConfigPath { get; }
    public string LogDirectory { get; }
    public string LogPath { get; }

    public static AppPaths Default()
    {
        var baseDirectory = Environment.GetEnvironmentVariable("NIGHTLOCK_DATA_DIR");
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), AppName)
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nightlockguard");
        }

        return new AppPaths(baseDirectory);
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogDirectory);
    }
}

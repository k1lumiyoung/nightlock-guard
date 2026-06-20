using System.Text.Json;

namespace NightLock.Core;

/// <summary>
/// @spec spec://modules/core/INFRA-001-windows-runtime-baseline#config
/// </summary>
public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppPaths _paths;

    public ConfigStore(AppPaths paths)
    {
        _paths = paths;
    }

    public NightLockSettings LoadOrCreateDefault()
    {
        _paths.EnsureCreated();

        if (!File.Exists(_paths.ConfigPath))
        {
            var defaults = new NightLockSettings();
            try
            {
                Save(defaults);
            }
            catch
            {
                // The session helper runs as a normal user and may not have write access
                // to the hardened data directory. Missing config simply falls back to defaults.
            }

            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_paths.ConfigPath);
            return JsonSerializer.Deserialize<NightLockSettings>(json, JsonOptions) ?? new NightLockSettings();
        }
        catch
        {
            return new NightLockSettings();
        }
    }

    public void Save(NightLockSettings settings)
    {
        _paths.EnsureCreated();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        AtomicWrite(_paths.ConfigPath, json);
    }

    public static void AtomicWrite(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(tempPath, content);
        if (File.Exists(path))
        {
            File.Replace(tempPath, path, null);
        }
        else
        {
            File.Move(tempPath, path);
        }
    }
}

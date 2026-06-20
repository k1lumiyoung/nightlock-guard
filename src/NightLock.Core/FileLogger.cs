namespace NightLock.Core;

/// <summary>
/// @spec spec://modules/core/INFRA-001-windows-runtime-baseline#logs
/// </summary>
public sealed class FileLogger
{
    private readonly AppPaths _paths;
    private readonly object _gate = new();

    public FileLogger(AppPaths paths)
    {
        _paths = paths;
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message, Exception exception) => Write("ERROR", $"{message}: {exception}");

    private void Write(string level, string message)
    {
        try
        {
            _paths.EnsureCreated();
            var line = $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}";
            lock (_gate)
            {
                File.AppendAllText(_paths.LogPath, line);
            }
        }
        catch
        {
            // Logging is diagnostic only; policy enforcement must not crash if ACLs block log writes.
        }
    }
}

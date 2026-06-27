using System.Text.Json.Serialization;

namespace NightLock.Core;

/// <summary>
/// @spec spec://modules/core/INFRA-001-windows-runtime-baseline#config
/// @spec spec://modules/core/FEAT-001-night-lock-window#schedule
/// @spec spec://modules/core/FEAT-004-emergency-stop-hotkey#hotkey-config
/// @spec spec://modules/core/FEAT-005-windows-key-suppression#root
/// @spec spec://modules/core/FEAT-006-trusted-time-source#config
/// </summary>
public sealed class NightLockSettings
{
    private static readonly string[] DefaultNtpServerValues = ["pool.ntp.org", "time.windows.com", "time.google.com"];

    public string LockWindowStart { get; set; } = "23:00";
    public string LockWindowEnd { get; set; } = "08:00";
    public int WarningOffsetMinutes { get; set; } = 10;
    public int OverrideMinutes { get; set; } = 30;
    public PasswordVerifier? ParentPassword { get; set; }
    public bool UseTrustedTime { get; set; } = true;
    public List<string> NtpServers { get; set; } = DefaultNtpServerValues.ToList();
    public int NtpResyncMinutes { get; set; } = 60;
    public int NtpTimeoutSeconds { get; set; } = 3;

    /// <summary>Suppress the Windows key during restricted hours (FEAT-005). On by default.</summary>
    public bool SuppressWindowsKey { get; set; } = true;

    /// <summary>Virtual-key codes for the emergency stop combo (FEAT-004). Default: LShift+RShift+6+7.</summary>
    public List<int> StopHotkeyKeys { get; set; } = Hotkey.DefaultKeys.ToList();

    [JsonIgnore]
    public TimeOnly LockStart => ParseTimeOrDefault(LockWindowStart, new TimeOnly(23, 0));

    [JsonIgnore]
    public TimeOnly LockEnd => ParseTimeOrDefault(LockWindowEnd, new TimeOnly(8, 0));

    [JsonIgnore]
    public TimeSpan WarningOffset => TimeSpan.FromMinutes(Math.Max(1, WarningOffsetMinutes));

    [JsonIgnore]
    public TimeSpan OverrideDuration => TimeSpan.FromMinutes(Math.Clamp(OverrideMinutes, 1, 240));

    /// <summary>The configured stop combo, falling back to the default if invalid.</summary>
    [JsonIgnore]
    public IReadOnlyList<int> StopHotkey => Hotkey.NormalizeOrDefault(StopHotkeyKeys);

    [JsonIgnore]
    public IReadOnlyList<string> NormalizedNtpServers
    {
        get
        {
            var servers = (NtpServers ?? [])
                .Where(server => !string.IsNullOrWhiteSpace(server))
                .Select(server => server.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return servers.Count == 0 ? DefaultNtpServerValues : servers;
        }
    }

    [JsonIgnore]
    public TimeSpan NtpResyncInterval => TimeSpan.FromMinutes(Math.Clamp(NtpResyncMinutes, 5, 1440));

    [JsonIgnore]
    public TimeSpan NtpTimeout => TimeSpan.FromSeconds(Math.Clamp(NtpTimeoutSeconds, 1, 15));

    private static TimeOnly ParseTimeOrDefault(string value, TimeOnly fallback)
    {
        return TimeOnly.TryParse(value, out var parsed) ? parsed : fallback;
    }
}

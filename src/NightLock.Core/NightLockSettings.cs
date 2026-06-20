using System.Text.Json.Serialization;

namespace NightLock.Core;

/// <summary>
/// @spec spec://modules/core/INFRA-001-windows-runtime-baseline#config
/// @spec spec://modules/core/FEAT-001-night-lock-window#schedule
/// @spec spec://modules/core/FEAT-004-emergency-stop-hotkey#hotkey-config
/// @spec spec://modules/core/FEAT-005-windows-key-suppression#root
/// </summary>
public sealed class NightLockSettings
{
    public string LockWindowStart { get; set; } = "23:30";
    public string LockWindowEnd { get; set; } = "08:00";
    public int WarningOffsetMinutes { get; set; } = 10;
    public int OverrideMinutes { get; set; } = 30;
    public PasswordVerifier? ParentPassword { get; set; }

    /// <summary>Suppress the Windows key during restricted hours (FEAT-005). On by default.</summary>
    public bool SuppressWindowsKey { get; set; } = true;

    /// <summary>Virtual-key codes for the emergency stop combo (FEAT-004). Default: LShift+RShift+6+7.</summary>
    public List<int> StopHotkeyKeys { get; set; } = Hotkey.DefaultKeys.ToList();

    [JsonIgnore]
    public TimeOnly LockStart => ParseTimeOrDefault(LockWindowStart, new TimeOnly(23, 30));

    [JsonIgnore]
    public TimeOnly LockEnd => ParseTimeOrDefault(LockWindowEnd, new TimeOnly(8, 0));

    [JsonIgnore]
    public TimeSpan WarningOffset => TimeSpan.FromMinutes(Math.Max(1, WarningOffsetMinutes));

    [JsonIgnore]
    public TimeSpan OverrideDuration => TimeSpan.FromMinutes(Math.Clamp(OverrideMinutes, 1, 240));

    /// <summary>The configured stop combo, falling back to the default if invalid.</summary>
    [JsonIgnore]
    public IReadOnlyList<int> StopHotkey => Hotkey.NormalizeOrDefault(StopHotkeyKeys);

    private static TimeOnly ParseTimeOrDefault(string value, TimeOnly fallback)
    {
        return TimeOnly.TryParse(value, out var parsed) ? parsed : fallback;
    }
}

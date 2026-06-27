using System.Buffers.Binary;
using NightLock.Core;

internal static class Program
{
    private static int Main()
    {
        TestPolicySchedule();
        TestPasswordVerifier();
        TestHotkey();
        TestSettingsDefaults();
        TestTrustedTime();
        Console.WriteLine("NightLock.Core.Tests passed.");
        return 0;
    }

    private static void TestPolicySchedule()
    {
        var settings = new NightLockSettings();

        AssertPhase(Local(2026, 06, 18, 22, 49), settings, LockPhase.Unrestricted);
        AssertPhase(Local(2026, 06, 18, 22, 50), settings, LockPhase.Warning);
        AssertPhase(Local(2026, 06, 18, 23, 00), settings, LockPhase.Restricted);
        AssertPhase(Local(2026, 06, 19, 07, 59), settings, LockPhase.Restricted);
        AssertPhase(Local(2026, 06, 19, 08, 00), settings, LockPhase.Unrestricted);

        var overridden = NightLockPolicy.Evaluate(
            Local(2026, 06, 18, 23, 45),
            settings,
            Local(2026, 06, 19, 00, 15));
        Assert(overridden.Phase == LockPhase.Overridden, "Expected overridden restricted window.");

        // A same-day (non-midnight-crossing) window must not treat the morning as restricted.
        var sameDay = new NightLockSettings { LockWindowStart = "08:00", LockWindowEnd = "17:00" };
        AssertPhase(Local(2026, 06, 18, 07, 00), sameDay, LockPhase.Unrestricted);
        AssertPhase(Local(2026, 06, 18, 12, 00), sameDay, LockPhase.Restricted);
        AssertPhase(Local(2026, 06, 18, 18, 00), sameDay, LockPhase.Unrestricted);
    }

    private static void TestPasswordVerifier()
    {
        var verifier = PasswordHasher.Create("parent-pass");
        Assert(PasswordHasher.Verify("parent-pass", verifier), "Expected valid password.");
        Assert(!PasswordHasher.Verify("wrong-pass", verifier), "Expected invalid password.");
        Assert(!string.IsNullOrWhiteSpace(verifier.SaltBase64), "Expected salt.");
        Assert(!string.Equals(verifier.HashBase64, "parent-pass", StringComparison.Ordinal), "Password must not be plaintext.");
    }

    private static void TestHotkey()
    {
        // Default combo is LShift + RShift + 6 + 7 and is valid.
        Assert(Hotkey.IsValid(Hotkey.DefaultKeys), "Default hotkey must be valid.");
        Assert(Hotkey.DefaultKeys.SequenceEqual(new[] { Hotkey.VkLShift, Hotkey.VkRShift, 0x36, 0x37 }),
            "Default hotkey must be LShift+RShift+6+7.");

        // Parsing friendly tokens yields the same key set.
        var parsed = Hotkey.Parse("LShift+RShift+6+7");
        Assert(parsed is not null && parsed.SequenceEqual(Hotkey.DefaultKeys), "Parse should round-trip the default combo.");

        // A combo of only modifiers is rejected (no non-modifier key).
        Assert(!Hotkey.IsValid(new[] { 0xA0, 0xA1 }), "Modifier-only combo must be invalid.");

        // Too few / too many keys are rejected.
        Assert(!Hotkey.IsValid(new[] { 0x41 }), "Single key must be invalid.");
        Assert(!Hotkey.IsValid(new[] { 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47 }), "Seven keys must be invalid.");

        // Unknown tokens fail to parse.
        Assert(Hotkey.Parse("LShift+Nope") is null, "Unknown token must fail to parse.");

        // ToTokenString is round-trippable through Parse.
        var token = Hotkey.ToTokenString(Hotkey.DefaultKeys);
        var reparsed = Hotkey.Parse(token);
        Assert(reparsed is not null && reparsed.SequenceEqual(Hotkey.DefaultKeys), $"Token string '{token}' must round-trip.");

        // NormalizeOrDefault falls back to the default for invalid input.
        Assert(Hotkey.NormalizeOrDefault(new[] { 0xA0 }).SequenceEqual(Hotkey.DefaultKeys),
            "Invalid combo must normalize to the default.");
    }

    private static void TestSettingsDefaults()
    {
        var settings = new NightLockSettings();
        Assert(settings.SuppressWindowsKey, "Windows-key suppression must default to on.");
        Assert(settings.StopHotkey.SequenceEqual(Hotkey.DefaultKeys), "StopHotkey must default to the standard combo.");
        Assert(settings.UseTrustedTime, "Trusted time must default to on.");
        Assert(settings.NormalizedNtpServers.SequenceEqual(new[] { "pool.ntp.org", "time.windows.com", "time.google.com" }),
            "NTP servers must default to the standard list.");
        Assert(settings.NtpResyncInterval == TimeSpan.FromMinutes(60), "NTP resync interval must default to 60 minutes.");
        Assert(settings.NtpTimeout == TimeSpan.FromSeconds(3), "NTP timeout must default to 3 seconds.");

        var clamped = new NightLockSettings
        {
            NtpServers = [" ", ""],
            NtpResyncMinutes = 1,
            NtpTimeoutSeconds = 99
        };
        Assert(clamped.NormalizedNtpServers.SequenceEqual(settings.NormalizedNtpServers), "Empty NTP server list must use defaults.");
        Assert(clamped.NtpResyncInterval == TimeSpan.FromMinutes(5), "NTP resync interval must clamp low values.");
        Assert(clamped.NtpTimeout == TimeSpan.FromSeconds(15), "NTP timeout must clamp high values.");

        var paths = new AppPaths(Path.Combine(Path.GetTempPath(), $"nightlock-tests-{Guid.NewGuid():N}"));
        try
        {
            var store = new ConfigStore(paths);
            store.Save(new NightLockSettings());
            var roundTripped = store.LoadOrCreateDefault();
            Assert(roundTripped.UseTrustedTime, "ConfigStore must round-trip UseTrustedTime.");
            Assert(roundTripped.NormalizedNtpServers.SequenceEqual(settings.NormalizedNtpServers),
                "ConfigStore must round-trip default NTP servers.");
            Assert(roundTripped.NtpResyncInterval == TimeSpan.FromMinutes(60), "ConfigStore must round-trip NTP resync default.");
            Assert(roundTripped.NtpTimeout == TimeSpan.FromSeconds(3), "ConfigStore must round-trip NTP timeout default.");
        }
        finally
        {
            if (Directory.Exists(paths.DataDirectory))
            {
                Directory.Delete(paths.DataDirectory, recursive: true);
            }
        }
    }

    private static void TestTrustedTime()
    {
        var request = SntpClient.BuildRequest();
        Assert(request.Length == 48, "SNTP request must be 48 bytes.");
        Assert(request[0] == 0x23, "SNTP request must use LI=0, VN=4, Mode=3.");

        var knownUtc = new DateTimeOffset(2026, 6, 25, 12, 34, 56, TimeSpan.Zero);
        var response = new byte[48];
        WriteNtpTransmitTimestamp(response, knownUtc);
        Assert(SntpClient.ParseTransmitTimestamp(response) == knownUtc, "SNTP transmit timestamp must parse known UTC.");

        AssertThrows(() => SntpClient.ParseTransmitTimestamp(new byte[48]), "Zero SNTP timestamp must be rejected.");
        var implausible = new byte[48];
        WriteNtpTransmitTimestamp(implausible, new DateTimeOffset(2019, 12, 31, 23, 59, 59, TimeSpan.Zero));
        AssertThrows(() => SntpClient.ParseTransmitTimestamp(implausible), "Implausible SNTP timestamp must be rejected.");

        var fakeMonotonic = new FakeMonotonicClock { ElapsedMilliseconds = 1_000 };
        var fallbackNow = Local(2035, 1, 1, 1, 2);
        var unsynced = new TrustedClock(fakeMonotonic, () => null, () => fallbackNow, TimeSpan.FromMinutes(2));
        Assert(!unsynced.IsTrusted, "Clock must start untrusted.");
        Assert(unsynced.Now == fallbackNow, "Untrusted clock must fall back to system time.");

        var anchorUtc = new DateTimeOffset(2026, 6, 25, 10, 0, 0, TimeSpan.Zero);
        var systemNow = Local(2099, 1, 1, 0, 0);
        var trusted = new TrustedClock(fakeMonotonic, () => anchorUtc, () => systemNow, TimeSpan.FromMinutes(2));
        trusted.TrySync();
        Assert(trusted.IsTrusted, "Successful sync must mark clock trusted.");

        fakeMonotonic.ElapsedMilliseconds += 5 * 60 * 1_000;
        var expected = TimeZoneInfo.ConvertTime(anchorUtc.AddMinutes(5), TimeZoneInfo.Local);
        Assert(trusted.Now == expected, "Trusted clock must advance by monotonic elapsed time and ignore system time.");

        systemNow = expected.AddMinutes(10);
        Assert(trusted.Drift is not null && Math.Abs(trusted.Drift.Value.TotalMinutes + 10) < 0.001,
            "Trusted clock must expose drift from system time.");
        Assert(trusted.TryConsumeDriftWarning(out var drift), "Large drift must trigger one tamper-evidence warning.");
        Assert(Math.Abs(drift.TotalMinutes + 10) < 0.001, "Drift warning must report the drift magnitude and direction.");
        Assert(!trusted.TryConsumeDriftWarning(out _), "Drift warning must be latched after first trigger.");
    }

    private static void AssertPhase(DateTimeOffset now, NightLockSettings settings, LockPhase expected)
    {
        var state = NightLockPolicy.Evaluate(now, settings, overrideUntil: null);
        Assert(state.Phase == expected, $"Expected {expected} at {now:O}, got {state.Phase}.");
    }

    private static DateTimeOffset Local(int year, int month, int day, int hour, int minute)
    {
        var local = new DateTime(year, month, day, hour, minute, 0);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void WriteNtpTransmitTimestamp(byte[] buffer, DateTimeOffset timestamp)
    {
        var ntpEpoch = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var seconds = (uint)(timestamp.ToUniversalTime() - ntpEpoch).TotalSeconds;
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(40, 4), seconds);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(44, 4), 0);
    }

    private sealed class FakeMonotonicClock : IMonotonicClock
    {
        public long ElapsedMilliseconds { get; set; }
    }
}

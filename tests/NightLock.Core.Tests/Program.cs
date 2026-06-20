using NightLock.Core;

internal static class Program
{
    private static int Main()
    {
        TestPolicySchedule();
        TestPasswordVerifier();
        TestHotkey();
        TestSettingsDefaults();
        Console.WriteLine("NightLock.Core.Tests passed.");
        return 0;
    }

    private static void TestPolicySchedule()
    {
        var settings = new NightLockSettings();

        AssertPhase(Local(2026, 06, 18, 23, 19), settings, LockPhase.Unrestricted);
        AssertPhase(Local(2026, 06, 18, 23, 20), settings, LockPhase.Warning);
        AssertPhase(Local(2026, 06, 18, 23, 30), settings, LockPhase.Restricted);
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
}

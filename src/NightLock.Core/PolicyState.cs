namespace NightLock.Core;

public enum LockPhase
{
    Unrestricted,
    Warning,
    Restricted,
    Overridden
}

public sealed record PolicyState(
    LockPhase Phase,
    DateTimeOffset Now,
    DateTimeOffset NextChangeAt,
    bool IsRestrictedWindow,
    bool IsOverrideActive,
    string Message);

using NightLock.Core;

/// <summary>
/// @spec spec://modules/core/FEAT-002-parent-password-override#password-setup
/// @spec spec://modules/core/FEAT-004-emergency-stop-hotkey#hotkey-config
/// @spec spec://modules/core/FEAT-005-windows-key-suppression#root
/// @spec spec://modules/core/INFRA-001-windows-runtime-baseline#config
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var paths = AppPaths.Default();
        var configStore = new ConfigStore(paths);
        var logger = new FileLogger(paths);

        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "set-password":
                    return SetPassword(args, configStore, logger);

                case "set-override":
                    return SetOverride(args, configStore, logger);

                case "set-winkey":
                    return SetWinKey(args, configStore, logger);

                case "set-hotkey":
                    return SetHotkey(args, configStore, logger);

                case "status":
                    return Status(configStore);

                default:
                    Console.Error.WriteLine($"Unknown command: {args[0]}");
                    PrintHelp();
                    return 2;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int SetPassword(string[] args, ConfigStore configStore, FileLogger logger)
    {
        var password = ReadOption(args, "--password");
        if (string.IsNullOrEmpty(password) && HasOption(args, "--password-stdin"))
        {
            password = Console.In.ReadLine();
        }

        if (string.IsNullOrEmpty(password))
        {
            Console.Error.WriteLine("Missing --password or --password-stdin.");
            return 2;
        }

        var settings = configStore.LoadOrCreateDefault();
        settings.ParentPassword = PasswordHasher.Create(password);
        configStore.Save(settings);
        logger.Info("Parent password configured from CLI.");
        Console.WriteLine("Parent password configured.");
        return 0;
    }

    private static int SetOverride(string[] args, ConfigStore configStore, FileLogger logger)
    {
        var raw = ReadOption(args, "--minutes");
        if (!int.TryParse(raw, out var minutes))
        {
            Console.Error.WriteLine("Missing or invalid --minutes <number>.");
            return 2;
        }

        var settings = configStore.LoadOrCreateDefault();
        settings.OverrideMinutes = Math.Clamp(minutes, 1, 240);
        configStore.Save(settings);
        logger.Info($"Override duration set to {settings.OverrideMinutes} minutes from CLI.");
        Console.WriteLine($"Override duration set to {settings.OverrideMinutes} minutes (clamped to 1-240).");
        return 0;
    }

    private static int SetWinKey(string[] args, ConfigStore configStore, FileLogger logger)
    {
        bool enabled;
        if (HasOption(args, "--on"))
        {
            enabled = true;
        }
        else if (HasOption(args, "--off"))
        {
            enabled = false;
        }
        else
        {
            Console.Error.WriteLine("Specify --on or --off.");
            return 2;
        }

        var settings = configStore.LoadOrCreateDefault();
        settings.SuppressWindowsKey = enabled;
        configStore.Save(settings);
        logger.Info($"Windows-key suppression set to {(enabled ? "on" : "off")} from CLI.");
        Console.WriteLine($"Windows-key suppression during restricted hours: {(enabled ? "on" : "off")}.");
        return 0;
    }

    private static int SetHotkey(string[] args, ConfigStore configStore, FileLogger logger)
    {
        var raw = ReadOption(args, "--keys");
        var parsed = Hotkey.Parse(raw);
        if (parsed is null || !Hotkey.IsValid(parsed))
        {
            Console.Error.WriteLine("Missing or invalid --keys. Example: --keys \"LShift+RShift+6+7\" (2-6 keys, at least one non-modifier).");
            return 2;
        }

        var settings = configStore.LoadOrCreateDefault();
        settings.StopHotkeyKeys = parsed.ToList();
        configStore.Save(settings);
        logger.Info("Emergency stop hotkey set from CLI.");
        Console.WriteLine($"Emergency stop hotkey set to: {Hotkey.Describe(settings.StopHotkey)}.");
        return 0;
    }

    private static int Status(ConfigStore configStore)
    {
        var settings = configStore.LoadOrCreateDefault();
        var now = DateTimeOffset.Now;
        var state = NightLockPolicy.Evaluate(now, settings, overrideUntil: null);
        Console.WriteLine($"Phase: {state.Phase}");
        Console.WriteLine($"Now: {state.Now:O}");
        Console.WriteLine($"Next change: {state.NextChangeAt:O}");
        Console.WriteLine($"Restricted window: {state.IsRestrictedWindow}");
        Console.WriteLine($"Lock window: {settings.LockWindowStart}-{settings.LockWindowEnd}");
        Console.WriteLine($"Override minutes: {(int)settings.OverrideDuration.TotalMinutes}");
        Console.WriteLine($"Windows-key suppression: {(settings.SuppressWindowsKey ? "on" : "off")}");
        Console.WriteLine($"Emergency stop hotkey: {Hotkey.Describe(settings.StopHotkey)}");
        Console.WriteLine($"Parent password set: {settings.ParentPassword is not null}");
        Console.WriteLine(state.Message);
        return 0;
    }

    private static string? ReadOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static bool HasOption(string[] args, string name)
    {
        return args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
    }

    private static void PrintHelp()
    {
        Console.WriteLine("NightLock.Cli commands:");
        Console.WriteLine("  set-password --password <value>");
        Console.WriteLine("  set-password --password-stdin");
        Console.WriteLine("  set-override --minutes <1-240>");
        Console.WriteLine("  set-winkey --on | --off");
        Console.WriteLine("  set-hotkey --keys \"LShift+RShift+6+7\"");
        Console.WriteLine("  status");
    }
}

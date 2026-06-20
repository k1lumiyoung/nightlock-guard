using System.Threading;
using NightLock.Admin;
using NightLock.Core;

/// <summary>
/// Hidden, password-gated parent admin panel. It is intentionally not registered in the
/// Start menu / Windows search (the installer creates no shortcut); it is launched from the
/// helper tray menu or its known install path. Obscurity is convenience only — the real gate
/// is the parent password plus the data-directory ACLs.
///
/// @spec spec://modules/core/FEAT-003-parent-admin-panel#root
/// @spec spec://modules/core/FEAT-003-parent-admin-panel#access-control
/// @spec spec://modules/core/FEAT-003-parent-admin-panel#discoverability
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, "Global\\NightLockGuard.Admin", out var created);
        if (!created)
        {
            return;
        }

        ApplicationConfiguration.Initialize();

        var paths = AppPaths.Default();
        var store = new ConfigStore(paths);
        var logger = new FileLogger(paths);
        var settings = store.LoadOrCreateDefault();

        // Require the parent password before showing any setting. If none is set yet (fresh
        // install), the panel opens so the parent can create one.
        if (settings.ParentPassword is not null)
        {
            using var auth = new AdminAuthForm(entered => PasswordHasher.Verify(entered, settings.ParentPassword));
            if (auth.ShowDialog() != DialogResult.OK)
            {
                logger.Info("Admin panel closed at the password prompt.");
                return;
            }
        }

        logger.Info("Admin panel opened.");
        Application.Run(new AdminSettingsForm(store, logger));
    }
}

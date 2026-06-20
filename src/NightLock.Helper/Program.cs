using System.Threading;

namespace NightLock.Helper;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, "Global\\NightLockGuard.Helper", out var created);
        if (!created)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new NightLockApplicationContext());
    }
}

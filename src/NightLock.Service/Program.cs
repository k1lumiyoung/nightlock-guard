using System.ServiceProcess;

namespace NightLock.Service;

internal static class Program
{
    public static void Main(string[] args)
    {
        if (args.Any(arg => string.Equals(arg, "--console", StringComparison.OrdinalIgnoreCase)))
        {
            using var daemon = new PolicyDaemon();
            daemon.Start();
            Console.WriteLine("NightLock service daemon is running. Press Enter to stop.");
            Console.ReadLine();
            daemon.Stop();
            return;
        }

        ServiceBase.Run(new NightLockWindowsService());
    }
}

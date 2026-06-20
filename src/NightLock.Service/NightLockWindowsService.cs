using System.ServiceProcess;

namespace NightLock.Service;

/// <summary>
/// @spec spec://modules/core/INFRA-001-windows-runtime-baseline#service
/// </summary>
public sealed class NightLockWindowsService : ServiceBase
{
    private readonly PolicyDaemon _daemon = new();

    public NightLockWindowsService()
    {
        ServiceName = "NightLockGuard";
        CanStop = true;
        CanShutdown = true;
        CanHandleSessionChangeEvent = true;
    }

    protected override void OnStart(string[] args) => _daemon.Start();

    protected override void OnStop() => _daemon.Stop();

    protected override void OnSessionChange(SessionChangeDescription changeDescription)
    {
        _daemon.OnSessionChanged(changeDescription.Reason.ToString());
        base.OnSessionChange(changeDescription);
    }

    protected override void OnShutdown()
    {
        _daemon.Stop();
        base.OnShutdown();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _daemon.Dispose();
        }

        base.Dispose(disposing);
    }
}

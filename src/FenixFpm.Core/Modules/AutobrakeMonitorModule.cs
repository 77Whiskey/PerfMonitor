using FenixFpm.Core.Abstractions;
using FenixFpm.Core.Models;

namespace FenixFpm.Core.Modules;

public sealed class AutobrakeMonitorModule : IAirbusModule
{
    private readonly object _sync = new();
    private bool _warningLatched;

    public string Name => "AutobrakeMonitor";

    public ValueTask<IReadOnlyList<FlightEvent>> EvaluateAsync(FlightSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            var shouldWarn = !snapshot.OnGround &&
                             snapshot.LandingConfiguration.GearDown &&
                             snapshot.LandingConfiguration.AutobrakeMode == AutobrakeMode.Max;

            if (!shouldWarn)
            {
                _warningLatched = false;
                return ValueTask.FromResult<IReadOnlyList<FlightEvent>>(Array.Empty<FlightEvent>());
            }

            if (_warningLatched)
            {
                return ValueTask.FromResult<IReadOnlyList<FlightEvent>>(Array.Empty<FlightEvent>());
            }

            _warningLatched = true;
            return ValueTask.FromResult<IReadOnlyList<FlightEvent>>(
            [
                new FlightEvent(
                    snapshot.TimestampUtc,
                    "Autobrake.MaxAirborne",
                    "MAX autobrake not recommended for landing while airborne with landing gear down.",
                    EventSeverity.Warning,
                    (double)snapshot.LandingConfiguration.AutobrakeMode,
                    (double)AutobrakeMode.Medium)
            ]);
        }
    }
}
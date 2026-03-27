using FenixFpm.Core.Abstractions;
using FenixFpm.Core.Models;

namespace FenixFpm.Core.Modules;

public sealed class EcoCruiseModule : IAirbusModule
{
    public string Name => "EcoCruise";

    public ValueTask<IReadOnlyList<FlightEvent>> EvaluateAsync(FlightSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IReadOnlyList<FlightEvent>>(Array.Empty<FlightEvent>());
    }
}
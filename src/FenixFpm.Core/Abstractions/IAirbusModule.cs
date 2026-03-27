using FenixFpm.Core.Models;

namespace FenixFpm.Core.Abstractions;

public interface IAirbusModule
{
    string Name { get; }

    ValueTask<IReadOnlyList<FlightEvent>> EvaluateAsync(FlightSnapshot snapshot, CancellationToken cancellationToken = default);
}
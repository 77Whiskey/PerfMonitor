using FenixFpm.Core.Models;

namespace FenixFpm.Core.Abstractions;

public interface IDebriefService
{
    Task<FlightSession?> GetLatestFlightAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<FlightSession>> GetRecentFlightsAsync(int count = 10, CancellationToken cancellationToken = default);
    Task<IEnumerable<FlightEvent>> GetFlightEventsAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<PerformanceMetrics?> GetFlightMetricsAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<Scorecard?> GetFlightScorecardAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
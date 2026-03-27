using FenixFpm.Core.Abstractions;
using FenixFpm.Core.Models;

namespace FenixFpm.Core.Modules;

public sealed class LandingPerformanceModule : IAirbusModule
{
    private readonly IPerformanceDataService _performanceDataService;

    public LandingPerformanceModule(IPerformanceDataService performanceDataService)
    {
        _performanceDataService = performanceDataService;
    }

    public string Name => "LandingPerformance";

    public async ValueTask<IReadOnlyList<FlightEvent>> EvaluateAsync(FlightSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        _ = await _performanceDataService.CalculateLandingDistanceAsync(
            new LandingDistanceRequest(
                snapshot.GrossWeightKg,
                snapshot.OutsideAirTemperatureC,
                snapshot.PressureAltitudeFeet,
                RunwayCondition.Dry),
            cancellationToken);

        return Array.Empty<FlightEvent>();
    }
}
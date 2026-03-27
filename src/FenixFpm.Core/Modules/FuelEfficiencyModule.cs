using FenixFpm.Core.Abstractions;
using FenixFpm.Core.Models;

namespace FenixFpm.Core.Modules;

public sealed class FuelEfficiencyModule : IAirbusModule
{
    private readonly IPerformanceDataService _performanceData;
    private const double FuelFlowCruiseTargetKgHr = 2400;
    private const double FuelFlowClimbTargetKgHr = 6000;
    private const double FuelFlowDescentTargetKgHr = 1800;
    private const double DeviationThresholdPercent = 15.0;

    private double _totalFuelBurnedKg;
    private DateTimeOffset _phaseStartTime;
    private double _phaseFuelBurnedKg;
    private FlightPhase _currentPhase = FlightPhase.Unknown;

    public FuelEfficiencyModule(IPerformanceDataService performanceData)
    {
        _performanceData = performanceData;
        _phaseStartTime = DateTimeOffset.UtcNow;
    }

    public string Name => "Fuel Efficiency Monitor";

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public ValueTask<IReadOnlyList<FlightEvent>> EvaluateAsync(FlightSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var events = new List<FlightEvent>();
        var fuelFlowKgHr = snapshot.Engines.TotalFuelFlowKgPerHour;
        var newPhase = DetectFuelPhase(snapshot);

        if (newPhase != _currentPhase)
        {
            if (_currentPhase != FlightPhase.Unknown)
            {
                _phaseFuelBurnedKg = 0;
            }
            _currentPhase = newPhase;
            _phaseStartTime = snapshot.TimestampUtc;
        }

        _totalFuelBurnedKg += fuelFlowKgHr / 3600.0;
        _phaseFuelBurnedKg += fuelFlowKgHr / 3600.0;

        var targetFlow = _currentPhase switch
        {
            FlightPhase.Climb => FuelFlowClimbTargetKgHr,
            FlightPhase.Cruise => FuelFlowCruiseTargetKgHr,
            FlightPhase.Descent => FuelFlowDescentTargetKgHr,
            _ => FuelFlowCruiseTargetKgHr
        };

        var deviationPercent = targetFlow > 0 ? Math.Abs((fuelFlowKgHr - targetFlow) / targetFlow * 100) : 0;

        if (deviationPercent > DeviationThresholdPercent)
        {
            var severity = deviationPercent > DeviationThresholdPercent * 2
                ? EventSeverity.Warning
                : EventSeverity.Advisory;

            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                "FuelFlowDeviation",
                $"Fuel flow {fuelFlowKgHr:F0} kg/hr deviates {deviationPercent:F1}% from {_currentPhase} target {targetFlow:F0} kg/hr",
                severity,
                fuelFlowKgHr,
                targetFlow * (1 + DeviationThresholdPercent / 100))
            {
                FlightPhase = _currentPhase.ToString(),
                IsPositive = false
            });
        }

        if (snapshot.GrossWeightKg < 5000)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                "LowFuel",
                $"Low fuel remaining: {snapshot.GrossWeightKg:F0} kg",
                EventSeverity.Warning,
                snapshot.GrossWeightKg,
                5000)
            {
                FlightPhase = _currentPhase.ToString()
            });
        }

        return ValueTask.FromResult<IReadOnlyList<FlightEvent>>(events);
    }

    private static FlightPhase DetectFuelPhase(FlightSnapshot snapshot)
    {
        if (snapshot.OnGround) return FlightPhase.Ground;
        if (snapshot.BaroAltitudeFeet < 2000) return FlightPhase.Climb;
        if (snapshot.BaroAltitudeFeet < 15000) return FlightPhase.Cruise;
        if (snapshot.BaroAltitudeFeet < 3000) return FlightPhase.Descent;
        return FlightPhase.Cruise;
    }

    public double GetTotalFuelBurnedKg() => _totalFuelBurnedKg;
    public double GetPhaseFuelBurnedKg(FlightPhase phase) => _currentPhase == phase ? _phaseFuelBurnedKg : 0;
}
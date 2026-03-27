using FenixFpm.Core.Abstractions;
using FenixFpm.Core.Models;
using FenixFpm.Core.Services;

namespace FenixFpm.Core.Modules;

public sealed class TaxiPerformanceModule : IAirbusModule
{
    private readonly IPerformanceDataService _performanceData;
    private readonly FlightPhaseDetector _phaseDetector = new();

    private const double EngineOnThresholdPercent = 5.0;
    private const int MaxTaxiSpeedKt = 20;
    private const int BrakeTempWarningThresholdC = 300;
    private const int BrakeTempCriticalThresholdC = 350;

    private bool _wasInTaxiPhase;
    private DateTimeOffset _taxiStartTime;

    public TaxiPerformanceModule(IPerformanceDataService performanceData)
    {
        _performanceData = performanceData;
    }

    public string Name => "Taxi Performance Monitor";
    public string Description => "Monitors taxi operations against A320 operational limitations";

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public ValueTask<IReadOnlyList<FlightEvent>> EvaluateAsync(FlightSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var events = new List<FlightEvent>();
        var phase = _phaseDetector.DetectPhase(snapshot, FlightPhase.Unknown);

        if (phase == FlightPhase.Taxi || (snapshot.OnGround && snapshot.GroundSpeedKnots > 1))
        {
            if (!_wasInTaxiPhase)
            {
                _wasInTaxiPhase = true;
                _taxiStartTime = snapshot.TimestampUtc;
            }

            CheckTaxiSpeed(snapshot, events);
            CheckWeightLimit(snapshot, events);
            CheckTurnSpeed(snapshot, events);
        }
        else
        {
            _wasInTaxiPhase = false;
        }

        return ValueTask.FromResult<IReadOnlyList<FlightEvent>>(events);
    }

    private void CheckTaxiSpeed(FlightSnapshot snapshot, List<FlightEvent> events)
    {
        var groundSpeed = snapshot.GroundSpeedKnots;
        var maxSpeed = _performanceData.MaxTaxiTurnSpeedHeavyKt;
        
        if (snapshot.GrossWeightKg > _performanceData.MaxTaxiTurnWeightKg)
        {
            maxSpeed = _performanceData.MaxTaxiTurnSpeedHeavyKt;
        }
        else
        {
            maxSpeed = Math.Min(25, maxSpeed + 5);
        }

        if (groundSpeed > maxSpeed)
        {
            var severity = groundSpeed > maxSpeed + 10 ? EventSeverity.Warning : EventSeverity.Advisory;
            var limit = maxSpeed;
            
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                "TaxiSpeedExceedance",
                $"Ground speed {groundSpeed:F0} kt exceeds taxi limit of {limit} kt",
                severity,
                groundSpeed,
                limit)
            {
                FlightPhase = FlightPhase.Taxi.ToString(),
                IsPositive = false
            });
        }
    }

    private void CheckWeightLimit(FlightSnapshot snapshot, List<FlightEvent> events)
    {
        var maxWeight = _performanceData.MaxTaxiTurnWeightKg;
        
        if (snapshot.GrossWeightKg > maxWeight)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                "TaxiWeightExceedance",
                $"Aircraft weight {snapshot.GrossWeightKg:F0} kg exceeds taxi turn weight limit of {maxWeight} kg",
                EventSeverity.Warning,
                snapshot.GrossWeightKg,
                maxWeight)
            {
                FlightPhase = FlightPhase.Taxi.ToString(),
                IsPositive = false
            });
        }
    }

    private void CheckTurnSpeed(FlightSnapshot snapshot, List<FlightEvent> events)
    {
        var bankAngle = Math.Abs(snapshot.BankDegrees);
        var groundSpeed = snapshot.GroundSpeedKnots;
        var weight = snapshot.GrossWeightKg;

        if (bankAngle > 15 && groundSpeed > 10)
        {
            var maxTurnSpeed = weight > _performanceData.MaxTaxiTurnWeightKg
                ? _performanceData.MaxTaxiTurnSpeedHeavyKt
                : 20;

            if (groundSpeed > maxTurnSpeed)
            {
                events.Add(new FlightEvent(
                    snapshot.TimestampUtc,
                    "TurnSpeedExceedance",
                    $"High speed {groundSpeed:F0} kt with {bankAngle:F1}° bank - risk of rudder/pylon strike",
                    EventSeverity.Warning,
                    groundSpeed,
                    maxTurnSpeed)
                {
                    FlightPhase = FlightPhase.Taxi.ToString(),
                    IsPositive = false
                });
            }
        }
    }
}
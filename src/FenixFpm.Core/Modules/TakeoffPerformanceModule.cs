using FenixFpm.Core.Abstractions;
using FenixFpm.Core.Models;

namespace FenixFpm.Core.Modules;

public sealed class TakeoffPerformanceModule : IAirbusModule
{
    private const double RotationSpeedV2Knots = 140.0;
    private const double MaxPitchAfterRotation = 15.0;
    private const double MinPitchAfterRotation = 8.0;
    private const double MaxRollAfterTakeoff = 15.0;
    private const double MinClimbSpeedKnots = 160.0;
    private const double AccelerationLimitFpm = -500.0;

    private bool _hasDetectedTakeoff;
    private bool _rotationEvaluated;
    private double _rotationSpeedKnots = double.NaN;

    public string Name => "TakeoffPerformance";

    public ValueTask<IReadOnlyList<FlightEvent>> EvaluateAsync(FlightSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var events = new List<FlightEvent>();
        var phase = DetectPhase(snapshot);

        if (phase == FlightPhase.Ground && snapshot.OnGround)
        {
            _hasDetectedTakeoff = false;
            _rotationEvaluated = false;
            _rotationSpeedKnots = double.NaN;
            return ValueTask.FromResult<IReadOnlyList<FlightEvent>>(events);
        }

        if (!_hasDetectedTakeoff && phase != FlightPhase.Ground && !snapshot.OnGround)
        {
            _hasDetectedTakeoff = true;
            if (snapshot.IndicatedAirspeedKnots >= 80 && snapshot.IndicatedAirspeedKnots <= 160)
            {
                _rotationSpeedKnots = snapshot.IndicatedAirspeedKnots;
            }
        }

        if (_hasDetectedTakeoff && !_rotationEvaluated && snapshot.OnGround == false)
        {
            _rotationEvaluated = true;
            events.AddRange(EvaluateTakeoff(snapshot));
        }

        if (phase == FlightPhase.Climb && _rotationEvaluated)
        {
            events.AddRange(EvaluateClimbout(snapshot));
        }

        return ValueTask.FromResult<IReadOnlyList<FlightEvent>>(events);
    }

    private static FlightPhase DetectPhase(FlightSnapshot snapshot)
    {
        if (snapshot.OnGround)
        {
            return FlightPhase.Ground;
        }

        if (snapshot.BaroAltitudeFeet < 1000)
        {
            return snapshot.RadioAltitudeFeet < 35 ? FlightPhase.Takeoff : FlightPhase.Climb;
        }

        return FlightPhase.Climb;
    }

    private IEnumerable<FlightEvent> EvaluateTakeoff(FlightSnapshot snapshot)
    {
        var events = new List<FlightEvent>();

        if (!double.IsNaN(_rotationSpeedKnots))
        {
            if (_rotationSpeedKnots < RotationSpeedV2Knots - 5)
            {
                events.Add(new FlightEvent(
                    snapshot.TimestampUtc,
                    "Takeoff.RotationSpeedLow",
                    $"Rotation speed {(_rotationSpeedKnots):F1} kt below V2",
                    EventSeverity.Warning,
                    _rotationSpeedKnots,
                    RotationSpeedV2Knots));
            }
            else if (_rotationSpeedKnots > RotationSpeedV2Knots + 15)
            {
                events.Add(new FlightEvent(
                    snapshot.TimestampUtc,
                    "Takeoff.RotationSpeedHigh",
                    $"Rotation speed {(_rotationSpeedKnots):F1} kt significantly above V2",
                    EventSeverity.Advisory,
                    _rotationSpeedKnots,
                    RotationSpeedV2Knots + 15));
            }
            else
            {
                events.Add(new FlightEvent(
                    snapshot.TimestampUtc,
                    "Takeoff.RotationSpeedOptimal",
                    $"Rotation speed {(_rotationSpeedKnots):F1} kt within V2 range",
                    EventSeverity.Information,
                    _rotationSpeedKnots,
                    RotationSpeedV2Knots)
                {
                    IsPositive = true
                });
            }
        }

        if (snapshot.PitchDegrees < MinPitchAfterRotation)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                "Takeoff.PitchLow",
                $"Pitch after rotation {snapshot.PitchDegrees:F1}° below minimum",
                EventSeverity.Warning,
                snapshot.PitchDegrees,
                MinPitchAfterRotation));
        }
        else if (snapshot.PitchDegrees > MaxPitchAfterRotation)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                "Takeoff.PitchHigh",
                $"Pitch after rotation {snapshot.PitchDegrees:F1}° exceeds maximum",
                EventSeverity.Warning,
                snapshot.PitchDegrees,
                MaxPitchAfterRotation));
        }
        else
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                "Takeoff.PitchOptimal",
                $"Pitch after rotation {snapshot.PitchDegrees:F1}° within optimal range",
                EventSeverity.Information,
                snapshot.PitchDegrees,
                MaxPitchAfterRotation)
            {
                IsPositive = true
            });
        }

        if (Math.Abs(snapshot.BankDegrees) > MaxRollAfterTakeoff)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                "Takeoff.BankExcessive",
                $"Bank angle {Math.Abs(snapshot.BankDegrees):F1}° exceeds limit during takeoff",
                EventSeverity.Warning,
                Math.Abs(snapshot.BankDegrees),
                MaxRollAfterTakeoff));
        }

        return events;
    }

    private static IEnumerable<FlightEvent> EvaluateClimbout(FlightSnapshot snapshot)
    {
        var events = new List<FlightEvent>();

        if (snapshot.IndicatedAirspeedKnots < MinClimbSpeedKnots)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                "Climbout.SpeedLow",
                $"Climbout speed {snapshot.IndicatedAirspeedKnots:F1} kt below recommended",
                EventSeverity.Advisory,
                snapshot.IndicatedAirspeedKnots,
                MinClimbSpeedKnots));
        }

        if (snapshot.VerticalSpeedFpm < AccelerationLimitFpm)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                "Climbout.VerticalSpeedLow",
                $"Vertical speed {snapshot.VerticalSpeedFpm:F0} fpm indicates possible slow climb",
                EventSeverity.Advisory,
                snapshot.VerticalSpeedFpm,
                AccelerationLimitFpm));
        }

        if (snapshot.LandingConfiguration.Configuration != LandingConfiguration.NotConfigured)
        {
            var configName = snapshot.LandingConfiguration.Configuration == LandingConfiguration.Config3 ? "CONF 3" : "FULL";
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                "Climbout.ConfigurationNotClean",
                $"Landing configuration ({configName}) not retracted during climbout",
                EventSeverity.Warning,
                (double)snapshot.LandingConfiguration.Configuration,
                (double)LandingConfiguration.NotConfigured));
        }

        return events;
    }
}
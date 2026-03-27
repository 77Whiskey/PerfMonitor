using FenixFpm.Core.Abstractions;
using FenixFpm.Core.Models;

namespace FenixFpm.Core.Modules;

public sealed class ApproachPerformanceModule : IAirbusModule
{
    private const double HighGateFeet = 1000.0;
    private const double MidGateFeet = 500.0;
    private const double LowGateFeet = 200.0;
    private const double VappMinKnots = 130.0;
    private const double VappMaxKnots = 170.0;
    private const double MaxSinkRateFpm = -1000.0;
    private const double MaxBankDegrees = 7.0;
    private const double MaxPitchHigh = 10.0;
    private const double MinPitch = -2.5;
    private const double GlideslopeDeviationDegrees = 1.5;
    private const double LocalizerDeviationDegrees = 2.0;

    private readonly object _sync = new();
    private int _highGateEvaluated;
    private int _midGateEvaluated;
    private int _lowGateEvaluated;
    private double _lastSmoothedRadioAltitude = double.NaN;
    private readonly Queue<double> _radioAltitudeWindow = new();
    private const int RadioAltitudeWindowSize = 10;

    public string Name => "ApproachPerformance";

    public ValueTask<IReadOnlyList<FlightEvent>> EvaluateAsync(FlightSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (snapshot.OnGround || snapshot.RadioAltitudeFeet > 1500)
            {
                Reset();
                return ValueTask.FromResult<IReadOnlyList<FlightEvent>>(Array.Empty<FlightEvent>());
            }

            if (!IsApproachPhase(snapshot))
            {
                return ValueTask.FromResult<IReadOnlyList<FlightEvent>>(Array.Empty<FlightEvent>());
            }

            var smoothedRadioAltitude = AddAndAverage(snapshot.RadioAltitudeFeet);
            var events = new List<FlightEvent>();

            if (_highGateEvaluated == 0 && CrossedGate(HighGateFeet, smoothedRadioAltitude))
            {
                _highGateEvaluated = 1;
                events.AddRange(EvaluateGate(snapshot, smoothedRadioAltitude, HighGateFeet, "1000"));
            }

            if (_midGateEvaluated == 0 && CrossedGate(MidGateFeet, smoothedRadioAltitude))
            {
                _midGateEvaluated = 1;
                events.AddRange(EvaluateGate(snapshot, smoothedRadioAltitude, MidGateFeet, "500"));
            }

            if (_lowGateEvaluated == 0 && CrossedGate(LowGateFeet, smoothedRadioAltitude))
            {
                _lowGateEvaluated = 1;
                events.AddRange(EvaluateGate(snapshot, smoothedRadioAltitude, LowGateFeet, "200"));
            }

            events.AddRange(EvaluateConfiguration(snapshot));
            events.AddRange(EvaluateAutopilot(snapshot));

            _lastSmoothedRadioAltitude = smoothedRadioAltitude;
            return ValueTask.FromResult<IReadOnlyList<FlightEvent>>(events);
        }
    }

    private static bool IsApproachPhase(FlightSnapshot snapshot)
    {
        return snapshot.Autopilot.ApproachModeActive ||
               snapshot.Autopilot.GlideslopeCaptured ||
               snapshot.Autopilot.LocalizerCaptured ||
               snapshot.Autopilot.VerticalMode is FmaVerticalMode.FinalApproach or FmaVerticalMode.Glideslope;
    }

    private double AddAndAverage(double radioAltitudeFeet)
    {
        _radioAltitudeWindow.Enqueue(radioAltitudeFeet);
        while (_radioAltitudeWindow.Count > RadioAltitudeWindowSize)
        {
            _radioAltitudeWindow.Dequeue();
        }
        return _radioAltitudeWindow.Average();
    }

    private bool CrossedGate(double gateFeet, double smoothedRadioAltitudeFeet)
    {
        if (double.IsNaN(_lastSmoothedRadioAltitude))
        {
            return smoothedRadioAltitudeFeet <= gateFeet;
        }
        return _lastSmoothedRadioAltitude > gateFeet && smoothedRadioAltitudeFeet <= gateFeet;
    }

    private static IEnumerable<FlightEvent> EvaluateGate(FlightSnapshot snapshot, double smoothedRadioAltitude, double gateFeet, string gateLabel)
    {
        var events = new List<FlightEvent>();

        var minSpeed = Math.Max(VappMinKnots, snapshot.VappKnots - 5);
        var maxSpeed = Math.Min(VappMaxKnots, snapshot.VappKnots + 10);

        if (snapshot.IndicatedAirspeedKnots < minSpeed)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                $"Approach.SpeedLow.{gateLabel}ft",
                $"Speed {snapshot.IndicatedAirspeedKnots:F1} kt below minimum at {gateLabel} ft",
                EventSeverity.Warning,
                snapshot.IndicatedAirspeedKnots,
                minSpeed));
        }
        else if (snapshot.IndicatedAirspeedKnots > maxSpeed)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                $"Approach.SpeedHigh.{gateLabel}ft",
                $"Speed {snapshot.IndicatedAirspeedKnots:F1} kt above target at {gateLabel} ft",
                EventSeverity.Warning,
                snapshot.IndicatedAirspeedKnots,
                maxSpeed));
        }
        else
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                $"Approach.SpeedOptimal.{gateLabel}ft",
                $"Speed {snapshot.IndicatedAirspeedKnots:F1} kt within target at {gateLabel} ft",
                EventSeverity.Information,
                snapshot.IndicatedAirspeedKnots,
                maxSpeed)
            {
                IsPositive = true
            });
        }

        if (snapshot.PitchDegrees < MinPitch)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                $"Approach.PitchLow.{gateLabel}ft",
                $"Pitch {snapshot.PitchDegrees:F1}° below minimum at {gateLabel} ft",
                EventSeverity.Warning,
                snapshot.PitchDegrees,
                MinPitch));
        }
        else if (snapshot.PitchDegrees > MaxPitchHigh)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                $"Approach.PitchHigh.{gateLabel}ft",
                $"Pitch {snapshot.PitchDegrees:F1}° above maximum at {gateLabel} ft",
                EventSeverity.Warning,
                snapshot.PitchDegrees,
                MaxPitchHigh));
        }

        if (Math.Abs(snapshot.BankDegrees) > MaxBankDegrees)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                $"Approach.BankHigh.{gateLabel}ft",
                $"Bank {Math.Abs(snapshot.BankDegrees):F1}° exceeds limit at {gateLabel} ft",
                EventSeverity.Warning,
                Math.Abs(snapshot.BankDegrees),
                MaxBankDegrees));
        }

        if (snapshot.VerticalSpeedFpm < MaxSinkRateFpm)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                $"Approach.SinkRateHigh.{gateLabel}ft",
                $"Sink rate {snapshot.VerticalSpeedFpm:F0} fpm exceeds limit at {gateLabel} ft",
                EventSeverity.Warning,
                snapshot.VerticalSpeedFpm,
                MaxSinkRateFpm));
        }
        else if (snapshot.VerticalSpeedFpm > -500 && gateFeet >= 500)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                $"Approach.SinkRateLow.{gateLabel}ft",
                $"Sink rate {snapshot.VerticalSpeedFpm:F0} fpm too shallow at {gateLabel} ft",
                EventSeverity.Advisory,
                snapshot.VerticalSpeedFpm,
                -500));
        }

        return events;
    }

    private static IEnumerable<FlightEvent> EvaluateConfiguration(FlightSnapshot snapshot)
    {
        var events = new List<FlightEvent>();

        if (snapshot.RadioAltitudeFeet < 1000 && snapshot.RadioAltitudeFeet > 500)
        {
            if (!snapshot.LandingConfiguration.IsLandingReady)
            {
                events.Add(new FlightEvent(
                    snapshot.TimestampUtc,
                    "Approach.ConfigurationNotReady",
                    $"Landing configuration not ready at {snapshot.RadioAltitudeFeet:F0} ft",
                    EventSeverity.Warning,
                    (double)snapshot.LandingConfiguration.Configuration,
                    (double)LandingConfiguration.Config3));
            }
        }

        if (snapshot.RadioAltitudeFeet < 1500 && snapshot.RadioAltitudeFeet > 1000)
        {
            if (snapshot.LandingConfiguration.AutobrakeMode == AutobrakeMode.Off)
            {
                events.Add(new FlightEvent(
                    snapshot.TimestampUtc,
                    "Approach.AutobrakeNotArmed",
                    $"Autobrake not armed above 1500 ft",
                    EventSeverity.Advisory,
                    (double)snapshot.LandingConfiguration.AutobrakeMode,
                    (double)AutobrakeMode.Low));
            }
        }

        return events;
    }

    private static IEnumerable<FlightEvent> EvaluateAutopilot(FlightSnapshot snapshot)
    {
        var events = new List<FlightEvent>();

        if (snapshot.RadioAltitudeFeet < 500 && !snapshot.Autopilot.LocalizerCaptured)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                "Approach.LocalizerNotCaptured",
                $"Localizer not captured at {snapshot.RadioAltitudeFeet:F0} ft",
                EventSeverity.Advisory,
                snapshot.Autopilot.LocalizerCaptured ? 1 : 0,
                1));
        }

        if (snapshot.RadioAltitudeFeet < 1000 && !snapshot.Autopilot.GlideslopeCaptured)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                "Approach.GlideslopeNotCaptured",
                $"Glideslope not captured at {snapshot.RadioAltitudeFeet:F0} ft",
                EventSeverity.Advisory,
                snapshot.Autopilot.GlideslopeCaptured ? 1 : 0,
                1));
        }

        return events;
    }

    private void Reset()
    {
        _highGateEvaluated = 0;
        _midGateEvaluated = 0;
        _lowGateEvaluated = 0;
        _lastSmoothedRadioAltitude = double.NaN;
        _radioAltitudeWindow.Clear();
    }
}
using FenixFpm.Core.Abstractions;
using FenixFpm.Core.Models;

namespace FenixFpm.Core.Modules;

public sealed class StabilizedApproachModule : IAirbusModule
{
    private const int RadioAltitudeWindowSize = 10;
    private readonly SopConfiguration _sopConfig;
    private readonly object _sync = new();
    private readonly Queue<double> _radioAltitudeWindow = new();

    private bool _highGateEvaluated;
    private bool _lowGateEvaluated;
    private double _lastSmoothedRadioAltitudeFeet = double.NaN;

    public string Name => "StabilizedApproach";

    public StabilizedApproachModule()
        : this(SopConfiguration.Default)
    {
    }

    public StabilizedApproachModule(SopConfiguration sopConfig)
    {
        _sopConfig = sopConfig ?? SopConfiguration.Default;
    }

    public StabilizedApproachModule(StabilizedApproachCriteria criteria)
        : this(ToSopConfiguration(criteria))
    {
    }

    public ValueTask<IReadOnlyList<FlightEvent>> EvaluateAsync(FlightSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (snapshot.OnGround || snapshot.RadioAltitudeFeet > 1500.0)
            {
                Reset();
            }

            var smoothedRadioAltitude = AddAndAverage(snapshot.RadioAltitudeFeet);
            var events = new List<FlightEvent>();

            var highGate = _sopConfig.StabilizedApproachGates?.Imc1000FtAal;
            var lowGate = _sopConfig.StabilizedApproachGates?.Vmc500FtAal;

            if (highGate is not null && !_highGateEvaluated && CrossedGate(highGate.GateHeightFtAal, smoothedRadioAltitude))
            {
                _highGateEvaluated = true;
                events.AddRange(EvaluateGate(snapshot, smoothedRadioAltitude, highGate));
            }

            if (lowGate is not null && !_lowGateEvaluated && CrossedGate(lowGate.GateHeightFtAal, smoothedRadioAltitude))
            {
                _lowGateEvaluated = true;
                events.AddRange(EvaluateGate(snapshot, smoothedRadioAltitude, lowGate));
            }

            _lastSmoothedRadioAltitudeFeet = smoothedRadioAltitude;
            return ValueTask.FromResult<IReadOnlyList<FlightEvent>>(events);
        }
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
        if (double.IsNaN(_lastSmoothedRadioAltitudeFeet))
        {
            return smoothedRadioAltitudeFeet <= gateFeet;
        }

        return _lastSmoothedRadioAltitudeFeet > gateFeet && smoothedRadioAltitudeFeet <= gateFeet;
    }

    private static IEnumerable<FlightEvent> EvaluateGate(
        FlightSnapshot snapshot,
        double smoothedRadioAltitudeFeet,
        SopApproachGate gate)
    {
        var events = new List<FlightEvent>();
        var requirements = gate.Requirements ?? SopConfiguration.Default.StabilizedApproachGates?.Imc1000FtAal?.Requirements ?? new SopGateRequirements
        {
            SpeedToleranceKt = new[] { -5.0, 10.0 },
            VsLimitFpm = -1000.0,
            PitchMinDeg = -2.5,
            PitchMaxDeg = 10.0,
            BankMaxDeg = 7.0
        };

        var speedTolerance = requirements.SpeedToleranceKt ?? new[] { -5.0, 10.0 };
        var minimumSpeed = snapshot.VappKnots + (speedTolerance.Length > 0 ? speedTolerance[0] : -5.0);
        var maximumSpeed = snapshot.VappKnots + (speedTolerance.Length > 1 ? speedTolerance[1] : 10.0);
        var gateLabel = $"{gate.GateHeightFtAal:F0}ft";

        if (snapshot.IndicatedAirspeedKnots < minimumSpeed)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                $"StabilizedApproach.SpeedLow.{gateLabel}",
                $"Speed below stabilized gate at {gate.GateHeightFtAal:F0} ft AAL. Smoothed RA {smoothedRadioAltitudeFeet:F1} ft.",
                EventSeverity.Warning,
                snapshot.IndicatedAirspeedKnots,
                minimumSpeed));
        }
        else if (snapshot.IndicatedAirspeedKnots > maximumSpeed)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                $"StabilizedApproach.SpeedHigh.{gateLabel}",
                $"Speed above stabilized gate at {gate.GateHeightFtAal:F0} ft AAL. Smoothed RA {smoothedRadioAltitudeFeet:F1} ft.",
                EventSeverity.Warning,
                snapshot.IndicatedAirspeedKnots,
                maximumSpeed));
        }

        if (snapshot.PitchDegrees < requirements.PitchMinDeg)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                $"StabilizedApproach.PitchLow.{gateLabel}",
                $"Pitch below stabilized threshold at {gate.GateHeightFtAal:F0} ft AAL.",
                EventSeverity.Warning,
                snapshot.PitchDegrees,
                requirements.PitchMinDeg));
        }
        else if (snapshot.PitchDegrees > requirements.PitchMaxDeg)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                $"StabilizedApproach.PitchHigh.{gateLabel}",
                $"Pitch above stabilized threshold at {gate.GateHeightFtAal:F0} ft AAL.",
                EventSeverity.Warning,
                snapshot.PitchDegrees,
                requirements.PitchMaxDeg));
        }

        if (Math.Abs(snapshot.BankDegrees) > requirements.BankMaxDeg)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                $"StabilizedApproach.BankHigh.{gateLabel}",
                $"Bank angle exceeded stabilized threshold at {gate.GateHeightFtAal:F0} ft AAL.",
                EventSeverity.Warning,
                Math.Abs(snapshot.BankDegrees),
                requirements.BankMaxDeg));
        }

        if (snapshot.VerticalSpeedFpm < requirements.VsLimitFpm)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                $"StabilizedApproach.VerticalSpeedLow.{gateLabel}",
                $"Vertical speed exceeded descent-rate threshold at {gate.GateHeightFtAal:F0} ft AAL.",
                EventSeverity.Warning,
                snapshot.VerticalSpeedFpm,
                requirements.VsLimitFpm));
        }

        return events;
    }

    private static SopConfiguration ToSopConfiguration(StabilizedApproachCriteria criteria)
    {
        var speedLower = criteria.PMExceedanceCallouts.SpeedKt.LowerLimit ?? -5.0;
        var speedUpper = criteria.PMExceedanceCallouts.SpeedKt.UpperLimit ?? 10.0;
        var pitchLower = criteria.PMExceedanceCallouts.PitchDeg.LowerLimit ?? -2.5;
        var pitchUpper = criteria.PMExceedanceCallouts.PitchDeg.UpperLimit ?? 10.0;
        var bankMax = criteria.PMExceedanceCallouts.BankDeg.Limit ?? 7.0;
        var sinkRateLimit = -Math.Abs(criteria.PMExceedanceCallouts.SinkRateFtMin.Limit ?? 1000.0);

        SopApproachGate BuildGate(string gateType, double fallbackHeight)
        {
            var gate = criteria.Gates.FirstOrDefault(x => x.Type.Contains(gateType, StringComparison.OrdinalIgnoreCase));
            return new SopApproachGate
            {
                GateHeightFtAal = gate?.HeightAalFt ?? fallbackHeight,
                Requirements = new SopGateRequirements
                {
                    SpeedToleranceKt = new[] { speedLower, speedUpper },
                    VsLimitFpm = sinkRateLimit,
                    PitchMinDeg = pitchLower,
                    PitchMaxDeg = pitchUpper,
                    BankMaxDeg = bankMax
                }
            };
        }

        return new SopConfiguration
        {
            StabilizedApproachGates = new StabilizedApproachGateSet
            {
                Imc1000FtAal = BuildGate("IMC", 1000.0),
                Vmc500FtAal = BuildGate("VMC", 500.0)
            }
        };
    }

    private void Reset()
    {
        _highGateEvaluated = false;
        _lowGateEvaluated = false;
        _lastSmoothedRadioAltitudeFeet = double.NaN;
        _radioAltitudeWindow.Clear();
    }
}

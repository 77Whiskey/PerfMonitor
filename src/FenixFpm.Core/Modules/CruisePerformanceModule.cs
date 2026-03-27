using FenixFpm.Core.Abstractions;
using FenixFpm.Core.Models;

namespace FenixFpm.Core.Modules;

public sealed class CruisePerformanceModule : IAirbusModule
{
    private const double OptimalAltitudeMinFeet = 28000.0;
    private const double OptimalAltitudeMaxFeet = 39000.0;
    private const double OptimalSpeedKnots = 280.0;
    private const double SpeedDeviationLimitKnots = 15.0;
    private const double AltitudeDeviationFeet = 200.0;
    private const double MaxVerticalSpeedFpm = 200.0;
    private const double FuelFlowOptimalKgH = 2400.0;
    private const double FuelFlowMaxKgH = 2800.0;

    private readonly Queue<(DateTimeOffset Timestamp, double Altitude)> _altitudeWindow = new();
    private readonly Queue<(DateTimeOffset Timestamp, double Speed)> _speedWindow = new();
    private const int WindowSize = 30;
    private double _peakFuelFlow;
    private double _minFuelFlow = double.MaxValue;
    private double _totalFuelFlow;
    private int _fuelFlowSamples;

    public string Name => "CruisePerformance";

    public ValueTask<IReadOnlyList<FlightEvent>> EvaluateAsync(FlightSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var events = new List<FlightEvent>();

        if (snapshot.BaroAltitudeFeet < 25000 || snapshot.BaroAltitudeFeet > 42000)
        {
            if (_altitudeWindow.Count > 0)
            {
                ResetMetrics();
            }
            return ValueTask.FromResult<IReadOnlyList<FlightEvent>>(events);
        }

        UpdateWindows(snapshot);
        events.AddRange(EvaluateAltitude(snapshot));
        events.AddRange(EvaluateSpeed(snapshot));
        events.AddRange(EvaluateFuelFlow(snapshot));
        events.AddRange(EvaluateStability(snapshot));

        return ValueTask.FromResult<IReadOnlyList<FlightEvent>>(events);
    }

    private void UpdateWindows(FlightSnapshot snapshot)
    {
        _altitudeWindow.Enqueue((snapshot.TimestampUtc, snapshot.BaroAltitudeFeet));
        while (_altitudeWindow.Count > WindowSize)
        {
            _altitudeWindow.Dequeue();
        }

        _speedWindow.Enqueue((snapshot.TimestampUtc, snapshot.IndicatedAirspeedKnots));
        while (_speedWindow.Count > WindowSize)
        {
            _speedWindow.Dequeue();
        }

        var totalFuelFlow = snapshot.Engines.TotalFuelFlowKgPerHour;
        if (totalFuelFlow > 0)
        {
            if (totalFuelFlow > _peakFuelFlow)
            {
                _peakFuelFlow = totalFuelFlow;
            }
            if (totalFuelFlow < _minFuelFlow)
            {
                _minFuelFlow = totalFuelFlow;
            }
            _totalFuelFlow += totalFuelFlow;
            _fuelFlowSamples++;
        }
    }

    private IEnumerable<FlightEvent> EvaluateAltitude(FlightSnapshot snapshot)
    {
        var events = new List<FlightEvent>();

        if (snapshot.BaroAltitudeFeet < OptimalAltitudeMinFeet - 1000)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                "Cruise.AltitudeLow",
                $"Cruise altitude {snapshot.BaroAltitudeFeet:F0} ft below optimal band",
                EventSeverity.Advisory,
                snapshot.BaroAltitudeFeet,
                OptimalAltitudeMinFeet - 1000));
        }
        else if (snapshot.BaroAltitudeFeet > OptimalAltitudeMaxFeet + 1000)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                "Cruise.AltitudeHigh",
                $"Cruise altitude {snapshot.BaroAltitudeFeet:F0} ft above optimal band",
                EventSeverity.Advisory,
                snapshot.BaroAltitudeFeet,
                OptimalAltitudeMaxFeet + 1000));
        }

        return events;
    }

    private IEnumerable<FlightEvent> EvaluateSpeed(FlightSnapshot snapshot)
    {
        var events = new List<FlightEvent>();

        var speedDeviation = Math.Abs(snapshot.IndicatedAirspeedKnots - OptimalSpeedKnots);
        if (speedDeviation > SpeedDeviationLimitKnots * 2)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                "Cruise.SpeedDeviation",
                $"Speed deviation {speedDeviation:F1} kt from optimal",
                EventSeverity.Advisory,
                snapshot.IndicatedAirspeedKnots,
                OptimalSpeedKnots));
        }
        else if (speedDeviation <= SpeedDeviationLimitKnots)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                "Cruise.SpeedOptimal",
                $"Speed {snapshot.IndicatedAirspeedKnots:F1} kt within optimal range",
                EventSeverity.Information,
                snapshot.IndicatedAirspeedKnots,
                OptimalSpeedKnots)
            {
                IsPositive = true
            });
        }

        return events;
    }

    private IEnumerable<FlightEvent> EvaluateFuelFlow(FlightSnapshot snapshot)
    {
        var events = new List<FlightEvent>();
        var totalFuelFlow = snapshot.Engines.TotalFuelFlowKgPerHour;

        if (totalFuelFlow > FuelFlowMaxKgH)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                "Cruise.FuelFlowHigh",
                $"Fuel flow {totalFuelFlow:F0} kg/h exceeds maximum",
                EventSeverity.Warning,
                totalFuelFlow,
                FuelFlowMaxKgH));
        }
        else if (totalFuelFlow > FuelFlowOptimalKgH && totalFuelFlow <= FuelFlowMaxKgH)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                "Cruise.FuelFlowElevated",
                $"Fuel flow {totalFuelFlow:F0} kg/h above optimal",
                EventSeverity.Advisory,
                totalFuelFlow,
                FuelFlowOptimalKgH));
        }

        return events;
    }

    private IEnumerable<FlightEvent> EvaluateStability(FlightSnapshot snapshot)
    {
        var events = new List<FlightEvent>();

        if (_altitudeWindow.Count >= 10)
        {
            var altitudes = _altitudeWindow.Select(x => x.Altitude).ToList();
            var avgAltitude = altitudes.Average();
            var maxDeviation = altitudes.Max(x => Math.Abs(x - avgAltitude));

            if (maxDeviation > AltitudeDeviationFeet)
            {
                events.Add(new FlightEvent(
                    snapshot.TimestampUtc,
                    "Cruise.AltitudeUnstable",
                    $"Altitude variance {maxDeviation:F0} ft indicates instability",
                    EventSeverity.Advisory,
                    maxDeviation,
                    AltitudeDeviationFeet));
            }
        }

        if (Math.Abs(snapshot.VerticalSpeedFpm) > MaxVerticalSpeedFpm * 2)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                "Cruise.VerticalSpeedUnstable",
                $"Vertical speed {snapshot.VerticalSpeedFpm:F0} fpm indicates turbulence or instability",
                EventSeverity.Advisory,
                Math.Abs(snapshot.VerticalSpeedFpm),
                MaxVerticalSpeedFpm * 2));
        }

        return events;
    }

    private void ResetMetrics()
    {
        _altitudeWindow.Clear();
        _speedWindow.Clear();
        _peakFuelFlow = 0;
        _minFuelFlow = double.MaxValue;
        _totalFuelFlow = 0;
        _fuelFlowSamples = 0;
    }
}
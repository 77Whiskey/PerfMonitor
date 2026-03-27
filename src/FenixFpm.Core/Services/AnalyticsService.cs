using FenixFpm.Core.Abstractions;
using FenixFpm.Core.Models;

namespace FenixFpm.Core.Services;

public interface IAnalyticsService
{
    Task<PerformanceMetrics> CalculateMetricsAsync(
        IEnumerable<FlightSnapshot> snapshots,
        IEnumerable<FlightEvent> events,
        CancellationToken cancellationToken = default);

    Task<Scorecard> GenerateScorecardAsync(
        Guid sessionId,
        IEnumerable<FlightSnapshot> snapshots,
        IEnumerable<FlightEvent> events,
        CancellationToken cancellationToken = default);

    Task<PerformanceTrend> CalculateTrendAsync(
        IEnumerable<FlightSession> sessions,
        string metricName,
        CancellationToken cancellationToken = default);

    Task<AnomalyReport> DetectAnomaliesAsync(
        IEnumerable<FlightSnapshot> snapshots,
        IEnumerable<FlightEvent> events,
        CancellationToken cancellationToken = default);
}

public sealed class AnalyticsService : IAnalyticsService
{
    private const double MaxAirspeedDeviationKnots = 20.0;
    private const double MaxAltitudeDeviationFeet = 300.0;
    private const double MaxVerticalSpeedDeviationFpm = 500.0;
    private const double WarningWeight = 2.0;
    private const double AdvisoryWeight = 1.0;
    private const double InformationWeight = 0.25;

    public Task<PerformanceMetrics> CalculateMetricsAsync(
        IEnumerable<FlightSnapshot> snapshots,
        IEnumerable<FlightEvent> events,
        CancellationToken cancellationToken = default)
    {
        var snapshotList = snapshots.ToList();
        var eventList = events.ToList();

        var airspeedStability = CalculateAirspeedStability(snapshotList);
        var altitudeKeeping = CalculateAltitudeKeeping(snapshotList);
        var verticalSpeedScore = CalculateVerticalSpeedScore(snapshotList);
        var configCompliance = CalculateConfigurationCompliance(eventList);
        var fuelEfficiency = CalculateFuelEfficiency(snapshotList);
        var overallScore = CalculateOverallScore(airspeedStability, altitudeKeeping, verticalSpeedScore, configCompliance, fuelEfficiency);

        return Task.FromResult(new PerformanceMetrics(
            Guid.NewGuid(),
            airspeedStability,
            altitudeKeeping,
            verticalSpeedScore,
            configCompliance,
            fuelEfficiency,
            overallScore));
    }

    public Task<Scorecard> GenerateScorecardAsync(
        Guid sessionId,
        IEnumerable<FlightSnapshot> snapshots,
        IEnumerable<FlightEvent> events,
        CancellationToken cancellationToken = default)
    {
        var snapshotList = snapshots.ToList();
        var eventList = events.ToList();
        var metrics = CalculateMetricsAsync(snapshotList, eventList, cancellationToken).Result;

        var notableEvents = eventList
            .Where(e => e.Severity != EventSeverity.Information)
            .OrderByDescending(e => e.Timestamp)
            .Take(10)
            .ToList();

        var strengths = new List<string>();
        var areasForImprovement = new List<string>();

        if (metrics.AirspeedStabilityScore >= 85)
        {
            strengths.Add("Excellent airspeed control throughout the flight");
        }
        else if (metrics.AirspeedStabilityScore < 60)
        {
            areasForImprovement.Add("Work on maintaining consistent airspeed, particularly in climb and descent");
        }

        if (metrics.AltitudeKeepingScore >= 85)
        {
            strengths.Add("Outstanding altitude control");
        }
        else if (metrics.AltitudeKeepingScore < 60)
        {
            areasForImprovement.Add("Improve altitude awareness and tracking");
        }

        if (metrics.ConfigurationComplianceScore >= 85)
        {
            strengths.Add("Proper configuration management");
        }
        else if (metrics.ConfigurationComplianceScore < 60)
        {
            areasForImprovement.Add("Review configuration changes and timing");
        }

        if (metrics.FuelEfficiencyScore >= 80)
        {
            strengths.Add("Good fuel burn management");
        }
        else if (metrics.FuelEfficiencyScore < 50)
        {
            areasForImprovement.Add("Review speed optimization and vertical profile");
        }

        var warningCount = eventList.Count(e => e.Severity == EventSeverity.Warning);
        var advisoryCount = eventList.Count(e => e.Severity == EventSeverity.Advisory);

        if (warningCount > 5)
        {
            areasForImprovement.Add($"Reduce warning events ({warningCount} warnings recorded)");
        }

        var scoreLevel = metrics.OverallScore switch
        {
            >= 90 => PerformanceScoreLevel.Excellent,
            >= 75 => PerformanceScoreLevel.AboveAverage,
            >= 60 => PerformanceScoreLevel.Average,
            >= 40 => PerformanceScoreLevel.BelowAverage,
            _ => PerformanceScoreLevel.Unsatisfactory
        };

        var dimensionScores = new Dictionary<string, double>
        {
            ["Airspeed Control"] = metrics.AirspeedStabilityScore,
            ["Altitude Management"] = metrics.AltitudeKeepingScore,
            ["Vertical Speed"] = metrics.VerticalSpeedScore,
            ["Configuration"] = metrics.ConfigurationComplianceScore,
            ["Fuel Efficiency"] = metrics.FuelEfficiencyScore
        };

        return Task.FromResult(new Scorecard(
            sessionId,
            snapshotList.FirstOrDefault()?.TimestampUtc ?? DateTimeOffset.UtcNow,
            "Fenix A320",
            metrics.OverallScore,
            scoreLevel,
            dimensionScores,
            notableEvents,
            strengths,
            areasForImprovement,
            null));
    }

    public Task<PerformanceTrend> CalculateTrendAsync(
        IEnumerable<FlightSession> sessions,
        string metricName,
        CancellationToken cancellationToken = default)
    {
        var sessionList = sessions.ToList();
        var dataPoints = sessionList
            .Where(s => s.OverallScore.HasValue)
            .OrderBy(s => s.StartedAtUtc)
            .Select(s => new TrendDataPoint(s.StartedAtUtc, s.OverallScore!.Value, FlightPhase.Unknown))
            .ToList();

        if (dataPoints.Count == 0)
        {
            return Task.FromResult(new PerformanceTrend(metricName, Array.Empty<TrendDataPoint>(), 0, 0, TrendDirection.Stable));
        }

        var average = dataPoints.Average(p => p.Value);
        var stdDev = dataPoints.Count > 1
            ? Math.Sqrt(dataPoints.Average(p => Math.Pow(p.Value - average, 2)))
            : 0;

        var direction = DetermineTrendDirection(dataPoints);

        return Task.FromResult(new PerformanceTrend(metricName, dataPoints, average, stdDev, direction));
    }

    public Task<AnomalyReport> DetectAnomaliesAsync(
        IEnumerable<FlightSnapshot> snapshots,
        IEnumerable<FlightEvent> events,
        CancellationToken cancellationToken = default)
    {
        var snapshotList = snapshots.ToList();
        var eventList = events.ToList();
        var anomalies = new List<Anomaly>();

        var warningEvents = eventList.Where(e => e.Severity == EventSeverity.Warning).ToList();
        if (warningEvents.Count > 5)
        {
            anomalies.Add(new Anomaly(
                AnomalyType.ExcessiveWarnings,
                $"Flight had {warningEvents.Count} warning events (threshold: 5)",
                warningEvents.Count,
                5,
                SeverityLevel.High));
        }

        if (snapshotList.Count > 0)
        {
            var speeds = snapshotList.Select(s => s.IndicatedAirspeedKnots).ToList();
            var avgSpeed = speeds.Average();
            var maxDeviation = speeds.Max(s => Math.Abs(s - avgSpeed));
            if (maxDeviation > 50)
            {
                anomalies.Add(new Anomaly(
                    AnomalyType.HighSpeedVariance,
                    $"High airspeed variance: {maxDeviation:F1} kt max deviation from average",
                    maxDeviation,
                    50,
                    SeverityLevel.Medium));
            }

            var maxAltitude = snapshotList.Max(s => s.BaroAltitudeFeet);
            if (maxAltitude > 41000)
            {
                anomalies.Add(new Anomaly(
                    AnomalyType.AltitudeAnomaly,
                    $"Unusual altitude: {maxAltitude:F0} ft (above typical A320 ceiling)",
                    maxAltitude,
                    41000,
                    SeverityLevel.Low));
            }
        }

        var advisoryCount = eventList.Count(e => e.Severity == EventSeverity.Advisory);
        if (advisoryCount > 20)
        {
            anomalies.Add(new Anomaly(
                AnomalyType.ExcessiveAdvisories,
                $"Flight had {advisoryCount} advisory events (threshold: 20)",
                advisoryCount,
                20,
                SeverityLevel.Medium));
        }

        return Task.FromResult(new AnomalyReport(Guid.NewGuid(), anomalies, anomalies.Any() ? "Anomalies detected" : "No anomalies found"));
    }

    private static double CalculateAirspeedStability(List<FlightSnapshot> snapshots)
    {
        if (snapshots.Count == 0) return 0;

        var cruiseSnapshots = snapshots.Where(s => s.BaroAltitudeFeet > 25000).ToList();
        if (cruiseSnapshots.Count < 10)
        {
            cruiseSnapshots = snapshots.Where(s => !s.OnGround).ToList();
        }

        if (cruiseSnapshots.Count < 5) return 50;

        var speeds = cruiseSnapshots.Select(s => s.IndicatedAirspeedKnots).ToList();
        var avgSpeed = speeds.Average();
        var maxDeviation = speeds.Max(s => Math.Abs(s - avgSpeed));

        return maxDeviation switch
        {
            <= 5 => 100,
            <= 10 => 90,
            <= 15 => 75,
            <= MaxAirspeedDeviationKnots => 60,
            _ => Math.Max(0, 60 - (maxDeviation - MaxAirspeedDeviationKnots) * 2)
        };
    }

    private static double CalculateAltitudeKeeping(List<FlightSnapshot> snapshots)
    {
        if (snapshots.Count == 0) return 0;

        var cruiseSnapshots = snapshots.Where(s => s.BaroAltitudeFeet > 20000).ToList();
        if (cruiseSnapshots.Count < 5)
        {
            cruiseSnapshots = snapshots.Where(s => !s.OnGround).ToList();
        }

        if (cruiseSnapshots.Count < 5) return 50;

        var altitudes = cruiseSnapshots.Select(s => s.BaroAltitudeFeet).ToList();
        var avgAltitude = altitudes.Average();
        var maxDeviation = altitudes.Max(a => Math.Abs(a - avgAltitude));

        return maxDeviation switch
        {
            <= 50 => 100,
            <= 100 => 90,
            <= 200 => 75,
            <= MaxAltitudeDeviationFeet => 60,
            _ => Math.Max(0, 60 - (maxDeviation - MaxAltitudeDeviationFeet) * 0.2)
        };
    }

    private static double CalculateVerticalSpeedScore(List<FlightSnapshot> snapshots)
    {
        if (snapshots.Count == 0) return 0;

        var approachSnapshots = snapshots
            .Where(s => s.RadioAltitudeFeet > 50 && s.RadioAltitudeFeet < 1500)
            .ToList();

        if (approachSnapshots.Count < 3) return 75;

        var sinkRates = approachSnapshots.Select(s => s.VerticalSpeedFpm).ToList();
        var violations = sinkRates.Count(s => s < -1200 || s > 200);
        var violationRate = (double)violations / sinkRates.Count;

        return violationRate switch
        {
            0 => 100,
            <= 0.05 => 90,
            <= 0.1 => 75,
            <= 0.2 => 60,
            _ => Math.Max(0, 60 - (violationRate - 0.2) * 100)
        };
    }

    private static double CalculateConfigurationCompliance(List<FlightEvent> events)
    {
        var configEvents = events.Where(e => e.EventType.Contains("Configuration")).ToList();
        var warnings = configEvents.Count(e => e.Severity == EventSeverity.Warning);
        var advisories = configEvents.Count(e => e.Severity == EventSeverity.Advisory);

        var penalty = warnings * WarningWeight + advisories * AdvisoryWeight;

        return Math.Max(0, 100 - penalty * 10);
    }

    private static double CalculateFuelEfficiency(List<FlightSnapshot> snapshots)
    {
        if (snapshots.Count == 0) return 0;

        var cruiseSnapshots = snapshots.Where(s => s.BaroAltitudeFeet > 28000).ToList();
        if (cruiseSnapshots.Count < 3)
        {
            cruiseSnapshots = snapshots.Where(s => !s.OnGround && s.BaroAltitudeFeet > 10000).ToList();
        }

        if (cruiseSnapshots.Count < 3) return 70;

        var avgFuelFlow = cruiseSnapshots.Average(s => s.Engines.TotalFuelFlowKgPerHour);

        return avgFuelFlow switch
        {
            <= 2200 => 100,
            <= 2400 => 90,
            <= 2600 => 75,
            <= 2800 => 60,
            _ => Math.Max(0, 60 - (avgFuelFlow - 2800) * 0.05)
        };
    }

    private static double CalculateOverallScore(
        double airspeedStability,
        double altitudeKeeping,
        double verticalSpeedScore,
        double configCompliance,
        double fuelEfficiency)
    {
        return (airspeedStability * 0.25 +
                altitudeKeeping * 0.25 +
                verticalSpeedScore * 0.2 +
                configCompliance * 0.15 +
                fuelEfficiency * 0.15);
    }

    private static TrendDirection DetermineTrendDirection(List<TrendDataPoint> dataPoints)
    {
        if (dataPoints.Count < 3) return TrendDirection.Stable;

        var firstHalf = dataPoints.Take(dataPoints.Count / 2).Average(p => p.Value);
        var secondHalf = dataPoints.Skip(dataPoints.Count / 2).Average(p => p.Value);
        var difference = secondHalf - firstHalf;

        return difference switch
        {
            > 5 => TrendDirection.Improving,
            < -5 => TrendDirection.Declining,
            _ => TrendDirection.Stable
        };
    }
}

public sealed record AnomalyReport(
    Guid SessionId,
    IReadOnlyList<Anomaly> Anomalies,
    string Summary);

public sealed record Anomaly(
    AnomalyType Type,
    string Description,
    double ActualValue,
    double ThresholdValue,
    SeverityLevel Severity);

public enum AnomalyType
{
    ExcessiveWarnings,
    ExcessiveAdvisories,
    HighSpeedVariance,
    AltitudeAnomaly,
    FuelAnomaly,
    ConfigurationAnomaly
}

public enum SeverityLevel
{
    Low,
    Medium,
    High,
    Critical
}
namespace FenixFpm.Core.Models;

public sealed record FlightEvent(
    DateTimeOffset Timestamp,
    string EventType,
    string Description,
    EventSeverity Severity,
    double Value,
    double Limit)
{
    public string FlightPhase { get; init; } = "Unknown";
    public string? ProcedureReference { get; init; }
    public bool IsPositive { get; init; }
}

public sealed record FlightSession(
    Guid Id,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    string AircraftType,
    int SnapshotCount)
{
    public FlightPhase CurrentPhase { get; init; } = FlightPhase.Unknown;
    public double? TotalFuelBurnedKg { get; init; }
    public double? MaxAltitudeFeet { get; init; }
    public int EventCount { get; init; }
    public int WarningCount { get; init; }
    public int AdvisoryCount { get; init; }
    public double? OverallScore { get; init; }
}

public sealed record PerformanceEngineResult(
    DateTimeOffset TimestampUtc,
    IReadOnlyList<FlightEvent> Events);

public sealed record LandingDistanceRequest(
    double WeightKg,
    double OutsideAirTemperatureC,
    double PressureAltitudeFeet,
    RunwayCondition RunwayCondition = RunwayCondition.Dry);

public sealed record InterpolationBounds(
    double WeightLowerKg,
    double WeightUpperKg,
    double OutsideAirTemperatureLowerC,
    double OutsideAirTemperatureUpperC,
    double PressureAltitudeLowerFeet,
    double PressureAltitudeUpperFeet);

public sealed record LandingDistanceResult(
    double DistanceMeters,
    InterpolationBounds Bounds,
    bool WasClamped);

public sealed record FlightPhaseTransition(
    DateTimeOffset Timestamp,
    FlightPhase FromPhase,
    FlightPhase ToPhase);

public sealed record PerformanceMetrics(
    Guid SessionId,
    double AirspeedStabilityScore,
    double AltitudeKeepingScore,
    double VerticalSpeedScore,
    double ConfigurationComplianceScore,
    double FuelEfficiencyScore,
    double OverallScore);

public sealed record ProcedureCheckResult(
    ProcedureType Procedure,
    bool Completed,
    DateTimeOffset? CompletedAt,
    bool Passed,
    string? FailureReason);

public sealed record TrendDataPoint(
    DateTimeOffset Date,
    double Value,
    FlightPhase Phase);

public sealed record PerformanceTrend(
    string MetricName,
    IReadOnlyList<TrendDataPoint> DataPoints,
    double AverageValue,
    double StandardDeviation,
    TrendDirection Direction);

public enum TrendDirection
{
    Stable = 0,
    Improving = 1,
    Declining = -1
}

public sealed record Scorecard(
    Guid SessionId,
    DateTimeOffset FlightDate,
    string AircraftType,
    double OverallScore,
    PerformanceScoreLevel ScoreLevel,
    IReadOnlyDictionary<string, double> DimensionScores,
    IReadOnlyList<FlightEvent> NotableEvents,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> AreasForImprovement,
    string? InstructorNotes);
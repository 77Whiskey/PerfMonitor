using FenixFpm.Core.Models;

namespace FenixFpm.Infrastructure.Persistence;

public sealed class FlightEventEntity
{
    public Guid Id { get; set; }
    public Guid FlightSessionId { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Severity { get; set; }
    public double Value { get; set; }
    public double Limit { get; set; }
    public string FlightPhase { get; set; } = "Unknown";
    public string? ProcedureReference { get; set; }
    public bool IsPositive { get; set; }

    public FlightEvent ToModel()
    {
        return new FlightEvent(
            TimestampUtc,
            EventType,
            Description,
            (EventSeverity)Severity,
            Value,
            Limit)
        {
            FlightPhase = FlightPhase,
            ProcedureReference = ProcedureReference,
            IsPositive = IsPositive
        };
    }

    public static FlightEventEntity FromModel(Guid sessionId, FlightEvent model)
    {
        return new FlightEventEntity
        {
            Id = Guid.NewGuid(),
            FlightSessionId = sessionId,
            TimestampUtc = model.Timestamp,
            EventType = model.EventType,
            Description = model.Description,
            Severity = (int)model.Severity,
            Value = model.Value,
            Limit = model.Limit,
            FlightPhase = model.FlightPhase,
            ProcedureReference = model.ProcedureReference,
            IsPositive = model.IsPositive
        };
    }
}

public sealed class PerformanceMetricsEntity
{
    public Guid Id { get; set; }
    public Guid FlightSessionId { get; set; }
    public double AirspeedStabilityScore { get; set; }
    public double AltitudeKeepingScore { get; set; }
    public double VerticalSpeedScore { get; set; }
    public double ConfigurationComplianceScore { get; set; }
    public double FuelEfficiencyScore { get; set; }
    public double OverallScore { get; set; }
    public DateTimeOffset CalculatedAtUtc { get; set; }

    public PerformanceMetrics ToModel()
    {
        return new PerformanceMetrics(
            FlightSessionId,
            AirspeedStabilityScore,
            AltitudeKeepingScore,
            VerticalSpeedScore,
            ConfigurationComplianceScore,
            FuelEfficiencyScore,
            OverallScore);
    }

    public static PerformanceMetricsEntity FromModel(Guid sessionId, PerformanceMetrics metrics)
    {
        return new PerformanceMetricsEntity
        {
            Id = metrics.SessionId == Guid.Empty ? Guid.NewGuid() : metrics.SessionId,
            FlightSessionId = sessionId,
            AirspeedStabilityScore = metrics.AirspeedStabilityScore,
            AltitudeKeepingScore = metrics.AltitudeKeepingScore,
            VerticalSpeedScore = metrics.VerticalSpeedScore,
            ConfigurationComplianceScore = metrics.ConfigurationComplianceScore,
            FuelEfficiencyScore = metrics.FuelEfficiencyScore,
            OverallScore = metrics.OverallScore,
            CalculatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}

public sealed class FlightPhaseEntity
{
    public Guid Id { get; set; }
    public Guid FlightSessionId { get; set; }
    public int Phase { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? EndedAtUtc { get; set; }
    public double? MaxAltitudeFeet { get; set; }
    public double? MaxSpeedKnots { get; set; }

    public FlightPhase ToModel()
    {
        return (FlightPhase)Phase;
    }

    public static FlightPhaseEntity Create(Guid sessionId, FlightPhase phase, DateTimeOffset startTime)
    {
        return new FlightPhaseEntity
        {
            Id = Guid.NewGuid(),
            FlightSessionId = sessionId,
            Phase = (int)phase,
            StartedAtUtc = startTime
        };
    }
}
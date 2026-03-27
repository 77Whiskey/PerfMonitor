using FenixFpm.Core.Models;

namespace FenixFpm.Infrastructure.Persistence;

public enum FlightSessionStatus
{
    Active = 0,
    Completed = 1,
    Archived = 2
}

public sealed class FlightSessionEntity
{
    public Guid Id { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? EndedAtUtc { get; set; }

    public string AircraftType { get; set; } = "Fenix A320";

    public string? TailNumber { get; set; }

    public string? FlightNumber { get; set; }

    public string? PilotId { get; set; }

    public FlightSessionStatus Status { get; set; } = FlightSessionStatus.Active;

    public TimeSpan TotalFlightTime { get; set; }

    public double? FuelBurnedKg { get; set; }

    public double? OverallScore { get; set; }

    public int SnapshotCount { get; set; }

    public int WarningCount { get; set; }

    public int AdvisoryCount { get; set; }

    public int EventCount { get; set; }

    public double? AirspeedControlScore { get; set; }

    public double? AltitudeKeepingScore { get; set; }

    public double? HeadingControlScore { get; set; }

    public double? VerticalSpeedScore { get; set; }

    public double? ConfigurationComplianceScore { get; set; }

    public double? FuelEfficiencyScore { get; set; }

    public double? ApproachStabilityScore { get; set; }

    public double? LandingTouchdownScore { get; set; }

    public double? MaxAltitudeFeet { get; set; }

    public double? MaxSpeedKnots { get; set; }

    public ICollection<TelemetrySnapshotEntity> Snapshots { get; set; } = new List<TelemetrySnapshotEntity>();

    public ICollection<FlightEventEntity> Events { get; set; } = new List<FlightEventEntity>();

    public ICollection<FlightPhaseEntity> Phases { get; set; } = new List<FlightPhaseEntity>();

    public FlightSession ToModel()
    {
        return new FlightSession(Id, StartedAtUtc, EndedAtUtc, AircraftType, SnapshotCount)
        {
            TotalFuelBurnedKg = FuelBurnedKg,
            MaxAltitudeFeet = MaxAltitudeFeet,
            EventCount = EventCount,
            WarningCount = WarningCount,
            AdvisoryCount = AdvisoryCount,
            OverallScore = OverallScore
        };
    }

    public static FlightSessionEntity FromModel(FlightSession model)
    {
        return new FlightSessionEntity
        {
            Id = model.Id,
            StartedAtUtc = model.StartedAtUtc,
            EndedAtUtc = model.EndedAtUtc,
            AircraftType = model.AircraftType,
            SnapshotCount = model.SnapshotCount,
            TotalFlightTime = model.EndedAtUtc.HasValue 
                ? model.EndedAtUtc.Value - model.StartedAtUtc 
                : TimeSpan.Zero,
            FuelBurnedKg = model.TotalFuelBurnedKg,
            MaxAltitudeFeet = model.MaxAltitudeFeet,
            EventCount = model.EventCount,
            WarningCount = model.WarningCount,
            AdvisoryCount = model.AdvisoryCount,
            OverallScore = model.OverallScore,
            Status = FlightSessionStatus.Completed
        };
    }
}
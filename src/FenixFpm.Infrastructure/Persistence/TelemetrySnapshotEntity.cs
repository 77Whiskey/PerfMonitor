using FenixFpm.Core.Models;

namespace FenixFpm.Infrastructure.Persistence;

public sealed class TelemetrySnapshotEntity
{
    public long Id { get; set; }

    public Guid FlightSessionId { get; set; }

    public FlightSessionEntity FlightSession { get; set; } = null!;

    public DateTimeOffset TimestampUtc { get; set; }
    public double IndicatedAirspeedKnots { get; set; }
    public double TrueAirspeedKnots { get; set; }
    public double GroundSpeedKnots { get; set; }
    public double BaroAltitudeFeet { get; set; }
    public double RadioAltitudeFeet { get; set; }
    public double VerticalSpeedFpm { get; set; }
    public double VappKnots { get; set; }
    public double GrossWeightKg { get; set; }
    public double OutsideAirTemperatureC { get; set; }
    public double PressureAltitudeFeet { get; set; }
    public double PitchDegrees { get; set; }
    public double BankDegrees { get; set; }
    public bool OnGround { get; set; }
    public LandingConfiguration LandingConfiguration { get; set; }
    public AutobrakeMode AutobrakeMode { get; set; }
    public bool GearDown { get; set; }
    public bool SpoilersArmed { get; set; }
    public FmaLateralMode LateralMode { get; set; }
    public FmaVerticalMode VerticalMode { get; set; }
    public ApproachCapability ApproachCapability { get; set; }
    public bool LandModeArmed { get; set; }
    public bool FlareModeArmed { get; set; }
    public bool AutothrustActive { get; set; }
    public bool ManagedSpeedActive { get; set; }
    public bool GlideslopeCaptured { get; set; }
    public bool LocalizerCaptured { get; set; }
    public bool ApproachModeActive { get; set; }
    public double LeftN1Percent { get; set; }
    public double RightN1Percent { get; set; }
    public double LeftEgtCelsius { get; set; }
    public double RightEgtCelsius { get; set; }
    public double LeftFuelFlowKgPerHour { get; set; }
    public double RightFuelFlowKgPerHour { get; set; }
    public double ThrustLever1Degrees { get; set; }
    public double ThrustLever2Degrees { get; set; }
    public double SideStickPitch { get; set; }
    public double SideStickRoll { get; set; }
    public double RudderPercent { get; set; }
    public double ElevatorPercent { get; set; }
    public double AileronPercent { get; set; }
    public double SpoilersPercent { get; set; }
    public double FlapsHandleIndex { get; set; }
    public double SpeedBrakeHandlePercent { get; set; }

    public static TelemetrySnapshotEntity FromDomain(Guid flightSessionId, FlightSnapshot snapshot)
    {
        return new TelemetrySnapshotEntity
        {
            FlightSessionId = flightSessionId,
            TimestampUtc = snapshot.TimestampUtc,
            IndicatedAirspeedKnots = snapshot.IndicatedAirspeedKnots,
            TrueAirspeedKnots = snapshot.TrueAirspeedKnots,
            GroundSpeedKnots = snapshot.GroundSpeedKnots,
            BaroAltitudeFeet = snapshot.BaroAltitudeFeet,
            RadioAltitudeFeet = snapshot.RadioAltitudeFeet,
            VerticalSpeedFpm = snapshot.VerticalSpeedFpm,
            VappKnots = snapshot.VappKnots,
            GrossWeightKg = snapshot.GrossWeightKg,
            OutsideAirTemperatureC = snapshot.OutsideAirTemperatureC,
            PressureAltitudeFeet = snapshot.PressureAltitudeFeet,
            PitchDegrees = snapshot.PitchDegrees,
            BankDegrees = snapshot.BankDegrees,
            OnGround = snapshot.OnGround,
            LandingConfiguration = snapshot.LandingConfiguration.Configuration,
            AutobrakeMode = snapshot.LandingConfiguration.AutobrakeMode,
            GearDown = snapshot.LandingConfiguration.GearDown,
            SpoilersArmed = snapshot.LandingConfiguration.SpoilersArmed,
            LateralMode = snapshot.Autopilot.LateralMode,
            VerticalMode = snapshot.Autopilot.VerticalMode,
            ApproachCapability = snapshot.Autopilot.ApproachCapability,
            LandModeArmed = snapshot.Autopilot.LandModeArmed,
            FlareModeArmed = snapshot.Autopilot.FlareModeArmed,
            AutothrustActive = snapshot.Autopilot.AutothrustActive,
            ManagedSpeedActive = snapshot.Autopilot.ManagedSpeedActive,
            GlideslopeCaptured = snapshot.Autopilot.GlideslopeCaptured,
            LocalizerCaptured = snapshot.Autopilot.LocalizerCaptured,
            ApproachModeActive = snapshot.Autopilot.ApproachModeActive,
            LeftN1Percent = snapshot.Engines.LeftN1Percent,
            RightN1Percent = snapshot.Engines.RightN1Percent,
            LeftEgtCelsius = snapshot.Engines.LeftEgtCelsius,
            RightEgtCelsius = snapshot.Engines.RightEgtCelsius,
            LeftFuelFlowKgPerHour = snapshot.Engines.LeftFuelFlowKgPerHour,
            RightFuelFlowKgPerHour = snapshot.Engines.RightFuelFlowKgPerHour,
            ThrustLever1Degrees = snapshot.Engines.ThrustLever1Degrees,
            ThrustLever2Degrees = snapshot.Engines.ThrustLever2Degrees,
            SideStickPitch = snapshot.Controls.SideStickPitch,
            SideStickRoll = snapshot.Controls.SideStickRoll,
            RudderPercent = snapshot.Controls.RudderPercent,
            ElevatorPercent = snapshot.Controls.ElevatorPercent,
            AileronPercent = snapshot.Controls.AileronPercent,
            SpoilersPercent = snapshot.Controls.SpoilersPercent,
            FlapsHandleIndex = snapshot.Controls.FlapsHandleIndex,
            SpeedBrakeHandlePercent = snapshot.Controls.SpeedBrakeHandlePercent
        };
    }

    public static FlightSnapshot ToDomain(TelemetrySnapshotEntity entity)
    {
        return new FlightSnapshot(
            entity.TimestampUtc,
            entity.IndicatedAirspeedKnots,
            entity.TrueAirspeedKnots,
            entity.GroundSpeedKnots,
            entity.BaroAltitudeFeet,
            entity.RadioAltitudeFeet,
            entity.VerticalSpeedFpm,
            entity.VappKnots,
            entity.GrossWeightKg,
            entity.OutsideAirTemperatureC,
            entity.PressureAltitudeFeet,
            entity.PitchDegrees,
            entity.BankDegrees,
            entity.OnGround,
            new LandingConfigurationSnapshot(entity.LandingConfiguration, entity.AutobrakeMode, entity.GearDown, entity.SpoilersArmed),
            new AutopilotSnapshot(
                entity.LateralMode,
                entity.VerticalMode,
                entity.ApproachCapability,
                entity.LandModeArmed,
                entity.FlareModeArmed,
                entity.AutothrustActive,
                entity.ManagedSpeedActive,
                entity.GlideslopeCaptured,
                entity.LocalizerCaptured,
                entity.ApproachModeActive),
            new EngineSnapshot(
                entity.LeftN1Percent,
                entity.RightN1Percent,
                entity.LeftEgtCelsius,
                entity.RightEgtCelsius,
                entity.LeftFuelFlowKgPerHour,
                entity.RightFuelFlowKgPerHour,
                entity.ThrustLever1Degrees,
                entity.ThrustLever2Degrees),
            new ControlSurfaceSnapshot(
                entity.SideStickPitch,
                entity.SideStickRoll,
                entity.RudderPercent,
                entity.ElevatorPercent,
                entity.AileronPercent,
                entity.SpoilersPercent,
                entity.FlapsHandleIndex,
                entity.SpeedBrakeHandlePercent));
    }
}
namespace FenixFpm.Core.Models;

public sealed record FlightSnapshot(
    DateTimeOffset TimestampUtc,
    double IndicatedAirspeedKnots,
    double TrueAirspeedKnots,
    double GroundSpeedKnots,
    double BaroAltitudeFeet,
    double RadioAltitudeFeet,
    double VerticalSpeedFpm,
    double VappKnots,
    double GrossWeightKg,
    double OutsideAirTemperatureC,
    double PressureAltitudeFeet,
    double PitchDegrees,
    double BankDegrees,
    bool OnGround,
    LandingConfigurationSnapshot LandingConfiguration,
    AutopilotSnapshot Autopilot,
    EngineSnapshot Engines,
    ControlSurfaceSnapshot Controls);

public sealed record LandingConfigurationSnapshot(
    LandingConfiguration Configuration,
    AutobrakeMode AutobrakeMode,
    bool GearDown,
    bool SpoilersArmed)
{
    public bool IsLandingReady =>
        GearDown && SpoilersArmed &&
        (Configuration is LandingConfiguration.Config3 or LandingConfiguration.Full);
}

public sealed record AutopilotSnapshot(
    FmaLateralMode LateralMode,
    FmaVerticalMode VerticalMode,
    ApproachCapability ApproachCapability,
    bool LandModeArmed,
    bool FlareModeArmed,
    bool AutothrustActive,
    bool ManagedSpeedActive,
    bool GlideslopeCaptured,
    bool LocalizerCaptured,
    bool ApproachModeActive)
{
    public bool HasLandingGuidance =>
        VerticalMode is FmaVerticalMode.Land or FmaVerticalMode.Flare ||
        LandModeArmed ||
        FlareModeArmed;
}

public sealed record EngineSnapshot(
    double LeftN1Percent,
    double RightN1Percent,
    double LeftEgtCelsius,
    double RightEgtCelsius,
    double LeftFuelFlowKgPerHour,
    double RightFuelFlowKgPerHour,
    double ThrustLever1Degrees,
    double ThrustLever2Degrees)
{
    public double TotalFuelFlowKgPerHour => LeftFuelFlowKgPerHour + RightFuelFlowKgPerHour;
}

public sealed record ControlSurfaceSnapshot(
    double SideStickPitch,
    double SideStickRoll,
    double RudderPercent,
    double ElevatorPercent,
    double AileronPercent,
    double SpoilersPercent,
    double FlapsHandleIndex,
    double SpeedBrakeHandlePercent);
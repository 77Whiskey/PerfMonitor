namespace FenixFpm.Core.Models;

public enum ModuleStatus
{
    Monitoring = 0,
    Nominal = 1,
    Advisory = 2,
    Warning = 3
}

public enum EventSeverity
{
    Information = 0,
    Advisory = 1,
    Warning = 2
}

public enum RunwayCondition
{
    Dry = 0,
    Wet = 1
}

public enum FmaLateralMode
{
    Unknown = 0,
    Heading = 1,
    Track = 2,
    Nav = 3,
    Loc = 4,
    Approach = 5,
    Rollout = 6
}

public enum FmaVerticalMode
{
    Unknown = 0,
    OpenClimb = 1,
    Climb = 2,
    OpenDescent = 3,
    Descent = 4,
    Glideslope = 5,
    FinalApproach = 6,
    Land = 7,
    Flare = 8,
    Rollout = 9
}

public enum ApproachCapability
{
    None = 0,
    Cat1 = 1,
    Cat2 = 2,
    Cat3Single = 3,
    Cat3Dual = 4,
    RawData = 5
}

public enum LandingConfiguration
{
    Unknown = 0,
    NotConfigured = 1,
    Config3 = 2,
    Full = 3
}

public enum AutobrakeMode
{
    Off = 0,
    Low = 1,
    Medium = 2,
    Max = 3
}

public enum FlightPhase
{
    Unknown = 0,
    Preflight = 1,
    Parked = 2,
    Taxi = 3,
    Takeoff = 4,
    Climb = 5,
    Cruise = 6,
    Descent = 7,
    Approach = 8,
    Landing = 9,
    Rollout = 10,
    Ground = 11
}

public enum ProcedureType
{
    Unknown = 0,
    PreFlightChecklist = 1,
    BeforeTakeoffChecklist = 2,
    ApproachBriefing = 3,
    LandingChecklist = 4,
    AfterLandingChecklist = 5,
    EngineStart = 6,
    EngineShutdown = 7
}

public enum PerformanceScoreLevel
{
    Unsatisfactory = 0,
    BelowAverage = 1,
    Average = 2,
    AboveAverage = 3,
    Excellent = 4
}
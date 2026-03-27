namespace FenixFpm.Contracts.Interop;

public enum FmaLateralMode : ushort
{
    Unknown = 0,
    Heading = 1,
    Track = 2,
    Nav = 3,
    Loc = 4,
    Approach = 5,
    Rollout = 6
}

public enum FmaVerticalMode : ushort
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

public enum ApproachCapability : ushort
{
    None = 0,
    Cat1 = 1,
    Cat2 = 2,
    Cat3Single = 3,
    Cat3Dual = 4,
    RawData = 5
}

public enum LandingConfigurationCode : ushort
{
    Unknown = 0,
    NotConfigured = 1,
    Config3 = 2,
    Full = 3
}

public enum AutobrakeMode : byte
{
    Off = 0,
    Low = 1,
    Medium = 2,
    Max = 3
}
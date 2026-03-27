using System.Runtime.InteropServices;

namespace FenixFpm.Contracts.Interop;

public static class FenixSharedMemoryLayout
{
    public const string MappingName = "FENIX_FPM_DATA";
    public const uint Version = 1;
    public static readonly int BufferSize = Marshal.SizeOf<FenixFpmSharedBuffer>();
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BufferHeader
{
    public uint Version;
    public uint SizeBytes;
    public ulong Sequence;
    public long TimestampUnixMicroseconds;
    public uint SampleRateHz;
    public uint Flags;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct AircraftState
{
    public double IndicatedAirspeedKnots;
    public double TrueAirspeedKnots;
    public double GroundSpeedKnots;
    public double BaroAltitudeFeet;
    public double RadioAltitudeFeet;
    public double VerticalSpeedFpm;
    public double VappKnots;
    public double GrossWeightKg;
    public double OutsideAirTemperatureC;
    public double PressureAltitudeFeet;
    public double PitchDegrees;
    public double BankDegrees;
    public ushort LandingConfiguration;
    public byte AutobrakeMode;
    public byte OnGround;
    public byte GearDownLocked;
    public byte SpoilersArmed;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct AutopilotFma
{
    public ushort LateralMode;
    public ushort VerticalMode;
    public ushort ApproachCapability;
    public ushort Reserved0;
    public byte LandModeArmed;
    public byte FlareModeArmed;
    public byte AutothrustActive;
    public byte ManagedSpeedActive;
    public byte GlideslopeCaptured;
    public byte LocalizerCaptured;
    public byte ApproachModeActive;
    public byte Reserved1;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct EngineData
{
    public double LeftN1Percent;
    public double RightN1Percent;
    public double LeftEgtCelsius;
    public double RightEgtCelsius;
    public double LeftFuelFlowKgPerHour;
    public double RightFuelFlowKgPerHour;
    public double ThrustLever1Degrees;
    public double ThrustLever2Degrees;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ControlSurfacePositions
{
    public double SideStickPitch;
    public double SideStickRoll;
    public double RudderPercent;
    public double ElevatorPercent;
    public double AileronPercent;
    public double SpoilersPercent;
    public double FlapsHandleIndex;
    public double SpeedBrakeHandlePercent;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct FenixSystemsData
{
    public byte ApuBleedActive;
    public byte Engine1AntiIce;
    public byte Engine2AntiIce;
    public byte WingAntiIce;
    public byte FcuHeadingManaged;
    public byte FcuSpeedManaged;
    public byte FcuAltitudeManaged;
    public byte FcuVerticalManaged;
    public byte Reserved1;
    public byte Reserved2;
    public byte Reserved3;
    public byte Reserved4;
    public byte Reserved5;
    public byte Reserved6;
    public byte Reserved7;
    public byte Reserved8;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct FenixFpmSharedBuffer
{
    public BufferHeader Header;
    public AircraftState Aircraft;
    public AutopilotFma Autopilot;
    public EngineData Engines;
    public ControlSurfacePositions Controls;
    public FenixSystemsData Systems;
    public uint Checksum;
}
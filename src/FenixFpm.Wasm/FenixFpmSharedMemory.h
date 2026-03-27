#pragma once

#include <cstddef>
#include <cstdint>

namespace fenixfpm
{
namespace wasm
{
constexpr char ClientDataName[] = "FENIX_FPM_DATA";
constexpr std::uint32_t BufferVersion = 1U;
constexpr std::uint32_t ClientDataAreaId = 0x1100U;
constexpr std::uint32_t ClientDataDefinitionId = 0x1100U;
constexpr std::uint32_t FrameEventId = 0x1100U;

enum class FmaLateralMode : std::uint16_t
{
    Unknown = 0,
    Heading = 1,
    Track = 2,
    Nav = 3,
    Loc = 4,
    Approach = 5,
    Rollout = 6
};

enum class FmaVerticalMode : std::uint16_t
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
};

enum class ApproachCapability : std::uint16_t
{
    None = 0,
    Cat1 = 1,
    Cat2 = 2,
    Cat3Single = 3,
    Cat3Dual = 4,
    RawData = 5
};

enum class LandingConfigurationCode : std::uint16_t
{
    Unknown = 0,
    NotConfigured = 1,
    Config3 = 2,
    Full = 3
};

enum class AutobrakeMode : std::uint8_t
{
    Off = 0,
    Low = 1,
    Medium = 2,
    Max = 3
};

#pragma pack(push, 1)
struct BufferHeader
{
    std::uint32_t Version;
    std::uint32_t SizeBytes;
    std::uint64_t Sequence;
    std::int64_t TimestampUnixMicroseconds;
    std::uint32_t SampleRateHz;
    std::uint32_t Flags;
};

struct AircraftState
{
    double IndicatedAirspeedKnots;
    double TrueAirspeedKnots;
    double GroundSpeedKnots;
    double BaroAltitudeFeet;
    double RadioAltitudeFeet;
    double VerticalSpeedFpm;
    double VappKnots;
    double GrossWeightKg;
    double OutsideAirTemperatureC;
    double PressureAltitudeFeet;
    double PitchDegrees;
    double BankDegrees;
    std::uint16_t LandingConfiguration;
    std::uint8_t AutobrakeMode;
    std::uint8_t OnGround;
    std::uint8_t GearDownLocked;
    std::uint8_t SpoilersArmed;
};

struct AutopilotFma
{
    std::uint16_t LateralMode;
    std::uint16_t VerticalMode;
    std::uint16_t ApproachCapability;
    std::uint16_t Reserved0;
    std::uint8_t LandModeArmed;
    std::uint8_t FlareModeArmed;
    std::uint8_t AutothrustActive;
    std::uint8_t ManagedSpeedActive;
    std::uint8_t GlideslopeCaptured;
    std::uint8_t LocalizerCaptured;
    std::uint8_t ApproachModeActive;
    std::uint8_t Reserved1;
};

struct EngineData
{
    double LeftN1Percent;
    double RightN1Percent;
    double LeftEgtCelsius;
    double RightEgtCelsius;
    double LeftFuelFlowKgPerHour;
    double RightFuelFlowKgPerHour;
    double ThrustLever1Degrees;
    double ThrustLever2Degrees;
};

struct ControlSurfacePositions
{
    double SideStickPitch;
    double SideStickRoll;
    double RudderPercent;
    double ElevatorPercent;
    double AileronPercent;
    double SpoilersPercent;
    double FlapsHandleIndex;
    double SpeedBrakeHandlePercent;
};

struct FenixSystemsData
{
    std::uint8_t ApuBleedActive;
    std::uint8_t Engine1AntiIce;
    std::uint8_t Engine2AntiIce;
    std::uint8_t WingAntiIce;
    std::uint8_t FcuHeadingManaged;
    std::uint8_t FcuSpeedManaged;
    std::uint8_t FcuAltitudeManaged;
    std::uint8_t FcuVerticalManaged;
    std::uint8_t Reserved1;
    std::uint8_t Reserved2;
    std::uint8_t Reserved3;
    std::uint8_t Reserved4;
    std::uint8_t Reserved5;
    std::uint8_t Reserved6;
    std::uint8_t Reserved7;
    std::uint8_t Reserved8;
};

struct FlightSnapshot
{
    AircraftState Aircraft;
    AutopilotFma Autopilot;
    EngineData Engines;
    ControlSurfacePositions Controls;
    FenixSystemsData Systems;
};

struct FenixFpmSharedBuffer
{
    BufferHeader Header;
    FlightSnapshot Flight;
    std::uint32_t Checksum;
};
#pragma pack(pop)

static_assert(sizeof(BufferHeader) == 32U, "BufferHeader size must match the managed contract.");
static_assert(sizeof(FenixSystemsData) == 16U, "FenixSystemsData size must match the managed contract.");
static_assert(sizeof(FenixFpmSharedBuffer) == 298U, "FenixFpmSharedBuffer size must match the managed contract.");
static_assert(offsetof(FenixFpmSharedBuffer, Flight) == sizeof(BufferHeader), "Flight snapshot offset must remain stable.");
} // namespace wasm
} // namespace fenixfpm

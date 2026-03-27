#include <MSFS/MSFS_WindowsTypes.h>
#include <MSFS/Legacy/gauges.h>
#include <SimConnect.h>
#include "FenixFpmSharedMemory.h"

#include <MSFS/MSFS.h>

#include <chrono>
#include <cmath>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <string>
#include <unordered_map>

#ifdef check_named_variable
#undef check_named_variable
#endif
#ifdef register_named_variable
#undef register_named_variable
#endif
#ifdef get_named_variable_value
#undef get_named_variable_value
#endif
#ifdef execute_calculator_code
#undef execute_calculator_code
#endif
#ifdef unregister_all_named_vars
#undef unregister_all_named_vars
#endif

using namespace fenixfpm::wasm;

namespace
{
struct GaugesImportTable
{
    struct
    {
        ID fnID;
        void* fnptr;
    } PANELSentry;

    struct
    {
        ID fnID;
        void* fnptr;
    } nullentry;
};

constexpr std::uint32_t GaugeModuleId = 0x00000013U;
constexpr std::uint32_t PanelsApiImportId = 0x0000000FU;
constexpr std::chrono::seconds ReconnectDelay(5);
constexpr double PoundsToKilograms = 0.45359237;

HANDLE g_hSimConnect = 0;
bool g_clientDataReady = false;
std::uint64_t g_sequence = 0;
std::chrono::steady_clock::time_point g_lastConnectAttempt = std::chrono::steady_clock::time_point::min();
std::unordered_map<std::string, ID> g_namedVariableCache;

char kGaugeName[] = "FenixFpmBridge";
char kGaugeUsage[] = "Fenix FPM telemetry bridge";

std::uint8_t ToByte(bool value)
{
    return value ? 1U : 0U;
}

double ClampFinite(double value, double fallback)
{
    return std::isfinite(value) ? value : fallback;
}

std::uint32_t NormalizeSampleRate(float frameRate)
{
    long roundedRate = 60;

    if (frameRate > 0.0F)
    {
        roundedRate = static_cast<long>(std::lround(frameRate));
    }

    if (roundedRate < 1)
    {
        roundedRate = 1;
    }
    else if (roundedRate > 240)
    {
        roundedRate = 240;
    }

    return static_cast<std::uint32_t>(roundedRate);
}

std::int64_t GetUnixMicroseconds()
{
    const std::chrono::time_point<std::chrono::system_clock, std::chrono::microseconds> now =
        std::chrono::time_point_cast<std::chrono::microseconds>(std::chrono::system_clock::now());
    return static_cast<std::int64_t>(now.time_since_epoch().count());
}

std::uint32_t ComputeChecksum(const FenixFpmSharedBuffer& buffer)
{
    std::uint32_t checksum = 2166136261U;
    const std::uint8_t* bytes = reinterpret_cast<const std::uint8_t*>(&buffer);
    const std::size_t length = sizeof(FenixFpmSharedBuffer) - sizeof(buffer.Checksum);

    for (std::size_t index = 0; index < length; ++index)
    {
        checksum ^= bytes[index];
        checksum *= 16777619U;
    }

    return checksum;
}

double ReadLVar(const char* variableName, double fallback)
{
    ID& variableId = g_namedVariableCache[variableName];

    if (variableId == 0)
    {
        variableId = check_named_variable(variableName);
        if (variableId == 0)
        {
            variableId = register_named_variable(variableName);
        }
    }

    if (variableId == 0)
    {
        return fallback;
    }

    return ClampFinite(get_named_variable_value(variableId), fallback);
}

bool ReadLVarBool(const char* variableName)
{
    return std::fabs(ReadLVar(variableName, 0.0)) >= 0.5;
}

double ReadSimVar(const char* simVarName, const char* units, int index, double fallback)
{
    char expression[256] = {};
    if (index > 0)
    {
        std::snprintf(expression, sizeof(expression), "(A:%s:%d, %s)", simVarName, index, units);
    }
    else
    {
        std::snprintf(expression, sizeof(expression), "(A:%s, %s)", simVarName, units);
    }

    FLOAT64 floatingValue = fallback;
    SINT32 integerValue = 0;
    PCSTRINGZ stringValue = 0;

    if (!execute_calculator_code(expression, &floatingValue, &integerValue, &stringValue))
    {
        return fallback;
    }

    return ClampFinite(static_cast<double>(floatingValue), fallback);
}

bool ReadSimBool(const char* simVarName, const char* units, int index)
{
    return std::fabs(ReadSimVar(simVarName, units, index, 0.0)) >= 0.5;
}

AutobrakeMode DetermineAutobrakeMode()
{
    if (ReadLVarBool("S_MIP_AUTOBRAKE_MAX"))
    {
        return AutobrakeMode::Max;
    }

    if (ReadLVarBool("S_MIP_AUTOBRAKE_MED"))
    {
        return AutobrakeMode::Medium;
    }

    if (ReadLVarBool("S_MIP_AUTOBRAKE_LO"))
    {
        return AutobrakeMode::Low;
    }

    return AutobrakeMode::Off;
}

LandingConfigurationCode DetermineLandingConfiguration(double flapsHandleIndex, bool gearDownLocked)
{
    if (!gearDownLocked)
    {
        return LandingConfigurationCode::NotConfigured;
    }

    if (flapsHandleIndex >= 4.0)
    {
        return LandingConfigurationCode::Full;
    }

    if (flapsHandleIndex >= 3.0)
    {
        return LandingConfigurationCode::Config3;
    }

    return LandingConfigurationCode::NotConfigured;
}

ApproachCapability DetermineApproachCapability(
    bool approachModeActive,
    bool localizerCaptured,
    bool glideslopeCaptured,
    double radioAltitudeFeet)
{
    if (!approachModeActive || !localizerCaptured)
    {
        return ApproachCapability::None;
    }

    if (glideslopeCaptured && radioAltitudeFeet <= 1500.0)
    {
        return ApproachCapability::Cat3Dual;
    }

    if (glideslopeCaptured)
    {
        return ApproachCapability::Cat2;
    }

    return ApproachCapability::Cat1;
}

FmaLateralMode DetermineLateralMode(
    bool headingManaged,
    bool localizerCaptured,
    bool approachModeActive,
    bool navCaptured,
    bool onGround)
{
    if (onGround)
    {
        return FmaLateralMode::Rollout;
    }

    if (approachModeActive && localizerCaptured)
    {
        return FmaLateralMode::Approach;
    }

    if (localizerCaptured)
    {
        return FmaLateralMode::Loc;
    }

    if (navCaptured)
    {
        return FmaLateralMode::Nav;
    }

    if (headingManaged)
    {
        return FmaLateralMode::Heading;
    }

    return FmaLateralMode::Unknown;
}

FmaVerticalMode DetermineVerticalMode(
    bool verticalManaged,
    bool glideslopeCaptured,
    bool landModeArmed,
    bool flareModeArmed,
    bool onGround,
    double radioAltitudeFeet,
    double verticalSpeedFpm)
{
    if (onGround)
    {
        return FmaVerticalMode::Rollout;
    }

    if (flareModeArmed && radioAltitudeFeet <= 50.0)
    {
        return FmaVerticalMode::Flare;
    }

    if (landModeArmed && radioAltitudeFeet <= 200.0)
    {
        return FmaVerticalMode::Land;
    }

    if (glideslopeCaptured)
    {
        return FmaVerticalMode::Glideslope;
    }

    if (verticalManaged)
    {
        return verticalSpeedFpm >= 0.0 ? FmaVerticalMode::OpenClimb : FmaVerticalMode::OpenDescent;
    }

    return FmaVerticalMode::Unknown;
}

void PopulateSnapshot(FenixFpmSharedBuffer& buffer)
{
    const bool onGround = ReadSimBool("SIM ON GROUND", "Bool", -1);
    const bool localizerCaptured = ReadSimBool("AUTOPILOT NAV1 LOCK", "Bool", -1);
    const bool glideslopeCaptured = ReadSimBool("AUTOPILOT GLIDESLOPE HOLD", "Bool", -1);
    const bool approachModeActive = ReadLVarBool("S_FCU_APPR") || ReadSimBool("AUTOPILOT APPROACH HOLD", "Bool", -1);
    const bool headingManaged = ReadLVarBool("S_FCU_HEADING");
    const bool speedManaged = ReadLVarBool("S_FCU_SPEED");
    const bool altitudeManaged = ReadLVarBool("S_FCU_ALTITUDE");
    const bool verticalManaged = ReadLVarBool("S_FCU_VERTICAL_SPEED");
    const bool autothrustActive = ReadLVarBool("I_FCU_ATHR") || ReadLVarBool("S_FCU_ATHR");
    const bool gear1Down = ReadLVarBool("I_MIP_GEAR_1_U");
    const bool gear2Down = ReadLVarBool("I_MIP_GEAR_2_U");
    const bool gear3Down = ReadLVarBool("I_MIP_GEAR_3_U");
    const bool gearDownLocked = gear1Down && gear2Down && gear3Down;

    const double flapsHandleIndex = ReadLVar("S_FC_FLAPS", 0.0);
    const double spoilersHandle = ReadLVar("A_FC_SPEEDBRAKE", 0.0);
    const double radioAltitudeFeet = ReadSimVar("RADIO HEIGHT", "Feet", -1, 0.0);
    const double verticalSpeedFpm = ReadSimVar("VERTICAL SPEED", "Feet per minute", -1, 0.0);
    const double indicatedAirspeedKnots = ReadSimVar("AIRSPEED INDICATED", "Knots", -1, 0.0);
    const double trueAirspeedKnots = ReadSimVar("AIRSPEED TRUE", "Knots", -1, 0.0);
    const double groundSpeedKnots = ReadSimVar("GPS GROUND SPEED", "Knots", -1, 0.0);
    const double baroAltitudeFeet = ReadSimVar("INDICATED ALTITUDE", "Feet", -1, 0.0);
    const double grossWeightKg = ReadSimVar("TOTAL WEIGHT", "Kilograms", -1, 0.0);
    const double outsideAirTemperatureC = ReadSimVar("AMBIENT TEMPERATURE", "Celsius", -1, 0.0);
    const double pressureAltitudeFeet = ReadSimVar("PRESSURE ALTITUDE", "Feet", -1, 0.0);
    const double pitchDegrees = ReadSimVar("PLANE PITCH DEGREES", "Degrees", -1, 0.0);
    const double bankDegrees = ReadSimVar("PLANE BANK DEGREES", "Degrees", -1, 0.0);
    const double leftN1Percent = ReadSimVar("GENERAL ENG N1", "Percent", 1, 0.0);
    const double rightN1Percent = ReadSimVar("GENERAL ENG N1", "Percent", 2, 0.0);
    const double leftEgtCelsius = ReadSimVar("GENERAL ENG EXHAUST GAS TEMPERATURE", "Celsius", 1, 0.0);
    const double rightEgtCelsius = ReadSimVar("GENERAL ENG EXHAUST GAS TEMPERATURE", "Celsius", 2, 0.0);
    const double leftFuelFlowKgPerHour = ReadSimVar("TURB ENG FUEL FLOW PPH", "Pounds per hour", 1, 0.0) * PoundsToKilograms;
    const double rightFuelFlowKgPerHour = ReadSimVar("TURB ENG FUEL FLOW PPH", "Pounds per hour", 2, 0.0) * PoundsToKilograms;
    const double thrustLever1Degrees = ReadLVar("A_FC_THROTTLE_LEFT_INPUT", 0.0);
    const double thrustLever2Degrees = ReadLVar("A_FC_THROTTLE_RIGHT_INPUT", 0.0);
    const double sideStickPitch = ReadLVar("N_FC_SIDESTICK_CAPT_PITCH", 0.0);
    const double sideStickRoll = ReadLVar("N_FC_SIDESTICK_CAPT_BANK", 0.0);
    const double rudderPercent = ReadLVar("N_FC_RUDDER", 0.0);
    const double elevatorPercent = ReadSimVar("ELEVATOR DEFLECTION PCT", "Percent", -1, 0.0);
    const double aileronPercent = ReadSimVar("AILERON LEFT DEFLECTION PCT", "Percent", -1, 0.0);
    const double vappKnots = ReadLVar("E_FCU_SPEED", indicatedAirspeedKnots);
    const AutobrakeMode autobrakeMode = DetermineAutobrakeMode();
    const LandingConfigurationCode landingConfiguration = DetermineLandingConfiguration(flapsHandleIndex, gearDownLocked);
    const bool spoilersArmed = spoilersHandle > 0.05;
    const bool landModeArmed = approachModeActive && localizerCaptured && glideslopeCaptured && radioAltitudeFeet <= 300.0;
    const bool flareModeArmed = landModeArmed && radioAltitudeFeet <= 100.0;
    const ApproachCapability approachCapability = DetermineApproachCapability(
        approachModeActive,
        localizerCaptured,
        glideslopeCaptured,
        radioAltitudeFeet);
    const FmaLateralMode lateralMode = DetermineLateralMode(
        headingManaged,
        localizerCaptured,
        approachModeActive,
        ReadSimBool("AUTOPILOT NAV1 LOCK", "Bool", -1),
        onGround);
    const FmaVerticalMode verticalMode = DetermineVerticalMode(
        verticalManaged,
        glideslopeCaptured,
        landModeArmed,
        flareModeArmed,
        onGround,
        radioAltitudeFeet,
        verticalSpeedFpm);

    buffer.Flight.Aircraft.IndicatedAirspeedKnots = indicatedAirspeedKnots;
    buffer.Flight.Aircraft.TrueAirspeedKnots = trueAirspeedKnots;
    buffer.Flight.Aircraft.GroundSpeedKnots = groundSpeedKnots;
    buffer.Flight.Aircraft.BaroAltitudeFeet = baroAltitudeFeet;
    buffer.Flight.Aircraft.RadioAltitudeFeet = radioAltitudeFeet;
    buffer.Flight.Aircraft.VerticalSpeedFpm = verticalSpeedFpm;
    buffer.Flight.Aircraft.VappKnots = vappKnots;
    buffer.Flight.Aircraft.GrossWeightKg = grossWeightKg;
    buffer.Flight.Aircraft.OutsideAirTemperatureC = outsideAirTemperatureC;
    buffer.Flight.Aircraft.PressureAltitudeFeet = pressureAltitudeFeet;
    buffer.Flight.Aircraft.PitchDegrees = pitchDegrees;
    buffer.Flight.Aircraft.BankDegrees = bankDegrees;
    buffer.Flight.Aircraft.LandingConfiguration = static_cast<std::uint16_t>(landingConfiguration);
    buffer.Flight.Aircraft.AutobrakeMode = static_cast<std::uint8_t>(autobrakeMode);
    buffer.Flight.Aircraft.OnGround = ToByte(onGround);
    buffer.Flight.Aircraft.GearDownLocked = ToByte(gearDownLocked);
    buffer.Flight.Aircraft.SpoilersArmed = ToByte(spoilersArmed);

    buffer.Flight.Autopilot.LateralMode = static_cast<std::uint16_t>(lateralMode);
    buffer.Flight.Autopilot.VerticalMode = static_cast<std::uint16_t>(verticalMode);
    buffer.Flight.Autopilot.ApproachCapability = static_cast<std::uint16_t>(approachCapability);
    buffer.Flight.Autopilot.Reserved0 = 0U;
    buffer.Flight.Autopilot.LandModeArmed = ToByte(landModeArmed);
    buffer.Flight.Autopilot.FlareModeArmed = ToByte(flareModeArmed);
    buffer.Flight.Autopilot.AutothrustActive = ToByte(autothrustActive);
    buffer.Flight.Autopilot.ManagedSpeedActive = ToByte(speedManaged);
    buffer.Flight.Autopilot.GlideslopeCaptured = ToByte(glideslopeCaptured);
    buffer.Flight.Autopilot.LocalizerCaptured = ToByte(localizerCaptured);
    buffer.Flight.Autopilot.ApproachModeActive = ToByte(approachModeActive);
    buffer.Flight.Autopilot.Reserved1 = 0U;

    buffer.Flight.Engines.LeftN1Percent = leftN1Percent;
    buffer.Flight.Engines.RightN1Percent = rightN1Percent;
    buffer.Flight.Engines.LeftEgtCelsius = leftEgtCelsius;
    buffer.Flight.Engines.RightEgtCelsius = rightEgtCelsius;
    buffer.Flight.Engines.LeftFuelFlowKgPerHour = leftFuelFlowKgPerHour;
    buffer.Flight.Engines.RightFuelFlowKgPerHour = rightFuelFlowKgPerHour;
    buffer.Flight.Engines.ThrustLever1Degrees = thrustLever1Degrees;
    buffer.Flight.Engines.ThrustLever2Degrees = thrustLever2Degrees;

    buffer.Flight.Controls.SideStickPitch = sideStickPitch;
    buffer.Flight.Controls.SideStickRoll = sideStickRoll;
    buffer.Flight.Controls.RudderPercent = rudderPercent;
    buffer.Flight.Controls.ElevatorPercent = elevatorPercent;
    buffer.Flight.Controls.AileronPercent = aileronPercent;
    buffer.Flight.Controls.SpoilersPercent = spoilersHandle;
    buffer.Flight.Controls.FlapsHandleIndex = flapsHandleIndex;
    buffer.Flight.Controls.SpeedBrakeHandlePercent = spoilersHandle;

    buffer.Flight.Systems.ApuBleedActive = ToByte(ReadLVarBool("S_OH_PNEUMATIC_APU_BLEED"));
    buffer.Flight.Systems.Engine1AntiIce = ToByte(ReadLVarBool("S_OH_PNEUMATIC_ENG1_ANTI_ICE"));
    buffer.Flight.Systems.Engine2AntiIce = ToByte(ReadLVarBool("S_OH_PNEUMATIC_ENG2_ANTI_ICE"));
    buffer.Flight.Systems.WingAntiIce = ToByte(ReadLVarBool("S_OH_PNEUMATIC_WING_ANTI_ICE"));
    buffer.Flight.Systems.FcuHeadingManaged = ToByte(headingManaged);
    buffer.Flight.Systems.FcuSpeedManaged = ToByte(speedManaged);
    buffer.Flight.Systems.FcuAltitudeManaged = ToByte(altitudeManaged);
    buffer.Flight.Systems.FcuVerticalManaged = ToByte(verticalManaged);
    buffer.Flight.Systems.Reserved1 = 0U;
    buffer.Flight.Systems.Reserved2 = 0U;
    buffer.Flight.Systems.Reserved3 = 0U;
    buffer.Flight.Systems.Reserved4 = 0U;
    buffer.Flight.Systems.Reserved5 = 0U;
    buffer.Flight.Systems.Reserved6 = 0U;
    buffer.Flight.Systems.Reserved7 = 0U;
    buffer.Flight.Systems.Reserved8 = 0U;
}

void PublishBuffer(float frameRate)
{
    if (g_hSimConnect == 0 || !g_clientDataReady)
    {
        return;
    }

    FenixFpmSharedBuffer buffer = {};
    buffer.Header.Version = BufferVersion;
    buffer.Header.SizeBytes = static_cast<std::uint32_t>(sizeof(FenixFpmSharedBuffer));
    buffer.Header.Sequence = ++g_sequence;
    buffer.Header.TimestampUnixMicroseconds = GetUnixMicroseconds();
    buffer.Header.SampleRateHz = NormalizeSampleRate(frameRate);
    buffer.Header.Flags = 0U;

    PopulateSnapshot(buffer);
    buffer.Checksum = ComputeChecksum(buffer);

    const HRESULT setDataHr = SimConnect_SetClientData(
        g_hSimConnect,
        static_cast<SIMCONNECT_CLIENT_DATA_ID>(ClientDataAreaId),
        static_cast<SIMCONNECT_CLIENT_DATA_DEFINITION_ID>(ClientDataDefinitionId),
        SIMCONNECT_CLIENT_DATA_SET_FLAG_DEFAULT,
        0,
        sizeof(FenixFpmSharedBuffer),
        &buffer);

    if (FAILED(setDataHr))
    {
        SimConnect_Close(g_hSimConnect);
        g_hSimConnect = 0;
        g_clientDataReady = false;
    }
}

void CALLBACK MyDispatchProc(SIMCONNECT_RECV* recvData, DWORD recvSize, void* context)
{
    (void)recvSize;
    (void)context;

    if (recvData == 0)
    {
        return;
    }

    switch (recvData->dwID)
    {
    case SIMCONNECT_RECV_ID_EVENT_FRAME:
    {
        SIMCONNECT_RECV_EVENT_FRAME* frameEvent = reinterpret_cast<SIMCONNECT_RECV_EVENT_FRAME*>(recvData);
        if (frameEvent->uEventID == FrameEventId)
        {
            PublishBuffer(frameEvent->fFrameRate);
        }
        break;
    }

    case SIMCONNECT_RECV_ID_EXCEPTION:
        if (g_hSimConnect != 0)
        {
            SimConnect_Close(g_hSimConnect);
            g_hSimConnect = 0;
        }
        g_clientDataReady = false;
        break;

    case SIMCONNECT_RECV_ID_QUIT:
        if (g_hSimConnect != 0)
        {
            SimConnect_Close(g_hSimConnect);
            g_hSimConnect = 0;
        }
        g_clientDataReady = false;
        break;

    default:
        break;
    }
}

void PumpDispatch()
{
    if (g_hSimConnect == 0)
    {
        return;
    }

    const HRESULT dispatchHr = SimConnect_CallDispatch(g_hSimConnect, MyDispatchProc, 0);
    if (FAILED(dispatchHr))
    {
        SimConnect_Close(g_hSimConnect);
        g_hSimConnect = 0;
        g_clientDataReady = false;
    }
}

void TryInitializeSimConnect(bool force)
{
    if (g_hSimConnect != 0)
    {
        return;
    }

    const std::chrono::steady_clock::time_point now = std::chrono::steady_clock::now();
    if (!force && g_lastConnectAttempt != std::chrono::steady_clock::time_point::min() &&
        now - g_lastConnectAttempt < ReconnectDelay)
    {
        return;
    }

    g_lastConnectAttempt = now;

    HANDLE simConnect = 0;
    const HRESULT openHr = SimConnect_Open(&simConnect, "FenixFpmWasm", 0, 0, 0, 0);
    if (FAILED(openHr))
    {
        return;
    }

    const HRESULT mapHr = SimConnect_MapClientDataNameToID(
        simConnect,
        ClientDataName,
        static_cast<SIMCONNECT_CLIENT_DATA_ID>(ClientDataAreaId));
    if (FAILED(mapHr))
    {
        SimConnect_Close(simConnect);
        return;
    }

    (void)SimConnect_CreateClientData(
        simConnect,
        static_cast<SIMCONNECT_CLIENT_DATA_ID>(ClientDataAreaId),
        sizeof(FenixFpmSharedBuffer),
        SIMCONNECT_CREATE_CLIENT_DATA_FLAG_DEFAULT);

    const HRESULT definitionHr = SimConnect_AddToClientDataDefinition(
        simConnect,
        static_cast<SIMCONNECT_CLIENT_DATA_DEFINITION_ID>(ClientDataDefinitionId),
        0,
        sizeof(FenixFpmSharedBuffer),
        0,
        0);

    const HRESULT subscribeHr = SimConnect_SubscribeToSystemEvent(
        simConnect,
        static_cast<SIMCONNECT_CLIENT_EVENT_ID>(FrameEventId),
        "Frame");

    if (FAILED(definitionHr) || FAILED(subscribeHr))
    {
        SimConnect_Close(simConnect);
        return;
    }

    g_hSimConnect = simConnect;
    g_clientDataReady = true;
    PumpDispatch();
}

void FSAPI QueryGauge()
{
}

void FSAPI InstallGauge(PVOID resourceFileHandle)
{
    (void)resourceFileHandle;
}

void FSAPI InitializeGauge()
{
    TryInitializeSimConnect(true);
}

void FSAPI UpdateGauge()
{
    TryInitializeSimConnect(false);
    PumpDispatch();
}

void FSAPI GenerateGauge(UINT32 phase)
{
    (void)phase;
}

void FSAPI DrawGauge()
{
}

void FSAPI KillGauge()
{
}

GAUGEHDR CreateGaugeHeader()
{
    GAUGEHDR gaugeHeader = {};
    gaugeHeader.gauge_header_version = GAUGE_HEADER_VERSION_FS1000;
    gaugeHeader.gauge_name = kGaugeName;
    gaugeHeader.elements_list = 0;
    gaugeHeader.query_routine = QueryGauge;
    gaugeHeader.install_routine = InstallGauge;
    gaugeHeader.initialize_routine = InitializeGauge;
    gaugeHeader.update_routine = UpdateGauge;
    gaugeHeader.generate_routine = GenerateGauge;
    gaugeHeader.draw_routine = DrawGauge;
    gaugeHeader.kill_routine = KillGauge;
    gaugeHeader.size_x_mm = 1U;
    gaugeHeader.size_y_mm = 1U;
    gaugeHeader.gauge_callback = 0;
    gaugeHeader.user_data = 0U;
    gaugeHeader.parameters = 0;
    gaugeHeader.usage = kGaugeUsage;
    gaugeHeader.key_id = 0U;
    return gaugeHeader;
}

GAUGEHDR g_gaugeHeader = CreateGaugeHeader();
} // namespace

extern "C" DECLSPEC_EXPORT GaugesImportTable ImportTable =
{
    { PanelsApiImportId, 0 },
    { 0x00000000, 0 }
};

extern "C" void FSAPI module_init(void)
{
    TryInitializeSimConnect(true);
}

extern "C" void FSAPI module_deinit(void)
{
    if (g_hSimConnect != 0)
    {
        SimConnect_Close(g_hSimConnect);
        g_hSimConnect = 0;
    }

    g_clientDataReady = false;
    g_namedVariableCache.clear();
    unregister_all_named_vars();
}

extern "C" BOOL WINAPI DllMain(HINSTANCE hDll, DWORD reason, LPVOID reserved)
{
    (void)hDll;
    (void)reason;
    (void)reserved;
    return TRUE;
}

extern "C" DECLSPEC_EXPORT GAUGESLINKAGE Linkage =
{
    GaugeModuleId,
    module_init,
    module_deinit,
    0,
    0,
    FS9LINK_VERSION,
    { &g_gaugeHeader, 0 }
};

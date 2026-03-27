using FenixFpm.Contracts.Interop;
using FenixFpm.Core.Models;
using ContractApproachCapability = FenixFpm.Contracts.Interop.ApproachCapability;
using ContractAutobrakeMode = FenixFpm.Contracts.Interop.AutobrakeMode;
using ContractLandingConfiguration = FenixFpm.Contracts.Interop.LandingConfigurationCode;
using ContractLateralMode = FenixFpm.Contracts.Interop.FmaLateralMode;
using ContractVerticalMode = FenixFpm.Contracts.Interop.FmaVerticalMode;
using DomainApproachCapability = FenixFpm.Core.Models.ApproachCapability;
using DomainAutobrakeMode = FenixFpm.Core.Models.AutobrakeMode;
using DomainLandingConfiguration = FenixFpm.Core.Models.LandingConfiguration;
using DomainLateralMode = FenixFpm.Core.Models.FmaLateralMode;
using DomainVerticalMode = FenixFpm.Core.Models.FmaVerticalMode;

namespace FenixFpm.Infrastructure.Mapping;

public static class FenixSnapshotMapper
{
    public static FlightSnapshot Map(FenixFpmSharedBuffer buffer)
    {
        return new FlightSnapshot(
            TimestampUtc: DateTimeOffset.FromUnixTimeMilliseconds(buffer.Header.TimestampUnixMicroseconds / 1000),
            IndicatedAirspeedKnots: buffer.Aircraft.IndicatedAirspeedKnots,
            TrueAirspeedKnots: buffer.Aircraft.TrueAirspeedKnots,
            GroundSpeedKnots: buffer.Aircraft.GroundSpeedKnots,
            BaroAltitudeFeet: buffer.Aircraft.BaroAltitudeFeet,
            RadioAltitudeFeet: buffer.Aircraft.RadioAltitudeFeet,
            VerticalSpeedFpm: buffer.Aircraft.VerticalSpeedFpm,
            VappKnots: buffer.Aircraft.VappKnots,
            GrossWeightKg: buffer.Aircraft.GrossWeightKg,
            OutsideAirTemperatureC: buffer.Aircraft.OutsideAirTemperatureC,
            PressureAltitudeFeet: buffer.Aircraft.PressureAltitudeFeet,
            PitchDegrees: buffer.Aircraft.PitchDegrees,
            BankDegrees: buffer.Aircraft.BankDegrees,
            OnGround: buffer.Aircraft.OnGround != 0,
            LandingConfiguration: new LandingConfigurationSnapshot(
                (DomainLandingConfiguration)(ContractLandingConfiguration)buffer.Aircraft.LandingConfiguration,
                (DomainAutobrakeMode)(ContractAutobrakeMode)buffer.Aircraft.AutobrakeMode,
                buffer.Aircraft.GearDownLocked != 0,
                buffer.Aircraft.SpoilersArmed != 0),
            Autopilot: new AutopilotSnapshot(
                (DomainLateralMode)(ContractLateralMode)buffer.Autopilot.LateralMode,
                (DomainVerticalMode)(ContractVerticalMode)buffer.Autopilot.VerticalMode,
                (DomainApproachCapability)(ContractApproachCapability)buffer.Autopilot.ApproachCapability,
                buffer.Autopilot.LandModeArmed != 0,
                buffer.Autopilot.FlareModeArmed != 0,
                buffer.Autopilot.AutothrustActive != 0,
                buffer.Autopilot.ManagedSpeedActive != 0,
                buffer.Autopilot.GlideslopeCaptured != 0,
                buffer.Autopilot.LocalizerCaptured != 0,
                buffer.Autopilot.ApproachModeActive != 0),
            Engines: new EngineSnapshot(
                buffer.Engines.LeftN1Percent,
                buffer.Engines.RightN1Percent,
                buffer.Engines.LeftEgtCelsius,
                buffer.Engines.RightEgtCelsius,
                buffer.Engines.LeftFuelFlowKgPerHour,
                buffer.Engines.RightFuelFlowKgPerHour,
                buffer.Engines.ThrustLever1Degrees,
                buffer.Engines.ThrustLever2Degrees),
            Controls: new ControlSurfaceSnapshot(
                buffer.Controls.SideStickPitch,
                buffer.Controls.SideStickRoll,
                buffer.Controls.RudderPercent,
                buffer.Controls.ElevatorPercent,
                buffer.Controls.AileronPercent,
                buffer.Controls.SpoilersPercent,
                buffer.Controls.FlapsHandleIndex,
                buffer.Controls.SpeedBrakeHandlePercent));
    }
}
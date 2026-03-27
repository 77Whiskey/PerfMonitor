using FenixFpm.Core.Abstractions;
using FenixFpm.Core.Models;

namespace FenixFpm.Core.Modules;

public sealed class ConfigurationManagementModule : IAirbusModule
{
    private readonly IPerformanceDataService _performanceData;

    private readonly Dictionary<LandingConfiguration, double> _flapSpeedRestrictions = new()
    {
        { LandingConfiguration.Full, 180 },
        { LandingConfiguration.Config3, 230 },
        { LandingConfiguration.NotConfigured, 250 }
    };

    private const int GearDownMaxSpeed = 230;
    private const int GearUpMaxSpeed = 250;

    private bool _takeoffFlapsSet;
    private DateTimeOffset? _flapsSetTime;
    private DateTimeOffset? _gearExtensionTime;
    private bool _gearExtendedThisPhase;
    private FlightPhase _lastPhase = FlightPhase.Unknown;
    private LandingConfigurationSnapshot _lastConfig = new(LandingConfiguration.Unknown, AutobrakeMode.Off, false, false);

    public ConfigurationManagementModule(IPerformanceDataService performanceData)
    {
        _performanceData = performanceData;
    }

    public string Name => "Configuration Management";

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public ValueTask<IReadOnlyList<FlightEvent>> EvaluateAsync(FlightSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var events = new List<FlightEvent>();
        var phase = DetectPhase(snapshot);
        var config = snapshot.LandingConfiguration;

        CheckSpeedConfigurationCompliance(snapshot, config, events);
        CheckFlapsTiming(snapshot, phase, config, events);
        CheckGearTiming(snapshot, phase, config, events);
        CheckConfigurationOnApproach(snapshot, config, events);

        _lastPhase = phase;
        _lastConfig = config;
        _gearExtendedThisPhase = false;

        return ValueTask.FromResult<IReadOnlyList<FlightEvent>>(events);
    }

    private static FlightPhase DetectPhase(FlightSnapshot snapshot)
    {
        if (snapshot.OnGround)
        {
            return snapshot.GroundSpeedKnots < 5 ? FlightPhase.Ground : FlightPhase.Takeoff;
        }

        if (snapshot.BaroAltitudeFeet < 50) return FlightPhase.Takeoff;
        if (snapshot.BaroAltitudeFeet < 2000) return FlightPhase.Climb;
        if (snapshot.BaroAltitudeFeet < 15000) return FlightPhase.Cruise;
        if (snapshot.BaroAltitudeFeet < 3000) return FlightPhase.Approach;
        return FlightPhase.Landing;
    }

    private void CheckSpeedConfigurationCompliance(FlightSnapshot snapshot, LandingConfigurationSnapshot config, List<FlightEvent> events)
    {
        var ias = snapshot.IndicatedAirspeedKnots;
        var flapConfig = config.Configuration;

        if (_flapSpeedRestrictions.TryGetValue(flapConfig, out var maxSpeed))
        {
            if (ias > maxSpeed)
            {
                var speedOver = ias - maxSpeed;
                events.Add(new FlightEvent(
                    snapshot.TimestampUtc,
                    "FlapsOverspeed",
                    $"{flapConfig} flaps overspeed: {ias:F0} kt exceeds Vle {maxSpeed} kt by {speedOver:F0} kt",
                    speedOver > 10 ? EventSeverity.Warning : EventSeverity.Advisory,
                    ias,
                    maxSpeed)
                {
                    FlightPhase = DetectPhase(snapshot).ToString(),
                    IsPositive = false
                });
            }
        }

        if (config.GearDown && ias > GearDownMaxSpeed)
        {
            var speedOver = ias - GearDownMaxSpeed;
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                "GearOverspeed",
                $"Gear down overspeed: {ias:F0} kt exceeds Vle {GearDownMaxSpeed} kt by {speedOver:F0} kt",
                speedOver > 10 ? EventSeverity.Warning : EventSeverity.Advisory,
                ias,
                GearDownMaxSpeed)
            {
                FlightPhase = DetectPhase(snapshot).ToString(),
                IsPositive = false
            });
        }
    }

    private void CheckFlapsTiming(FlightSnapshot snapshot, FlightPhase phase, LandingConfigurationSnapshot config, List<FlightEvent> events)
    {
        var flapConfig = config.Configuration;

        if (phase == FlightPhase.Takeoff && !_takeoffFlapsSet && flapConfig != LandingConfiguration.NotConfigured)
        {
            _takeoffFlapsSet = true;
            _flapsSetTime = snapshot.TimestampUtc;
        }

        if (phase == FlightPhase.Climb && _takeoffFlapsSet && flapConfig == LandingConfiguration.Full)
        {
            var minutesSinceTakeoff = (_flapsSetTime.HasValue)
                ? (snapshot.TimestampUtc - _flapsSetTime.Value).TotalMinutes
                : 0;

            if (minutesSinceTakeoff > 3)
            {
                events.Add(new FlightEvent(
                    snapshot.TimestampUtc,
                    "LateFlapsRetraction",
                    $"Flaps retracted after {minutesSinceTakeoff:F1} min (target < 3 min)",
                    EventSeverity.Advisory,
                    minutesSinceTakeoff,
                    3)
                {
                    FlightPhase = FlightPhase.Climb.ToString()
                });
            }

            _takeoffFlapsSet = false;
        }

        if (phase == FlightPhase.Approach && snapshot.IndicatedAirspeedKnots > 180 && flapConfig != LandingConfiguration.Full)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                "LateFlapsExtension",
                $"Flaps not set to FULL at {snapshot.IndicatedAirspeedKnots:F0} kt (Vapp typically ~140 kt)",
                EventSeverity.Advisory,
                snapshot.IndicatedAirspeedKnots,
                180)
            {
                FlightPhase = FlightPhase.Approach.ToString()
            });
        }
    }

    private void CheckGearTiming(FlightSnapshot snapshot, FlightPhase phase, LandingConfigurationSnapshot config, List<FlightEvent> events)
    {
        if (phase == FlightPhase.Climb && !_gearExtendedThisPhase && config.GearDown)
        {
            _gearExtendedThisPhase = true;
            _gearExtensionTime = snapshot.TimestampUtc;
        }

        if (phase == FlightPhase.Climb && _gearExtensionTime.HasValue && !config.GearDown)
        {
            var minutesSinceGear = (snapshot.TimestampUtc - _gearExtensionTime.Value).TotalMinutes;
            if (minutesSinceGear > 2)
            {
                events.Add(new FlightEvent(
                    snapshot.TimestampUtc,
                    "LateGearRetraction",
                    $"Gear retracted after {minutesSinceGear:F1} min in climb (target < 2 min)",
                    EventSeverity.Advisory,
                    minutesSinceGear,
                    2)
                {
                    FlightPhase = FlightPhase.Climb.ToString()
                });
            }
            _gearExtensionTime = null;
        }

        if (phase == FlightPhase.Approach && snapshot.RadioAltitudeFeet < 2000 && !config.GearDown)
        {
            events.Add(new FlightEvent(
                snapshot.TimestampUtc,
                "LateGearExtension",
                $"Gear not extended at {snapshot.RadioAltitudeFeet:F0} ft (standard: extension by 1500 ft)",
                EventSeverity.Advisory,
                snapshot.RadioAltitudeFeet,
                1500)
            {
                FlightPhase = FlightPhase.Approach.ToString()
            });
        }
    }

    private void CheckConfigurationOnApproach(FlightSnapshot snapshot, LandingConfigurationSnapshot config, List<FlightEvent> events)
    {
        if (DetectPhase(snapshot) != FlightPhase.Approach) return;

        if (snapshot.RadioAltitudeFeet < 1500)
        {
            var issues = new List<string>();
            var flapConfig = config.Configuration;

            if (!config.GearDown) issues.Add("gear up");
            if (flapConfig != LandingConfiguration.Full && flapConfig != LandingConfiguration.Config3) issues.Add($"flaps {flapConfig}");
            if (snapshot.IndicatedAirspeedKnots > 180) issues.Add($"speed {snapshot.IndicatedAirspeedKnots:F0} kt");

            if (issues.Count > 1)
            {
                events.Add(new FlightEvent(
                    snapshot.TimestampUtc,
                    "ApproachConfiguration",
                    $"Below 1500 ft: {string.Join(", ", issues)} - not properly configured",
                    EventSeverity.Warning,
                    snapshot.RadioAltitudeFeet,
                    1500)
                {
                    FlightPhase = FlightPhase.Approach.ToString(),
                    IsPositive = false
                });
            }
        }
    }
}
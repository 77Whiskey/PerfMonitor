using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FenixFpm.Contracts.Interop;
using FenixFpm.Core.Models;
using FenixFpm.Infrastructure.Ingestion;

namespace FenixFpm.Desktop.ViewModels;

public partial class ActiveFlightViewModel : ObservableObject
{
    private readonly TelemetryIngestionWorker _telemetryWorker;
    private readonly SopConfiguration _sopConfig;
    private DateTimeOffset? _lastPitchSampleTimestampUtc;
    private double _lastPitchSampleDegrees;

    [ObservableProperty]
    private double _indicatedAirspeedKnots;

    [ObservableProperty]
    private double _groundSpeedKnots;

    [ObservableProperty]
    private double _baroAltitudeFeet;

    [ObservableProperty]
    private double _radioAltitudeFeet;

    [ObservableProperty]
    private double _verticalSpeedFpm;

    [ObservableProperty]
    private double _pitchDegrees;

    [ObservableProperty]
    private double _bankDegrees;

    [ObservableProperty]
    private double _grossWeightKg;

    [ObservableProperty]
    private double _fuelFlowKgPerHour;

    [ObservableProperty]
    private double _outsideAirTempC;

    [ObservableProperty]
    private string _flightPhase = "Unknown";

    [ObservableProperty]
    private bool _isOnGround = true;

    [ObservableProperty]
    private bool _gearDown;

    [ObservableProperty]
    private bool _spoilersArmed;

    [ObservableProperty]
    private bool _landingConfigurationReady;

    [ObservableProperty]
    private bool _autothrustActive;

    [ObservableProperty]
    private bool _managedSpeedActive;

    [ObservableProperty]
    private double _leftN1Percent;

    [ObservableProperty]
    private double _rightN1Percent;

    [ObservableProperty]
    private double _leftFuelFlowKgPerHour;

    [ObservableProperty]
    private double _rightFuelFlowKgPerHour;

    [ObservableProperty]
    private string _lateralMode = "Unknown";

    [ObservableProperty]
    private string _verticalMode = "Unknown";

    [ObservableProperty]
    private double _flapsHandleIndex;

    [ObservableProperty]
    private double _spoilersPercent;

    [ObservableProperty]
    private ObservableCollection<FlightEvent> _recentEvents = new();

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private int _advisoryCount;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _apuBleedActive;

    [ObservableProperty]
    private bool _engine1AntiIce;

    [ObservableProperty]
    private bool _engine2AntiIce;

    [ObservableProperty]
    private bool _wingAntiIce;

    [ObservableProperty]
    private bool _fcuHeadingManaged;

    [ObservableProperty]
    private bool _fcuSpeedManaged;

    [ObservableProperty]
    private string _autobrakeMode = "OFF";

    [ObservableProperty]
    private double _engine1N1;

    [ObservableProperty]
    private double _engine2N1;

    [ObservableProperty]
    private double _rotationRateDegreesPerSecond;

    [ObservableProperty]
    private string _parkingBrakeState = "SET";

    public SopConfiguration SopConfig => _sopConfig;

    public ActiveFlightViewModel(
        TelemetryIngestionWorker telemetryWorker,
        SopConfiguration sopConfig)
    {
        _telemetryWorker = telemetryWorker;
        _sopConfig = sopConfig;

        _telemetryWorker.TelemetryReceived += OnTelemetryReceived;
        _telemetryWorker.SnapshotReceived += OnSnapshotReceived;
        _telemetryWorker.EventDetected += OnEventDetected;
        _telemetryWorker.PhaseChanged += OnPhaseChanged;
        IsConnected = false;
    }

    private void OnTelemetryReceived(object? sender, FenixFpmSharedBuffer buffer)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            IsConnected = true;
            ApuBleedActive = buffer.Systems.ApuBleedActive != 0;
            Engine1AntiIce = buffer.Systems.Engine1AntiIce != 0;
            Engine2AntiIce = buffer.Systems.Engine2AntiIce != 0;
            WingAntiIce = buffer.Systems.WingAntiIce != 0;
            FcuHeadingManaged = buffer.Systems.FcuHeadingManaged != 0;
            FcuSpeedManaged = buffer.Systems.FcuSpeedManaged != 0;
        });
    }

    private void OnSnapshotReceived(object? sender, FlightSnapshot snapshot)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            IsConnected = true;
            IndicatedAirspeedKnots = snapshot.IndicatedAirspeedKnots;
            GroundSpeedKnots = snapshot.GroundSpeedKnots;
            BaroAltitudeFeet = snapshot.BaroAltitudeFeet;
            RadioAltitudeFeet = snapshot.RadioAltitudeFeet;
            VerticalSpeedFpm = snapshot.VerticalSpeedFpm;
            PitchDegrees = snapshot.PitchDegrees;
            BankDegrees = snapshot.BankDegrees;
            GrossWeightKg = snapshot.GrossWeightKg;
            OutsideAirTempC = snapshot.OutsideAirTemperatureC;
            IsOnGround = snapshot.OnGround;
            GearDown = snapshot.LandingConfiguration.GearDown;
            SpoilersArmed = snapshot.LandingConfiguration.SpoilersArmed;
            AutothrustActive = snapshot.Autopilot.AutothrustActive;
            ManagedSpeedActive = snapshot.Autopilot.ManagedSpeedActive;
            LeftN1Percent = snapshot.Engines.LeftN1Percent;
            RightN1Percent = snapshot.Engines.RightN1Percent;
            Engine1N1 = snapshot.Engines.LeftN1Percent;
            Engine2N1 = snapshot.Engines.RightN1Percent;
            LeftFuelFlowKgPerHour = snapshot.Engines.LeftFuelFlowKgPerHour;
            RightFuelFlowKgPerHour = snapshot.Engines.RightFuelFlowKgPerHour;
            FuelFlowKgPerHour = snapshot.Engines.TotalFuelFlowKgPerHour;
            LateralMode = snapshot.Autopilot.LateralMode.ToString().ToUpperInvariant();
            VerticalMode = snapshot.Autopilot.VerticalMode.ToString().ToUpperInvariant();
            FlapsHandleIndex = snapshot.Controls.FlapsHandleIndex;
            SpoilersPercent = snapshot.Controls.SpoilersPercent;
            LandingConfigurationReady = snapshot.LandingConfiguration.IsLandingReady;
            AutobrakeMode = snapshot.LandingConfiguration.AutobrakeMode switch
            {
                FenixFpm.Core.Models.AutobrakeMode.Off => "OFF",
                FenixFpm.Core.Models.AutobrakeMode.Low => "LO",
                FenixFpm.Core.Models.AutobrakeMode.Medium => "MED",
                FenixFpm.Core.Models.AutobrakeMode.Max => "MAX",
                _ => "OFF"
            };

            if (_lastPitchSampleTimestampUtc.HasValue)
            {
                var elapsedSeconds = (snapshot.TimestampUtc - _lastPitchSampleTimestampUtc.Value).TotalSeconds;
                RotationRateDegreesPerSecond = elapsedSeconds > 0.0
                    ? (snapshot.PitchDegrees - _lastPitchSampleDegrees) / elapsedSeconds
                    : 0.0;
            }
            else
            {
                RotationRateDegreesPerSecond = 0.0;
            }

            _lastPitchSampleDegrees = snapshot.PitchDegrees;
            _lastPitchSampleTimestampUtc = snapshot.TimestampUtc;

            ParkingBrakeState = snapshot.OnGround && snapshot.GroundSpeedKnots < 1.0
                ? "SET"
                : "RELEASED";
        });
    }

    private void OnEventDetected(object? sender, FlightEvent evt)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            RecentEvents.Insert(0, evt);
            if (RecentEvents.Count > 50)
            {
                RecentEvents.RemoveAt(RecentEvents.Count - 1);
            }

            if (evt.Severity == EventSeverity.Warning)
            {
                WarningCount++;
            }
            else if (evt.Severity == EventSeverity.Advisory)
            {
                AdvisoryCount++;
            }
        });
    }

    private void OnPhaseChanged(object? sender, FlightPhase phase)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            FlightPhase = phase.ToString().ToUpperInvariant();
        });
    }

    [RelayCommand]
    private void ClearEvents()
    {
        RecentEvents.Clear();
        WarningCount = 0;
        AdvisoryCount = 0;
    }
}


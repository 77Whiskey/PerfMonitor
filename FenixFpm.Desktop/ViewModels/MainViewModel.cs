using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FenixFpm.Core.Abstractions;

namespace FenixFpm.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ISimConnectService _simConnectService;
    private readonly PreflightViewModel _preflightViewModel;
    private readonly TakeoffViewModel _takeoffViewModel;
    private readonly ClimbCruiseViewModel _climbCruiseViewModel;
    private readonly ApproachViewModel _approachViewModel;
    private readonly LandingViewModel _landingViewModel;
    private readonly DebriefViewModel _debriefViewModel;

    [ObservableProperty]
    private string _currentView = "Preflight & Taxi";

    [ObservableProperty]
    private object? _currentViewModel;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatus = "Disconnected";

    public ActiveFlightViewModel ActiveFlight { get; }

    public MainViewModel(
        ActiveFlightViewModel activeFlight,
        ISimConnectService simConnectService,
        PreflightViewModel preflightViewModel,
        TakeoffViewModel takeoffViewModel,
        ClimbCruiseViewModel climbCruiseViewModel,
        ApproachViewModel approachViewModel,
        LandingViewModel landingViewModel,
        DebriefViewModel debriefViewModel)
    {
        ActiveFlight = activeFlight;
        _simConnectService = simConnectService;
        _preflightViewModel = preflightViewModel;
        _takeoffViewModel = takeoffViewModel;
        _climbCruiseViewModel = climbCruiseViewModel;
        _approachViewModel = approachViewModel;
        _landingViewModel = landingViewModel;
        _debriefViewModel = debriefViewModel;

        IsConnected = _simConnectService.IsConnected;
        ConnectionStatus = _simConnectService.ConnectionStatus;
        _simConnectService.ConnectionStateChanged += OnConnectionStateChanged;

        ShowView("Preflight & Taxi", _preflightViewModel);
    }

    [RelayCommand]
    private void NavigateToPreflight() => ShowView("Preflight & Taxi", _preflightViewModel);

    [RelayCommand]
    private void NavigateToTakeoff() => ShowView("Takeoff", _takeoffViewModel);

    [RelayCommand]
    private void NavigateToClimbCruise() => ShowView("Climb & Cruise", _climbCruiseViewModel);

    [RelayCommand]
    private void NavigateToApproach() => ShowView("Approach", _approachViewModel);

    [RelayCommand]
    private void NavigateToLanding() => ShowView("Landing & Rollout", _landingViewModel);

    [RelayCommand]
    private async Task NavigateToDebriefAsync()
    {
        await _debriefViewModel.LoadLatestSessionAsync();
        ShowView("Post-Flight Debrief", _debriefViewModel);
    }

    [RelayCommand]
    private void ConnectToSim()
    {
        _simConnectService.ForceConnect();
    }

    private void OnConnectionStateChanged()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsConnected = _simConnectService.IsConnected;
            ConnectionStatus = _simConnectService.ConnectionStatus;
        });
    }

    private void ShowView(string title, object viewModel)
    {
        CurrentView = title;
        CurrentViewModel = viewModel;
    }
}

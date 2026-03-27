using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FenixFpm.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _currentView = "Preflight & Taxi";

    [ObservableProperty]
    private object? _currentViewModel;

    public ActiveFlightViewModel ActiveFlight { get; }

    private readonly PreflightViewModel _preflightViewModel;
    private readonly TakeoffViewModel _takeoffViewModel;
    private readonly ClimbCruiseViewModel _climbCruiseViewModel;
    private readonly ApproachViewModel _approachViewModel;
    private readonly LandingViewModel _landingViewModel;
    private readonly DebriefViewModel _debriefViewModel;

    public MainViewModel(
        ActiveFlightViewModel activeFlight,
        PreflightViewModel preflightViewModel,
        TakeoffViewModel takeoffViewModel,
        ClimbCruiseViewModel climbCruiseViewModel,
        ApproachViewModel approachViewModel,
        LandingViewModel landingViewModel,
        DebriefViewModel debriefViewModel)
    {
        ActiveFlight = activeFlight;
        _preflightViewModel = preflightViewModel;
        _takeoffViewModel = takeoffViewModel;
        _climbCruiseViewModel = climbCruiseViewModel;
        _approachViewModel = approachViewModel;
        _landingViewModel = landingViewModel;
        _debriefViewModel = debriefViewModel;

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

    private void ShowView(string title, object viewModel)
    {
        CurrentView = title;
        CurrentViewModel = viewModel;
    }
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace FenixFpm.Desktop.ViewModels;

public partial class ClimbCruiseViewModel : ObservableObject
{
    public ActiveFlightViewModel ActiveFlight { get; }

    public ClimbCruiseViewModel(ActiveFlightViewModel activeFlight)
    {
        ActiveFlight = activeFlight;
    }
}

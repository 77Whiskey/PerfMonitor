using CommunityToolkit.Mvvm.ComponentModel;

namespace FenixFpm.Desktop.ViewModels;

public partial class PreflightViewModel : ObservableObject
{
    public ActiveFlightViewModel ActiveFlight { get; }

    public PreflightViewModel(ActiveFlightViewModel activeFlight)
    {
        ActiveFlight = activeFlight;
    }
}

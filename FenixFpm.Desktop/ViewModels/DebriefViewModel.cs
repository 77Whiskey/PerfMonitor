using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FenixFpm.Core.Abstractions;
using FenixFpm.Core.Models;
using FenixFpm.Core.Services;

namespace FenixFpm.Desktop.ViewModels;

public partial class DebriefViewModel : ObservableObject
{
    private readonly IDebriefService _debriefService;
    private readonly AnalyticsService _analyticsService;

    [ObservableProperty]
    private double _overallScore;

    [ObservableProperty]
    private TimeSpan _flightDuration;

    [ObservableProperty]
    private int _totalWarnings;

    [ObservableProperty]
    private string _selectedFlightLabel = "No completed flight loaded.";

    [ObservableProperty]
    private bool _hasSession;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<FlightEvent> ExceedancesLog { get; } = [];

    public string ScoreBand => OverallScore switch
    {
        > 80.0 => "Green",
        >= 60.0 => "Yellow",
        _ => "Red"
    };

    public DebriefViewModel(IDebriefService debriefService, AnalyticsService analyticsService)
    {
        _debriefService = debriefService;
        _analyticsService = analyticsService;
    }

    public async Task LoadLatestSessionAsync(CancellationToken cancellationToken = default)
    {
        var latestSession = await _debriefService.GetLatestFlightAsync(cancellationToken);
        if (latestSession is null)
        {
            Reset();
            return;
        }

        await LoadSessionAsync(latestSession.Id, cancellationToken);
    }

    public async Task LoadSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            Reset();
            return;
        }

        IsLoading = true;
        try
        {
            var session = (await _debriefService.GetRecentFlightsAsync(100, cancellationToken))
                .FirstOrDefault(x => x.Id == sessionId);

            if (session is null)
            {
                Reset();
                return;
            }

            var events = (await _debriefService.GetFlightEventsAsync(session.Id, cancellationToken))
                .OrderByDescending(x => x.Timestamp)
                .ToList();

            var metrics = await _debriefService.GetFlightMetricsAsync(session.Id, cancellationToken);
            var scorecard = metrics is null
                ? await _debriefService.GetFlightScorecardAsync(session.Id, cancellationToken)
                : null;

            OverallScore = Math.Round(
                session.OverallScore ??
                metrics?.OverallScore ??
                scorecard?.OverallScore ??
                0.0,
                1,
                MidpointRounding.AwayFromZero);

            FlightDuration = session.EndedAtUtc.HasValue
                ? session.EndedAtUtc.Value - session.StartedAtUtc
                : TimeSpan.Zero;

            TotalWarnings = events.Count(x => x.Severity == EventSeverity.Warning);
            SelectedFlightLabel = $"{session.AircraftType} {session.StartedAtUtc:dd MMM yyyy HH:mm} UTC";
            HasSession = true;

            ExceedancesLog.Clear();
            foreach (var flightEvent in events)
            {
                ExceedancesLog.Add(flightEvent);
            }

            OnPropertyChanged(nameof(ScoreBand));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Reset()
    {
        OverallScore = 0.0;
        FlightDuration = TimeSpan.Zero;
        TotalWarnings = 0;
        SelectedFlightLabel = "No completed flight loaded.";
        HasSession = false;
        ExceedancesLog.Clear();
        OnPropertyChanged(nameof(ScoreBand));
    }
}

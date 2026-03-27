using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace FenixFpm.Desktop.ViewModels;

public partial class TakeoffViewModel : ObservableObject
{
    private const int WindowSeconds = 45;
    private readonly ObservableCollection<double> _pitchSamples = [];
    private DateTimeOffset _lastSampleAtUtc = DateTimeOffset.MinValue;

    public ActiveFlightViewModel ActiveFlight { get; }

    public ObservableCollection<ISeries> PitchSeries { get; }

    public Axis[] XAxes { get; }

    public Axis[] YAxes { get; }

    public TakeoffViewModel(ActiveFlightViewModel activeFlight)
    {
        ActiveFlight = activeFlight;

        PitchSeries =
        [
            new LineSeries<double>
            {
                Name = "Pitch",
                Values = _pitchSamples,
                GeometrySize = 0,
                LineSmoothness = 0.25,
                Fill = null,
                Stroke = new SolidColorPaint(new SKColor(0, 168, 255), 3)
            }
        ];

        XAxes =
        [
            new Axis
            {
                MinLimit = 0,
                MaxLimit = WindowSeconds - 1,
                MinStep = 5,
                Labeler = value => $"-{Math.Max(0, WindowSeconds - 1 - value):0}s",
                LabelsPaint = new SolidColorPaint(new SKColor(150, 160, 170)),
                SeparatorsPaint = new SolidColorPaint(new SKColor(40, 40, 40)),
                TextSize = 11,
                ForceStepToMin = true,
                ShowSeparatorLines = true
            }
        ];

        YAxes =
        [
            new Axis
            {
                MinLimit = -2,
                MaxLimit = 18,
                Labeler = value => $"{value:0}",
                Name = "Pitch (deg)",
                NamePaint = new SolidColorPaint(new SKColor(150, 160, 170)),
                LabelsPaint = new SolidColorPaint(new SKColor(150, 160, 170)),
                SeparatorsPaint = new SolidColorPaint(new SKColor(40, 40, 40)),
                TextSize = 11,
                ShowSeparatorLines = true
            }
        ];

        ActiveFlight.PropertyChanged += OnActiveFlightPropertyChanged;
    }

    private void OnActiveFlightPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ActiveFlightViewModel.PitchDegrees))
        {
            return;
        }

        if (ActiveFlight.IsOnGround && ActiveFlight.IndicatedAirspeedKnots < 30.0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (now - _lastSampleAtUtc < TimeSpan.FromSeconds(1))
        {
            return;
        }

        _lastSampleAtUtc = now;
        PushPitchSample(ActiveFlight.PitchDegrees);
    }

    private void PushPitchSample(double pitchDegrees)
    {
        if (_pitchSamples.Count >= WindowSeconds)
        {
            _pitchSamples.RemoveAt(0);
        }

        _pitchSamples.Add(pitchDegrees);
    }
}

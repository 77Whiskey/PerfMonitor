using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace FenixFpm.Desktop.ViewModels;

public partial class ApproachViewModel : ObservableObject
{
    private const int WindowSeconds = 60;
    private readonly ObservableCollection<double> _sinkRateValues = [];
    private DateTimeOffset _lastSampleAtUtc = DateTimeOffset.MinValue;

    public ActiveFlightViewModel ActiveFlight { get; }

    public ObservableCollection<ISeries> SinkRateSeries { get; }

    public Axis[] XAxes { get; }

    public Axis[] YAxes { get; }

    public ApproachViewModel(ActiveFlightViewModel activeFlight)
    {
        ActiveFlight = activeFlight;

        SinkRateSeries =
        [
            new LineSeries<double>
            {
                Name = "Sink Rate",
                Values = _sinkRateValues,
                GeometrySize = 0,
                LineSmoothness = 0.35,
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
                MinStep = 10,
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
                MinLimit = -2500,
                MaxLimit = 500,
                Labeler = value => $"{value:0}",
                Name = "FPM",
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
        if (e.PropertyName != nameof(ActiveFlightViewModel.VerticalSpeedFpm))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (now - _lastSampleAtUtc < TimeSpan.FromSeconds(1))
        {
            return;
        }

        _lastSampleAtUtc = now;
        PushSinkRateSample(ActiveFlight.VerticalSpeedFpm);
    }

    private void PushSinkRateSample(double sinkRateFpm)
    {
        if (_sinkRateValues.Count >= WindowSeconds)
        {
            _sinkRateValues.RemoveAt(0);
        }

        _sinkRateValues.Add(sinkRateFpm);
    }
}

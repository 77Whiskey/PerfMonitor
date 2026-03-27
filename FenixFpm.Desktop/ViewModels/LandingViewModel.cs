using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace FenixFpm.Desktop.ViewModels;

public partial class LandingViewModel : ObservableObject
{
    private const int WindowSeconds = 90;
    private readonly ObservableCollection<double> _airspeedSamples = [];
    private DateTimeOffset _lastSampleAtUtc = DateTimeOffset.MinValue;

    public ActiveFlightViewModel ActiveFlight { get; }

    public ObservableCollection<ISeries> AirspeedSeries { get; }

    public Axis[] XAxes { get; }

    public Axis[] YAxes { get; }

    public LandingViewModel(ActiveFlightViewModel activeFlight)
    {
        ActiveFlight = activeFlight;

        AirspeedSeries =
        [
            new LineSeries<double>
            {
                Name = "Airspeed",
                Values = _airspeedSamples,
                GeometrySize = 0,
                LineSmoothness = 0.2,
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
                MinLimit = 0,
                MaxLimit = 170,
                Labeler = value => $"{value:0}",
                Name = "IAS (kt)",
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
        if (e.PropertyName != nameof(ActiveFlightViewModel.IndicatedAirspeedKnots))
        {
            return;
        }

        if (ActiveFlight.IsOnGround && ActiveFlight.IndicatedAirspeedKnots < 5.0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (now - _lastSampleAtUtc < TimeSpan.FromSeconds(1))
        {
            return;
        }

        _lastSampleAtUtc = now;
        PushAirspeedSample(ActiveFlight.IndicatedAirspeedKnots);
    }

    private void PushAirspeedSample(double airspeedKnots)
    {
        if (_airspeedSamples.Count >= WindowSeconds)
        {
            _airspeedSamples.RemoveAt(0);
        }

        _airspeedSamples.Add(airspeedKnots);
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using FenixFpm.Core.Abstractions;
using FenixFpm.Core.Models;

namespace FenixFpm.Core.Services;

public sealed class PerformanceDataService : IPerformanceDataService
{
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private PreparedDataset? _dataset;
    private A320PerformanceData? _a320Data;

    public A320PerformanceData? A320Data => _a320Data;

    public int BrakeTemperatureLimitC => _a320Data?.Dataset.Limitations.WeightAndLoading.BrakeTemperatureLimitC ?? 300;
    public int MaxTaxiTurnSpeedHeavyKt => _a320Data?.Dataset.Limitations.WeightAndLoading.MaxTaxiTurnSpeedHeavyKt ?? 20;
    public int MaxTaxiTurnWeightKg => _a320Data?.Dataset.Limitations.WeightAndLoading.MaxTaxiTurnWeightKg ?? 76000;
    public int MaxTireGroundSpeedKt => _a320Data?.Dataset.Limitations.SpeedAndEnvironment.MaxTireGroundSpeedKt ?? 195;
    public int VappMinCorrectionKt => _a320Data?.Dataset.FlightPhases.DescentAndApproach.VappCalculation.MinApprCorKt ?? 5;
    public int VappMaxCorrectionKt => _a320Data?.Dataset.FlightPhases.DescentAndApproach.VappCalculation.MaxApprCorKt ?? 15;
    public double MaxBankAngleDeg => _a320Data?.Dataset.FlightPhases.StabilizedApproach.PMExceedanceCallouts.BankDeg.Limit ?? 7;
    public int MaxSinkRateFpm => (int)(_a320Data?.Dataset.FlightPhases.StabilizedApproach.PMExceedanceCallouts.SinkRateFtMin.Limit ?? 1000);
    public int StabilizedApproachGateHeightAalFt => _a320Data?.Dataset.FlightPhases.StabilizedApproach.Gates.FirstOrDefault(g => g.Type == "IMC Gate")?.HeightAalFt ?? 1000;
    public int LandingGateHeightAalFt => _a320Data?.Dataset.FlightPhases.StabilizedApproach.Gates.FirstOrDefault(g => g.Type == "VMC Gate")?.HeightAalFt ?? 500;
    public int CirclingGateHeightAalFt => _a320Data?.Dataset.FlightPhases.StabilizedApproach.Gates.FirstOrDefault(g => g.Type == "Circling Gate")?.HeightAalFt ?? 500;

    public async Task LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (_dataset is not null && _a320Data is not null)
        {
            return;
        }

        await _loadGate.WaitAsync(cancellationToken);
        try
        {
            if (_dataset is not null && _a320Data is not null)
            {
                return;
            }

            if (!File.Exists(filePath))
            {
                return;
            }

            await using var stream = File.OpenRead(filePath);
            
            if (filePath.EndsWith("A320-data.json", StringComparison.OrdinalIgnoreCase))
            {
                _a320Data = await JsonSerializer.DeserializeAsync<A320PerformanceData>(stream, _jsonOptions, cancellationToken);
            }
            else
            {
                var rawDataset = await JsonSerializer.DeserializeAsync<PerformanceReferenceDataset>(stream, _jsonOptions, cancellationToken)
                    ?? throw new InvalidOperationException("Performance reference file is empty or invalid.");
                _dataset = Prepare(rawDataset);
            }
        }
        finally
        {
            _loadGate.Release();
        }
    }

    public async Task LoadA320DataAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (_a320Data is not null)
        {
            return;
        }

        await _loadGate.WaitAsync(cancellationToken);
        try
        {
            if (_a320Data is not null)
            {
                return;
            }

            if (!File.Exists(filePath))
            {
                return;
            }

            await using var stream = File.OpenRead(filePath);
            _a320Data = await JsonSerializer.DeserializeAsync<A320PerformanceData>(stream, _jsonOptions, cancellationToken);
        }
        finally
        {
            _loadGate.Release();
        }
    }

    public ValueTask<LandingDistanceResult> CalculateLandingDistanceAsync(
        LandingDistanceRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dataset = _dataset ?? throw new InvalidOperationException("Performance data has not been loaded.");

        if (!dataset.Tables.TryGetValue(request.RunwayCondition, out var table))
        {
            throw new KeyNotFoundException($"No landing performance table for runway condition '{request.RunwayCondition}'.");
        }

        return ValueTask.FromResult(Interpolate(table, request));
    }

    private static PreparedDataset Prepare(PerformanceReferenceDataset rawDataset)
    {
        var tables = new Dictionary<RunwayCondition, PreparedLandingTable>();
        foreach (var table in rawDataset.LandingTables)
        {
            var weights = table.WeightsKg.Order().ToArray();
            var temperatures = table.OutsideAirTemperaturesC.Order().ToArray();
            var pressureAltitudes = table.PressureAltitudesFeet.Order().ToArray();
            var samples = table.Points.ToDictionary(
                point => (point.WeightKg, point.OutsideAirTemperatureC, point.PressureAltitudeFeet),
                point => point.DistanceMeters);

            tables[table.RunwayCondition] = new PreparedLandingTable(weights, temperatures, pressureAltitudes, samples);
        }

        return new PreparedDataset(rawDataset.Aircraft, tables);
    }

    private static LandingDistanceResult Interpolate(PreparedLandingTable table, LandingDistanceRequest request)
    {
        var (w0, w1, weightClamped) = Bracket(request.WeightKg, table.WeightsKg);
        var (t0, t1, temperatureClamped) = Bracket(request.OutsideAirTemperatureC, table.OutsideAirTemperaturesC);
        var (p0, p1, altitudeClamped) = Bracket(request.PressureAltitudeFeet, table.PressureAltitudesFeet);

        var wf = Factor(request.WeightKg, w0, w1);
        var tf = Factor(request.OutsideAirTemperatureC, t0, t1);
        var pf = Factor(request.PressureAltitudeFeet, p0, p1);

        var c000 = table.Lookup(w0, t0, p0);
        var c100 = table.Lookup(w1, t0, p0);
        var c010 = table.Lookup(w0, t1, p0);
        var c110 = table.Lookup(w1, t1, p0);
        var c001 = table.Lookup(w0, t0, p1);
        var c101 = table.Lookup(w1, t0, p1);
        var c011 = table.Lookup(w0, t1, p1);
        var c111 = table.Lookup(w1, t1, p1);

        var c00 = Lerp(c000, c100, wf);
        var c10 = Lerp(c010, c110, wf);
        var c01 = Lerp(c001, c101, wf);
        var c11 = Lerp(c011, c111, wf);
        var c0 = Lerp(c00, c10, tf);
        var c1 = Lerp(c01, c11, tf);
        var distance = Lerp(c0, c1, pf);

        var bounds = new InterpolationBounds(w0, w1, t0, t1, p0, p1);
        return new LandingDistanceResult(distance, bounds, weightClamped || temperatureClamped || altitudeClamped);
    }

    private static (double Lower, double Upper, bool WasClamped) Bracket(double value, double[] axis)
    {
        if (value < axis[0])
        {
            return (axis[0], axis[0], true);
        }

        if (Math.Abs(value - axis[0]) < double.Epsilon)
        {
            return (axis[0], axis[0], false);
        }

        if (value > axis[^1])
        {
            return (axis[^1], axis[^1], true);
        }

        if (Math.Abs(value - axis[^1]) < double.Epsilon)
        {
            return (axis[^1], axis[^1], false);
        }

        for (var index = 0; index < axis.Length - 1; index++)
        {
            if (value >= axis[index] && value <= axis[index + 1])
            {
                return (axis[index], axis[index + 1], false);
            }
        }

        throw new InvalidOperationException("Unable to locate interpolation bracket.");
    }

    private static double Factor(double value, double lower, double upper)
    {
        if (Math.Abs(upper - lower) < double.Epsilon)
        {
            return 0.0;
        }

        return (value - lower) / (upper - lower);
    }

    private static double Lerp(double a, double b, double factor)
    {
        return a + ((b - a) * factor);
    }

    private sealed record PerformanceReferenceDataset(string Aircraft, IReadOnlyList<LandingDistanceTable> LandingTables);

    private sealed record LandingDistanceTable(
        RunwayCondition RunwayCondition,
        IReadOnlyList<double> WeightsKg,
        IReadOnlyList<double> OutsideAirTemperaturesC,
        IReadOnlyList<double> PressureAltitudesFeet,
        IReadOnlyList<LandingDistancePoint> Points);

    private sealed record LandingDistancePoint(
        double WeightKg,
        double OutsideAirTemperatureC,
        double PressureAltitudeFeet,
        double DistanceMeters);

    private sealed record PreparedDataset(string Aircraft, IReadOnlyDictionary<RunwayCondition, PreparedLandingTable> Tables);

    private sealed class PreparedLandingTable
    {
        private readonly IReadOnlyDictionary<(double WeightKg, double OatC, double PressureAltitudeFeet), double> _samples;

        public PreparedLandingTable(
            double[] weightsKg,
            double[] outsideAirTemperaturesC,
            double[] pressureAltitudesFeet,
            IReadOnlyDictionary<(double WeightKg, double OatC, double PressureAltitudeFeet), double> samples)
        {
            WeightsKg = weightsKg;
            OutsideAirTemperaturesC = outsideAirTemperaturesC;
            PressureAltitudesFeet = pressureAltitudesFeet;
            _samples = samples;
        }

        public double[] WeightsKg { get; }

        public double[] OutsideAirTemperaturesC { get; }

        public double[] PressureAltitudesFeet { get; }

        public double Lookup(double weightKg, double oatC, double pressureAltitudeFeet)
        {
            if (_samples.TryGetValue((weightKg, oatC, pressureAltitudeFeet), out var distance))
            {
                return distance;
            }

            throw new KeyNotFoundException(
                $"Missing interpolation sample for weight={weightKg}, oat={oatC}, pressureAltitude={pressureAltitudeFeet}.");
        }
    }
}
using FenixFpm.Core.Models;

namespace FenixFpm.Core.Abstractions;

public interface IPerformanceDataService
{
    Task LoadAsync(string filePath, CancellationToken cancellationToken = default);

    ValueTask<LandingDistanceResult> CalculateLandingDistanceAsync(
        LandingDistanceRequest request,
        CancellationToken cancellationToken = default);

    A320PerformanceData? A320Data { get; }

    int BrakeTemperatureLimitC { get; }
    int MaxTaxiTurnSpeedHeavyKt { get; }
    int MaxTaxiTurnWeightKg { get; }
    int MaxTireGroundSpeedKt { get; }
    int VappMinCorrectionKt { get; }
    int VappMaxCorrectionKt { get; }
    double MaxBankAngleDeg { get; }
    int MaxSinkRateFpm { get; }
    int StabilizedApproachGateHeightAalFt { get; }
    int LandingGateHeightAalFt { get; }
    int CirclingGateHeightAalFt { get; }
}
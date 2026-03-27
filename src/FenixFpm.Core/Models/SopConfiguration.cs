using System.Text.Json.Serialization;

namespace FenixFpm.Core.Models;

public record SopConfiguration
{
    public static SopConfiguration Default { get; } = new()
    {
        StabilizedApproachGates = new StabilizedApproachGateSet
        {
            Imc1000FtAal = CreateGate(1000.0),
            Vmc500FtAal = CreateGate(500.0)
        }
    };

    [JsonPropertyName("Stabilized_Approach_Gates")]
    public StabilizedApproachGateSet? StabilizedApproachGates { get; init; }

    private static SopApproachGate CreateGate(double heightFeet)
    {
        return new SopApproachGate
        {
            GateHeightFtAal = heightFeet,
            Requirements = new SopGateRequirements
            {
                SpeedToleranceKt = new[] { -5.0, 10.0 },
                VsLimitFpm = -1000.0,
                PitchMinDeg = -2.5,
                PitchMaxDeg = 10.0,
                BankMaxDeg = 7.0
            }
        };
    }
}

public record StabilizedApproachGateSet
{
    [JsonPropertyName("IMC_1000ft_AAL")]
    public SopApproachGate? Imc1000FtAal { get; init; }

    [JsonPropertyName("VMC_500ft_AAL")]
    public SopApproachGate? Vmc500FtAal { get; init; }
}

public record SopApproachGate
{
    [JsonPropertyName("Gate_Height_ft_AAL")]
    public double GateHeightFtAal { get; init; }

    [JsonPropertyName("Requirements")]
    public SopGateRequirements? Requirements { get; init; }
}

public record SopGateRequirements
{
    [JsonPropertyName("Speed_Tolerance_kt")]
    public double[]? SpeedToleranceKt { get; init; }

    [JsonPropertyName("VS_Limit_fpm")]
    public double VsLimitFpm { get; init; }

    [JsonPropertyName("Pitch_Min_deg")]
    public double PitchMinDeg { get; init; }

    [JsonPropertyName("Pitch_Max_deg")]
    public double PitchMaxDeg { get; init; }

    [JsonPropertyName("Bank_Max_deg")]
    public double BankMaxDeg { get; init; }
}

using System.Text.Json.Serialization;

namespace FenixFpm.Core.Models;

public sealed record A320PerformanceData(
    [property: JsonPropertyName("A320_Performance_Monitoring_Dataset")]
    A320Dataset Dataset);

public sealed record A320Dataset(
    [property: JsonPropertyName("Metadata")]
    Metadata Metadata,
    
    [property: JsonPropertyName("Limitations_and_Structural_Boundaries")]
    Limitations Limitations,
    
    [property: JsonPropertyName("Operational_Philosophy")]
    OperationalPhilosophy OperationalPhilosophy,
    
    [property: JsonPropertyName("FMGS_and_Data_Entry")]
    FmgsData FmgsData,
    
    [property: JsonPropertyName("Flight_Phases_and_Performance")]
    FlightPhases FlightPhases,
    
    [property: JsonPropertyName("Abnormal_and_Emergency_Operations")]
    AbnormalOperations AbnormalOperations,
    
    [property: JsonPropertyName("AOC_and_Data_Uplink")]
    AocData AocData);

public sealed record Metadata(
    [property: JsonPropertyName("Aircraft_Model")]
    string AircraftModel,
    
    [property: JsonPropertyName("MSN")]
    string Msn,
    
    [property: JsonPropertyName("Engine_Type")]
    string EngineType,
    
    [property: JsonPropertyName("Measurement_Units")]
    MeasurementUnits MeasurementUnits,
    
    [property: JsonPropertyName("Performance_Database_References")]
    List<string> PerformanceDatabaseReferences);

public sealed record MeasurementUnits(
    [property: JsonPropertyName("Weight")]
    string Weight,
    
    [property: JsonPropertyName("Distance")]
    string Distance,
    
    [property: JsonPropertyName("Temperature")]
    string Temperature);

public sealed record Limitations(
    [property: JsonPropertyName("Weight_and_Loading")]
    WeightLimitations WeightAndLoading,
    
    [property: JsonPropertyName("Speed_and_Environment")]
    SpeedLimitations SpeedAndEnvironment);

public sealed record WeightLimitations(
    [property: JsonPropertyName("MTOW")]
    string MTOW,
    
    [property: JsonPropertyName("MLW")]
    string MLW,
    
    [property: JsonPropertyName("Max_Taxi_Turn_Weight_Threshold_kg")]
    int MaxTaxiTurnWeightKg,
    
    [property: JsonPropertyName("Max_Taxi_Turn_Speed_Heavy_kt")]
    int MaxTaxiTurnSpeedHeavyKt,
    
    [property: JsonPropertyName("Brake_Temperature_Limit_C")]
    int BrakeTemperatureLimitC);

public sealed record SpeedLimitations(
    [property: JsonPropertyName("Max_Tire_Ground_Speed_kt")]
    int MaxTireGroundSpeedKt,
    
    [property: JsonPropertyName("IRS_Alignment_Latitude_Limit_Basic_deg")]
    double IrsAlignmentLatitudeLimitBasicDeg,
    
    [property: JsonPropertyName("IRS_Alignment_Latitude_Limit_Modified_deg")]
    double IrsAlignmentLatitudeLimitModifiedDeg,
    
    [property: JsonPropertyName("IRS_Magnetic_Heading_Limit_North_deg")]
    double IrsMagneticHeadingLimitNorthDeg,
    
    [property: JsonPropertyName("IRS_Magnetic_Heading_Limit_South_deg")]
    double IrsMagneticHeadingLimitSouthDeg);

public sealed record OperationalPhilosophy(
    [property: JsonPropertyName("Golden_Rules_Hierarchy")]
    List<string> GoldenRulesHierarchy,
    
    [property: JsonPropertyName("Tasksharing")]
    TaskSharing Tasksharing,
    
    [property: JsonPropertyName("Briefing_Structure")]
    BriefingStructure BriefingStructure);

public sealed record TaskSharing(
    [property: JsonPropertyName("Pilot_Flying_PF")]
    List<string> PilotFlying,
    
    [property: JsonPropertyName("Pilot_Monitoring_PM")]
    List<string> PilotMonitoring);

public sealed record BriefingStructure(
    [property: JsonPropertyName("Phases")]
    List<string> Phases);

public sealed record FmgsData(
    [property: JsonPropertyName("MCDU_Data_Formats")]
    McduFormats McduDataFormats,
    
    [property: JsonPropertyName("MCDU_Color_Coding")]
    McduColorCoding McduColorCoding);

public sealed record McduFormats(
    [property: JsonPropertyName("Cost_Index")]
    RangeFormat CostIndex,
    
    [property: JsonPropertyName("Cruise_Flight_Level")]
    CruiseFlightLevelFormat CruiseFlightLevel,
    
    [property: JsonPropertyName("Managed_Speed_CAS")]
    RangeFormat ManagedSpeedCas,
    
    [property: JsonPropertyName("THS_Trim")]
    ThsTrimLimit ThsTrim);

public sealed record RangeFormat(
    [property: JsonPropertyName("Range_Min")]
    double RangeMin,
    
    [property: JsonPropertyName("Range_Max")]
    double RangeMax,
    
    [property: JsonPropertyName("Units")]
    string? Units = null);

public sealed record CruiseFlightLevelFormat(
    [property: JsonPropertyName("Format")]
    string Format,
    
    [property: JsonPropertyName("Max_Value")]
    List<int> MaxValue);

public sealed record ThsTrimLimit(
    [property: JsonPropertyName("Limit_UP_deg")]
    double LimitUpDeg,
    
    [property: JsonPropertyName("Limit_DN_deg")]
    double LimitDnDeg);

public sealed record McduColorCoding(
    [property: JsonPropertyName("Boxed")]
    string Boxed,
    
    [property: JsonPropertyName("Blue")]
    string Blue,
    
    [property: JsonPropertyName("Green")]
    string Green);

public sealed record FlightPhases(
    [property: JsonPropertyName("Takeoff")]
    PhaseData Takeoff,
    
    [property: JsonPropertyName("Climb_and_Cruise")]
    ClimbCruiseData ClimbAndCruise,
    
    [property: JsonPropertyName("Descent_and_Approach")]
    DescentApproachData DescentAndApproach,
    
    [property: JsonPropertyName("Stabilized_Approach_Criteria")]
    StabilizedApproachCriteria StabilizedApproach,
    
    [property: JsonPropertyName("Landing_and_Runway_Performance")]
    LandingData LandingAndRunway);

public sealed record PhaseData(
    [property: JsonPropertyName("Thrust")]
    ThrustData? Thrust = null,
    
    [property: JsonPropertyName("Acceleration_Altitudes")]
    object? AccelerationAltitudes = null);

public sealed record ThrustData(
    [property: JsonPropertyName("Allowed_Modes")]
    List<string> AllowedModes,
    
    [property: JsonPropertyName("Contaminated_Runway_Restriction")]
    string ContaminatedRunwayRestriction);

public sealed record ClimbCruiseData(
    [property: JsonPropertyName("Speed_Selection_Logic")]
    object SpeedSelectionLogic,
    
    [property: JsonPropertyName("Cruise_Monitoring")]
    object CruiseMonitoring);

public sealed record DescentApproachData(
    [property: JsonPropertyName("Descent_Profile_Optimization_DPO")]
    object DescentProfileOptimization,
    
    [property: JsonPropertyName("VAPP_Calculation")]
    VappCalculation VappCalculation);

public sealed record VappCalculation(
    [property: JsonPropertyName("Base_Formula")]
    string BaseFormula,
    
    [property: JsonPropertyName("Min_APPR_COR_kt")]
    int MinApprCorKt,
    
    [property: JsonPropertyName("Max_APPR_COR_kt")]
    int MaxApprCorKt);

public sealed record StabilizedApproachCriteria(
    [property: JsonPropertyName("Gates")]
    List<ApproachGate> Gates,
    
    [property: JsonPropertyName("Mandatory_Criteria")]
    List<string> MandatoryCriteria,
    
    [property: JsonPropertyName("PM_Exceedance_Callouts")]
    ExceedanceCallouts PMExceedanceCallouts);

public sealed record ApproachGate(
    [property: JsonPropertyName("Type")]
    string Type,
    
    [property: JsonPropertyName("Height_AAL_ft")]
    int HeightAalFt,
    
    [property: JsonPropertyName("Status")]
    string Status);

public sealed record ExceedanceCallouts(
    [property: JsonPropertyName("Speed_kt")]
    ExceedanceLimit SpeedKt,
    
    [property: JsonPropertyName("Pitch_deg")]
    ExceedanceLimit PitchDeg,
    
    [property: JsonPropertyName("Bank_deg")]
    ExceedanceLimit BankDeg,
    
    [property: JsonPropertyName("Sink_Rate_ft_min")]
    ExceedanceLimit SinkRateFtMin);

public sealed record ExceedanceLimit(
    [property: JsonPropertyName("Upper_Limit")]
    double? UpperLimit = null,
    
    [property: JsonPropertyName("Lower_Limit")]
    double? LowerLimit = null,
    
    [property: JsonPropertyName("Limit")]
    double? Limit = null,
    
    [property: JsonPropertyName("Callout")]
    string? Callout = null);

public sealed record LandingData(
    [property: JsonPropertyName("Factored_Landing_Distance_FLD")]
    object FactoredLandingDistance,
    
    [property: JsonPropertyName("Autoland_Distance_Increments")]
    object AutolandDistanceIncrements,
    
    [property: JsonPropertyName("Tailwind_Restrictions")]
    TailwindRestrictions TailwindRestrictions);

public sealed record TailwindRestrictions(
    [property: JsonPropertyName("Limit_kt")]
    int LimitKt,
    
    [property: JsonPropertyName("Operational_Constraint")]
    string OperationalConstraint);

public sealed record AbnormalOperations(
    [property: JsonPropertyName("Unreliable_Speed_Indication")]
    object UnreliableSpeedIndication,
    
    [property: JsonPropertyName("Dual_Engine_Failure_Glide")]
    object DualEngineFailureGlide);

public sealed record AocData(
    [property: JsonPropertyName("Uplink_Validation")]
    UplinkValidation UplinkValidation,
    
    [property: JsonPropertyName("Wind_Uplink")]
    object WindUplink,
    
    [property: JsonPropertyName("Rejected_Takeoff_RTO_Logic")]
    object RejectedTakeoffRtoLogic);

public sealed record UplinkValidation(
    [property: JsonPropertyName("TOW_Discrepancy_Upper_Limit_t")]
    double TowDiscrepancyUpperLimitT,
    
    [property: JsonPropertyName("TOW_Discrepancy_Lower_Limit_t")]
    double TowDiscrepancyLowerLimitT,
    
    [property: JsonPropertyName("Logic")]
    string Logic);
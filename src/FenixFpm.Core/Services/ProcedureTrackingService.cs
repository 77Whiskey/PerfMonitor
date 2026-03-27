using FenixFpm.Core.Models;

namespace FenixFpm.Core.Services;

public interface IProcedureTrackingService
{
    IReadOnlyList<ProcedureCheckResult> EvaluateProcedures(
        IEnumerable<FlightSnapshot> snapshots,
        IEnumerable<FlightEvent> events,
        FlightPhase currentPhase);
    
    void Reset();
}

public sealed class ProcedureTrackingService : IProcedureTrackingService
{
    private bool _preFlightChecked;
    private bool _beforeTakeoffChecked;
    private bool _approachBriefingChecked;
    private bool _landingChecklistChecked;
    private bool _afterLandingChecked;
    private bool _engineStartChecked;
    private bool _engineShutdownChecked;
    private DateTimeOffset? _preFlightTime;
    private DateTimeOffset? _beforeTakeoffTime;
    private DateTimeOffset? _approachBriefingTime;
    private DateTimeOffset? _landingChecklistTime;
    private DateTimeOffset? _afterLandingTime;

    public IReadOnlyList<ProcedureCheckResult> EvaluateProcedures(
        IEnumerable<FlightSnapshot> snapshots,
        IEnumerable<FlightEvent> events,
        FlightPhase currentPhase)
    {
        var snapshotList = snapshots.ToList();
        var eventList = events.ToList();
        var results = new List<ProcedureCheckResult>();

        var groundEvents = eventList.Where(e => e.EventType.Contains("Configuration") && e.Severity != EventSeverity.Information).ToList();

        var preFlightComplete = snapshotList.Count >= 20 && groundEvents.Any(e => e.EventType.Contains("Configuration"));
        results.Add(new ProcedureCheckResult(
            ProcedureType.PreFlightChecklist,
            preFlightComplete,
            preFlightComplete ? DateTimeOffset.UtcNow : null,
            true,
            null));

        if (currentPhase == FlightPhase.Climb && snapshotList.Count > 50)
        {
            var takeoffs = snapshotList.Where(s => s.RadioAltitudeFeet < 100).Take(30).ToList();
            if (takeoffs.Count > 10)
            {
                _beforeTakeoffChecked = true;
                _beforeTakeoffTime = takeoffs.LastOrDefault()?.TimestampUtc;
            }
        }

        results.Add(new ProcedureCheckResult(
            ProcedureType.BeforeTakeoffChecklist,
            _beforeTakeoffChecked,
            _beforeTakeoffTime,
            true,
            null));

        if (currentPhase == FlightPhase.Approach || currentPhase == FlightPhase.Descent)
        {
            var approachSnapshots = snapshotList.Where(s => s.BaroAltitudeFeet < 10000 && s.BaroAltitudeFeet > 5000).ToList();
            if (approachSnapshots.Any(s => s.Autopilot.ApproachModeActive))
            {
                _approachBriefingChecked = true;
                _approachBriefingTime = approachSnapshots.FirstOrDefault(s => s.Autopilot.ApproachModeActive)?.TimestampUtc;
            }
        }

        results.Add(new ProcedureCheckResult(
            ProcedureType.ApproachBriefing,
            _approachBriefingChecked,
            _approachBriefingTime,
            true,
            null));

        if (currentPhase == FlightPhase.Approach)
        {
            var approachSnapshots = snapshotList.Where(s => s.RadioAltitudeFeet < 1500 && s.RadioAltitudeFeet > 500).ToList();
            if (approachSnapshots.Any(s => s.LandingConfiguration.IsLandingReady))
            {
                _landingChecklistChecked = true;
                _landingChecklistTime = approachSnapshots.FirstOrDefault(s => s.LandingConfiguration.IsLandingReady)?.TimestampUtc;
            }
        }

        results.Add(new ProcedureCheckResult(
            ProcedureType.LandingChecklist,
            _landingChecklistChecked,
            _landingChecklistTime,
            true,
            null));

        if (currentPhase == FlightPhase.Ground && snapshotList.LastOrDefault()?.OnGround == true)
        {
            _afterLandingChecked = true;
            _afterLandingTime = snapshotList.LastOrDefault()?.TimestampUtc;
        }

        results.Add(new ProcedureCheckResult(
            ProcedureType.AfterLandingChecklist,
            _afterLandingChecked,
            _afterLandingTime,
            true,
            null));

        return results;
    }

    public void Reset()
    {
        _preFlightChecked = false;
        _beforeTakeoffChecked = false;
        _approachBriefingChecked = false;
        _landingChecklistChecked = false;
        _afterLandingChecked = false;
        _engineStartChecked = false;
        _engineShutdownChecked = false;
        _preFlightTime = null;
        _beforeTakeoffTime = null;
        _approachBriefingTime = null;
        _landingChecklistTime = null;
        _afterLandingTime = null;
    }
}
using FenixFpm.Core.Models;

namespace FenixFpm.Core.Services;

public enum FlightState
{
    OnGround,
    TaxiOut,
    Takeoff,
    Climb,
    Cruise,
    Descent,
    Approach,
    Landing,
    TaxiIn,
    Parked
}

public interface IFlightSessionService
{
    FlightPhase DetectPhase(FlightSnapshot snapshot, FlightPhase currentPhase);
    FlightState DetectState(FlightSnapshot snapshot, FlightState currentState);
    FlightSession CreateSession(string? tailNumber = null, string? flightNumber = null, string? pilotId = null);
    FlightSession? CompleteSession(FlightSession session, FlightSnapshot finalSnapshot);
    void UpdateSessionMetrics(FlightSession session, FlightSnapshot snapshot, IEnumerable<FlightEvent> events);
    bool IsFlightStart(FlightSnapshot snapshot, FlightState previousState);
    bool IsFlightEnd(FlightSnapshot snapshot, FlightState previousState);
    TimeSpan CalculateFlightDuration(FlightSession session);
}

public sealed class FlightSessionService : IFlightSessionService
{
    private const double TakeoffAltitudeThreshold = 50.0;
    private const double ClimbAltitudeThreshold = 2000.0;
    private const double CruiseAltitudeThreshold = 25000.0;
    private const double DescentAltitudeThreshold = 15000.0;
    private const double ApproachAltitudeThreshold = 3000.0;
    private const double LandingAltitudeThreshold = 100.0;
    private const double RolloutSpeedThreshold = 30.0;
    private const double TaxiSpeedThreshold = 5.0;
    private const double TakeoffSpeedThreshold = 80.0;
    private const double CruiseSpeedMin = 250.0;

    public FlightPhase DetectPhase(FlightSnapshot snapshot, FlightPhase currentPhase)
    {
        if (snapshot.OnGround)
        {
            if (currentPhase == FlightPhase.Rollout || currentPhase == FlightPhase.Landing)
            {
                return snapshot.GroundSpeedKnots < RolloutSpeedThreshold
                    ? FlightPhase.Ground
                    : FlightPhase.Rollout;
            }
            return FlightPhase.Ground;
        }

        var radioAltitude = snapshot.RadioAltitudeFeet;
        var baroAltitude = snapshot.BaroAltitudeFeet;

        if (baroAltitude < TakeoffAltitudeThreshold || radioAltitude < 30)
        {
            return FlightPhase.Takeoff;
        }

        if (baroAltitude < ClimbAltitudeThreshold)
        {
            return FlightPhase.Climb;
        }

        if (baroAltitude < CruiseAltitudeThreshold)
        {
            if (currentPhase is FlightPhase.Climb or FlightPhase.Cruise)
            {
                return FlightPhase.Cruise;
            }
            return FlightPhase.Climb;
        }

        if (baroAltitude > CruiseAltitudeThreshold)
        {
            if (currentPhase == FlightPhase.Cruise)
            {
                return FlightPhase.Cruise;
            }
            return FlightPhase.Cruise;
        }

        if (baroAltitude < DescentAltitudeThreshold && baroAltitude >= ApproachAltitudeThreshold)
        {
            return FlightPhase.Descent;
        }

        if (baroAltitude < ApproachAltitudeThreshold)
        {
            return FlightPhase.Approach;
        }

        return currentPhase;
    }

    public FlightState DetectState(FlightSnapshot snapshot, FlightState currentState)
    {
        var onGround = snapshot.OnGround;
        var groundSpeed = snapshot.GroundSpeedKnots;
        var radioAltitude = snapshot.RadioAltitudeFeet;
        var baroAltitude = snapshot.BaroAltitudeFeet;
        var ias = snapshot.IndicatedAirspeedKnots;

        if (onGround)
        {
            if (groundSpeed < TaxiSpeedThreshold)
            {
                return currentState switch
                {
                    FlightState.Landing or FlightState.TaxiIn => FlightState.Parked,
                    FlightState.Takeoff => FlightState.TaxiOut,
                    _ => FlightState.OnGround
                };
            }

            if (groundSpeed < TakeoffSpeedThreshold)
            {
                return currentState == FlightState.OnGround || currentState == FlightState.Parked
                    ? FlightState.TaxiOut
                    : FlightState.TaxiIn;
            }

            return currentState == FlightState.Takeoff ? FlightState.TaxiOut : FlightState.TaxiIn;
        }

        if (radioAltitude < 30 || baroAltitude < TakeoffAltitudeThreshold)
        {
            return FlightState.Takeoff;
        }

        if (baroAltitude < ClimbAltitudeThreshold)
        {
            return FlightState.Climb;
        }

        if (ias >= CruiseSpeedMin && baroAltitude >= CruiseAltitudeThreshold)
        {
            return currentState == FlightState.Climb ? FlightState.Cruise : FlightState.Cruise;
        }

        if (baroAltitude < DescentAltitudeThreshold && baroAltitude >= ApproachAltitudeThreshold)
        {
            return FlightState.Descent;
        }

        if (baroAltitude < ApproachAltitudeThreshold)
        {
            return FlightState.Approach;
        }

        return currentState;
    }

    public bool IsFlightStart(FlightSnapshot snapshot, FlightState previousState)
    {
        var currentState = DetectState(snapshot, previousState);
        return currentState == FlightState.Takeoff && previousState == FlightState.TaxiOut;
    }

    public bool IsFlightEnd(FlightSnapshot snapshot, FlightState previousState)
    {
        var currentState = DetectState(snapshot, previousState);
        return currentState == FlightState.Parked && previousState == FlightState.TaxiIn;
    }

    public FlightSession CreateSession(string? tailNumber = null, string? flightNumber = null, string? pilotId = null)
    {
        return new FlightSession(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            null,
            "Fenix A320",
            0)
        {
            CurrentPhase = FlightPhase.Unknown
        };
    }

    public FlightSession? CompleteSession(FlightSession session, FlightSnapshot finalSnapshot)
    {
        if (session.EndedAtUtc.HasValue)
        {
            return null;
        }

        var duration = finalSnapshot.TimestampUtc - session.StartedAtUtc;
        return session with
        {
            EndedAtUtc = finalSnapshot.TimestampUtc,
            SnapshotCount = session.SnapshotCount + 1
        };
    }

    public void UpdateSessionMetrics(FlightSession session, FlightSnapshot snapshot, IEnumerable<FlightEvent> events)
    {
        _ = session;
        _ = snapshot;
        _ = events;
    }

    public TimeSpan CalculateFlightDuration(FlightSession session)
    {
        if (!session.EndedAtUtc.HasValue)
        {
            return DateTimeOffset.UtcNow - session.StartedAtUtc;
        }
        return session.EndedAtUtc.Value - session.StartedAtUtc;
    }
}
using FenixFpm.Core.Models;

namespace FenixFpm.Core.Services;

public sealed class FlightPhaseDetector
{
    private const double TakeoffAltitudeThreshold = 50.0;
    private const double ClimbAltitudeThreshold = 1500.0;
    private const double CruiseAltitudeThreshold = 25000.0;
    private const double DescentAltitudeThreshold = 15000.0;
    private const double ApproachAltitudeThreshold = 10000.0;
    private const double LandingAltitudeThreshold = 50.0;
    private const double RolloutSpeedThreshold = 30.0;
    private const double TaxiSpeedThreshold = 1.0;
    private const double EngineOnThreshold = 5.0;

    private bool _wasOnGround = true;
    private double _lastAltitude = 0;
    private double _lastVerticalSpeed = 0;
    private bool _lastEnginesOn = false;

    public FlightPhase DetectPhase(FlightSnapshot snapshot, FlightPhase previousPhase)
    {
        var onGround = snapshot.OnGround;
        var radioAltitude = snapshot.RadioAltitudeFeet;
        var baroAltitude = snapshot.BaroAltitudeFeet;
        var groundSpeed = snapshot.GroundSpeedKnots;
        var verticalSpeed = snapshot.VerticalSpeedFpm;
        var enginesOn = snapshot.Engines.LeftN1Percent > EngineOnThreshold || snapshot.Engines.RightN1Percent > EngineOnThreshold;
        var flaps = snapshot.LandingConfiguration.Configuration;
        var gearDown = snapshot.LandingConfiguration.GearDown;

        if (onGround)
        {
            if (!enginesOn)
            {
                return FlightPhase.Preflight;
            }

            if (groundSpeed <= TaxiSpeedThreshold)
            {
                return _wasOnGround && !_lastEnginesOn ? FlightPhase.Preflight : FlightPhase.Parked;
            }

            return FlightPhase.Taxi;
        }

        if (_wasOnGround && !onGround)
        {
            return FlightPhase.Takeoff;
        }

        if (radioAltitude < LandingAltitudeThreshold && verticalSpeed < 0)
        {
            return FlightPhase.Landing;
        }

        if (radioAltitude >= LandingAltitudeThreshold && radioAltitude < 200 && verticalSpeed < 0)
        {
            return FlightPhase.Landing;
        }

        if (baroAltitude < ApproachAltitudeThreshold && (flaps != LandingConfiguration.NotConfigured || gearDown))
        {
            return FlightPhase.Approach;
        }

        if (verticalSpeed < -100 && baroAltitude < DescentAltitudeThreshold && baroAltitude > ApproachAltitudeThreshold)
        {
            return FlightPhase.Descent;
        }

        if (baroAltitude >= CruiseAltitudeThreshold && Math.Abs(verticalSpeed) < 200)
        {
            return FlightPhase.Cruise;
        }

        if (baroAltitude >= ClimbAltitudeThreshold && baroAltitude < CruiseAltitudeThreshold && verticalSpeed > 100)
        {
            return FlightPhase.Climb;
        }

        if (baroAltitude >= CruiseAltitudeThreshold && verticalSpeed > 100)
        {
            return FlightPhase.Climb;
        }

        if (radioAltitude < ApproachAltitudeThreshold && onGround && groundSpeed > RolloutSpeedThreshold)
        {
            return FlightPhase.Rollout;
        }

        return previousPhase;
    }

    public void UpdateState(bool onGround, double altitude, double vs, bool enginesOn)
    {
        _wasOnGround = onGround;
        _lastAltitude = altitude;
        _lastVerticalSpeed = vs;
        _lastEnginesOn = enginesOn;
    }

    public bool IsTakeoffTransition(FlightSnapshot snapshot) =>
        !snapshot.OnGround && _wasOnGround;

    public bool IsLandingTransition(FlightSnapshot snapshot) =>
        snapshot.OnGround && !_wasOnGround && snapshot.RadioAltitudeFeet < 50;
}
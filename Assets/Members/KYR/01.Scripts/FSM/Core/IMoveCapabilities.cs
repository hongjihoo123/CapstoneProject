namespace Members.KYR._01_Scripts.FSM.Core
{
    public interface IMoveCapabilities
    {
        bool CanAim { get; }
        bool CanFire { get; }
        bool IsCrouching { get; }
        bool IsAirborne { get; }
        float PlanarSpeed { get; }
    }
}

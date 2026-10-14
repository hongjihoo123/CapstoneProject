namespace Members.KYR._01_Scripts.FSM.Core
{
    public interface IWeaponCapabilities
    {
        bool AllowsFire { get; }
        bool AllowsAim { get; }
        bool LocksSprint { get; }
        float MoveSpeedMultiplier { get; }
    }
}

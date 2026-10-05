namespace RobotWeapons
{
    public interface IBurstWeapon
    {
        bool IsBursting { get; }
    }

    public interface IAttackSpeedScalable
    {
        float AttackSpeedMultiplier { get; set; }
    }

    public interface IReloadSpeedScalable
    {
        float ReloadSpeedMultiplier { get; set; }
    }

    public interface IUltimateWeapon
    {
        void ActivateUltimate(float duration);
    }

    public interface IComboWeapon
    {
        int ComboIndex { get; }
        int ComboCount { get; }
    }
}

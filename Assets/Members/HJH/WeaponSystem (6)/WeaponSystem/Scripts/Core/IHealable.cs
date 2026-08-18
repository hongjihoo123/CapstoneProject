namespace RobotWeapons
{
    public interface IHealable
    {
        bool IsAlive { get; }
        void Heal(float amount);
    }
}

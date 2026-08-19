using UnityEngine;

namespace RobotWeapons
{
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(float amount, GameObject source);
    }
}

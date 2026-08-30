using UnityEngine;

namespace RobotWeapons
{
    public interface IHitEffectSource
    {
        GameObject HitEffectPrefab { get; }
    }
}

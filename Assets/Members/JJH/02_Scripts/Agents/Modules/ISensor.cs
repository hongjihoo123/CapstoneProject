using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Modules
{
    public interface ISensor
    {
        LayerMask TargetLayer { get; }

        public bool IsTargetInRange(float range, out Collider hitCollider);
    }
}

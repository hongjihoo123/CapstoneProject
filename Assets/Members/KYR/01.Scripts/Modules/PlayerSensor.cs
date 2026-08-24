using System.Linq;
using Members.JJH._02_Scripts.Agents.Modules;
using Members.JJH._02_Scripts.Systems.ModuleSystem;
using UnityEngine;

namespace Members.KYR._01_Scripts.Modules
{
    public class PlayerSensor : Module, ISensor
    {
        [field: SerializeField] public LayerMask ObstacleLayer { get; private set; }
        [field: SerializeField] public LayerMask TargetLayer { get; private set; }

        public bool IsTargetInRange(float range, out Collider hitCollider)
        {
            hitCollider = Physics.OverlapSphere(transform.position, range, TargetLayer).FirstOrDefault();
            return hitCollider != null;
        }
    }
}

using Members.JJH._02_Scripts.Agents.Modules;
using Members.JJH._02_Scripts.Systems.ModuleSystem;
using System.Linq;
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

        public bool IsTargetInSight(float range, float sight)
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, range, TargetLayer);

            foreach (Collider collider in colliders)
            {
                Vector3 direction = collider.transform.position - transform.position;
                float angle = Vector3.Angle(transform.forward, direction);

                if (angle <= sight)
                    return true;
            }

            return false;
        }
    }
}

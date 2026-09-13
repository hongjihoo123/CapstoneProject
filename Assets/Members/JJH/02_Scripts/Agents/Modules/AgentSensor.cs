using Members.JJH._02_Scripts.Systems.ModuleSystem;
using System.Linq;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Modules
{
    public class AgentSensor : Module, ISensor
    {
        [Header("Layer")]
        [field: SerializeField] public LayerMask TargetLayer { get; private set; }

        private float _debugRange = 0;

        public bool IsTargetInRange(float range, out Collider hitCollider)
        {
            hitCollider = Physics.OverlapSphere(transform.position, range, TargetLayer).FirstOrDefault();
            _debugRange = range;
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

        private void OnDrawGizmos()
        {
            if (_debugRange > 0f)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(transform.position, _debugRange);
            }
        }
    }
}
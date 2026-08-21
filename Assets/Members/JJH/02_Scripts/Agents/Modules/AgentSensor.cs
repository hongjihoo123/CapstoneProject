using Members.JJH._02_Scripts.Systems.ModuleSystem;
using System.Linq;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Modules
{
    public class AgentSensor : Module, ISensor
    {
        [Header("Layer")]
        [field: SerializeField] public LayerMask ObstacleLayer { get; private set; }
        [field: SerializeField] public LayerMask TargetLayer { get; private set; }

        [Header("Layer")]
        [SerializeField] private float selfCheckRadius;
        [SerializeField] private float viewAngle = 120f;

        private float _debugRange = 0;

        public bool IsTargetInRange(float range, out Collider hitCollider)
        {
            hitCollider = Physics.OverlapSphere(transform.position, range, TargetLayer).FirstOrDefault();
            _debugRange = range;
            return hitCollider != null;
        }

        private void OnDrawGizmos()
        {
            _debugRange = selfCheckRadius;
            if (_debugRange > 0f)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(transform.position, _debugRange);
            }
        }
    }
}
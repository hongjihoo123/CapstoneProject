using Members.JJH._02_Scripts.Systems.ModuleSystem;
using System.Linq;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Modules
{
    public class AgentSensor : AbstractModule, ISensor
    {
        [Header("Layer")]
        [field: SerializeField] public LayerMask TargetLayer { get; private set; }
        [field: SerializeField] public LayerMask ObstacleLayer { get; private set; }

        [Header("Layer")]
        [field: SerializeField] public Color DebugRangeColor { get; private set; } = Color.blue;
        [field: SerializeField] public Color DebugSightColor { get; private set; } = Color.yellow;

        private float _debugRange = 0;
        private float _debugSightRange;
        private float _debugSightAngle;

        public bool IsTargetInRange(float range, out Collider hitCollider)
        {
            hitCollider = Physics.OverlapSphere(transform.position, range, TargetLayer).FirstOrDefault();
            _debugRange = range;
            return hitCollider != null;
        }

        public bool IsTargetInSight(float range, float sight)
        {
            _debugSightRange = range;
            _debugSightAngle = sight;

            Collider[] colliders = Physics.OverlapSphere(transform.position, range, TargetLayer);

            foreach (Collider collider in colliders)
            {
                Vector3 direction = collider.transform.position - transform.position;
                float angle = Vector3.Angle(transform.forward, direction);

                if (angle > sight)
                    continue;

                float distance = direction.magnitude;

                if (Physics.Raycast(transform.position, direction.normalized, distance, ObstacleLayer))
                    continue;

                return true;
            }

            return false;
        }

        private void OnDrawGizmos()
        {
            if (_debugRange > 0f)
            {
                Gizmos.color = DebugRangeColor;
                Gizmos.DrawWireSphere(transform.position, _debugRange);
            }

            if (_debugSightRange > 0f)
            {
                Gizmos.color = DebugSightColor;

                Vector3 previousPoint = transform.position + Quaternion.Euler(0, -_debugSightAngle, 0) * transform.forward * _debugSightRange;

                Gizmos.DrawLine(transform.position, previousPoint);

                for (int i = 1; i <= 30; i++)
                {
                    float angle = Mathf.Lerp(-_debugSightAngle, _debugSightAngle, i / 30f);
                    Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;
                    Vector3 currentPoint = transform.position + direction * _debugSightRange;

                    Gizmos.DrawLine(previousPoint, currentPoint);
                    previousPoint = currentPoint;
                }

                Gizmos.DrawLine(transform.position, previousPoint);
            }
        }
    }
}
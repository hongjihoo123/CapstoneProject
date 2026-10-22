using Members.JJH._02_Scripts.Systems.ModuleSystem;
using UnityEngine;
using UnityEngine.AI;

namespace Members.JJH._02_Scripts.Agents.Modules
{
    public class AgentNavMesh : Module, INavMesh
    {
        public NavMeshAgent NavMeshAgent { get; private set; }

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            NavMeshAgent = GetComponentInParent<NavMeshAgent>();
            Debug.Assert(NavMeshAgent != null, $"{gameObject.name} : AgentNavMesh에는 NavMeshAgent 컴포넌트가 필요합니다.");
        }

        public void SetNavMeshAgent(float speed, float acceleration)
        {
            NavMeshAgent.speed = speed;
            NavMeshAgent.acceleration = acceleration;
        }

        public void MoveTo(Vector3 targetPosition)
        {
            NavMeshAgent.SetDestination(targetPosition);
        }

        public void KeepChase(bool value)
        {
            NavMeshAgent.isStopped = !value;

            if (!value)
            {
                NavMeshAgent.ResetPath();
                NavMeshAgent.velocity = Vector3.zero;
            }
        }

        public bool RotateToTarget(Vector3 targetPosition, float angleThreshold = 1f)
        {
            Transform agentTransform = NavMeshAgent.transform;

            Vector3 direction = targetPosition - agentTransform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.0001f)
                return true;

            Quaternion targetRotation = Quaternion.LookRotation(direction);

            if (Quaternion.Angle(agentTransform.rotation, targetRotation) <= angleThreshold)
            {
                agentTransform.rotation = targetRotation;
                return true;
            }

            agentTransform.rotation = Quaternion.RotateTowards(agentTransform.rotation, targetRotation,
                                                                                                    NavMeshAgent.angularSpeed * Time.deltaTime);

            return Quaternion.Angle(agentTransform.rotation, targetRotation) <= angleThreshold;
        }

        public void StopImmediately()
        {
            if (NavMeshAgent == null || !NavMeshAgent.enabled || !NavMeshAgent.isOnNavMesh)
                return;

            NavMeshAgent.ResetPath();
            NavMeshAgent.velocity = Vector3.zero;
        }
    }
}
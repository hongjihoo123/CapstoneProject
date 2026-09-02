using Members.JJH._02_Scripts.Agents.Enemies;
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
            NavMeshAgent.angularSpeed = 720f;
            NavMeshAgent.updateRotation = true;
            NavMeshAgent.autoBraking = true;

            if (owner is AbstractEnemy enemy)
                NavMeshAgent.speed = enemy.EnemyData.EnemySpeed;
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

        public void StopImmediately()
        {
            NavMeshAgent.ResetPath();
            NavMeshAgent.velocity = Vector3.zero;
        }
    }
}
using UnityEngine;
using UnityEngine.AI;

namespace Members.JJH._02_Scripts.Agents.Modules
{
    public interface INavMesh
    {
        NavMeshAgent NavMeshAgent { get; }

        void SetNavMeshAgent(float speed, float angularSpeed, float acceleration);
        void MoveTo(Vector3 targetPosition);
        void KeepChase(bool value);
        void RotateToTarget(Vector3 targetPosition);
        void StopImmediately();
    }
}

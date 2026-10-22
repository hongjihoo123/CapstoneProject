using UnityEngine;
using UnityEngine.AI;

namespace Members.JJH._02_Scripts.Agents.Modules
{
    public interface INavMesh
    {
        NavMeshAgent NavMeshAgent { get; }

        void SetNavMeshAgent(float speed, float acceleration);
        void MoveTo(Vector3 targetPosition);
        void KeepChase(bool value);
        bool RotateToTarget(Vector3 targetPosition, float angleThreshold = 1f);
        void StopImmediately();
    }
}
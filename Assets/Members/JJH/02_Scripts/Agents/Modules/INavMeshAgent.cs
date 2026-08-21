using UnityEngine;
using UnityEngine.AI;

namespace Members.JJH._02_Scripts.Agents.Modules
{
    public interface INavMesh
    {
        public NavMeshAgent NavMeshAgent { get; }

        public void MoveTo(Vector3 targetPosition);
        public void KeepChase(bool value);
        public void StopImmediately();
    }
}

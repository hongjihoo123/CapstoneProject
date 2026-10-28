using System;
using Members.JJH._02_Scripts.Agents.Modules;
using UnityEngine;

namespace Members.KYR._01_Scripts.Spawner
{
    public class SpawnedEnemyTracker : MonoBehaviour
    {
        public event Action<SpawnedEnemyTracker> Died;

        private bool _reported;

        public void Watch(AgentHealth health)
        {
            if (health != null && health.OnDeathEvent != null)
                health.OnDeathEvent.AddListener(Report);
        }

        private void OnDestroy()
        {
            Report();
        }

        private void Report()
        {
            if (_reported)
                return;

            _reported = true;
            Died?.Invoke(this);
        }
    }
}

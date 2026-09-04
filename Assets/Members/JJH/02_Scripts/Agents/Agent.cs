using Members.JJH._02_Scripts.Agents.Modules;
using Members.JJH._02_Scripts.Systems.ModuleSystem;
using RobotWeapons;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents
{
    public class Agent : ModuleOwner, IDamageable
    {
        public IRenderer Renderer { get; private set; }
        public ISensor Sensor { get; private set; }
        public IHealth Health { get; private set; }
        public virtual bool IsAlive { get => Health.CurrentHealth > 0; }

        protected override void InitializeModules()
        {
            base.InitializeModules();

            Sensor = GetModule<ISensor>();
            Debug.Assert(Sensor != null, $"{gameObject.name}에는 ISensor모듈이 필요합니다.");
            Renderer = GetModule<IRenderer>();
            Debug.Assert(Renderer != null, $"{gameObject.name}에는 IRenderer모듈이 필요합니다.");
            Health = GetModule<IHealth>();
            Debug.Assert(Health != null, $"{gameObject.name}에는 IHealth모듈이 필요합니다.");
        }
        public virtual void TakeDamage(float amount, GameObject source)
        {
            bool wasAlive = IsAlive;
            Health.TakeDamage(amount);
            if (wasAlive && !IsAlive && source != null && source.TryGetComponent(out Members.KYR._01_Scripts.PlayerAgent killer))
                killer.OnEnemyKilled();
        }
    }
}
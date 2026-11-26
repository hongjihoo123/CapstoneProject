using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public abstract class PassiveData : ScriptableObject
    {
        [SerializeField] private Sprite icon;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;

        public Sprite Icon => icon;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public string Description => description;

        public abstract IPassive CreateRuntime(IPassiveHost host);
    }

    public interface IPassiveHost
    {
        ISkillContext Context { get; }
        void ReduceCooldown(SkillSlotId slot, float seconds);
    }

    public interface IPassive
    {
        void OnDamageDealt(in DamageDealtInfo info);
        void OnEnemyKilled();
        void OnSkillUsed(in SkillUsedInfo info);
        void Tick(float deltaTime);
        void Dispose();
    }

    public abstract class PassiveRuntime : IPassive
    {
        protected readonly IPassiveHost Host;

        protected PassiveRuntime(IPassiveHost host) => Host = host;

        protected ISkillContext Context => Host.Context;

        public virtual void OnDamageDealt(in DamageDealtInfo info) { }
        public virtual void OnEnemyKilled() { }
        public virtual void OnSkillUsed(in SkillUsedInfo info) { }
        public virtual void Tick(float deltaTime) { }
        public virtual void Dispose() { }
    }
}

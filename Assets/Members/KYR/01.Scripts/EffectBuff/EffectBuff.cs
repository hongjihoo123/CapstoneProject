using UnityEngine;

namespace Members.KYR._01_Scripts.EffectBuffs
{
    public abstract class EffectBuff : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private float duration = 10f;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public float Duration => duration;

        public virtual object CreateState() => null;

        public virtual void OnStart(EffectBuffContext context, object state)
        {
        }

        public virtual void Tick(EffectBuffContext context, object state, float deltaTime)
        {
        }

        public virtual void OnBasicAttack(EffectBuffContext context, object state)
        {
        }

        public virtual void OnEnd(EffectBuffContext context, object state)
        {
        }
    }

    public abstract class EffectBuff<TState> : EffectBuff where TState : class, new()
    {
        public override object CreateState() => new TState();

        public sealed override void OnStart(EffectBuffContext context, object state)
        {
            OnStart(context, (TState)state);
        }

        public sealed override void Tick(EffectBuffContext context, object state, float deltaTime)
        {
            Tick(context, (TState)state, deltaTime);
        }

        public sealed override void OnBasicAttack(EffectBuffContext context, object state)
        {
            OnBasicAttack(context, (TState)state);
        }

        public sealed override void OnEnd(EffectBuffContext context, object state)
        {
            OnEnd(context, (TState)state);
        }

        protected virtual void OnStart(EffectBuffContext context, TState state)
        {
        }

        protected virtual void Tick(EffectBuffContext context, TState state, float deltaTime)
        {
        }

        protected virtual void OnBasicAttack(EffectBuffContext context, TState state)
        {
        }

        protected virtual void OnEnd(EffectBuffContext context, TState state)
        {
        }
    }
}

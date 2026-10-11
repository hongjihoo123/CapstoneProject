using Assets.Members.HJH._02.Scripts.Char.Visual;
using Members.KYR._01_Scripts.Stats;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Chain
{
    // Character 2 (chain bruiser): heals for a share of all damage dealt (weapon and skills),
    // and every kill adds an attack speed stack (refreshes the timer, capped).
    // Attack speed goes through the AttackSpeed stat, so weapon timings and the animator follow it.
    [CreateAssetMenu(menuName = "Skill/Passive/Blood Frenzy")]
    public class BloodFrenzyPassiveData : PassiveData
    {
        [SerializeField, Range(0f, 1f)] private float lifesteal = 0.08f;
        [SerializeField, Tooltip("Attack speed per stack (0.1 = +10%).")]
        private float attackSpeedPerStack = 0.1f;
        [SerializeField, Min(1)] private int maxStacks = 5;
        [SerializeField] private float stackDuration = 3f;
        [SerializeField] private Color bloodColor = new(1f, 0.12f, 0.15f);
        [SerializeField, Tooltip("Lifesteal drops, stack ring and ember aura. Off = the passive works without its own effects.")]
        private bool showEffects = true;

        public override IPassive CreateRuntime(IPassiveHost host) => new Runtime(this, host);

        private sealed class Runtime : PassiveRuntime
        {
            private readonly BloodFrenzyPassiveData _data;
            private int _stacks;
            private float _remaining;
            private FxHandle _aura;

            public Runtime(BloodFrenzyPassiveData data, IPassiveHost host) : base(host) => _data = data;

            public override void OnDamageDealt(in DamageDealtInfo info)
            {
                Context.Heal(info.Amount * _data.lifesteal);

                // Blood drawn from the target toward the player.
                if (info.Target is Component target)
                {
                    Vector3 from = target.transform.position + Vector3.up;
                    if (_data.showEffects)
                        Fx.Lifesteal(from, Context.Transform.position + Vector3.up, _data.bloodColor);
                }
            }

            public override void OnEnemyKilled()
            {
                _stacks = Mathf.Min(_data.maxStacks, _stacks + 1);
                _remaining = _data.stackDuration;
                ApplyStacks();

                if (_data.showEffects)
                    Fx.FrenzyStack(Context.Transform.position, _data.bloodColor, _stacks);
            }

            public override void Tick(float deltaTime)
            {
                if (_stacks == 0)
                    return;

                _remaining -= deltaTime;
                if (_remaining > 0f)
                    return;

                _stacks = 0;
                ApplyStacks();
            }

            public override void Dispose()
            {
                _stacks = 0;
                ApplyStacks();
            }

            private void ApplyStacks()
            {
                UpdateAura();
                if (!Context.StatsReady)
                    return;

                Context.RemoveStatModifiers(this);
                if (_stacks > 0)
                    Context.AddStatModifier(PlayerStatId.AttackSpeed,
                        new StatModifier(this, StatModifierType.PercentAdd, _data.attackSpeedPerStack * _stacks));
            }

            // Red embers around the player while frenzied; denser with more stacks.
            private void UpdateAura()
            {
                if (_stacks <= 0 || !_data.showEffects)
                {
                    _aura?.Kill();
                    _aura = null;
                    return;
                }

                _aura ??= Fx.FrenzyAura(Context.Transform, _data.bloodColor, _stacks);
                _aura.SetValue(_stacks);
            }
        }
    }
}

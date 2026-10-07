using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.RuneMage
{
    // Shared rune pool between the rune mage's passive (fills it) and skills (spend it).
    public interface IRuneReservoir
    {
        int Runes { get; }
        int MaxRunes { get; }
        int ConsumeAll();
    }

    // Character 3 passive: every weapon hit adds a rune that orbits the player. Orbiting runes
    // grind enemies close to the player, so poking from range charges up the dive.
    // Runes fade one by one after a while without new hits.
    [CreateAssetMenu(menuName = "Skill/Passive/Rune Resonance")]
    public class RuneResonancePassiveData : PassiveData
    {
        [SerializeField, Min(1)] private int maxRunes = 5;
        [SerializeField] private float orbitRadius = 1.8f;
        [SerializeField, Tooltip("Degrees per second.")] private float orbitSpeed = 240f;
        [SerializeField] private float runeRadius = 0.3f;
        [SerializeField] private float tickInterval = 0.5f;
        [SerializeField, Tooltip("Damage per rune per tick to every enemy inside the orbit.")]
        private float damagePerRune = 5f;
        [SerializeField] private float decayDelay = 6f;
        [SerializeField] private float decayInterval = 1.5f;
        [SerializeField] private Color runeColor = new(0.7f, 0.4f, 1f);

        public override IPassive CreateRuntime(IPassiveHost host) => new Runtime(this, host);

        private sealed class Runtime : PassiveRuntime, IRuneReservoir
        {
            private readonly RuneResonancePassiveData _data;
            private IShotHitSource _shots;
            private readonly List<FxHandle> _orbs = new();
            private float _angle;
            private float _tickTimer;
            private float _idleTime;

            public Runtime(RuneResonancePassiveData data, IPassiveHost host) : base(host) => _data = data;

            public int Runes { get; private set; }
            public int MaxRunes => _data.maxRunes;

            public int ConsumeAll()
            {
                int spent = Runes;
                Runes = 0;
                SyncOrbs();
                return spent;
            }

            public override void Tick(float deltaTime)
            {
                Rebind();
                SyncOrbs();
                if (Runes == 0)
                    return;

                _angle = (_angle + _data.orbitSpeed * deltaTime) % 360f;
                DrawRunes();

                _tickTimer -= deltaTime;
                if (_tickTimer <= 0f)
                {
                    _tickTimer = _data.tickInterval;
                    GrindNearby();
                }

                _idleTime += deltaTime;
                if (_idleTime >= _data.decayDelay + _data.decayInterval)
                {
                    _idleTime = _data.decayDelay;
                    Runes--;
                }
            }

            public override void Dispose()
            {
                Bind(null);
                Runes = 0;
                SyncOrbs();
            }

            private void DrawRunes()
            {
                Vector3 center = Context.Transform.position;
                for (int i = 0; i < Runes; i++)
                {
                    float angle = _angle + 360f * i / Runes;
                    Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * _data.orbitRadius;
                    Vector3 point = center + offset + Vector3.up * 1.1f;
                    if (i < _orbs.Count)
                        _orbs[i].MoveTo(point);
                    AttackAreaBus.Raise(AttackArea.Circle(AttackAreaKind.Skill, center + offset, _data.runeRadius, 0.02f));
                }
            }

            // One glowing orb per stored rune; extra orbs burst away when runes are spent or fade.
            private void SyncOrbs()
            {
                Vector3 center = Context.Transform.position + Vector3.up * 1.1f;
                while (_orbs.Count < Runes)
                    _orbs.Add(Fx.Orb(center, _data.runeColor, 0.32f));

                while (_orbs.Count > Runes)
                {
                    FxHandle orb = _orbs[_orbs.Count - 1];
                    _orbs.RemoveAt(_orbs.Count - 1);
                    Fx.RuneGain(orb.Position, _data.runeColor);
                    orb.Kill();
                }
            }

            private void GrindNearby()
            {
                float radius = _data.orbitRadius + _data.runeRadius;
                foreach (IDamageable target in Context.OverlapSphere(Context.Transform.position + Vector3.up, radius))
                    Context.DealDamage(target, _data.damagePerRune * Runes);
            }

            private void HandleShotHit(IDamageable target, bool empowered)
            {
                if (Runes < _data.maxRunes)
                    Fx.RuneGain(Context.Transform.position + Vector3.up * 1.1f, _data.runeColor);

                Runes = Mathf.Min(_data.maxRunes, Runes + 1);
                _idleTime = 0f;
            }

            private void Rebind()
            {
                IShotHitSource current = Context.Weapon as IShotHitSource;
                if (!ReferenceEquals(current, _shots))
                    Bind(current);
            }

            private void Bind(IShotHitSource source)
            {
                if (_shots != null)
                    _shots.ShotHit -= HandleShotHit;

                _shots = source;

                if (_shots != null)
                    _shots.ShotHit += HandleShotHit;
            }
        }
    }
}

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

    // Character 3 passive: every weapon hit adds a rune that orbits the player. Each rune hurts
    // whatever it touches, so more runes = more hits per second, and poking from range charges up the dive.
    // Runes fade one by one after a while without new hits.
    [CreateAssetMenu(menuName = "Skill/Passive/Rune Resonance")]
    public class RuneResonancePassiveData : PassiveData
    {
        [SerializeField, Min(1)] private int maxRunes = 5;
        [SerializeField] private float orbitRadius = 1.8f;
        [SerializeField, Tooltip("Degrees per second.")] private float orbitSpeed = 240f;
        [SerializeField, Tooltip("룬 하나의 판정 크기. 링 안쪽에 붙은 적도 닿게 조금 넉넉하게")]
        private float runeRadius = 0.5f;
        [SerializeField, Tooltip("룬이 적에 닿을 때마다 들어가는 피해")]
        private float touchDamage = 15f;
        [SerializeField, Tooltip("같은 룬이 같은 적을 다시 때리기까지 (초). 룬이 적을 한 번 지나가는 동안 한 대만 맞게 (지나가는 데 약 0.3초)")]
        private float retouchDelay = 0.5f;
        [SerializeField] private float decayDelay = 6f;
        [SerializeField] private float decayInterval = 1.5f;
        [SerializeField] private Color runeColor = new(0.7f, 0.4f, 1f);
        [SerializeField, Tooltip("도는 룬이 적을 갈 때 맞은 자리에 터지는 이펙트 색 (룬 색이 은색이라 바닥에 묻히지 않게 따로 둠)")]
        private Color hitColor = new(0.62f, 0.52f, 1f);

        public override IPassive CreateRuntime(IPassiveHost host) => new Runtime(this, host);

        private sealed class Runtime : PassiveRuntime, IRuneReservoir
        {
            private readonly RuneResonancePassiveData _data;
            private IShotHitSource _shots;
            private readonly List<FxHandle> _orbs = new();
            // (룬 번호, 적) -> 마지막으로 때린 시간. 한 번 지나갈 때 한 번만 맞게
            private readonly Dictionary<(int, IDamageable), float> _lastTouch = new();
            private readonly List<(int, IDamageable)> _expired = new();
            private float _angle;
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
                MoveRunes();
                ForgetOldTouches();

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
                _lastTouch.Clear();
                SyncOrbs();
            }

            // 룬을 돌리고, 룬 자리에 닿은 적을 바로 때림 (룬 최대 5개라 판정 5번, 가벼움)
            private void MoveRunes()
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

                    foreach (IDamageable target in Context.OverlapSphere(center + offset + Vector3.up, _data.runeRadius))
                        Touch(i, target, center);
                }
            }

            private void Touch(int rune, IDamageable target, Vector3 center)
            {
                var key = (rune, target);
                if (_lastTouch.TryGetValue(key, out float last) && Time.time - last < _data.retouchDelay)
                    return;

                _lastTouch[key] = Time.time;
                Context.DealDamage(target, _data.touchDamage);

                // 룬이 갈아버린 느낌: 맞은 적 몸에서 룬 파편이 튀고, 플레이어 바깥쪽으로 스파크
                if (target is Component component)
                {
                    Vector3 body = component.transform.position + Vector3.up * 1.1f;
                    Vector3 outward = component.transform.position - center;
                    outward.y = 0f;
                    Fx.RuneGain(body, _data.hitColor);
                    Fx.Hit(body, outward, _data.hitColor, Runes >= _data.maxRunes);
                }
            }

            // 오래된 기록은 지움 (죽은 적이 계속 남아있지 않게)
            private void ForgetOldTouches()
            {
                _expired.Clear();
                foreach (KeyValuePair<(int, IDamageable), float> touch in _lastTouch)
                {
                    if (Time.time - touch.Value >= _data.retouchDelay)
                        _expired.Add(touch.Key);
                }

                foreach ((int, IDamageable) key in _expired)
                    _lastTouch.Remove(key);
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

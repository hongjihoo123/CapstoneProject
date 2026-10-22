using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // FxWorkbench 의 허수아비 적 하나.
    //  - 이 오브젝트(판정용)는 스킬 파트의 조준 지점에 있음 → 스킬은 여기를 맞힌다고 계산 (투사체가 멈추는 곳, 터지는 곳)
    //  - Body(보이는 캡슐)는 적 파트에 따로 서 있음 → 맞은 이펙트·CC 표시·데미지 숫자·밀려남은 전부 여기에 (IFxBody)
    // 죽지 않음. CC 는 받은 시간만큼 이름표에 표시.
    public class WorkbenchDummy : MonoBehaviour, IDamageable, ICrowdControlTarget, IFxBody
    {
        public Transform Body { get; private set; }
        public Transform FxBody => Body;
        public bool IsAlive => true;

        private Vector3 _home;
        private float _slowUntil, _rootUntil, _stunUntil, _knockUntil;
        private float _slowPercent;
        private Vector3 _knockOffset;
        private float _burnUntil, _burnNext, _burnTick, _burnDamage;

        public void Setup(Transform body)
        {
            Body = body;
            _home = body.position;
        }

        public void ResetState()
        {
            _slowUntil = _rootUntil = _stunUntil = _knockUntil = _burnUntil = 0f;
            _knockOffset = Vector3.zero;
            if (Body != null)
                Body.position = _home;
        }

        public void TakeDamage(float amount, GameObject source)
        {
            if (Body != null && amount > 0f)
                Fx.DamageNumber(Body.position + Vector3.up * 2.4f, amount, Color.white);
        }

        public void ApplySlow(float percent, float duration)
        {
            _slowPercent = percent;
            _slowUntil = Mathf.Max(_slowUntil, Time.time + duration);
        }

        public void ApplyRoot(float duration) => _rootUntil = Mathf.Max(_rootUntil, Time.time + duration);
        public void ApplyStun(float duration) => _stunUntil = Mathf.Max(_stunUntil, Time.time + duration);

        // 실제 적처럼 밀려났다가, 잠깐 뒤 제자리로 돌아옴 (계속 확인할 수 있게)
        public void ApplyKnockback(Vector3 direction, float distance)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                direction = transform.forward;
            _knockOffset = direction.normalized * Mathf.Min(distance, 3f);
            _knockUntil = Time.time + 0.8f;
        }

        public void ApplyBurn(float damagePerTick, float tickInterval, float duration)
        {
            _burnDamage = damagePerTick;
            _burnTick = Mathf.Max(0.1f, tickInterval);
            _burnUntil = Time.time + duration;
            _burnNext = Time.time + _burnTick;
        }

        public bool Has(CrowdControlType type) => type switch
        {
            CrowdControlType.Slow => Time.time < _slowUntil,
            CrowdControlType.Root => Time.time < _rootUntil,
            CrowdControlType.Stun => Time.time < _stunUntil,
            CrowdControlType.Knockback => Time.time < _knockUntil,
            _ => false
        };

        private void Update()
        {
            if (Body == null)
                return;

            // 밀려남: 0.15초 동안 휙 → 0.8초 뒤 제자리
            Vector3 target = Time.time < _knockUntil ? _home + _knockOffset : _home;
            Body.position = Vector3.MoveTowards(Body.position, target, Time.deltaTime * 20f);

            if (Time.time < _burnUntil && Time.time >= _burnNext)
            {
                _burnNext += _burnTick;
                TakeDamage(_burnDamage, null);
            }
        }

        // 이름표에 쓸 지금 상태 ("기절 1.2s" 같은 거)
        public string Status
        {
            get
            {
                string text = "";
                if (Time.time < _stunUntil) text += $"기절 {_stunUntil - Time.time:0.0}s  ";
                if (Time.time < _rootUntil) text += $"속박 {_rootUntil - Time.time:0.0}s  ";
                if (Time.time < _slowUntil) text += $"둔화 {_slowPercent:0}% {_slowUntil - Time.time:0.0}s  ";
                if (Time.time < _knockUntil) text += "밀려남  ";
                if (Time.time < _burnUntil) text += $"화상 {_burnUntil - Time.time:0.0}s";
                return text;
            }
        }
    }
}

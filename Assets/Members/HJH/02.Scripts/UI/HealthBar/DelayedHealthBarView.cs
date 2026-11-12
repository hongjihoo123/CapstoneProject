using Assets.Members.HJH._02.Scripts.Char.Health;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI.HealthBar
{
    // 지연 체력바 (Gunfire Reborn / SoD 식). 값은 IHealthSignal 이벤트로만 받고, 여기서는 그리기만 함.
    //  - 피격: 실제 바(fill)는 즉시 줄고, 뒤의 밝은 잔상(damageGhost)이 잠깐 머문 뒤 천천히 따라 내려옴
    //  - 회복: 밝은 예고 바(healGhost)가 회복될 위치까지 먼저 차고, 실제 바가 부드럽게 따라 차오름
    // 적/플레이어 공용. Image 는 전부 Filled(Horizontal) 이고, 뒤→앞 순서: 배경, damageGhost, healGhost, shield, fill
    public sealed class DelayedHealthBarView : MonoBehaviour
    {
        [Header("바 (Filled Image)")]
        [SerializeField] private Image fill;
        [SerializeField] private Image damageGhost;
        [SerializeField] private Image healGhost;
        [SerializeField, Tooltip("없어도 됨. (체력+보호막)/최대 위치까지")] private Image shield;

        [Header("색")]
        [SerializeField] private Color highColor = new(0.32f, 0.82f, 0.38f, 1f);
        [SerializeField] private Color lowColor = new(0.86f, 0.22f, 0.18f, 1f);
        [SerializeField, Tooltip("깎인 양 잔상 색 (밝게)")] private Color damageGhostColor = new(1f, 0.93f, 0.62f, 1f);
        [SerializeField, Tooltip("회복 예고 바 색")] private Color healGhostColor = new(0.78f, 1f, 0.72f, 0.95f);

        [Header("피격 잔상")]
        [SerializeField, Tooltip("잔상이 그 자리에 머무는 시간 (초). 연타 중엔 계속 머묾")] private float damageHold = 0.25f;
        [SerializeField, Tooltip("머문 뒤 실제 바까지 내려오는 시간 (초)")] private float damageDrainTime = 0.3f;
        [SerializeField, Tooltip("잔상이 처음 나타날 때 하얗게 번쩍이는 시간 (초). 0 = 끔")] private float damageFlashTime = 0.08f;

        [Header("회복")]
        [SerializeField, Tooltip("예고 바가 먼저 찬 뒤 실제 바가 따라가기 시작할 때까지 (초)")] private float healHold = 0.15f;
        [SerializeField, Tooltip("실제 바가 예고 위치까지 차오르는 시간 (초)")] private float healFillTime = 0.4f;

        [SerializeField, Tooltip("히트스톱·슬로모션 중에도 같은 속도로 움직이게")] private bool unscaledTime = true;

        private IHealthSignal _signal;
        private float _shown = 1f;        // 실제 바가 지금 그리는 값
        private float _target = 1f;       // 실제 체력 비율
        private float _ghost = 1f;        // 피격 잔상 위치
        private float _ghostFrom = 1f;
        private float _drainAt;           // 이 시각부터 잔상이 내려감
        private float _flashUntil;
        private float _healFrom = 1f;
        private float _healAt;            // 이 시각부터 실제 바가 차오름
        private bool _healing;

        private float Now => unscaledTime ? Time.unscaledTime : Time.time;

        private void OnEnable()
        {
            _signal = GetComponentInParent<IHealthSignal>();
            if (_signal == null)
                return;

            _signal.Changed += OnChanged;
            if (_signal.Last.Max > 0f)
                Snap(_signal.Last.Ratio);
        }

        private void OnDisable()
        {
            if (_signal != null)
                _signal.Changed -= OnChanged;
        }

        private void OnChanged(HealthChange change)
        {
            float ratio = change.Ratio;

            // 처음 붙었거나, 죽었다 다시 살아난(풀에서 재사용) 경우는 연출 없이 맞춤
            bool revived = change.PreviousRatio <= 0f && !change.IsDamage;
            bool sameValue = !change.IsDamage && !change.IsHeal;   // 첫 값, 최대 체력·보호막만 바뀜
            if (revived || sameValue)
            {
                if (revived || Mathf.Abs(_target - ratio) > 0.0005f)
                    Snap(ratio);
                DrawShield(change);
                return;
            }

            if (change.IsDamage)
                OnDamage(ratio);
            else if (change.IsHeal)
                OnHeal(ratio);

            DrawShield(change);
        }

        private void OnDamage(float ratio)
        {
            // 잔상은 지금 보이는 맨 위 위치에서 시작 (회복이 차오르던 중이면 그 위치)
            float top = Mathf.Max(_ghost, _shown);
            if (_ghost <= _shown + 0.0001f)
                _flashUntil = Now + damageFlashTime;

            _healing = false;
            _target = ratio;
            _shown = ratio;               // 실제 바는 즉시
            _ghost = top;
            _ghostFrom = top;
            _drainAt = Now + damageHold;  // 연타면 계속 미뤄져서 깎인 양이 쌓여 보임
        }

        private void OnHeal(float ratio)
        {
            _target = ratio;
            _healFrom = _shown;
            _healAt = Now + healHold;
            _healing = true;
            if (_ghost < ratio)
                _ghost = _shown;          // 잔상이 회복 위치 아래로 숨게
        }

        private void Snap(float ratio)
        {
            _shown = _target = _ghost = _ghostFrom = _healFrom = ratio;
            _healing = false;
            Draw();
        }

        private void LateUpdate()
        {
            float now = Now;

            if (_healing)
            {
                float t = healFillTime > 0f ? Mathf.Clamp01((now - _healAt) / healFillTime) : 1f;
                if (now >= _healAt)
                    _shown = Mathf.Lerp(_healFrom, _target, EaseOutCubic(t));
                if (t >= 1f)
                {
                    _shown = _target;
                    _healing = false;
                }
            }

            if (_ghost > _shown && now >= _drainAt)
            {
                float t = damageDrainTime > 0f ? Mathf.Clamp01((now - _drainAt) / damageDrainTime) : 1f;
                _ghost = Mathf.Lerp(_ghostFrom, _shown, EaseInOutQuad(t));
                if (t >= 1f)
                    _ghost = _shown;
            }
            else if (_ghost < _shown)
            {
                _ghost = _shown;
            }

            Draw();
        }

        private void Draw()
        {
            if (fill != null)
            {
                fill.fillAmount = _shown;
                fill.color = Color.Lerp(lowColor, highColor, _shown);
            }

            if (damageGhost != null)
            {
                damageGhost.fillAmount = _ghost;
                damageGhost.color = Now < _flashUntil ? Color.white : damageGhostColor;
                damageGhost.enabled = _ghost > _shown + 0.0005f;
            }

            if (healGhost != null)
            {
                healGhost.fillAmount = _target;
                healGhost.color = healGhostColor;
                healGhost.enabled = _healing;
            }
        }

        private void DrawShield(HealthChange change)
        {
            if (shield == null)
                return;

            shield.enabled = change.Shield > 0.01f;
            shield.fillAmount = change.Max > 0f ? Mathf.Clamp01((change.Current + change.Shield) / change.Max) : 0f;
        }

        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        private static float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
    }
}

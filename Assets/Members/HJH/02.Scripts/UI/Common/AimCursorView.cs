using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Char.TopDown;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using DG.Tweening;
using Members.KYR._01_Scripts;
using RobotWeapons;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 마우스 조준 커서임 (프리팹 Data/UI/AimCursor, 씬에 놓여 있고 플레이어랑 연결돼 있음)
    //  - 평소: 얇은 링 + 눈금 4개, 총 쏘거나 휘두르거나 스킬 쓰면 눈금이 탁 벌어짐
    //  - 스킬 조준 중: 금색으로 넓어짐, 최대 사거리 넘어가면 빨갛게 흐려짐 (스킬은 사거리 끝에 떨어짐)
    //  - 맞추면 X 표시, 죽이면 더 크고 빨간 X
    // 교체 카드나 툴팁 같은 UI 위로 가면 원래 마우스 커서가 다시 보임 (그건 CursorService 가 관리함)
    //
    // 색이랑 눈금 벌어짐은 매 프레임 목표값 쪽으로 천천히 따라가고,
    // 반동이랑 X 표시는 DOTween 으로 한 번씩 재생하는 거임
    public class AimCursorView : MonoBehaviour
    {
        [SerializeField] private PlayerAgent player;
        [SerializeField] private TopDownAimController aimController;

        [Header("부품")]
        [SerializeField] private RectTransform crosshair;
        [SerializeField] private Image ring;
        [SerializeField] private Image dot;
        [SerializeField, Tooltip("Up, right, down, left.")]
        private Image[] ticks = new Image[4];
        [SerializeField] private RectTransform hitMarker;
        [SerializeField] private Image[] hitMarks;

        [Header("모양")]
        [SerializeField] private float ringSize = 30f;
        [SerializeField] private float aimingRingGrow = 8f;
        [SerializeField] private float tickLength = 8f;
        [SerializeField] private float tickGap = 10f;
        [SerializeField] private float aimingGap = 16f;
        [SerializeField] private float stateFollow = 16f;
        [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color aimingColor = new Color(1f, 0.83f, 0.42f, 1f);
        [SerializeField] private Color outOfRangeColor = new Color(1f, 0.42f, 0.35f, 0.55f);
        [SerializeField, Range(0f, 1f)] private float idleRingAlpha = 0.45f;

        [Header("반동")]
        [SerializeField] private float shotKick = 6f;
        [SerializeField] private float swingKick = 4f;
        [SerializeField] private float skillKick = 12f;
        [SerializeField] private float kickRecover = 0.18f;
        [SerializeField] private Ease kickEase = Ease.OutQuad;

        [Header("맞춤 표시")]
        [SerializeField] private float hitDuration = 0.14f;
        [SerializeField] private float killDuration = 0.3f;
        [SerializeField] private float hitStartScale = 1.25f;
        [SerializeField] private float killScale = 1.5f;
        [SerializeField] private Color hitColor = Color.white;
        [SerializeField] private Color killColor = new Color(1f, 0.3f, 0.25f, 1f);

        private readonly WeaponVisualTracker _weapon = new();
        private readonly RangeProbe _probe = new();
        private SkillStateModule _skills;
        private float _kick;
        private Tween _kickTween;
        private Sequence _hitTween;
        private bool _hitIsKill;
        private Color _color;
        private float _gap;
        private float _aimBlend;
        private bool _shown;

        private void Awake()
        {
            Debug.Assert(player != null, $"{name}: AimCursorView needs the player reference.");
            _color = normalColor;
            _gap = tickGap;
            crosshair.gameObject.SetActive(false);
            if (hitMarker != null)
                hitMarker.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (player != null)
                player.DamageDealt += HandleDamageDealt;
            _weapon.ShotFired += HandleShot;
            _weapon.Swung += HandleSwing;
        }

        private void Start()
        {
            // SkillFsm 은 PlayerAgent.Awake 에서 만들어져서 Start 때는 이미 있음
            _skills = player != null ? player.SkillFsm : null;
            if (_skills != null)
                _skills.SkillUsed += HandleSkillUsed;
        }

        private void OnDisable()
        {
            if (player != null)
                player.DamageDealt -= HandleDamageDealt;
            _weapon.ShotFired -= HandleShot;
            _weapon.Swung -= HandleSwing;
            _weapon.Clear();
            SetShown(false);
        }

        private void OnDestroy()
        {
            if (_skills != null)
                _skills.SkillUsed -= HandleSkillUsed;
        }

        private void HandleShot(ShotVisual shot) => Kick(shotKick);
        private void HandleSwing(SwingVisual swing) => Kick(swingKick);
        private void HandleSkillUsed(SkillUsedInfo info) => Kick(skillKick);

        // 반동은 겹쳐서 계속 커지진 않음, 새로 쏘면 회복만 처음부터 다시 시작
        private void Kick(float amount)
        {
            _kickTween?.Kill();
            _kick = Mathf.Max(_kick, amount);
            _kickTween = DOTween.To(() => _kick, value => _kick = value, 0f, kickRecover)
                .SetEase(kickEase)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void HandleDamageDealt(DamageDealtInfo info)
        {
            if (hitMarker == null)
                return;
            // 죽였을 때 X 는 같은 순간 들어온 일반 타격 X 로 안 덮어씀
            if (_hitTween != null && _hitIsKill && !info.Killed)
                return;

            _hitIsKill = info.Killed;
            float duration = _hitIsKill ? killDuration : hitDuration;
            float size = _hitIsKill ? killScale : 1f;
            Color color = _hitIsKill ? killColor : hitColor;

            _hitTween?.Kill();
            hitMarker.gameObject.SetActive(true);
            hitMarker.localScale = Vector3.one * (size * hitStartScale);
            _hitTween = DOTween.Sequence().Join(hitMarker.DOScale(size, duration).SetEase(Ease.OutQuad));
            foreach (Image mark in hitMarks)
            {
                mark.color = color;
                _hitTween.Join(mark.DOFade(0f, duration).SetEase(Ease.InQuad));
            }

            _hitTween.SetUpdate(true)
                .SetLink(gameObject)
                .OnKill(() =>
                {
                    _hitTween = null;
                    if (hitMarker != null)
                        hitMarker.gameObject.SetActive(false);
                });
        }

        private void LateUpdate()
        {
            _weapon.Track(player);

            Mouse mouse = Mouse.current;
            bool alive = player == null || player.IsAlive;
            bool overUi = UiFocusService.IsUiFocused
                          || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
            SetShown(mouse != null && alive && !overUi && Application.isFocused);
            if (!_shown)
                return;

            float follow = 1f - Mathf.Exp(-stateFollow * Time.unscaledDeltaTime);
            crosshair.position = mouse.position.ReadValue();

            bool aiming = _skills != null && _skills.IsAimingSkill;
            Color target = aiming && IsPastRange() ? outOfRangeColor : aiming ? aimingColor : normalColor;
            _color = Color.Lerp(_color, target, follow);
            _gap = Mathf.Lerp(_gap, aiming ? aimingGap : tickGap, follow);
            _aimBlend = Mathf.Lerp(_aimBlend, aiming ? 1f : 0f, follow);

            float gap = _gap + _kick;
            for (int i = 0; i < ticks.Length; i++)
            {
                RectTransform tick = ticks[i].rectTransform;
                tick.anchoredPosition = Direction(i) * (gap + tickLength * 0.5f);
                ticks[i].color = _color;
            }

            ring.color = new Color(_color.r, _color.g, _color.b, _color.a * Mathf.Lerp(idleRingAlpha, 0.9f, _aimBlend));
            ring.rectTransform.sizeDelta = Vector2.one * (ringSize + _kick * 1.5f + aimingRingGrow * _aimBlend);
            dot.color = _color;
        }

        private void SetShown(bool shown)
        {
            if (_shown == shown)
                return;

            _shown = shown;
            if (crosshair != null)
                crosshair.gameObject.SetActive(shown);
            CursorService.SetHidden(this, shown);
        }

        // 조준 미리보기가 스킬 최대 사거리를 알려줌, 그 밖이면 스킬은 사거리 끝에 떨어짐
        private bool IsPastRange()
        {
            _probe.Reset();
            _skills.DescribeAimPreview(_probe);
            if (!_probe.HasRange || aimController == null)
                return false;

            Vector3 delta = aimController.AimPoint - _probe.Center;
            delta.y = 0f;
            return delta.magnitude > _probe.Radius + 0.05f;
        }

        private static Vector2 Direction(int i) => i switch
        {
            0 => Vector2.up,
            1 => Vector2.right,
            2 => Vector2.down,
            _ => Vector2.left,
        };

        // 사거리 원만 기록하고 나머지 미리보기 호출은 무시함
        private sealed class RangeProbe : ISkillPreview
        {
            public bool HasRange { get; private set; }
            public Vector3 Center { get; private set; }
            public float Radius { get; private set; }

            public void Reset() => HasRange = false;

            public void RangeCircle(Vector3 center, float radius)
            {
                HasRange = true;
                Center = center;
                Radius = radius;
            }

            public void Circle(Vector3 center, float radius) { }
            public void Path(Vector3 from, Vector3 to, float width) { }
            public void DirectionLine(Vector3 from, Vector3 to) { }
        }
    }
}

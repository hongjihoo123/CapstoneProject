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
    // Top-down crosshair (prefab Data/UI/AimCursor, placed in the scene, wired to the player).
    //  - normal: thin ring + 4 ticks; ticks kick out on every real shot / swing / skill cast
    //  - aiming a skill: gold and wider; dimmed red past the skill's max range (it lands at the edge)
    //  - hitmarker X on every hit, bigger and red on a kill
    // The system cursor comes back over UI (swap cards, tooltips); CursorService owns its visibility.
    //
    // Colour and tick spread follow the current state every frame (Lerp toward a moving target);
    // kicks and hitmarkers are one-shot DOTween tweens.
    public class AimCursorView : MonoBehaviour
    {
        [SerializeField] private PlayerAgent player;
        [SerializeField] private TopDownAimController aimController;

        [Header("Parts")]
        [SerializeField] private RectTransform crosshair;
        [SerializeField] private Image ring;
        [SerializeField] private Image dot;
        [SerializeField, Tooltip("Up, right, down, left.")]
        private Image[] ticks = new Image[4];
        [SerializeField] private RectTransform hitMarker;
        [SerializeField] private Image[] hitMarks;

        [Header("Look")]
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

        [Header("Kick")]
        [SerializeField] private float shotKick = 6f;
        [SerializeField] private float swingKick = 4f;
        [SerializeField] private float skillKick = 12f;
        [SerializeField] private float kickRecover = 0.18f;
        [SerializeField] private Ease kickEase = Ease.OutQuad;

        [Header("Hitmarker")]
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
            // SkillFsm is created in PlayerAgent.Awake, so it is ready by Start.
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

        // Kicks never stack past the strongest one in flight; each new kick restarts the recovery.
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
            // A kill marker is not overwritten by the plain hits that land in the same moment.
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

        // The aim preview reports the skill's max range; past it the skill is clamped to the edge.
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

        // Records only the range circle; every other preview call is ignored.
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

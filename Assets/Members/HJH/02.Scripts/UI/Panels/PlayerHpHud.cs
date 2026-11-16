using Assets.Members.HJH._02.Scripts.Char.Health;
using Members.KYR._01.Scripts.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 좌상단 플레이어 체력바 (기울어진 얇은 유리 바). 체력은 HealthSignal 이 알려주는 값만 읽음.
    // 깎이면 연한 잔상이 늦게 따라 내려옴. 모양은 코드로 만들어서 프리팹엔 빈 오브젝트 하나만 있으면 됨.
    public class PlayerHpHud : MonoBehaviour
    {
        [SerializeField] private Vector2 size = new(350f, 50f);
        [SerializeField, Min(0.01f)] private float trailSpeed = 0.5f;
        [SerializeField] private float trailDelay = 0.35f;

        private Image _fill;
        private Image _trail;
        private TMP_Text _label;
        private IHealthSignal _signal;
        private float _ratio = 1f;
        private float _trailRatio = 1f;
        private float _holdUntil;

        private void Awake()
        {
            var rect = (RectTransform)transform;
            HudUiKit.Place(rect, new Vector2(0f, 1f), new Vector2(34f, -28f), size);

            Image back = HudUiKit.NewImage("Back", transform, HudUiKit.Load("hpbar_slant_bg"), Color.white);
            HudUiKit.Stretch(back.rectTransform);
            _trail = HudUiKit.NewImage("Trail", transform, HudUiKit.Load("hpbar_slant_fill"), new Color(1f, 1f, 1f, 0.35f));
            HudUiKit.Stretch(_trail.rectTransform);
            _fill = HudUiKit.NewImage("Fill", transform, HudUiKit.Load("hpbar_slant_fill"), Color.white);
            HudUiKit.Stretch(_fill.rectTransform);
            foreach (Image bar in new[] { _trail, _fill })
            {
                bar.type = Image.Type.Filled;
                bar.fillMethod = Image.FillMethod.Horizontal;
            }

            Image frame = HudUiKit.NewImage("Frame", transform, HudUiKit.Load("hpbar_slant_frame"), Color.white);
            HudUiKit.Stretch(frame.rectTransform);

            _label = HudUiKit.NewText("Label", transform, string.Empty, 20f, HudUiKit.Value, TextAlignmentOptions.MidlineLeft);
            HudUiKit.Stretch(_label.rectTransform);
            _label.rectTransform.offsetMin = new Vector2(28f, 0f);
        }

        private void Start() => TryBind();

        private void OnDestroy()
        {
            if (_signal != null)
                _signal.Changed -= OnChanged;
        }

        private void TryBind()
        {
            if (_signal != null)
                return;

            PlayerAgent agent = FindFirstObjectByType<PlayerAgent>();
            _signal = agent != null ? agent.GetComponentInChildren<HealthSignal>(true) : null;
            if (_signal == null)
                return;

            _signal.Changed += OnChanged;
            if (_signal.Last.Max > 0f)
                OnChanged(_signal.Last);
        }

        private void OnChanged(HealthChange change)
        {
            _ratio = change.Ratio;
            if (change.IsDamage)
                _holdUntil = Time.unscaledTime + trailDelay;
            else
                _trailRatio = Mathf.Max(_trailRatio, _ratio);

            if (_label != null)
                _label.text = $"{Mathf.CeilToInt(change.Current)}/{Mathf.CeilToInt(change.Max)}";
        }

        private void Update()
        {
            if (_signal == null)
                TryBind();

            if (_fill == null)
                return;

            _fill.fillAmount = _ratio;
            if (Time.unscaledTime >= _holdUntil && _trailRatio > _ratio)
                _trailRatio = Mathf.Max(_ratio, _trailRatio - trailSpeed * Time.unscaledDeltaTime);
            _trail.fillAmount = _trailRatio;
        }
    }
}

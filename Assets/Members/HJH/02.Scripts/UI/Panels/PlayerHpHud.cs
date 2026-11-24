using Assets.Members.HJH._02.Scripts.Char.Health;
using Members.KYR._01.Scripts.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 좌상단 플레이어 체력바 (기울어진 얇은 유리 바). 체력은 HealthSignal 이 알려주는 값만 읽음.
    // 깎이면 연한 잔상이 늦게 따라 내려옴. 모양은 하이어라키에 저장된 자식 그대로 (인스펙터에서 수정)
    public class PlayerHpHud : MonoBehaviour, IUiBake
    {
        [SerializeField, Min(0.01f)] private float trailSpeed = 0.5f;
        [SerializeField] private float trailDelay = 0.35f;
        [SerializeField] private Image fill;
        [SerializeField] private Image trail;
        [SerializeField] private TMP_Text label;

        private IHealthSignal _signal;
        private float _ratio = 1f;
        private float _trailRatio = 1f;
        private float _holdUntil;

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

            if (label != null)
                label.text = $"{Mathf.CeilToInt(change.Current)}/{Mathf.CeilToInt(change.Max)}";
        }

        private void Update()
        {
            if (_signal == null)
                TryBind();

            if (fill == null || trail == null)
                return;

            fill.fillAmount = _ratio;
            if (Time.unscaledTime >= _holdUntil && _trailRatio > _ratio)
                _trailRatio = Mathf.Max(_ratio, _trailRatio - trailSpeed * Time.unscaledDeltaTime);
            trail.fillAmount = _trailRatio;
        }

        [ContextMenu("레이아웃 굽기 (자식 다시 만들기)")]
        public void BakeLayout()
        {
#if UNITY_EDITOR
            HudUiKit.ClearChildren(transform);
            HudUiKit.Place((RectTransform)transform, new Vector2(0f, 1f), new Vector2(34f, -28f), new Vector2(350f, 50f));

            Image back = HudUiKit.NewImage("Back", transform, HudUiKit.Load("hpbar_slant_bg"), Color.white);
            HudUiKit.Stretch(back.rectTransform);
            trail = HudUiKit.NewImage("Trail", transform, HudUiKit.Load("hpbar_slant_fill"), new Color(1f, 1f, 1f, 0.35f));
            HudUiKit.Stretch(trail.rectTransform);
            fill = HudUiKit.NewImage("Fill", transform, HudUiKit.Load("hpbar_slant_fill"), Color.white);
            HudUiKit.Stretch(fill.rectTransform);
            foreach (Image bar in new[] { trail, fill })
            {
                bar.type = Image.Type.Filled;
                bar.fillMethod = Image.FillMethod.Horizontal;
            }

            Image frame = HudUiKit.NewImage("Frame", transform, HudUiKit.Load("hpbar_slant_frame"), Color.white);
            HudUiKit.Stretch(frame.rectTransform);

            label = HudUiKit.NewText("Label", transform, "100/100", 20f, HudUiKit.Value, TextAlignmentOptions.MidlineLeft);
            HudUiKit.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(28f, 0f);
#endif
        }
    }
}

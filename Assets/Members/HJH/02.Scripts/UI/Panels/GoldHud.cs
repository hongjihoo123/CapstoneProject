using Members.KYR._01.Scripts.Currency;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 골드 표시: 왼쪽 아이콘, 오른쪽 숫자. 박스 없음. 값은 CurrencyService(RunGold) 에서 받음.
    // 예전 RunGold 의 판(GoldPlate) 대신 이걸 씀 (골드 지급 로직 RunGoldGrant 는 그대로 둠).
    // 모양은 하이어라키에 저장된 자식 그대로 (인스펙터에서 수정). 처음 만들 때만 ⋮ > 레이아웃 굽기
    public class GoldHud : MonoBehaviour, IUiBake
    {
        [SerializeField] private CurrencyType type = CurrencyType.RunGold;
        [SerializeField] private TMP_Text text;
        [SerializeField, Tooltip("값이 바뀔 때 톡 커지는 묶음 (아이콘+숫자)")] private RectTransform pop;

        private CurrencyService _service;
        private float _popUntil;

        private void OnEnable() => TryBind();

        private void OnDisable()
        {
            if (_service != null)
                _service.OnChanged -= OnChanged;
            _service = null;
        }

        private void Update()
        {
            if (_service == null)
                TryBind();

            // 값이 바뀔 때 숫자가 톡 커졌다 돌아옴
            if (pop != null)
            {
                float t = Mathf.Clamp01((_popUntil - Time.unscaledTime) / 0.2f);
                pop.localScale = Vector3.one * (1f + 0.15f * t);
            }
        }

        private void TryBind()
        {
            CurrencyService service = GameBootstrap.Currency;
            if (service == null || _service == service || text == null)
                return;

            _service = service;
            _service.OnChanged += OnChanged;
            text.text = _service.Get(type).ToString();
        }

        private void OnChanged(CurrencyType changed, int value)
        {
            if (changed != type)
                return;

            text.text = value.ToString();
            _popUntil = Time.unscaledTime + 0.2f;
        }

        [ContextMenu("레이아웃 굽기 (자식 다시 만들기)")]
        public void BakeLayout()
        {
#if UNITY_EDITOR
            HudUiKit.ClearChildren(transform);
            HudUiKit.Place((RectTransform)transform, new Vector2(0f, 1f), new Vector2(34f, -88f), new Vector2(240f, 44f));

            pop = HudUiKit.NewRect("Pop", transform);
            HudUiKit.Stretch(pop);

            Image icon = HudUiKit.NewImage("Icon", pop, HudUiKit.Load("gold_icon"), Color.white);
            HudUiKit.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(40f, 40f));

            text = HudUiKit.NewText("Text", pop, "0", 30f, new Color(1f, 0.86f, 0.35f, 1f), TextAlignmentOptions.MidlineLeft);
            HudUiKit.Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(54f, 0f), new Vector2(180f, 44f));
            text.fontStyle = FontStyles.Bold;
#endif
        }
    }
}

using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 화면 위쪽 가운데 안내 한 줄. 모양은 프리팹(05.Data/Prefab/UI/ScreenNotice, PortalNotice)에서 수정.
    //  - Show: 잠깐 띄우고 사라짐 (상점 "기회를 소진하셨습니다." 등)
    //  - Hold: 매 프레임 부르는 동안 계속 보임 (포탈 근처 "아직 활성화되지 않았습니다")
    // 쓰는 쪽은 프리팹을 인스펙터로 받아서 ScreenNotice.Show(prefab, ...) 또는 Instantiate 해서 씀
    public class ScreenNotice : MonoBehaviour, IUiBake
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text text;
        [SerializeField, Min(0.1f)] private float fadeInSpeed = 6f;
        [SerializeField, Min(0.1f)] private float fadeOutSpeed = 2.5f;

        private static ScreenNotice _shared;
        private float _until;

        // 같은 프리팹으로 씬에 하나만 만들어 재사용
        public static void Show(ScreenNotice prefab, string message, Color? color = null, float seconds = 1.8f)
        {
            if (_shared == null)
            {
                if (prefab == null)
                {
                    Debug.LogWarning("[ScreenNotice] 프리팹이 연결되지 않았습니다: " + message);
                    return;
                }
                _shared = Instantiate(prefab);
            }
            _shared.ShowText(message, color, seconds);
            _shared.Punch();
        }

        public void ShowText(string message, Color? color = null, float seconds = 1.8f)
        {
            text.text = message;
            if (color.HasValue)
                text.color = color.Value;
            _until = Time.unscaledTime + seconds;
        }

        public void Hold(string message, Color color)
        {
            text.text = message;
            text.color = color;
            _until = Mathf.Max(_until, Time.unscaledTime + 0.15f);
        }

        public void Punch()
        {
            Transform target = group.transform;
            target.DOKill(true);
            target.DOPunchScale(Vector3.one * 0.1f, 0.25f, 6, 0.6f).SetUpdate(true);
        }

        private void Awake()
        {
            if (group != null)
                group.alpha = 0f;
        }

        private void Update()
        {
            if (group == null)
                return;
            bool visible = Time.unscaledTime < _until;
            group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * (visible ? fadeInSpeed : fadeOutSpeed));
        }

        private void OnDestroy()
        {
            if (_shared == this)
                _shared = null;
        }

        [ContextMenu("레이아웃 굽기 (자식 다시 만들기)")]
        public void BakeLayout()
        {
#if UNITY_EDITOR
            HudUiKit.ClearChildren(transform);
            var canvas = GetComponent<Canvas>() ? GetComponent<Canvas>() : gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 950;   // HUD 위, 바인딩 창(1000) 아래
            var scaler = GetComponent<UnityEngine.UI.CanvasScaler>() ? GetComponent<UnityEngine.UI.CanvasScaler>() : gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 상단 시간/스테이지 표시 바로 아래
            RectTransform rect = HudUiKit.NewRect("Notice", transform);
            HudUiKit.Place(rect, new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(700f, 72f));
            group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            text = HudUiKit.NewText("Text", rect, "안내 문구", 26f, HudUiKit.Title, TextAlignmentOptions.Top);
            text.richText = true;
            HudUiKit.Stretch(text.rectTransform);
#endif
        }
    }
}

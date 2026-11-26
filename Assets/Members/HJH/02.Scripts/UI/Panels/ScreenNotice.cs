using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.UI
{
    public class ScreenNotice : MonoBehaviour, IUiBake
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text text;
        [SerializeField, Min(0.1f)] private float fadeInSpeed = 6f;
        [SerializeField, Min(0.1f)] private float fadeOutSpeed = 2.5f;

        private static ScreenNotice _shared;
        private float _until;

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
            canvas.sortingOrder = 950;
            var scaler = GetComponent<UnityEngine.UI.CanvasScaler>() ? GetComponent<UnityEngine.UI.CanvasScaler>() : gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform rect = HudUiKit.NewRect("Notice", transform);
            HudUiKit.Place(rect, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(700f, 72f));
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

using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Members.JJH._02_Scripts.Augments
{
    public class AugmentUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Button button;

        public AugmentTrackSO Track { get; private set; }
        public int NextTier { get; private set; }
        public RectTransform RectTransform { get; private set; }

        private Action<AugmentUI> onClickCallback;
        private CanvasGroup canvasGroup;

        private void Awake()
        {
            RectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void Setup(AugmentTrackSO track, int nextTier, Action<AugmentUI> onClick)
        {
            Track = track;
            NextTier = nextTier;
            onClickCallback = onClick;

            AugmentTierInfo info = track.GetTierInfo(nextTier);

            if (iconImage != null)
                iconImage.sprite = track.icon;
            if (nameText != null)
                nameText.text = $"{track.trackName} Lv.{nextTier}";
            if (descriptionText != null)
                descriptionText.text = info != null ? info.description : string.Empty;

            canvasGroup.alpha = 1f;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleClick);
        }

        private void HandleClick()
        {
            onClickCallback?.Invoke(this);
        }

        public void SetInteractable(bool value)
        {
            if (button != null) button.interactable = value;
        }

        public void PlayAppear(Vector2 targetPos, float duration, float delay, Ease ease = Ease.OutBack)
        {
            RectTransform.DOAnchorPos(targetPos, duration)
                .SetDelay(delay)
                .SetEase(ease);
        }

        public void PlaySelected(Vector2 centerPos, float moveDuration, float scaleMultiplier, float fadeDuration, Action onComplete)
        {
            SetInteractable(false);

            Sequence seq = DOTween.Sequence();
            seq.Append(RectTransform.DOAnchorPos(centerPos, moveDuration).SetEase(Ease.OutCubic));
            seq.Join(RectTransform.DOScale(Vector3.one * scaleMultiplier, moveDuration).SetEase(Ease.OutCubic));
            seq.AppendInterval(0.15f);
            seq.Append(canvasGroup.DOFade(0f, fadeDuration));
            seq.Join(RectTransform.DOScale(Vector3.one * scaleMultiplier * 1.2f, fadeDuration));

            seq.OnComplete(() =>
            {
                onComplete?.Invoke();
                Destroy(gameObject);
            });
        }

        public void PlayDismiss(Vector2 offScreenPos, float duration, Action onComplete = null)
        {
            SetInteractable(false);

            Sequence seq = DOTween.Sequence();
            seq.Append(RectTransform.DOAnchorPos(offScreenPos, duration).SetEase(Ease.InCubic));
            seq.Join(canvasGroup.DOFade(0f, duration));

            seq.OnComplete(() =>
            {
                onComplete?.Invoke();
                Destroy(gameObject);
            });
        }
    }
}

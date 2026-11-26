using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    public class TitleAmbience : MonoBehaviour
    {
        [Header("반딧불이")]
        [SerializeField] private RectTransform[] fireflies;
        [SerializeField, Min(0f)] private float fireflyRange = 70f;
        [SerializeField, Min(0.1f)] private float fireflyTime = 4f;

        [Header("안개 (좌우로 천천히 흐름)")]
        [SerializeField] private RectTransform[] mists;
        [SerializeField, Min(0f)] private float mistDrift = 90f;
        [SerializeField, Min(0.1f)] private float mistTime = 14f;

        [Header("빛 숨쉬기")]
        [SerializeField] private Image[] glows;
        [SerializeField, Range(0f, 1f)] private float glowDip = 0.35f;
        [SerializeField, Min(0.1f)] private float glowTime = 3.5f;

        private void Start()
        {
            foreach (RectTransform fly in fireflies)
                if (fly != null)
                    Wander(fly);

            for (int i = 0; i < mists.Length; i++)
            {
                RectTransform mist = mists[i];
                if (mist == null)
                    continue;
                float dir = i % 2 == 0 ? 1f : -1f;
                Vector2 from = mist.anchoredPosition - new Vector2(mistDrift * dir, 0f);
                mist.anchoredPosition = from;
                mist.DOAnchorPosX(from.x + 2f * mistDrift * dir, mistTime * Random.Range(0.8f, 1.2f))
                    .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(mist.gameObject);
            }

            foreach (Image glow in glows)
            {
                if (glow == null)
                    continue;
                float alpha = glow.color.a;
                glow.DOFade(alpha * (1f - glowDip), glowTime * Random.Range(0.8f, 1.2f))
                    .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(glow.gameObject);
            }
        }

        private void Wander(RectTransform fly)
        {
            Image image = fly.GetComponent<Image>();
            Vector2 home = fly.anchoredPosition;
            if (image != null)
            {
                float alpha = image.color.a;
                image.DOFade(alpha * 0.15f, Random.Range(0.8f, 1.8f))
                    .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(fly.gameObject);
            }
            Roam(fly, home, fireflyTime * Random.Range(0.7f, 1.3f));
        }

        private void Roam(RectTransform fly, Vector2 home, float time)
        {
            fly.DOAnchorPos(home + Random.insideUnitCircle * fireflyRange, time)
                .SetEase(Ease.InOutSine).SetUpdate(true).SetLink(fly.gameObject)
                .OnComplete(() => Roam(fly, home, time));
        }
    }
}

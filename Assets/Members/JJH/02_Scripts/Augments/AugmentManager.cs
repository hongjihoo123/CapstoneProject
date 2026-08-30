using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Members.JJH._02_Scripts.Augments
{
    public class AugmentManager : MonoBehaviour
    {
        [Header("Augments")]
        [SerializeField] private List<AugmentSO> allAugments = new List<AugmentSO>();

        [Header("UI")]
        [SerializeField] private AugmentUI augmentUIPrefab;
        [SerializeField] private RectTransform augmentParent;
        [SerializeField] private RectTransform[] slotAnchors = new RectTransform[3];

        [Header("Animation Setting")]
        [SerializeField] private float appearDuration = 0.5f;
        [SerializeField] private float appearStagger = 0.1f;
        [SerializeField] private float selectMoveDuration = 0.4f;
        [SerializeField] private float selectedScale = 1.4f;
        [SerializeField] private float selectedFadeDuration = 0.3f;
        [SerializeField] private float dismissDuration = 0.4f;
        [SerializeField] private float screenOffsetY = 1200f;

        private readonly List<AugmentUI> currentAugmentUIs = new List<AugmentUI>();

        public event Action<AugmentSO> OnAugmentChosen;

        private void Start()
        {
            ShowRandomAugments();
        }

        public void ShowRandomAugments()
        {
            ClearCurrentAugments();

            List<AugmentSO> picked = PickRandomAugments(3);

            for (int i = 0; i < picked.Count; i++)
            {
                AugmentUI ui = Instantiate(augmentUIPrefab, augmentParent);
                ui.Setup(picked[i], OnAugmentClicked);

                RectTransform targetRect = slotAnchors[i];
                Vector2 targetPos = targetRect.anchoredPosition;

                Vector2 startPos = targetPos + Vector2.down * screenOffsetY;
                ui.RectTransform.anchoredPosition = startPos;
                ui.RectTransform.localScale = Vector3.one;

                ui.PlayAppear(targetPos, appearDuration, i * appearStagger);

                currentAugmentUIs.Add(ui);
            }
        }

        private List<AugmentSO> PickRandomAugments(int count)
        {
            return allAugments
                .OrderBy(_ => UnityEngine.Random.value)
                .Take(Mathf.Min(count, allAugments.Count))
                .ToList();
        }

        private void OnAugmentClicked(AugmentUI clicked)
        {
            foreach (var ui in currentAugmentUIs)
                ui.SetInteractable(false);

            Vector2 centerPos = Vector2.zero;

            foreach (var ui in currentAugmentUIs)
            {
                if (ui == clicked)
                {
                    ui.PlaySelected(centerPos, selectMoveDuration, selectedScale, selectedFadeDuration, () =>
                    {
                        OnAugmentChosen?.Invoke(clicked.Data);
                    });
                }
                else
                {
                    Vector2 currentPos = ui.RectTransform.anchoredPosition;
                    Vector2 offScreenPos = currentPos + Vector2.down * screenOffsetY;
                    ui.PlayDismiss(offScreenPos, dismissDuration);
                }
            }

            currentAugmentUIs.Clear();
        }

        private void ClearCurrentAugments()
        {
            foreach (var ui in currentAugmentUIs)
            {
                if (ui != null) Destroy(ui.gameObject);
            }
            currentAugmentUIs.Clear();
        }
    }
}
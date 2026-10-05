using DG.Tweening;
using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Members.JJH._02_Scripts.ElementsSystem
{
    public class ElementStackView : MonoBehaviour
    {
        [SerializeField] private EventChannelSO systemChannel;

        [Header("Objects")]
        [SerializeField] private Image[] slotImages;
        [SerializeField] private Slider timerSlider;

        [Header("Color")]
        [Tooltip("ElementType 순서: Fire, Water, Wind, Electric, Earth")]
        [SerializeField]
        private Color[] elementColors =
        {
            new Color(1f, 0.35f, 0.2f),   // Fire
            new Color(0.25f, 0.55f, 1f),  // Water
            new Color(0.5f, 1f, 0.7f),    // Wind
            new Color(1f, 0.9f, 0.2f),    // Electric
            new Color(0.6f, 0.4f, 0.2f)   // Earth
        };
        [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.15f);

        [Header("Setting")]
        [Tooltip("발동 직후 완성된 스택을 잠깐 보여주는 시간"), SerializeField]
        private float clearDelay = 0.35f;
        [Tooltip("발동 연출 시 슬롯이 커지는 배율"), SerializeField]
        private float clearScale = 1.3f;

        private float _duration;
        private float _remaining;
        private int _filledCount;
        private Coroutine _clearRoutine;

        private void Awake()
        {
            if (timerSlider == null)
                return;

            timerSlider.minValue = 0f;
            timerSlider.wholeNumbers = false;
            timerSlider.interactable = false;
        }

        private void OnEnable()
        {
            systemChannel.AddListener<ElementStackChangedEvent>(HandleStackChanged);
            ClearSlots();
            SetTimer(0f);
        }

        private void OnDisable()
        {
            systemChannel.RemoveListener<ElementStackChangedEvent>(HandleStackChanged);
            _clearRoutine = null;
            ResetSlotScale();
        }

        private void Update()
        {
            if (_duration <= 0f)
                return;

            _remaining -= Time.deltaTime;
            SetTimer(_remaining);
        }

        private void HandleStackChanged(ElementStackChangedEvent evt)
        {
            if (evt.Stacks.Count == 0)
            {
                _duration = 0f;
                SetTimer(0f);
                if (_clearRoutine == null)
                {
                    PlayClearEffect();
                    _clearRoutine = StartCoroutine(ClearAfterDelay());
                }
                return;
            }

            if (_clearRoutine != null)
            {
                StopCoroutine(_clearRoutine);
                _clearRoutine = null;
                ResetSlotScale();
            }

            Render(evt.Stacks);

            _duration = evt.TimerDuration;
            _remaining = evt.TimerDuration;

            if (timerSlider != null && _duration > 0f)
                timerSlider.maxValue = _duration;

            SetTimer(_remaining);
        }

        private void PlayClearEffect()
        {
            for (int i = 0; i < _filledCount; i++)
            {
                RectTransform rt = slotImages[i].rectTransform;
                rt.DOKill();
                rt.localScale = Vector3.one;
                rt.DOScale(clearScale, clearDelay).SetEase(Ease.OutQuad);
            }
        }

        private IEnumerator ClearAfterDelay()
        {
            yield return new WaitForSeconds(clearDelay);
            ClearSlots();
            _clearRoutine = null;
        }

        private void Render(IReadOnlyList<ElementType> stacks)
        {
            _filledCount = Mathf.Min(stacks.Count, slotImages.Length);

            for (int i = 0; i < slotImages.Length; i++)
            {
                slotImages[i].color = i < stacks.Count ? elementColors[(int)stacks[i]] : emptyColor;
            }
        }

        private void ClearSlots()
        {
            _filledCount = 0;
            ResetSlotScale();

            foreach (Image slot in slotImages)
                slot.color = emptyColor;
        }

        private void ResetSlotScale()
        {
            foreach (Image slot in slotImages)
            {
                RectTransform rt = slot.rectTransform;
                rt.DOKill();
                rt.localScale = Vector3.one;
            }
        }

        private void SetTimer(float remainingSeconds)
        {
            if (timerSlider != null)
                timerSlider.value = Mathf.Max(0f, remainingSeconds);
        }
    }
}
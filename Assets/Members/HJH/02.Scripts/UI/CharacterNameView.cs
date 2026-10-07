using Assets.Members.HJH._02.Scripts.Char.Character;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // Top-left character plate: the current character's name in its theme color over an ornament line,
    // with the control hint underneath. Swapping characters slides the name in.
    public class CharacterNameView : MonoBehaviour
    {
        [SerializeField] private CharacterSwitcher switcher;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image accent;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private float swapDuration = 0.25f;
        [SerializeField] private float swapTravel = 24f;

        private Vector2 _nameHome;
        private Sequence _swap;

        private void Awake() => _nameHome = nameText.rectTransform.anchoredPosition;

        private void OnEnable() => switcher.CharacterChanged += HandleChanged;

        private void OnDisable() => switcher.CharacterChanged -= HandleChanged;

        // The switcher picks its first character in Start, possibly before this subscribed.
        private void Start()
        {
            if (switcher.Current != null)
                Show(switcher.Current, animate: false);
        }

        private void HandleChanged(CharacterData character) => Show(character, animate: true);

        private void Show(CharacterData character, bool animate)
        {
            nameText.text = character.DisplayName;
            nameText.color = character.ThemeColor;
            if (accent != null)
                accent.color = new Color(character.ThemeColor.r, character.ThemeColor.g, character.ThemeColor.b, 0.8f);
            if (hintText != null)
                hintText.text = $"[1~{switcher.Roster.Length}] 캐릭터 변경   [T] 더미 리셋";

            // Slides in from the left while fading in.
            _swap?.Kill();
            RectTransform rect = nameText.rectTransform;
            rect.anchoredPosition = _nameHome;
            nameText.alpha = 1f;
            if (!animate)
                return;

            rect.anchoredPosition = _nameHome + Vector2.left * swapTravel;
            nameText.alpha = 0f;
            _swap = DOTween.Sequence()
                .Join(rect.DOAnchorPos(_nameHome, swapDuration).SetEase(Ease.OutQuad))
                .Join(nameText.DOFade(1f, swapDuration).SetEase(Ease.OutQuad))
                .OnKill(() => _swap = null)
                .SetUpdate(true)
                .SetLink(gameObject);
        }
    }
}

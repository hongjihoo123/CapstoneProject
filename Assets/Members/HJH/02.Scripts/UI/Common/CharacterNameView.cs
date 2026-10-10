using Assets.Members.HJH._02.Scripts.Char.Character;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 왼쪽 위 캐릭터 이름판. 지금 캐릭터 이름을 그 캐릭터 색으로 + 밑에 조작 안내
    // 캐릭터 바꾸면(1/2/3) 이름이 옆에서 스르륵 들어옴
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

        // CharacterSwitcher 가 Start 에서 첫 캐릭터를 고르는데, 그게 여기 구독보다 먼저일 수도 있어서
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

            // 왼쪽에서 밀려 들어오면서 서서히 나타남
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

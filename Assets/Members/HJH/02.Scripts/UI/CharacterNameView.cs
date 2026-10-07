using Assets.Members.HJH._02.Scripts.Char.Character;
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
        private float _swapTime = -1f;

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

            _swapTime = animate ? 0f : -1f;
            if (!animate)
                Pose(1f);
        }

        private void Update()
        {
            if (_swapTime < 0f)
                return;

            _swapTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_swapTime / swapDuration);
            Pose(1f - (1f - t) * (1f - t));
            if (t >= 1f)
                _swapTime = -1f;
        }

        private void Pose(float t)
        {
            nameText.rectTransform.anchoredPosition = _nameHome + Vector2.left * (swapTravel * (1f - t));
            nameText.alpha = t;
        }
    }
}

using Assets.Members.HJH._02.Scripts.Hud;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    public class PlayerPortraitHud : MonoBehaviour
    {
        [SerializeField] private Image portrait;
        [SerializeField, Tooltip("캐릭터 테마 색으로 칠할 테두리 (비워도 됨)")] private Image frame;
        [SerializeField, Range(0f, 1f)] private float frameAlpha = 0.9f;

        private Object _shown;

        private void Update()
        {
            PlayerHudSource hud = PlayerHudSource.Instance;
            var character = hud != null ? hud.Character : null;
            if (character == _shown)
                return;
            _shown = character;

            if (portrait != null)
            {
                portrait.sprite = character != null ? character.Portrait : null;
                portrait.enabled = portrait.sprite != null;
            }
            if (frame != null && character != null)
            {
                Color c = character.ThemeColor;
                frame.color = new Color(c.r, c.g, c.b, frameAlpha);
            }
        }
    }
}

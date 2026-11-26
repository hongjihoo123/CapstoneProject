using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Members.KYR._01.Scripts.SkillUpgrade.UI
{
    public class SkillUpgradeRow : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text keyLabel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text effectLabel;
        [SerializeField] private TMP_Text costLabel;

        [Header("색")]
        [SerializeField] private Color normalColor = new Color(0.16f, 0.18f, 0.23f, 1f);
        [SerializeField] private Color disabledColor = new Color(0.11f, 0.12f, 0.15f, 1f);
        [SerializeField] private Color effectColor = Color.white;
        [SerializeField] private Color costColor = new Color(1f, 0.86f, 0.35f, 1f);
        [SerializeField] private Color shortColor = new Color(1f, 0.35f, 0.3f, 1f);
        [SerializeField] private Color mutedColor = new Color(0.6f, 0.6f, 0.62f, 1f);

        private Action _onClick;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(() => _onClick?.Invoke());
        }

        public void Bind(SkillUpgradeRowData data, Action onClick)
        {
            _onClick = onClick;

            if (icon != null)
            {
                icon.sprite = data.Icon;
                icon.enabled = data.Icon != null;
            }

            Set(keyLabel, data.Key);
            Set(nameLabel, data.Name);
            Set(levelLabel, data.Level);
            Set(effectLabel, data.Effect);
            Set(costLabel, data.Cost);

            if (costLabel != null)
                costLabel.color = data.State == SkillUpgradeResult.NotEnoughGold ? shortColor
                    : data.State == SkillUpgradeResult.Ok ? costColor : mutedColor;
            if (effectLabel != null)
                effectLabel.color = data.State == SkillUpgradeResult.Ok || data.State == SkillUpgradeResult.NotEnoughGold
                    ? effectColor : mutedColor;

            bool interactable = data.State == SkillUpgradeResult.Ok;
            if (button != null)
                button.interactable = interactable;
            if (background != null)
                background.color = interactable ? normalColor : disabledColor;
        }

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text ?? string.Empty;
        }
    }

    public struct SkillUpgradeRowData
    {
        public Sprite Icon;
        public string Key;
        public string Name;
        public string Level;
        public string Effect;
        public string Cost;
        public SkillUpgradeResult State;
    }
}

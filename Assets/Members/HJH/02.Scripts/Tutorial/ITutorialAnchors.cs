using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    public interface ITutorialAnchors
    {
        bool TryGetAnchor(string id, out RectTransform rect);
    }

    public interface ITutorialPickScreen : ITutorialAnchors
    {
        int SelectionChanges { get; }
        int ComboChanges { get; }
    }

    public static class TutorialAnchorIds
    {
        public const string PickPortraits = "Pick.Portraits";
        public const string PickSkills = "Pick.Skills";
        public const string PickCombos = "Pick.Combos";
        public const string PickStart = "Pick.Start";

        public const string HudSkillBar = "Hud.SkillBar";
        public const string HudSlotPrefix = "Hud.Slot.";
        public const string HudCombo = "Hud.Combo";
        public const string HudQuest = "Hud.Quest";
        public const string HudHp = "Hud.Hp";
    }
}

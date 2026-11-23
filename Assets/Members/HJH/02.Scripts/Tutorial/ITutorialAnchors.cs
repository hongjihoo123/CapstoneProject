using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    // 튜토리얼이 "여기를 보세요" 하고 테두리를 칠 UI 자리를 이름으로 내주는 쪽.
    // 화면(픽 화면 등)이 구현하면 튜토리얼은 그 화면의 내부 구조를 몰라도 됨.
    // 인게임 HUD 는 TutorialAnchors 가 HUD 부품을 직접 찾아 줌.
    public interface ITutorialAnchors
    {
        bool TryGetAnchor(string id, out RectTransform rect);
    }

    // 픽 화면이 튜토리얼에 알려 주는 것 (튜토리얼 단계는 이 인터페이스만 봄)
    public interface ITutorialPickScreen : ITutorialAnchors
    {
        int SelectionChanges { get; }   // 캐릭터를 바꾼 횟수
        int ComboChanges { get; }       // 콤보를 고르거나 뺀 횟수
    }

    // 앵커 이름 모음 (오타 방지)
    public static class TutorialAnchorIds
    {
        public const string PickPortraits = "Pick.Portraits";
        public const string PickSkills = "Pick.Skills";
        public const string PickCombos = "Pick.Combos";
        public const string PickStart = "Pick.Start";

        public const string HudSkillBar = "Hud.SkillBar";
        public const string HudSlotPrefix = "Hud.Slot.";   // + SkillSlotId 이름 (Hud.Slot.Weapon)
        public const string HudCombo = "Hud.Combo";
        public const string HudQuest = "Hud.Quest";
        public const string HudHp = "Hud.Hp";
    }
}

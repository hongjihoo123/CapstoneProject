using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    public static class TutorialDefaults
    {
        public const string ResourceFolder = "HJH/Tutorial/";
        public const string PickName = "Tutorial_Pick";
        public const string StageName = "Tutorial_Stage";
        public const string MainMenuScene = "MainMenu";
        public const string StageScene = "Map_Training";

        public const string PickComboTitle = "시작 콤보";
        public const string PickComboBody = "우클릭 스킬과 궁극기 원소로 만든 콤보 두 개 중 하나를 고르세요.\n튜토리얼과 초반에는 이 콤보로 싸우고, 게임 중 상점에서 새 콤보를 바인딩할 수 있습니다.";
        public const string BindTitle = "콤보 바인딩";
        public const string BindBody = "콤보 상점에 가까이 가서 {F} 를 누르세요.\n원소를 2~4칸 순서대로 넣고 저장하면 콤보가 추가됩니다. 상점 하나당 한 번, 최대 3개까지입니다.";

        public static TutorialSequence Load(string name)
        {
            var asset = Resources.Load<TutorialSequence>(ResourceFolder + name);
            if (asset != null)
                return asset;

            Debug.Log($"[Tutorial] {name} 에셋이 없어 기본 순서로 진행 (메뉴: HJH/튜토리얼/기본 튜토리얼 에셋 만들기)");
            return name == PickName ? CreatePick() : CreateStage(null);
        }

        public static TutorialSequence CreatePick() => TutorialSequence.Create("", true,
            Step<PickScreenStep>().Set(PickScreenStep.PickTask.Character).Configure(
                "캐릭터 선택",
                "숫자 {1~3}, ←/→ 또는 초상화를 눌러 캐릭터를 바꿔 보세요.\n캐릭터마다 무기·스킬·플레이 방식이 다릅니다.",
                TutorialAnchorIds.PickPortraits, pauseTime: false),
            Step<PickScreenStep>().Set(PickScreenStep.PickTask.Character).Configure(
                "고정 스킬",
                "패시브·우클릭·대시·궁극기는 캐릭터마다 정해져 있습니다. 아이콘에 마우스를 올리면 설명이 바뀝니다.\nQ/E 칸은 바꿔 낄 수 있는 교체 스킬 자리입니다.",
                TutorialAnchorIds.PickSkills, pauseTime: false),
            Step<PickScreenStep>().Set(PickScreenStep.PickTask.Combo).Configure(
                PickComboTitle,
                PickComboBody,
                TutorialAnchorIds.PickCombos, pauseTime: false),
            Step<PickScreenStep>().Set(PickScreenStep.PickTask.Start).Configure(
                "게임 시작",
                "준비가 되면 게임 시작을 누르세요.\n왕국 훈련장에서 튜토리얼이 이어집니다.",
                TutorialAnchorIds.PickStart, pauseTime: false));

        public static TutorialSequence CreateStage(GameObject targetPrefab)
        {
            ActionStep basic = Step<ActionStep>().Set(ActionStep.ActionKind.BasicAttack, uses: 1);
            basic.Configure("기본 공격", "조준: 마우스   ·   사격: {Fire}\n앞에 세워진 훈련 더미를 부수세요.");
            basic.SetTarget(targetPrefab);

            ActionStep weapon = Step<ActionStep>().Set(ActionStep.ActionKind.Skill, SkillSlotId.Weapon);
            weapon.Configure("무기 스킬", "무기 스킬 {Weapon}  —  캐릭터마다 다른 기술입니다.\n무기 스킬을 써서 더미를 부수세요.");
            weapon.SetTarget(targetPrefab);

            ActionStep dash = Step<ActionStep>().Set(ActionStep.ActionKind.Skill, SkillSlotId.Dash);
            dash.Configure("대시", "대시 {Dash}  —  움직이는 방향으로 빠르게 이동합니다.\n적의 공격을 피할 때 씁니다.");
            dash.SetTarget(null, spawn: false);

            ComboStep combo = Step<ComboStep>();
            combo.Configure("원소 콤보", "아래 순서대로 스킬을 이어 써서 콤보를 성공시키고, 더미를 부수세요.", TutorialAnchorIds.HudCombo);
            combo.SetTarget(targetPrefab);

            ComboBindStep bind = Step<ComboBindStep>();
            bind.Configure(BindTitle, BindBody);

            ActionStep ultimate = Step<ActionStep>().Set(ActionStep.ActionKind.Skill, SkillSlotId.Ultimate);
            ultimate.Configure("궁극기", "궁극기 {Ultimate}  —  가장 강한 기술입니다. 콤보로 쌓은 스택은 {Finisher} 로 준비하면 다음 스킬이 더 강해집니다.\n궁극기로 더미를 부수세요.");
            ultimate.SetTarget(targetPrefab);

            TargetWaveStep wave = Step<TargetWaveStep>();
            wave.Configure("마무리 훈련", "배운 것을 모두 써서 훈련장에 세워진 더미를 전부 부수세요.\n화면 가장자리 화살표가 더미 쪽을 가리킵니다.");
            wave.SetTarget(targetPrefab);

            return TutorialSequence.Create(MainMenuScene, false,
                Step<InfoStep>().Configure(
                    "왕국 훈련장",
                    "침식 수정을 처리하러 떠나기 전, 왕국 훈련장에서 마지막으로 기량을 점검합니다.\n안내가 뜨는 동안은 시간이 멈춥니다."),
                Step<MoveStep>().Configure("이동", "이동 {W A S D}  —  캐릭터는 마우스 쪽을 바라봅니다.\n훈련장을 걸으며 몸을 풀어 보세요."),
                basic,
                weapon,
                dash,
                Step<InfoStep>().Configure(
                    "원소",
                    "스킬마다 원소가 있습니다. 스킬 칸 테두리 색이 그 스킬의 원소입니다.\n원소 스킬을 정해진 순서로 이어 쓰면 콤보가 터지고 강화 효과가 붙습니다.",
                    TutorialAnchorIds.HudSkillBar),
                combo,
                bind,
                ultimate,
                wave,
                Step<InfoStep>().Configure(
                    "훈련 완료",
                    "훈련을 마쳤습니다. 수고하셨습니다!\n메인 화면으로 돌아갑니다. 게임 시작을 누르면 침식 수정을 향한 원정이 시작됩니다.",
                    resumeHint: "{Confirm} 메인 화면으로"));
        }

        private static T Step<T>() where T : TutorialStep => ScriptableObject.CreateInstance<T>();
    }
}

using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    // 픽 화면 단계 (시간은 안 멈춤, 화면 조작을 직접 해 보게 함). 픽 화면은 ITutorialPickScreen 으로만 봄.
    //   Character : 캐릭터를 바꿔 보거나 Space 로 넘어가기
    //   Combo     : 콤보를 넣거나 빼 보거나 Space 로 넘어가기
    //               ※ 콤보 선택 기능(기본 원소 조합 중 1개 고르기)이 생기면 이 단계 자리에 그 기능용 단계를 넣으면 됨
    //   Start     : "게임 시작" 안내. 씬이 넘어가면서 끝남 (다음 씬에서 스테이지 튜토리얼이 이어짐)
    [CreateAssetMenu(menuName = "HJH/Tutorial/Step - Pick Screen", fileName = "Step_Pick")]
    public class PickScreenStep : TutorialStep
    {
        public enum PickTask { Character, Combo, Start }

        [SerializeField] private PickTask task = PickTask.Character;

        [System.NonSerialized] private int _start;

        public PickTask Task => task;

        public PickScreenStep Set(PickTask task)
        {
            this.task = task;
            return this;
        }

        public override bool ShouldRun(TutorialContext ctx) => ctx.Pick != null;

        public override void Begin(TutorialContext ctx) => _start = Count(ctx);

        private int Count(TutorialContext ctx) => task switch
        {
            PickTask.Character => ctx.Pick.SelectionChanges,
            PickTask.Combo => ctx.Pick.ComboChanges,
            _ => 0,
        };

        public override bool IsResumeInput(TutorialContext ctx) => true;

        public override bool IsDone(TutorialContext ctx)
        {
            if (task == PickTask.Start)
                return false;
            // 여러 개 둘러보게 바꾸는 것만으로는 안 넘어감. Space 로 다음
            return ctx.Input.SpacePressed;
        }

        public override string Progress(TutorialContext ctx) => task switch
        {
            PickTask.Start => TutorialContext.Key("Enter") + " 게임 시작",
            _ => (Count(ctx) > _start ? "<color=#D6B878>좋아요!</color>   " : "") + TutorialContext.Key("Space") + " 다음",
        };
    }
}

using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
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
            return ctx.Input.SpacePressed;
        }

        public override string Progress(TutorialContext ctx) => task switch
        {
            PickTask.Start => TutorialContext.Key("Enter") + " 게임 시작",
            _ => (Count(ctx) > _start ? "<color=#D6B878>좋아요!</color>   " : "") + TutorialContext.Key("Space") + " 다음",
        };
    }
}

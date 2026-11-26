using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    [CreateAssetMenu(menuName = "HJH/Tutorial/Step - Move", fileName = "Step_Move")]
    public class MoveStep : TutorialStep
    {
        [SerializeField, Min(0.5f)] private float distance = 4f;

        [System.NonSerialized] private float _start;

        public override void Begin(TutorialContext ctx) => _start = ctx.MovedDistance;

        public override bool IsResumeInput(TutorialContext ctx) => ctx.Input.MovePressed || ctx.Input.ConfirmPressed;
        protected override string DefaultResumeHint => "<color=#D6B878>[W A S D]</color> 눌러서 시작";

        private float Moved(TutorialContext ctx) => ctx.MovedDistance - _start;

        public override bool IsDone(TutorialContext ctx) => Moved(ctx) >= distance;

        public override string Progress(TutorialContext ctx) => $"{Mathf.Min(Moved(ctx), distance):0.0} / {distance:0} m";
    }
}

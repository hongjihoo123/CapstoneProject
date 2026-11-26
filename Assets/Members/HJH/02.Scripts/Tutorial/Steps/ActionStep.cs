using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    [CreateAssetMenu(menuName = "HJH/Tutorial/Step - Action", fileName = "Step_Action")]
    public class ActionStep : TargetPracticeStep
    {
        public enum ActionKind { BasicAttack, Skill }

        [Header("행동")]
        [SerializeField] private ActionKind kind = ActionKind.BasicAttack;
        [SerializeField] private SkillSlotId slot = SkillSlotId.Weapon;
        [SerializeField, Min(1)] private int uses = 1;

        [System.NonSerialized] private int _start;

        public ActionKind Kind => kind;
        public SkillSlotId Slot => slot;

        public ActionStep Set(ActionKind kind, SkillSlotId slot = SkillSlotId.Weapon, int uses = 1)
        {
            this.kind = kind;
            this.slot = slot;
            this.uses = Mathf.Max(1, uses);
            return this;
        }

        public override string AnchorId =>
            string.IsNullOrEmpty(base.AnchorId) && kind == ActionKind.Skill ? TutorialAnchorIds.HudSlotPrefix + slot : base.AnchorId;

        public override bool ShouldRun(TutorialContext ctx) => kind == ActionKind.BasicAttack || ctx.HasSkill(slot);

        public override void Begin(TutorialContext ctx)
        {
            _start = Count(ctx);
            base.Begin(ctx);
        }

        private int Count(TutorialContext ctx) => kind == ActionKind.BasicAttack ? ctx.BasicAttacks : ctx.SkillUses(slot);

        private int Used(TutorialContext ctx) => Count(ctx) - _start;

        public override bool IsResumeInput(TutorialContext ctx)
        {
            if (ctx.Input.ConfirmPressed)
                return true;
            return kind == ActionKind.BasicAttack ? ctx.Input.FirePressed : ctx.Input.SkillPressed(ctx, slot);
        }

        protected override string DefaultResumeHint =>
            (kind == ActionKind.BasicAttack ? "{Fire}" : "{" + slot + "}") + " 눌러서 시작";

        public override bool IsDone(TutorialContext ctx) => Used(ctx) >= uses && TargetDown;

        public override string Progress(TutorialContext ctx)
        {
            int used = Mathf.Min(Used(ctx), uses);
            string use = used >= uses ? "<color=#D6B878>사용 완료</color>" : $"사용 {used} / {uses}";

            if (kind == ActionKind.Skill && used < uses && ctx.Hud != null)
            {
                var info = ctx.Hud.GetSlot(slot);
                if (!info.IsReady && info.CooldownRemaining > 0f)
                    use = $"준비 중 {info.CooldownRemaining:0.0}s";
            }

            string target = TargetProgress;
            return target != null ? $"{use}   ·   {target}" : use;
        }
    }
}

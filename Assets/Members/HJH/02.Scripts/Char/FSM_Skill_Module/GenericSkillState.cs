using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public class GenericSkillState : SkillStateBase
    {
        private ISkillExecution _execution;

        public SkillSlotId Slot { get; }
        public SkillData Data { get; }

        public override float Cooldown => Data.Cooldown;
        public override bool AllowsMove => Data.AllowsMove;
        public override bool AllowsFire => Data.AllowsFire;
        public override float MoveSpeedMultiplier => Data.MoveSpeedMultiplier;
        public override bool IsFinished => Time.time - EnterTime >= Data.Duration;

        public GenericSkillState(ISkillHost host, SkillSlotId slot, SkillData data) : base(host)
        {
            Slot = slot;
            Data = data;
        }

        public override void Enter()
        {
            base.Enter();
            _execution = Data.Begin(Host.Context);
        }

        public override void Tick(float deltaTime) => _execution?.Tick(Time.time - EnterTime);

        public override void Exit()
        {
            _execution?.End();
            _execution = null;
            base.Exit();
        }

        public void SendAnimationEvent(SkillAnimationEvent animationEvent) => _execution?.OnAnimationEvent(animationEvent);

        public override void OnAnimationHitEvent() => Data.OnAnimationHitEvent(Host.Context);
    }
}

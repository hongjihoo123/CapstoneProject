using Assets.Members.HJH._02.Scripts.Element.Upgrade;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // State-machine state that runs one SkillData in one slot: Enter starts the skill's execution, Tick drives it,
    // Exit ends it. Cooldown / charges / swap lock come from SkillStateBase.
    public class GenericSkillState : SkillStateBase
    {
        private ISkillExecution _execution;

        public SkillSlotId Slot { get; }
        public SkillData Data { get; }

        public override float Cooldown => Data.Cooldown;

        // 기존 쿨감 스탯 + 스킬 트리의 원소별 쿨감. 최대 75% 까지만
        protected override float CooldownReduction =>
            Mathf.Clamp(base.CooldownReduction + SkillUpgrades.Bonus(SkillUpgradeKind.Cooldown, Data) + CritSystem.ChainHasteBonus(), 0f, 0.75f);
        public override int MaxCharges => Data.Charges + (Slot == SkillSlotId.Dash ? Mathf.RoundToInt(SkillUpgrades.Exact(SkillUpgradeKind.DashCharge, SkillUpgradeScope.Dash)) : 0);
        public override bool AllowsMove => Data.AllowsMove;
        public override bool AllowsFire => Data.AllowsFire;
        public override float MoveSpeedMultiplier => Data.MoveSpeedMultiplier;
        public override bool IsFinished => Time.time - EnterTime >= Data.Duration;

        public GenericSkillState(ISkillHost host, SkillSlotId slot, SkillData data) : base(host)
        {
            Slot = slot;
            Data = data;
            SkillUpgrades.BindSlot(data, slot);
        }

        public override void Enter()
        {
            base.Enter();
            Host.Context.ActiveSkill = Data;
            CritSystem.OnCast((Host as SkillStateModule)?.Player, Slot);
            _execution = Data.Begin(Host.Context);
        }

        public override void Tick(float deltaTime) => _execution?.Tick(Time.time - EnterTime);

        public override void Exit()
        {
            _execution?.End();
            _execution = null;
            if (Host.Context.ActiveSkill == Data)
                Host.Context.ActiveSkill = null;
            base.Exit();
        }

        public void SendAnimationEvent(SkillAnimationEvent animationEvent) => _execution?.OnAnimationEvent(animationEvent);

        public override void OnAnimationHitEvent() => Data.OnAnimationHitEvent(Host.Context);
    }
}

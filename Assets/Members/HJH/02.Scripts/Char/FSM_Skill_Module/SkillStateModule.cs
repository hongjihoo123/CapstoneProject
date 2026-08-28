using Members.JJH._02_Scripts.Systems.ModuleSystem;
using Members.KYR._01_Scripts;
using Members.KYR._01_Scripts.FSM.Core;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public class SkillStateModule : Module, IAfterInitModule
    {
        private static readonly AllowAllSkillFallback Fallback = new();

        [SerializeField] private SkillData qSkillData;
        [SerializeField] private SkillData eSkillData;
        [SerializeField] private SkillData xSkillData;

        private GenericSkillState _idleSkill;
        private GenericSkillState _qSkill;
        private GenericSkillState _eSkill;
        private GenericSkillState _xSkill;

        public PlayerAgent Player { get; private set; }
        public StateMachine Machine { get; private set; }
        public ISkillCapabilities Capabilities => Machine?.Current as ISkillCapabilities ?? Fallback;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            Player = owner as PlayerAgent;
            Debug.Assert(Player != null, $"{owner.name}의 SkillStateModule은 PlayerAgent 아래여야 합니다.");
        }

        public void Tick(float deltaTime)
        {
            Machine.Tick(deltaTime);
        }

        public void ChangeState<T>() where T : IState
        {
            Machine.ChangeState<T>();
        }

        public void ForceIdle()
        {
            ChangeState<IdleSkillState>();
        }

        public void AfterInit()
        {
            Machine = new StateMachine();

            _idleSkill = new GenericSkillState(this, null); // Idle 전용은 아래처럼 별도 처리 필요
                                                            // Idle은 SkillData 없이 항상 통과시켜야 하므로 IdleSkillState는 그대로 유지 추천
            var idle = new IdleSkillState(this);
            _qSkill = new GenericSkillState(this, qSkillData);
            _eSkill = new GenericSkillState(this, eSkillData);
            _xSkill = new GenericSkillState(this, xSkillData);

            Machine.Register(idle);
            Machine.Register(_qSkill);
            Machine.Register(_eSkill);
            Machine.Register(_xSkill);
            Machine.ChangeState<IdleSkillState>();
        }

        public void ResolveInput()
        {
            if (Machine.Current is ISkillState { IsFinished: false }) return;

            var input = Player.Input;
            if (input.QPressed && _qSkill.IsReady) { Machine.ChangeState(_qSkill); return; }
            if (input.EPressed && _eSkill.IsReady) { Machine.ChangeState(_eSkill); return; }
            if (input.XPressed && _xSkill.IsReady) { Machine.ChangeState(_xSkill); return; }
            if (!Machine.IsCurrent<IdleSkillState>()) Machine.ChangeState<IdleSkillState>();
        }

        private sealed class AllowAllSkillFallback : ISkillCapabilities
        {
            public bool AllowsMove => true;
            public bool AllowsFire => true;
            public float MoveSpeedMultiplier => 1f;
        }
    }
}
using Members.JJH._02_Scripts.Systems.ModuleSystem;
using Members.KYR._01_Scripts;
using Members.KYR._01_Scripts.FSM.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public class SkillStateModule : Module, IAfterInitModule
    {
        private static readonly AllowAllSkillFallback Fallback = new();

        [SerializeField] private SkillData qSkillData;
        [SerializeField] private SkillData eSkillData;
        [SerializeField] private SkillData xSkillData;
        [SerializeField] private PassiveData passiveData;

        private GenericSkillState _qSkill;
        private GenericSkillState _eSkill;
        private GenericSkillState _xSkill;

        private IdleSkillState _idleSkill;

        private readonly HashSet<RobotWeapons.IDamageable> _hitThisActivation = new();

        public PlayerAgent Player { get; private set; }
        public StateMachine Machine { get; private set; }
        public ISkillCapabilities Capabilities => Machine?.Current as ISkillCapabilities ?? Fallback;

        public int AnimBlendIndex
        {
            get
            {
                var current = Machine?.Current;
                if (current == _qSkill) return 0;
                if (current == _eSkill) return 1;
                if (current == _xSkill) return 2;
                return 3;
            }
        }

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            Player = owner as PlayerAgent;
            Debug.Assert(Player != null, $"{owner.name}의 SkillStateModule은 PlayerAgent 아래에서만 사용할 수 있습니다.");
        }

        public void AfterInit()
        {
            Machine = new StateMachine();
            _idleSkill = new IdleSkillState(this);

            if (qSkillData != null) _qSkill = new GenericSkillState(this, qSkillData);
            else Debug.LogWarning($"{name}의 SkillStateModule에 Q SkillData가 비어있습니다. (Q 스킬 비활성화)");

            if (eSkillData != null) _eSkill = new GenericSkillState(this, eSkillData);
            else Debug.LogWarning($"{name}의 SkillStateModule에 E SkillData가 비어있습니다. (E 스킬 비활성화)");

            if (xSkillData != null) _xSkill = new GenericSkillState(this, xSkillData);
            else Debug.LogWarning($"{name}의 SkillStateModule에 X SkillData가 비어있습니다. (X 스킬 비활성화)");

            Machine.Register(_idleSkill);
            Machine.ChangeState<IdleSkillState>();
        }

        public void Tick(float deltaTime)
        {
            Machine.Tick(deltaTime);
        }

        public void ForceIdle()
        {
            Machine.ChangeState<IdleSkillState>();
        }

        public void ResetHitTracking() => _hitThisActivation.Clear();

        public bool TryRegisterHit(RobotWeapons.IDamageable target) => _hitThisActivation.Add(target);

        public void Anim_SkillOverlapHit()
        {
            if (Machine.Current is SkillStateBase state)
                state.OnAnimationHitEvent();
        }

        public void ResolveInput()
        {
            if (Machine.Current is SkillStateBase skillState && !skillState.IsFinished)
            {
                return;
            }

            PlayerInputState input = Player.Input;

            if (_qSkill != null && input.QPressed && _qSkill.IsReady) { Machine.ChangeState(_qSkill); return; }
            if (_eSkill != null && input.EPressed && _eSkill.IsReady) { Machine.ChangeState(_eSkill); return; }
            if (_xSkill != null && input.XPressed && _xSkill.IsReady) { Machine.ChangeState(_xSkill); return; }

            if (!Machine.IsCurrent<IdleSkillState>())
                Machine.ChangeState<IdleSkillState>();
        }

        public void NotifyEnemyKilled()
        {
            Debug.Log($"[패시브] 킬 이벤트 수신, passiveData null? {passiveData == null}");
            passiveData?.OnEnemyKilled(this);
        }
        private sealed class AllowAllSkillFallback : ISkillCapabilities
        {
            public bool AllowsMove => true;
            public bool AllowsFire => true;
            public bool AllowsReload => true;
            public float MoveSpeedMultiplier => 1f;
        }
    }
}

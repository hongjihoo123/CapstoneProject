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

        [Header("캐릭터별 스킬 데이터")]
        [SerializeField] private SkillData qSkillData;
        [SerializeField] private SkillData eSkillData;
        [SerializeField] private SkillData xSkillData;

        private IdleSkillState _idleSkill;
        private QSkillState _qSkill;
        private ESkillState _eSkill;
        private XSkillState _xSkill;

        private readonly HashSet<RobotWeapons.IDamageable> _hitThisActivation = new();

        public PlayerAgent Player { get; private set; }
        public StateMachine Machine { get; private set; }
        public ISkillCapabilities Capabilities => Machine?.Current as ISkillCapabilities ?? Fallback;

        public int AnimBlendIndex
        {
            get
            {
                var current = Machine?.CurrentType;
                if (current == typeof(QSkillState)) return 0;
                if (current == typeof(ESkillState)) return 1;
                if (current == typeof(XSkillState)) return 2;
                return 3;
            }
        }

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            Player = owner as PlayerAgent;
            Debug.Assert(Player != null, $"{owner.name}의 SkillStateModule은 PlayerAgent 아래여야 합니다.");
        }

        public void AfterInit()
        {
            Debug.Assert(qSkillData != null, $"{name}의 SkillStateModule에 Q SkillData가 비어있습니다.");
            Debug.Assert(eSkillData != null, $"{name}의 SkillStateModule에 E SkillData가 비어있습니다.");
            Debug.Assert(xSkillData != null, $"{name}의 SkillStateModule에 X SkillData가 비어있습니다.");

            Machine = new StateMachine();
            _idleSkill = new IdleSkillState(this);
            _qSkill = new QSkillState(this, qSkillData);
            _eSkill = new ESkillState(this, eSkillData);
            _xSkill = new XSkillState(this, xSkillData);

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

        // 이미 맞은 대상 데미지 스킵
        public bool TryRegisterHit(RobotWeapons.IDamageable target) => _hitThisActivation.Add(target);

        // 애니메이션 이벤트에서 직접 호출하는 용도
        public void Anim_SkillOverlapHit()
        {
            if (Machine.Current is SkillStateBase state)
                state.OnAnimationHitEvent();
        }

        public void ResolveInput()
        {
            if (Machine.Current is SkillStateBase skillState && !skillState.IsFinished)
            {
                Debug.Log($"[Skill] 대기중 / State={Machine.CurrentType?.Name} / Time.time={Time.time:F3} / 경과={skillState.DebugElapsed:F3} / Duration={skillState.DebugDuration:F3}");
                return;
            }

            PlayerInputState input = Player.Input;

            if (input.QPressed) Debug.Log($"[Skill] Q입력 감지 / _qSkill null={_qSkill == null} / IsReady={_qSkill?.IsReady}");
            if (input.EPressed) Debug.Log($"[Skill] E입력 감지 / _eSkill null={_eSkill == null} / IsReady={_eSkill?.IsReady}");

            if (input.QPressed && _qSkill.IsReady) { Debug.Log("[Skill] Q 상태로 전환함"); Machine.ChangeState(_qSkill); return; }
            if (input.EPressed && _eSkill.IsReady) { Debug.Log("[Skill] E 상태로 전환함"); Machine.ChangeState(_eSkill); return; }
            if (input.XPressed && _xSkill.IsReady) { Machine.ChangeState(_xSkill); return; }

            if (!Machine.IsCurrent<IdleSkillState>())
                Machine.ChangeState<IdleSkillState>();
        }

        private sealed class AllowAllSkillFallback : ISkillCapabilities
        {
            public bool AllowsMove => true;
            public bool AllowsFire => true;
            public float MoveSpeedMultiplier => 1f;
        }
    }
}
using System;
using System.Collections.Generic;
using Members.JJH._02_Scripts.Systems.ModuleSystem;
using Members.KYR._01_Scripts;
using Members.KYR._01_Scripts.FSM.Core;
using Members.KYR._01_Scripts.Modules;
using Members.KYR._01_Scripts.Stats;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public class SkillStateModule : Module, IAfterInitModule, ISkillHost
    {
        [Serializable]
        public struct SlotBinding
        {
            public SkillSlotId slot;
            public SkillData data;
        }

        private static readonly AllowAllSkillFallback Fallback = new();

        [SerializeField] private WeaponKitData kit;
        [SerializeField] private SlotBinding[] initialSkills;
        [SerializeField] private PassiveData passiveData;
        [SerializeField] private LayerMask skillTargetMask = ~0;

        private readonly Dictionary<SkillSlotId, GenericSkillState> _slots = new();
        private PlayerSkillContext _context;
        private IdleSkillState _idleSkill;

        public PlayerAgent Player { get; private set; }
        public StateMachine Machine { get; private set; }
        public ISkillContext Context => _context;
        public ISkillCapabilities Capabilities => Machine?.Current as ISkillCapabilities ?? Fallback;

        public int IdleBlendIndex => SkillSlots.Count;
        public int AnimBlendIndex => Machine?.Current is GenericSkillState active ? (int)active.Slot : IdleBlendIndex;

        public float CooldownReduction
        {
            get
            {
                PlayerStatsModule stats = Player != null ? Player.Stats : null;
                return stats != null && stats.Tree != null
                    ? Mathf.Clamp01(stats.Get(PlayerStatId.SkillCooldownReduction))
                    : 0f;
            }
        }

        public event Action<SkillUsedInfo> SkillUsed;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            Player = owner as PlayerAgent;
            Debug.Assert(Player != null, $"{owner.name}의 SkillStateModule은 PlayerAgent 아래에서만 사용할 수 있습니다.");
            _context = new PlayerSkillContext(Player, skillTargetMask);
        }

        public void AfterInit()
        {
            Machine = new StateMachine();
            _idleSkill = new IdleSkillState(this);
            Machine.Register(_idleSkill);

            if (kit != null)
                EquipKit(kit);

            if (initialSkills != null)
            {
                foreach (SlotBinding binding in initialSkills)
                    Equip(binding.slot, binding.data);
            }

            foreach (SkillSlotId slot in SkillSlots.All)
            {
                if (!SkillSlots.IsFree(slot) && !_slots.ContainsKey(slot))
                    Debug.LogWarning($"{name}의 SkillStateModule에 {slot} 스킬이 비어있습니다.");
            }

            Machine.ChangeState<IdleSkillState>();
        }

        public void EquipKit(WeaponKitData newKit)
        {
            kit = newKit;
            passiveData = newKit != null ? newKit.passive : null;

            foreach (SkillSlotId slot in SkillSlots.All)
            {
                if (!SkillSlots.IsFree(slot))
                    Equip(slot, newKit != null ? newKit.GetSkill(slot) : null);
            }
        }

        public void Equip(SkillSlotId slot, SkillData data)
        {
            if (_slots.TryGetValue(slot, out GenericSkillState previous) && Machine != null && ReferenceEquals(Machine.Current, previous))
                ForceIdle();

            if (data == null)
                _slots.Remove(slot);
            else
                _slots[slot] = new GenericSkillState(this, slot, data);
        }

        public SkillData GetSkill(SkillSlotId slot) => _slots.TryGetValue(slot, out GenericSkillState state) ? state.Data : null;

        public float GetCooldownRemaining(SkillSlotId slot) => _slots.TryGetValue(slot, out GenericSkillState state) ? state.CooldownRemaining : 0f;

        public void Tick(float deltaTime) => Machine.Tick(deltaTime);

        public void ForceIdle() => Machine.ChangeState<IdleSkillState>();

        public void ResolveInput() => ResolveInput(Player.Input);

        public void ResolveInput(ISkillInputSource input)
        {
            if (Machine.Current is SkillStateBase active && !active.IsFinished)
                return;

            Machine.ChangeState<IdleSkillState>();

            foreach (SkillSlotId slot in SkillSlots.All)
            {
                if (!input.WasSkillPressed(slot) || !_slots.TryGetValue(slot, out GenericSkillState state) || !state.IsReady)
                    continue;

                Machine.ChangeState(state);
                SkillUsed?.Invoke(new SkillUsedInfo(slot, state.Data));
                return;
            }
        }

        public void Anim_SkillOverlapHit()
        {
            if (Machine.Current is SkillStateBase state)
                state.OnAnimationHitEvent();
        }

        public void NotifyEnemyKilled() => passiveData?.OnEnemyKilled(_context);

        void ISkillHost.OnSkillEntered() => _context.BeginActivation();

        private sealed class AllowAllSkillFallback : ISkillCapabilities
        {
            public bool AllowsMove => true;
            public bool AllowsFire => true;
            public bool AllowsReload => true;
            public float MoveSpeedMultiplier => 1f;
        }
    }
}

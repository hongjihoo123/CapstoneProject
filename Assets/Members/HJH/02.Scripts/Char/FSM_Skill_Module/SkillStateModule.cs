using System;
using System.Collections.Generic;
using Members.JJH._02_Scripts.Systems.ModuleSystem;
using Members.KYR._01_Scripts;
using Members.KYR._01_Scripts.FSM.Core;
using Members.KYR._01_Scripts.Modules;
using Members.KYR._01_Scripts.Stats;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public class SkillStateModule : Module, IAfterInitModule, ISkillHost, IPassiveHost
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
        [SerializeField, Tooltip("Ground and walls. Skills that fly pass through everything else.")]
        private LayerMask environmentMask = 1;

        private readonly Dictionary<SkillSlotId, GenericSkillState> _slots = new();
        private PlayerSkillContext _context;
        private IPassive _passive;
        private IdleSkillState _idleSkill;
        private SkillSlotId? _aimingSlot;

        public bool IsAimingSkill => _aimingSlot != null;
        public SkillSlotId? AimingSlot => _aimingSlot;

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
            _context = new PlayerSkillContext(Player, skillTargetMask, environmentMask);
            Player.DamageDealt += HandleDamageDealt;
        }

        public void AfterInitalize()
        {
            Machine = new StateMachine();
            _idleSkill = new IdleSkillState(this);
            Machine.Register(_idleSkill);

            if (kit != null)
                EquipKit(kit);
            else if (passiveData != null)
                SetPassive(passiveData);

            if (initialSkills != null)
            {
                foreach (SlotBinding binding in initialSkills)
                    Equip(binding.slot, binding.data);
            }

            foreach (SkillSlotId slot in SkillSlots.All)
            {
                if (SkillSlots.IsKitSlot(slot) && !_slots.ContainsKey(slot))
                    Debug.LogWarning($"{name}의 SkillStateModule에 {slot} 스킬이 비어있습니다.");
            }

            Machine.ChangeState<IdleSkillState>();
        }

        public void EquipKit(WeaponKitData newKit)
        {
            kit = newKit;
            SetPassive(newKit != null ? newKit.passive : null);

            foreach (SkillSlotId slot in SkillSlots.All)
            {
                if (SkillSlots.IsKitSlot(slot))
                    Equip(slot, newKit != null ? newKit.GetSkill(slot) : null);
            }
        }

        public void Equip(SkillSlotId slot, SkillData data)
        {
            if (_aimingSlot == slot)
                CancelAim();

            if (_slots.TryGetValue(slot, out GenericSkillState previous) && Machine != null && ReferenceEquals(Machine.Current, previous))
                ForceIdle();

            if (data == null)
                _slots.Remove(slot);
            else
                _slots[slot] = new GenericSkillState(this, slot, data);
        }

        public SkillData GetSkill(SkillSlotId slot) => _slots.TryGetValue(slot, out GenericSkillState state) ? state.Data : null;

        public float GetCooldownDuration(SkillSlotId slot) => _slots.TryGetValue(slot, out GenericSkillState state) ? state.CooldownDuration : 0f;

        public PassiveData Passive => passiveData;

        public float GetCooldownRemaining(SkillSlotId slot) => _slots.TryGetValue(slot, out GenericSkillState state) ? state.CooldownRemaining : 0f;

        public int GetCharges(SkillSlotId slot) => _slots.TryGetValue(slot, out GenericSkillState state) ? state.Charges : 0;

        public int GetMaxCharges(SkillSlotId slot) => _slots.TryGetValue(slot, out GenericSkillState state) ? state.MaxCharges : 0;

        public void Tick(float deltaTime)
        {
            Machine.Tick(deltaTime);
            _passive?.Tick(deltaTime);
        }

        public void ForceIdle() => Machine.ChangeState<IdleSkillState>();

        public void ResolveInput() => ResolveInput(Player.Input);

        public void ResolveInput(ISkillInputSource input)
        {
            if (Machine.Current is GenericSkillState running && !running.IsFinished)
            {
                if (running.Data.Cancelable && running.Elapsed >= running.Data.CancelStartTime && ShouldCancel(running, input))
                    Machine.ChangeState<IdleSkillState>();
                return;
            }

            Machine.ChangeState<IdleSkillState>();

            if (_aimingSlot == null)
            {
                if (TryCastOnPress(input))
                    return;

                TryBeginAim(input);
            }

            if (_aimingSlot == null)
                return;

            // While a skill is held, the cancel key (right click) only cancels; it does not dash.
            SkillSlotId slot = _aimingSlot.Value;
            if (input.WasCancelPressed())
            {
                CancelAim();
                return;
            }

            if (input.IsSkillHeld(slot) && !input.WasSkillReleased(slot))
                return;

            CancelAim();
            Cast(slot);
        }

        private bool TryCastOnPress(ISkillInputSource input)
        {
            foreach (SkillSlotId slot in SkillSlots.All)
            {
                if (SkillSlots.CastsOnPress(slot) && input.WasSkillPressed(slot) && Cast(slot))
                    return true;
            }

            return false;
        }

        private bool Cast(SkillSlotId slot)
        {
            if (!_slots.TryGetValue(slot, out GenericSkillState state) || !state.IsReady)
                return false;

            Machine.ChangeState(state);
            var info = new SkillUsedInfo(slot, state.Data);
            _passive?.OnSkillUsed(info);
            SkillUsed?.Invoke(info);
            return true;
        }

        public void CancelAim() => _aimingSlot = null;

        private bool ShouldCancel(GenericSkillState running, ISkillInputSource input)
        {
            if (input.WasJumpPressed() || input.WasCancelPressed() || input.WasSkillPressed(running.Slot))
                return true;

            foreach (SkillSlotId slot in SkillSlots.All)
            {
                if (slot != running.Slot && input.WasSkillPressed(slot) && _slots.TryGetValue(slot, out GenericSkillState other) && other.IsReady)
                    return true;
            }

            return false;
        }

        public void DescribeAimPreview(ISkillPreview preview)
        {
            if (_aimingSlot != null && _slots.TryGetValue(_aimingSlot.Value, out GenericSkillState state))
                state.Data.DescribePreview(_context, preview);
        }

        private void TryBeginAim(ISkillInputSource input)
        {
            foreach (SkillSlotId slot in SkillSlots.All)
            {
                if (SkillSlots.CastsOnPress(slot) || !input.WasSkillPressed(slot)
                    || !_slots.TryGetValue(slot, out GenericSkillState state) || !state.IsReady)
                    continue;

                _aimingSlot = slot;
                return;
            }
        }

        public void Anim_SkillHitBegin() => SendAnimationEvent(SkillAnimationEvent.HitBegin);

        public void Anim_SkillHitEnd() => SendAnimationEvent(SkillAnimationEvent.HitEnd);

        private void SendAnimationEvent(SkillAnimationEvent animationEvent)
        {
            if (Machine.Current is GenericSkillState state)
                state.SendAnimationEvent(animationEvent);
        }

        public void Anim_SkillOverlapHit()
        {
            if (Machine.Current is SkillStateBase state)
                state.OnAnimationHitEvent();
        }

        public void NotifyEnemyKilled() => _passive?.OnEnemyKilled();

        public void ReduceCooldown(SkillSlotId slot, float seconds)
        {
            if (_slots.TryGetValue(slot, out GenericSkillState state))
                state.ReduceCooldown(seconds);
        }

        private void SetPassive(PassiveData data)
        {
            _passive?.Dispose();
            passiveData = data;
            _passive = data != null && _context != null ? data.CreateRuntime(this) : null;
            if (_context != null)
                _context.Passive = _passive;
        }

        private void HandleDamageDealt(DamageDealtInfo info) => _passive?.OnDamageDealt(info);

        private void OnDestroy()
        {
            if (Player != null)
                Player.DamageDealt -= HandleDamageDealt;

            _passive?.Dispose();
            _passive = null;
        }

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

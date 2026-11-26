using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using Members.KYR._01.Scripts.Player;
using Members.KYR._01.Scripts.Stats;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Element.Upgrade
{
    public static class CritSystem
    {
        public const float BaseMultiplier = 1.5f;
        private const float RefundCapPerSecond = 0.5f;
        public const float UltDefaultWindow = 3f;
        public const int UltDefaultNeed = 8;
        public const int UltDefaultChainCap = 1;
        public const float ChainBaseTime = 6f;
        private const float UltCountAnyTime = 4f;
        private const int StackMax = 5;
        private const float StackTime = 3f;
        private const float StreakHasteTime = 5f;
        private const int StreakNeed = 5;
        private const float DashWindowBase = 2f;
        private const float StillFullTime = 2f;
        private const float FarFullDistance = 14f;
        private const float MarkBaseTime = 4f;
        private const float ChainResetInternalCooldown = 2f;

        public static SkillUpgradeScope CurrentSlot { get; set; } = SkillUpgradeScope.All;

        public static bool Suppressed { get; set; }

        public static event Action<SkillUpgradeScope> Landed;

        public static event Action<SkillSlotId> Cast;

        public static event Action<int> UltChained;

        private static int _stacks;
        private static float _stackUntil;
        private static int _streak;
        private static float _hasteUntil;
        private static float _dashUntil;
        private static int _guaranteed;
        private static float _refundWindowStart;
        private static float _refundedInWindow;
        private static Vector3 _lastPos;
        private static float _lastMoveTime;
        private static readonly Dictionary<IDamageable, float> Marks = new();

        private static int _ultChain;
        private static bool _ultCarry;
        private static int _ultCrits;
        private static float _ultCastTime;
        private static float _ultWindowUntil;
        private static float _chainUntil;
        private static bool _ultCounting;
        private static float _lastChainResetTime = -99f;

        public static int UltChain => _ultChain;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Reset();

        public static void Reset()
        {
            _stacks = 0; _stackUntil = 0f; _streak = 0; _hasteUntil = 0f; _dashUntil = 0f; _guaranteed = 0;
            _refundWindowStart = 0f; _refundedInWindow = 0f;
            _lastPos = Vector3.zero; _lastMoveTime = 0f;
            Marks.Clear();
            _ultChain = 0; _ultCarry = false; _ultCrits = 0; _ultCastTime = -99f; _ultWindowUntil = 0f; _chainUntil = 0f;
            _ultCounting = false; _lastChainResetTime = -99f;
            CurrentSlot = SkillUpgradeScope.All;
            Suppressed = false;
            Landed = null;
            Cast = null;
            UltChained = null;
        }

        public static float StatValue(PlayerAgent player, PlayerStatId id) =>
            player != null && player.Stats != null && player.Stats.Tree != null ? player.Stats.Tree.Get(id) : 0f;

        public static SkillUpgradeScope SlotNow()
        {
            if (CurrentSlot != SkillUpgradeScope.All)
                return CurrentSlot;
            return HitSource.Current switch
            {
                HitSourceKind.Weapon => SkillUpgradeScope.Attack,
                HitSourceKind.Passive => SkillUpgradeScope.Charged,
                _ => SkillUpgradeScope.All,
            };
        }

        public static float AttackSpeedMultiplier()
        {
            float bonus = SkillUpgrades.Bonus(SkillUpgradeKind.AttackSpeed, SkillUpgradeScope.Attack);
            if (Time.time < _hasteUntil)
                bonus += SkillUpgrades.Exact(SkillUpgradeKind.CritStreakHaste, SkillUpgradeScope.Attack);
            return Mathf.Max(0.1f, 1f + bonus);
        }

        public static float ChainHasteBonus() =>
            _ultChain > 0 && Time.time < _chainUntil ? SkillUpgrades.Exact(SkillUpgradeKind.ChainHaste, SkillUpgradeScope.Ultimate) : 0f;

        public static bool MarkingEnabled =>
            SkillUpgrades.Exact(SkillUpgradeKind.MarkTaken, SkillUpgradeScope.Shock) > 0f
            || SkillUpgrades.Exact(SkillUpgradeKind.MarkCritChance, SkillUpgradeScope.Shock) > 0f
            || SkillUpgrades.Exact(SkillUpgradeKind.MarkCritDamage, SkillUpgradeScope.Shock) > 0f;

        public static void Mark(IDamageable target)
        {
            if (target == null || !MarkingEnabled)
                return;
            float time = MarkBaseTime * (1f + SkillUpgrades.Exact(SkillUpgradeKind.MarkDuration, SkillUpgradeScope.Shock));
            Marks[target] = Time.time + time;
        }

        public static bool IsMarked(IDamageable target)
        {
            if (target == null || !Marks.TryGetValue(target, out float until))
                return false;
            if (Time.time <= until)
                return true;
            Marks.Remove(target);
            return false;
        }

        public static float ChanceFor(SkillUpgradeScope slot, IDamageable target = null)
        {
            float chance = SkillUpgrades.Bonus(SkillUpgradeKind.CritChance, slot);
            if (Time.time < _dashUntil)
                chance += SkillUpgrades.Bonus(SkillUpgradeKind.DashCritWindow, SkillUpgradeScope.Dash);
            if (slot == SkillUpgradeScope.Attack)
            {
                if (Time.time < _stackUntil)
                    chance += _stacks * SkillUpgrades.Exact(SkillUpgradeKind.CritStackChance, SkillUpgradeScope.Attack);
                chance += SkillUpgrades.Exact(SkillUpgradeKind.CritStill, SkillUpgradeScope.Attack) * StillFactor();
                if (IsMarked(target))
                    chance += SkillUpgrades.Exact(SkillUpgradeKind.MarkCritChance, SkillUpgradeScope.Shock);
            }
            if (slot == SkillUpgradeScope.Ultimate)
                chance += _ultChain * SkillUpgrades.Exact(SkillUpgradeKind.ChainStageCrit, SkillUpgradeScope.Ultimate);
            return Mathf.Clamp01(chance);
        }

        public static float MultiplierFor(SkillUpgradeScope slot, IDamageable target = null, float distance = 0f)
        {
            float mult = BaseMultiplier + SkillUpgrades.Bonus(SkillUpgradeKind.CritDamage, slot);
            if (Time.time < _dashUntil)
                mult += SkillUpgrades.Exact(SkillUpgradeKind.DashCritDamage, SkillUpgradeScope.Dash);
            if (slot == SkillUpgradeScope.Attack)
                mult += SkillUpgrades.Exact(SkillUpgradeKind.CritFar, SkillUpgradeScope.Attack) * Mathf.Clamp01(distance / FarFullDistance);
            if (slot == SkillUpgradeScope.Ultimate)
                mult += _ultChain * SkillUpgrades.Exact(SkillUpgradeKind.ChainStageCritDamage, SkillUpgradeScope.Ultimate);
            if (IsMarked(target))
                mult += SkillUpgrades.Exact(SkillUpgradeKind.MarkCritDamage, SkillUpgradeScope.Shock);
            return mult;
        }

        private static float StillFactor() => Mathf.Clamp01((Time.time - _lastMoveTime) / StillFullTime);

        public static float Apply(PlayerAgent player, IDamageable target, float amount, out bool crit)
        {
            crit = false;
            if (Suppressed || amount <= 0f)
                return amount;

            float now = Time.time;
            float distance = 0f;
            if (player != null)
            {
                Vector3 pos = player.transform.position;
                if ((pos - _lastPos).sqrMagnitude > 0.04f)
                    _lastMoveTime = now;
                _lastPos = pos;
                if (target is Component part)
                    distance = Vector3.Distance(pos, part.transform.position);
            }

            SkillUpgradeScope slot = SlotNow();
            if (slot == SkillUpgradeScope.Ultimate)
                amount *= UltDamageScale();
            if (IsMarked(target))
                amount *= 1f + SkillUpgrades.Exact(SkillUpgradeKind.MarkTaken, SkillUpgradeScope.Shock);

            bool sure = slot == SkillUpgradeScope.Attack && _guaranteed > 0;
            if (sure)
                _guaranteed--;

            if (!sure)
            {
                float chance = Mathf.Clamp01(ChanceFor(slot, target) + StatValue(player, PlayerStatId.CritChance));
                if (chance <= 0f || UnityEngine.Random.value >= chance)
                {
                    if (slot == SkillUpgradeScope.Attack)
                        _streak = 0;
                    return amount;
                }
            }

            crit = true;
            amount *= MultiplierFor(slot, target, distance) + StatValue(player, PlayerStatId.CritDamage);
            OnCrit(player, slot);
            return amount;
        }

        private static float UltDamageScale()
        {
            float scale = 1f + _ultChain * SkillUpgrades.Exact(SkillUpgradeKind.ChainStageDamage, SkillUpgradeScope.Ultimate);
            if (SkillUpgrades.Exact(SkillUpgradeKind.ChainCap, SkillUpgradeScope.Ultimate) > 0f)
                scale *= Mathf.Max(0.1f, 1f - 0.15f * _ultChain);
            return scale;
        }

        private static void OnCrit(PlayerAgent player, SkillUpgradeScope slot)
        {
            float now = Time.time;

            Refund(player, SkillSlotId.Ultimate, SkillUpgrades.Exact(SkillUpgradeKind.CritRefundUlt, slot));
            Refund(player, SkillSlotId.Weapon, SkillUpgrades.Exact(SkillUpgradeKind.CritRefundSkill, slot));
            Refund(player, SkillSlotId.Dash, SkillUpgrades.Exact(SkillUpgradeKind.CritRefundDash, slot));
            SkillSlotId? self = slot switch
            {
                SkillUpgradeScope.Dash => SkillSlotId.Dash,
                SkillUpgradeScope.Shock => SkillSlotId.Weapon,
                SkillUpgradeScope.Ultimate => SkillSlotId.Ultimate,
                _ => null,
            };
            if (self.HasValue)
                Refund(player, self.Value, SkillUpgrades.Exact(SkillUpgradeKind.CritRefundSelf, slot));

            float proc = SkillUpgrades.Exact(SkillUpgradeKind.CritUltProc, slot);
            if (proc > 0f && UnityEngine.Random.value < proc && player != null && player.SkillFsm != null)
                player.SkillFsm.ReduceCooldown(SkillSlotId.Ultimate, 3f);

            if (slot == SkillUpgradeScope.Attack)
            {
                _stacks = now < _stackUntil ? Mathf.Min(StackMax, _stacks + 1) : 1;
                _stackUntil = now + StackTime;

                _streak++;
                if (_streak >= StreakNeed)
                {
                    _streak = 0;
                    _hasteUntil = now + StreakHasteTime;
                }
            }

            bool anyCounts = (slot == SkillUpgradeScope.Attack || slot == SkillUpgradeScope.Shock)
                && now <= _ultCastTime + UltCountAnyTime
                && SkillUpgrades.Exact(SkillUpgradeKind.UltCountAny, SkillUpgradeScope.Ultimate) > 0f;
            if (slot == SkillUpgradeScope.Ultimate || anyCounts)
                CountUltCrit(player, now);

            Landed?.Invoke(slot);
        }

        private static void Refund(PlayerAgent player, SkillSlotId target, float seconds)
        {
            if (seconds <= 0f || player == null || player.SkillFsm == null)
                return;

            float now = Time.time;
            if (now - _refundWindowStart >= 1f)
            {
                _refundWindowStart = now;
                _refundedInWindow = 0f;
            }

            float allowed = Mathf.Max(0f, RefundCapPerSecond - _refundedInWindow);
            float give = Mathf.Min(seconds, allowed);
            if (give <= 0f)
                return;

            _refundedInWindow += give;
            player.SkillFsm.ReduceCooldown(target, give);
        }

        public static bool TryChainReset(PlayerAgent player, int crits)
        {
            if (crits < 3 || player == null || player.SkillFsm == null)
                return false;
            if (SkillUpgrades.Exact(SkillUpgradeKind.ChainReset, SkillUpgradeScope.Shock) <= 0f)
                return false;
            if (Time.time - _lastChainResetTime < ChainResetInternalCooldown)
                return false;

            _lastChainResetTime = Time.time;
            player.SkillFsm.ReduceCooldown(SkillSlotId.Weapon, 9999f);
            return true;
        }

        public static void OnCast(PlayerAgent player, SkillSlotId slot)
        {
            float now = Time.time;
            if (slot == SkillSlotId.Dash)
            {
                _dashUntil = now + DashWindowBase + SkillUpgrades.Exact(SkillUpgradeKind.DashCritWindowTime, SkillUpgradeScope.Dash);
                _guaranteed = Mathf.RoundToInt(SkillUpgrades.Exact(SkillUpgradeKind.GuaranteedCrits, SkillUpgradeScope.Dash));
                _lastMoveTime = now;
                float refund = SkillUpgrades.Exact(SkillUpgradeKind.DashRefundUlt, SkillUpgradeScope.Dash);
                if (refund > 0f && player != null && player.SkillFsm != null)
                    player.SkillFsm.ReduceCooldown(SkillSlotId.Ultimate, refund);
            }
            else if (slot == SkillSlotId.Ultimate)
            {
                if (now > _chainUntil || !_ultCarry)
                    _ultChain = 0;
                _ultCarry = false;
                _ultCrits = 0;
                _ultCounting = true;
                _ultCastTime = now;
                _ultWindowUntil = now + UltDefaultWindow + SkillUpgrades.Exact(SkillUpgradeKind.UltWindow, SkillUpgradeScope.Ultimate);
                _chainUntil = now + ChainTime();
            }

            Cast?.Invoke(slot);
        }

        private static float ChainTime() => ChainBaseTime + SkillUpgrades.Exact(SkillUpgradeKind.ChainTime, SkillUpgradeScope.Ultimate);

        public static int UltNeed()
        {
            int need = UltDefaultNeed - Mathf.RoundToInt(SkillUpgrades.Exact(SkillUpgradeKind.UltCritNeed, SkillUpgradeScope.Ultimate));
            if (SkillUpgrades.Exact(SkillUpgradeKind.ChainCap, SkillUpgradeScope.Ultimate) > 0f)
                need += 2 * _ultChain;
            return Mathf.Max(1, need);
        }

        public static float UltWindowSeconds() => UltDefaultWindow + SkillUpgrades.Exact(SkillUpgradeKind.UltWindow, SkillUpgradeScope.Ultimate);
        public static float ChainSeconds() => ChainTime();
        public static int UltChainStage => _ultChain;

        public static bool TryGetUltProgress(out int crits, out int need, out float timeLeft)
        {
            crits = _ultCrits;
            need = UltNeed();
            timeLeft = Mathf.Max(0f, _ultWindowUntil - Time.time);
            return _ultCounting && timeLeft > 0f && _ultChain < UltChainCap();
        }

        public static int UltChainCap() =>
            UltDefaultChainCap + Mathf.RoundToInt(SkillUpgrades.Exact(SkillUpgradeKind.ChainCap, SkillUpgradeScope.Ultimate));

        private static void CountUltCrit(PlayerAgent player, float now)
        {
            if (!_ultCounting || now > _ultWindowUntil)
                return;

            _ultCrits++;
            if (_ultCrits < UltNeed() || _ultChain >= UltChainCap() || player == null || player.SkillFsm == null)
                return;

            _ultCounting = false;
            _ultChain++;
            _ultCarry = true;
            _chainUntil = now + ChainTime();
            player.SkillFsm.ReduceCooldown(SkillSlotId.Ultimate, 9999f);

            HitFeel.Play(0.07f, 0.4f);
            HitFeel.Zoom(0.045f, 0.4f);
            UltChained?.Invoke(_ultChain);
        }
    }
}

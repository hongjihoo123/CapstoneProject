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
    // 건슬링어 스킬 트리의 중심: 치명타.
    //  - 플레이어가 주는 모든 피해가 지나는 한 곳(PlayerAgent.ApplyScaledDamage)에서 Apply 를 부르면
    //    "어느 칸의 공격인가"(평타/충전탄/대쉬/충격파/궁극기)에 맞는 치명 확률·배율을 읽어 굴리고,
    //    치명이면 Landed 로 알림 -> 쿨 환류 / 게이지 / 연쇄 같은 효과가 여기서 한꺼번에 처리됨.
    //  - 값은 전부 SkillUpgrades 판에서 읽음 (스킬 트리를 안 찍으면 확률 0 이라 아무 일도 안 일어남).
    public static class CritSystem
    {
        public const float BaseMultiplier = 1.5f;    // 치명 기본 배율 (치명 피해 강화는 여기에 더함)
        private const float RefundCapPerSecond = 0.5f;   // 쿨 환류 합계 상한 (초당 0.5초)
        public const float UltDefaultWindow = 3f;       // 궁극기 파편 치명 집계 기본 시간
        public const int UltDefaultNeed = 8;            // 궁극기 초기화에 필요한 기본 파편 치명 횟수
        public const int UltDefaultChainCap = 1;        // 기본 연쇄 상한
        public const float ChainBaseTime = 6f;          // 연쇄가 이어지는 기본 시간
        private const float UltCountAnyTime = 4f;        // 정밀 집계: 궁극기 후 이 시간 동안
        private const int StackMax = 5;
        private const float StackTime = 3f;
        private const float StreakHasteTime = 5f;
        private const int StreakNeed = 5;
        private const float DashWindowBase = 2f;
        private const float StillFullTime = 2f;          // 정조준: 이만큼 가만히 있으면 가득
        private const float FarFullDistance = 14f;       // 급소 사격: 이 거리에서 가득
        private const float MarkBaseTime = 4f;
        private const float ChainResetInternalCooldown = 2f;

        // 지금 나가는 피해의 칸. PlayerSkillContext.DealDamage 가 감싸서 지정 (All = 지정 안 됨)
        public static SkillUpgradeScope CurrentSlot { get; set; } = SkillUpgradeScope.All;

        // 지속 피해(화상 틱 등)는 치명 안 남
        public static bool Suppressed { get; set; }

        // 치명이 났을 때 (슬롯). 충전탄 게이지 같은 구독자가 듣는다
        public static event Action<SkillUpgradeScope> Landed;

        // 스킬 시전 알림 (칸). 대쉬 시 게이지 같은 구독자가 듣는다
        public static event Action<SkillSlotId> Cast;

        // 궁극기 쿨이 초기화돼서 연쇄가 한 단계 올라감 (새 연쇄 단계). 배너/연출이 듣는다
        public static event Action<int> UltChained;

        // ---- 상태
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
        private static bool _ultCarry;          // 방금 쿨이 초기화돼서 이어지는 연쇄인가
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

        // 스탯 트리(PlayerStatId.CritChance / CritDamage) 몫. 스킬 트리 값에 더해서 쓰고, 스탯 창에도 같은 값이 보임
        // CritDamage 는 배율에 더하는 값 (0.5 = 치명 배율 +0.5)
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

        // 평타 공속 배율: 트리 강화 + 쾌감 사격 보너스
        public static float AttackSpeedMultiplier()
        {
            float bonus = SkillUpgrades.Bonus(SkillUpgradeKind.AttackSpeed, SkillUpgradeScope.Attack);
            if (Time.time < _hasteUntil)
                bonus += SkillUpgrades.Exact(SkillUpgradeKind.CritStreakHaste, SkillUpgradeScope.Attack);
            return Mathf.Max(0.1f, 1f + bonus);
        }

        // 연쇄 중 모든 쿨타임이 빨라짐 (연쇄 가속)
        public static float ChainHasteBonus() =>
            _ultChain > 0 && Time.time < _chainUntil ? SkillUpgrades.Exact(SkillUpgradeKind.ChainHaste, SkillUpgradeScope.Ultimate) : 0f;

        // ---- 표식 (충격파가 찍음)
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

        // 치명 확률 (표시/검증용으로도 씀). attacker/target 은 조건부 노드(가만히/표식)용 - 없으면 조건 없는 값만
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

        // 피해 하나를 굴림. 치명이면 배율을 곱해 돌려주고 crit = true
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

        // 연쇄 단계당 피해 +, 키 노드의 회차 감소 -15%
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

            // 치명 시 쿨 환류 (합계 초당 0.5초까지)
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

            // 환류 사격: 치명 시 일정 확률로 궁극기 쿨 -3초 (상한 없음 - 확률이 낮아서)
            float proc = SkillUpgrades.Exact(SkillUpgradeKind.CritUltProc, slot);
            if (proc > 0f && UnityEngine.Random.value < proc && player != null && player.SkillFsm != null)
                player.SkillFsm.ReduceCooldown(SkillSlotId.Ultimate, 3f);

            if (slot == SkillUpgradeScope.Attack)
            {
                // 치명의 방아쇠: 치명이 나면 3초간 확률 중첩
                _stacks = now < _stackUntil ? Mathf.Min(StackMax, _stacks + 1) : 1;
                _stackUntil = now + StackTime;

                // 쾌감 사격: 5연속 치명이면 5초간 공속
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

        // 충격파가 한 번에 3명 이상 치명을 냈을 때 (연쇄 충격). 내부 쿨 2초
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

        // ---- 스킬 시전 알림 (GenericSkillState.Enter 에서)
        public static void OnCast(PlayerAgent player, SkillSlotId slot)
        {
            float now = Time.time;
            if (slot == SkillSlotId.Dash)
            {
                _dashUntil = now + DashWindowBase + SkillUpgrades.Exact(SkillUpgradeKind.DashCritWindowTime, SkillUpgradeScope.Dash);
                _guaranteed = Mathf.RoundToInt(SkillUpgrades.Exact(SkillUpgradeKind.GuaranteedCrits, SkillUpgradeScope.Dash));
                _lastMoveTime = now;   // 대쉬는 움직임
                float refund = SkillUpgrades.Exact(SkillUpgradeKind.DashRefundUlt, SkillUpgradeScope.Dash);
                if (refund > 0f && player != null && player.SkillFsm != null)
                    player.SkillFsm.ReduceCooldown(SkillSlotId.Ultimate, refund);
            }
            else if (slot == SkillSlotId.Ultimate)
            {
                // 연쇄 시간이 지났으면 새로 시작, 아니면 이어서 (쿨이 방금 초기화돼서 이어 쏘는 것)
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

        // ---- HUD 가 읽는 값: 궁극기 초기화 규칙과 지금 진행도
        public static float UltWindowSeconds() => UltDefaultWindow + SkillUpgrades.Exact(SkillUpgradeKind.UltWindow, SkillUpgradeScope.Ultimate);
        public static float ChainSeconds() => ChainTime();
        public static int UltChainStage => _ultChain;

        // 파편 치명을 세는 중이면 true (지금까지 횟수, 필요한 횟수, 남은 집계 시간). 연쇄 상한에 닿았으면 세지 않음
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

            // 파편 치명이 충분 -> 궁극기 쿨 초기화, 다음 시전은 연쇄 한 단계 위
            _ultCounting = false;
            _ultChain++;
            _ultCarry = true;
            _chainUntil = now + ChainTime();
            player.SkillFsm.ReduceCooldown(SkillSlotId.Ultimate, 9999f);

            // 초기화되는 순간의 한 방: 짧은 멈춤 + 흔들림 + 살짝 다가감, 배너는 UltChained 를 듣는 쪽이 띄움
            HitFeel.Play(0.07f, 0.4f);
            HitFeel.Zoom(0.045f, 0.4f);
            UltChained?.Invoke(_ultChain);
        }
    }
}

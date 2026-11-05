using Members.JJH._02_Scripts.ElementsSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
using Members.JJH._02_Scripts.Systems.ModuleSystem;
using Members.KYR._01.Scripts.Combat;
using Members.KYR._01.Scripts.Stats;
using System;
using UnityEngine;

namespace Members.KYR._01.Scripts.Modules
{
    public class PlayerStatsModule : AbstractModule, ICombatStats
    {
        [Header("Formula")]
        [SerializeField] private CombatFormulaManagerSO combatFormula;

        [Header("Event")]
        [SerializeField] private EventChannelSO systemChannel;

        [Header("공격")]
        [SerializeField] private float damage = 1f;                 // 공격력
        [SerializeField] private float attackSpeed = 1f;            // 공격 속도
        [SerializeField] private float armorPenetration;            // 물리 관통력
        [SerializeField] private float magicPenetration;            // 마법 관통력
        [SerializeField] private float omnivamp;                    // 피해 흡혈
        [SerializeField] private float skillCooldownReduction;      // 스킬 쿨타임 감소
        [SerializeField] private float firePenetration;             // 화염 관통력
        [SerializeField] private float waterPenetration;            // 물 관통력
        [SerializeField] private float windPenetration;             // 바람 관통력
        [SerializeField] private float electricPenetration;         // 전기 관통력
        [SerializeField] private float earthPenetration;            // 대지 관통력

        [Header("방어")]
        [SerializeField] private float maxHp = 100f;                // 최대 체력
        [SerializeField] private float damageReduction;             // 피해 감소
        [SerializeField] private float incomingDamageTaken = 1f;    // 받는 피해 배율
        [SerializeField] private float healReceived = 1f;           // 받는 회복 배율
        [SerializeField] private float tenacity;                    // 강인함
        [SerializeField] private float armorDefense;                // 물리 방어력
        [SerializeField] private float magicDefense;                // 마법 방어력

        [Header("이동")]
        [SerializeField] private float moveSpeed = 6f;              // 이동 속도
        [SerializeField] private float dashSpeed = 1f;              // 대시 속도
        [SerializeField] private float dashDuration = 1f;           // 대시 지속시간

        public PlayerStatTree Tree { get; private set; }

        private float[] _snapshot;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            if (combatFormula != null)
                StatFormulas.Settings = combatFormula;

            Tree = new PlayerStatTree(new PlayerStatBases
            {
                Damage = damage,
                AttackSpeed = attackSpeed,
                ArmorPenetration = armorPenetration,
                MagicPenetration = magicPenetration,
                Omnivamp = omnivamp,
                SkillCooldownReduction = skillCooldownReduction,
                FirePenetration = firePenetration,
                WaterPenetration = waterPenetration,
                WindPenetration = windPenetration,
                ElectricPenetration = electricPenetration,
                EarthPenetration = earthPenetration,

                MaxHp = maxHp,
                DamageReduction = damageReduction,
                IncomingDamageTaken = incomingDamageTaken,
                HealReceived = healReceived,
                Tenacity = tenacity,
                ArmorDefense = armorDefense,
                MagicDefense = magicDefense,

                MoveSpeed = moveSpeed,
                DashSpeed = dashSpeed,
                DashDuration = dashDuration
            });

            _snapshot = new float[Enum.GetNames(typeof(PlayerStatId)).Length];

            if (systemChannel != null)
                systemChannel.AddListener<PlayerStatsRequestEvent>(HandleStatsRequest);

            Publish(true);
        }

        private void OnDestroy()
        {
            if (systemChannel != null)
                systemChannel.RemoveListener<PlayerStatsRequestEvent>(HandleStatsRequest);
        }

        private void HandleStatsRequest(PlayerStatsRequestEvent evt)
        {
            Publish(true);
        }

        private void Publish(bool force)
        {
            if (Tree == null || _snapshot == null)
                return;

            bool changed = force;
            for (int i = 1; i < _snapshot.Length; i++)
            {
                float value = Tree.Get((PlayerStatId)i);
                if (Mathf.Approximately(value, _snapshot[i]))
                    continue;

                _snapshot[i] = value;
                changed = true;
            }

            if (changed && systemChannel != null)
                systemChannel.RaiseEvent(SystemEvents.PlayerStatsChangedEvent.Init(_snapshot));
        }

        public float Get(PlayerStatId id)
        {
            return Tree != null ? Tree.Get(id) : NeutralCombatStats.Instance.Get(id);
        }

        /// <summary>마법 관통력 + 해당 속성 관통력. StatFormulas.Incoming의 elementPenetration에 사용.</summary>
        public float GetElementPenetration(ElementType element)
        {
            return Get(PlayerStatId.MagicPenetration) + Get(PlayerElementStats.PenetrationStat(element));
        }

        public void AddModifier(PlayerStatId id, StatModifier modifier)
        {
            Tree.AddModifier(id, modifier);
            Publish(false);
        }

        public void RemoveModifiers(object source)
        {
            Tree.RemoveModifiers(source);
            Publish(false);
        }

        public void Tick(float deltaTime)
        {
            if (Tree == null)
                return;

            Tree.Tick(deltaTime);
            Publish(false); // 시간제 모디파이어 만료 등 반영
        }

        [ContextMenu("Log Stats")]
        private void LogStats()
        {
            if (Tree == null)
            {
                Debug.Log($"{name} Stats tree is not initialized.", this);
                return;
            }

            Debug.Log(
                $"{name} [공격] Dmg={Get(PlayerStatId.Damage):0.##} AtkSpd={Get(PlayerStatId.AttackSpeed):0.##} " +
                $"ArmorPen={Get(PlayerStatId.ArmorPenetration):0.##} MagicPen={Get(PlayerStatId.MagicPenetration):0.##} " +
                $"Omni={Get(PlayerStatId.Omnivamp):0.##} Cdr={Get(PlayerStatId.SkillCooldownReduction):0.##} " +
                $"FirePen={Get(PlayerStatId.FirePenetration):0.##} WaterPen={Get(PlayerStatId.WaterPenetration):0.##} " +
                $"WindPen={Get(PlayerStatId.WindPenetration):0.##} ElecPen={Get(PlayerStatId.ElectricPenetration):0.##} " +
                $"EarthPen={Get(PlayerStatId.EarthPenetration):0.##}\n" +
                $"[방어] MaxHp={Get(PlayerStatId.MaxHp):0.##} DR={Get(PlayerStatId.DamageReduction):0.##} " +
                $"Taken={Get(PlayerStatId.IncomingDamageTaken):0.##} HealRecv={Get(PlayerStatId.HealReceived):0.##} " +
                $"Tenacity={Get(PlayerStatId.Tenacity):0.##} ArmorDef={Get(PlayerStatId.ArmorDefense):0.##} " +
                $"MagicDef={Get(PlayerStatId.MagicDefense):0.##}\n" +
                $"[이동] Move={Get(PlayerStatId.MoveSpeed):0.##} " +
                $"Dash={Get(PlayerStatId.DashSpeed):0.##}/{Get(PlayerStatId.DashDuration):0.##}",
                this);
        }
    }
}
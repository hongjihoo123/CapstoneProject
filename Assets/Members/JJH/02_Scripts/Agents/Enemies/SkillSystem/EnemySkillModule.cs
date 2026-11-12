using Assets.Members.HJH._02.Scripts.Char.Visual;
using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills;
using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.BossSkills;
using Members.JJH._02_Scripts.Systems.ModuleSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.EnemySkillSO;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem
{
    public class EnemySkillModule : AbstractModule, IEnemySkillModule, IAfterInitModule
    {
        [field: SerializeField] public AttackAreaVisualizer AreaVisualizer { get; private set; }
        [field: SerializeField] public LayerMask TargetLayer { get; private set; }

        public AbstractEnemy enemy { get; private set; }
        public float LastSkillEndTime { get; private set; } = -1f;


        protected Dictionary<Type, IEnemySkill> _skillDict = new Dictionary<Type, IEnemySkill>();
        private readonly Dictionary<Type, float> _skillCooldowns = new Dictionary<Type, float>();

        private bool _wasSkillUsing;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            enemy = _owner as AbstractEnemy;

            AreaVisualizer = GetComponent<AttackAreaVisualizer>();
            Debug.Assert(AreaVisualizer != null, $"{gameObject.name} : EnemySkillModule은 AttackAreaVisualizer를 가져야 합니다.");

            _skillDict = GetComponentsInChildren<IEnemySkill>().ToDictionary(module => module.GetType());
        }

        #region Skill Initialize

        public void AfterInitalize()
        {
            InitializeComponents();
            AfterInitializeComponents();
        }

        protected virtual void InitializeComponents()
        {
            foreach (IEnemySkill skill in _skillDict.Values)
            {
                skill.Initialize(enemy, this);
            }
        }

        protected virtual void AfterInitializeComponents()
        {
            foreach (IAfterInitModule module in _skillDict.Values.OfType<IAfterInitModule>())
            {
                module.AfterInitalize();
            }
        }

        #endregion

        private void Update()
        {
            bool isSkillUsing = IsSkillUsing();

            if (_wasSkillUsing && !isSkillUsing)
                LastSkillEndTime = Time.time;

            _wasSkillUsing = isSkillUsing;
        }

        public IEnemySkill GetTransitionSkill()
        {
            return _skillDict.Values.FirstOrDefault(skill => skill is ITransitionSkill);
        }

        public bool UseSkill(Type skillType)
        {
            if (!_skillDict.TryGetValue(skillType, out IEnemySkill skill) || skill.IsUsing)
                return false;

            var skillData = enemy.EnemyData.EnemySkills.FirstOrDefault(d => d != null && d.SkillType == skillType);
            if (skillData == null)
                return false;

            if (_skillCooldowns.TryGetValue(skillType, out float next) && Time.time < next)
                return false;

            skill.UseSkill();
            LastSkillEndTime = Time.time;
            _skillCooldowns[skillType] = Time.time + GetSkillSetting(skillData).Cooltime;
            return true;
        }

        public void CancelAllSkills()
        {
            foreach (IEnemySkill skill in _skillDict.Values)
                skill.CancelSkill();

            AreaVisualizer.Hide();
            LastSkillEndTime = Time.time;
        }

        public bool IsSkillUsing()
        {
            return _skillDict.Values.Any(skill => skill.IsUsing);
        }

        public bool HasAvailableSkill(float distance)
        {
            foreach (var data in enemy.EnemyData.EnemySkills.Where(data => data != null)
                                              .OrderByDescending(data => GetSkillSetting(data).Range))
            {
                EnemySkillSetting setting = GetSkillSetting(data);

                if (distance > setting.Range)
                    continue;

                Type skillType = data.SkillType;
                if (skillType == null || !_skillDict.TryGetValue(skillType, out IEnemySkill skill) || skill.IsUsing)
                    continue;

                if (_skillCooldowns.TryGetValue(skillType, out float nextAvailableTime) && Time.time < nextAvailableTime)
                    continue;

                return true;
            }

            return false;
        }

        public bool HasSkillInRange(float distance)
        {
            return enemy.EnemyData.EnemySkills.Any(data => data != null && data.SkillType != null
                                                                                            && distance <= GetSkillSetting(data).Range
                                                                                            && _skillDict.ContainsKey(data.SkillType));
        }

        private EnemySkillSetting GetSkillSetting(EnemySkillSO skillData)
        {
            if (skillData is NormalEnemySkillSO normal)
                return normal.SkillSetting;

            if (skillData is BossEnemySkillSO boss && enemy is BossEnemy bossEnemy)
                return bossEnemy.Phase == BossPhase.Phase2 ? boss.Phase2SkillSetting : boss.Phase1SkillSetting;

            return default;
        }
    }
}
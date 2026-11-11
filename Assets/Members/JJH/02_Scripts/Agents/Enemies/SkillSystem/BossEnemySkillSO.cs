using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem
{
    [CreateAssetMenu(fileName = "Boss Enemy Skill", menuName = "SO/Enemy/Boss Enemy Skill", order = 2)]
    public class BossEnemySkillSO : EnemySkillSO
    {
        [field: SerializeField] public EnemySkillSetting Phase1SkillSetting { get; private set; }
        [field: SerializeField] public EnemySkillSetting Phase2SkillSetting { get; private set; }
    }
}
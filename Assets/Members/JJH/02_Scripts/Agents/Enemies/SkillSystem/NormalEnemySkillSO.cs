using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem
{
    [CreateAssetMenu(fileName = "Normal Enemy Skill", menuName = "SO/Enemy/Normal Enemy Skill", order = 1)]
    public class NormalEnemySkillSO : EnemySkillSO
    {
        [field: SerializeField] public EnemySkillSetting SkillSetting { get; private set; }
    }
}
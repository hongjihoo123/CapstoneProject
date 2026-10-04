using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem
{
    public interface IEnemySkillModule
    {
        public IEnemySkill GetSkill<T>() where T : IEnemySkill;
        public void UseSkill<T>() where T : IEnemySkill;
    }
}
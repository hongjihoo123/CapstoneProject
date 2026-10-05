namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public interface IEnemySkill
    {
        void Initialize(AbstractEnemy owner);
        void UseSkill();
    }
}
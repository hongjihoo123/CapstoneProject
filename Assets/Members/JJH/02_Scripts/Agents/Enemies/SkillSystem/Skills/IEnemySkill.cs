namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public interface IEnemySkill
    {
        bool IsUsing { get; }

        void Initialize(AbstractEnemy owner, EnemySkillModule skillModule);
        void UseSkill();
    }
}
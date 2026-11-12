namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public interface IEnemySkill
    {
        EnemySkillSO SkillSO { get; }
        bool IsUsing { get; }

        void Initialize(AbstractEnemy owner, EnemySkillModule skillModule);
        void UseSkill();
        void CancelSkill();
    }
}
using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills;
using System;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem
{
    public interface IEnemySkillModule
    {
        IEnemySkill GetSkill<T>() where T : IEnemySkill;
        void UseSkill(Type skillType);
        bool IsSkillUsing();
        bool HasAvailableSkill(float distance);
    }
}
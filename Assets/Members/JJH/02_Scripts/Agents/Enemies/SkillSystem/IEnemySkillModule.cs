using Assets.Members.HJH._02.Scripts.Char.Visual;
using Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills;
using System;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem
{
    public interface IEnemySkillModule
    {
        float LastSkillEndTime { get; }

        AttackAreaVisualizer AreaVisualizer { get; }
        LayerMask TargetLayer { get; }
        AbstractEnemy enemy { get; }

        IEnemySkill GetSkill<T>() where T : IEnemySkill;
        void UseSkill(Type skillType);
        bool IsSkillUsing();
        bool HasAvailableSkill(float distance);
    }
}
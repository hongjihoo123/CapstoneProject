using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public abstract class PassiveData : ScriptableObject
    {
        public abstract void OnEnemyKilled(ISkillContext context);
    }
}

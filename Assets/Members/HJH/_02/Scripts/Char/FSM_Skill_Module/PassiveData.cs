using UnityEngine;
namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public abstract class PassiveData : ScriptableObject
    {
        // 트리거 조건은 캐릭터마다 다르게 구현해야함, 일단 임시방편임 이건
        public abstract void OnEnemyKilled(SkillStateModule owner);
    }
}
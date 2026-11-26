#if UNITY_EDITOR || PERF_PROBE
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Debugging
{
    public sealed class PerfSkillList : ScriptableObject
    {
        public SkillData[] skills;
    }
}
#endif

#if UNITY_EDITOR || PERF_PROBE
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Debugging
{
    // 측정용 빌드에만 잠깐 만들어지는 스킬 목록 (Resources/HJH/_PerfSkillList). 빌드 씬에 없는 스킬도 exe에서 돌려 보려고.
    // PerfProbeBuild 가 빌드 직전에 만들고 끝나면 지움.
    public sealed class PerfSkillList : ScriptableObject
    {
        public SkillData[] skills;
    }
}
#endif

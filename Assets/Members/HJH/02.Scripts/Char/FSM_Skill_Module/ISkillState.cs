using Members.KYR._01_Scripts.FSM.Core;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public interface ISkillState : ISkillCapabilities, IState
    {
        bool IsFinished { get; }
        float Cooldown { get; }
        bool IsReady { get; }
    }
}
using Members.KYR._01_Scripts.FSM.Core;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // A skill-machine state: what it allows while running, whether it is done, and its cooldown.
    public interface ISkillState : ISkillCapabilities, IState
    {
        bool IsFinished { get; }
        float Cooldown { get; }
        bool IsReady { get; }
    }
}
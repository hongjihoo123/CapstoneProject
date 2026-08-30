namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public interface ISkillCapabilities
    {
        bool AllowsMove { get; }
        bool AllowsFire { get; }
        float MoveSpeedMultiplier { get; }
    }
}
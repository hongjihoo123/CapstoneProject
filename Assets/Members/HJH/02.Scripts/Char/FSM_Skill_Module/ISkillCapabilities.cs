namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // What the player may do while the current skill runs (read by movement / weapon code).
    public interface ISkillCapabilities
    {
        bool AllowsMove { get; }
        bool AllowsFire { get; }
        bool AllowsReload { get; }
        float MoveSpeedMultiplier { get; }
    }
}
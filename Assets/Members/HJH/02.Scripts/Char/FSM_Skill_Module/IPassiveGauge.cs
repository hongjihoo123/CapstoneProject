namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // 게이지가 있는 패시브가 구현하는 거. UI(스킬바 패시브 칸)가 이걸 보고 게이지를 그림
    public interface IPassiveGauge
    {
        // 0 = 빈 게이지, 1 = 꽉 참
        float Fill { get; }
        bool IsFull { get; }
    }
}

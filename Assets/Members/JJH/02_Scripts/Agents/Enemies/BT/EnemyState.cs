using Unity.Behavior;

namespace Members.JJH._02_Scripts.Agents.Enemies.BT
{
    [BlackboardEnum]
    public enum EnemyState
    {
        IDLE,
        CHASE,
        NormalATTACK,
        SKILL,
        STUNNED,
        DEAD
    }
}
using Unity.Behavior;

namespace Members.JJH._02_Scripts.Agents.Enemies.BT
{
    [BlackboardEnum]
    public enum EnemyState
    {
        CHASE,
        ATTACK,
        STUNNED,
        DEAD
    }
}
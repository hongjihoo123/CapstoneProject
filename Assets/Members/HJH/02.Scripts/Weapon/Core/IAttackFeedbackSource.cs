using UnityEngine;

namespace RobotWeapons
{
    public struct AttackFeedback
    {
        public bool IsFire;
        public float ShakeForce;
        public bool HasTracer;
        public Vector3 TracerStart;
        public Vector3 TracerEnd;
    }

    public interface IAttackFeedbackSource
    {
        AttackFeedback DescribeAttack(string animId);
    }
}

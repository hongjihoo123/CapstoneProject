using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // Drawing surface for the aim preview while a skill key is held (implemented by AttackAreaVisualizer).
    public interface ISkillPreview
    {
        void Circle(Vector3 center, float radius);
        void Path(Vector3 from, Vector3 to, float width);

        // Thin guide lines: how far the skill can reach, and where it is pointing.
        void RangeCircle(Vector3 center, float radius);
        void DirectionLine(Vector3 from, Vector3 to);
    }
}

using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public interface ISkillPreview
    {
        void Circle(Vector3 center, float radius);
        void Path(Vector3 from, Vector3 to, float width);

        void RangeCircle(Vector3 center, float radius);
        void DirectionLine(Vector3 from, Vector3 to);
        void Cone(Vector3 apex, Vector3 direction, float radius, float angle);
    }
}

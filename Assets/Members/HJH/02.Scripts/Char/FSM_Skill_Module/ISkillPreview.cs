using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public interface ISkillPreview
    {
        void Circle(Vector3 center, float radius);
        void Path(Vector3 from, Vector3 to, float width);
    }
}

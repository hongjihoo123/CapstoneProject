using System;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public static class SkillImpacts
    {
        public static event Action<Vector3, float> Impacted;
        public static event Action<Vector3, float, SkillData> ImpactedBy;

        public static void Raise(Vector3 position, float radius, SkillData skill = null)
        {
            if (Impacted != null)
            {
                foreach (Delegate listener in Impacted.GetInvocationList())
                    Safe(() => ((Action<Vector3, float>)listener)(position, radius));
            }

            if (ImpactedBy != null)
            {
                foreach (Delegate listener in ImpactedBy.GetInvocationList())
                    Safe(() => ((Action<Vector3, float, SkillData>)listener)(position, radius, skill));
            }
        }

        private static void Safe(Action call)
        {
            try
            {
                call();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}

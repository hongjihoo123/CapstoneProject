using System;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // 스킬이 세상의 어디를 쳤는지 알리는 방송.
    // 스킬 시스템은 듣는 쪽(물 튀김, 연출 등)을 모르고, 듣는 쪽이 알아서 구독한다.
    public static class SkillImpacts
    {
        // 위치(월드), 반지름(m)
        public static event Action<Vector3, float> Impacted;
        // 위치, 반지름, 친 스킬 (모르면 null) — 스킬 중요도에 맞춘 연출용
        public static event Action<Vector3, float, SkillData> ImpactedBy;

        // 듣는 쪽 하나가 오류를 내도 스킬 코드까지 올라오지 않게 하나씩 따로 부른다.
        // (연출 오류 때문에 스킬이 끝 처리를 못 하면 회전 이펙트가 안 꺼지는 등 더 큰 문제가 생김)
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

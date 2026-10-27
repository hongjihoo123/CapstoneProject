using System;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // 스킬이 세상의 어디를 쳤는지 알리는 방송.
    // 스킬 시스템은 듣는 쪽(물 튀김, 바닥 자국 등)을 모르고, 듣는 쪽이 알아서 구독한다.
    public static class SkillImpacts
    {
        // 위치(월드), 반지름(m)
        public static event Action<Vector3, float> Impacted;

        // 듣는 쪽 하나가 오류를 내도 스킬 코드까지 올라오지 않게 하나씩 따로 부른다.
        // (연출 오류 때문에 스킬이 끝 처리를 못 하면 회전 이펙트가 안 꺼지는 등 더 큰 문제가 생김)
        public static void Raise(Vector3 position, float radius)
        {
            if (Impacted == null)
                return;

            foreach (Delegate listener in Impacted.GetInvocationList())
            {
                try
                {
                    ((Action<Vector3, float>)listener)(position, radius);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}

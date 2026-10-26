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

        public static void Raise(Vector3 position, float radius) => Impacted?.Invoke(position, radius);
    }
}

using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using UnityEngine;

namespace Assets.Members.HJH.Environment
{
    // 스킬 충격(SkillImpacts)을 듣고, 물 위면 물을 튀긴다.
    // 스킬 코드는 물을 모른다 → 새 스킬을 만들어도 자동으로 물이 튄다.
    // 장판·대쉬처럼 매 프레임 판정하는 스킬이 물보라를 도배하지 않도록, 최근 물보라 안쪽이면 건너뛴다.
    public class WaterSkillSplashListener : MonoBehaviour
    {
        [SerializeField] private float minRadius = 0.8f;
        [SerializeField] private float maxRadius = 3f;
        [SerializeField] private float strength = 1f;
        [Tooltip("이 시간 안에 이미 튄 자리(반지름 안)는 다시 안 튐")]
        [SerializeField] private float repeatWindow = 0.25f;

        private struct RecentSplash
        {
            public Vector2 Position;
            public float Radius;
            public float Time;
        }

        private readonly List<RecentSplash> _recent = new();

        private void OnEnable() => SkillImpacts.Impacted += OnImpact;
        private void OnDisable() => SkillImpacts.Impacted -= OnImpact;

        private void OnImpact(Vector3 position, float radius)
        {
            float now = Time.time;
            _recent.RemoveAll(s => now - s.Time > repeatWindow);

            var flat = new Vector2(position.x, position.z);
            foreach (RecentSplash s in _recent)
                if (Vector2.Distance(s.Position, flat) < s.Radius)
                    return;

            float r = Mathf.Clamp(radius, minRadius, maxRadius);
            if (JungleWater.Splash(position, r, strength))
                _recent.Add(new RecentSplash { Position = flat, Radius = r, Time = now });
        }
    }
}

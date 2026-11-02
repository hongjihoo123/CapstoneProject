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
        [SerializeField] private float minRadius = 1.2f;
        [SerializeField] private float maxRadius = 4f;
        [Tooltip("스킬이 터질 때 물결 세기 (걷는 물결보다 훨씬 크게: 스킬의 존재감)")]
        [SerializeField] private float skillSplashStrength = 1.1f;
        [Tooltip("이 시간 안에 이미 튄 자리(반지름 안)는 다시 안 튐. 장판·돌진이 물결을 도배하지 않게")]
        [SerializeField] private float repeatSeconds = 0.8f;

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
            _recent.RemoveAll(s => now - s.Time > repeatSeconds);

            var flat = new Vector2(position.x, position.z);
            foreach (RecentSplash s in _recent)
                if (Vector2.Distance(s.Position, flat) < s.Radius * 1.5f)
                    return;

            float r = Mathf.Clamp(radius, minRadius, maxRadius);
            if (JungleWater.Splash(position, r, skillSplashStrength))
                _recent.Add(new RecentSplash { Position = flat, Radius = r, Time = now });
        }
    }
}

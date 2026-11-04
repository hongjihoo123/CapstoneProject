using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using UnityEngine;

namespace Assets.Members.HJH.Environment
{
    // 스킬 충격(SkillImpacts)을 듣고, 물 위면 물을 튀긴다.
    // 스킬 코드는 물을 모른다 → 새 스킬을 만들어도 자동으로 물이 튄다.
    //
    // 2026-11-02 피드백: "한 번에 팍 튀고, 그 뒤엔 걸을 때 정도의 잔잔한 파동만 남게"
    //  - 처음 맞은 자리: 물보라(파티클) + 큰 물결 고리 한 번 (확!) — 0.9초면 가라앉음
    //  - 같은 자리를 잠시 동안 또 맞으면(장판·연속 판정): 물보라 없이 걷기만 한 작은 물결
    public class WaterSkillSplashListener : MonoBehaviour
    {
        [SerializeField] private float minRadius = 1.2f;
        [SerializeField] private float maxRadius = 4f;
        [Tooltip("처음 튈 때 물결 세기 (확! 한 번). 걷기(WaterRippleEmitter)는 0.12")]
        [SerializeField] private float skillSplashStrength = 1.4f;
        [Tooltip("처음 튈 때 물결 반지름 상한 (m). 물보라 파티클은 스킬 크기 그대로")]
        [SerializeField] private float splashRippleRadius = 2.6f;
        [Tooltip("같은 자리를 또 맞았을 때 물결 (걷기와 같은 정도)")]
        [SerializeField] private float aftershockStrength = 0.12f;
        [SerializeField] private float aftershockRadius = 0.6f;
        [Tooltip("이 시간 안에 같은 자리(반지름 안)를 또 맞으면 물보라 대신 잔잔한 물결만")]
        [SerializeField] private float repeatSeconds = 2.5f;
        [Tooltip("잔잔한 물결끼리도 이 간격보다 자주는 안 만듦 (매 프레임 판정하는 스킬)")]
        [SerializeField] private float aftershockInterval = 0.35f;

        private struct RecentSplash
        {
            public Vector2 Position;
            public float Radius;
            public float Time;
            public float LastAftershock;
        }

        private readonly List<RecentSplash> _recent = new();

        private void OnEnable() => SkillImpacts.Impacted += OnImpact;
        private void OnDisable() => SkillImpacts.Impacted -= OnImpact;

        private void OnImpact(Vector3 position, float radius)
        {
            float now = Time.time;
            for (int i = _recent.Count - 1; i >= 0; i--)
                if (now - _recent[i].Time > repeatSeconds)
                    _recent.RemoveAt(i);

            var flat = new Vector2(position.x, position.z);
            for (int i = 0; i < _recent.Count; i++)
            {
                RecentSplash s = _recent[i];
                if (Vector2.Distance(s.Position, flat) >= s.Radius * 1.5f)
                    continue;

                // 이미 크게 튄 자리 → 걷기 정도의 물결만, 너무 자주는 말고
                if (now - s.LastAftershock >= aftershockInterval && JungleWater.HasWater)
                {
                    JungleWater.Ripple(new Vector3(position.x, JungleWater.SurfaceHeight, position.z), aftershockRadius, aftershockStrength);
                    s.LastAftershock = now;
                    _recent[i] = s;
                }
                return;
            }

            float r = Mathf.Clamp(radius, minRadius, maxRadius);
            if (JungleWater.Splash(position, r, skillSplashStrength, Mathf.Min(r, splashRippleRadius)))
                _recent.Add(new RecentSplash { Position = flat, Radius = r, Time = now, LastAftershock = now });
        }
    }
}

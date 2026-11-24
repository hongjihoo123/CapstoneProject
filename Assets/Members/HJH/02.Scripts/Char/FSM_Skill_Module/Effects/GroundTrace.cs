using System.Collections.Generic;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Effects
{
    // 탑다운이라 탄·빔은 "땅에서 일정 높이"로 지형을 타고 가야 함.
    // 완전 수평으로 쏘면 오르막에선 땅에 박혀 사라지고, 내리막에선 적 머리 위로 지나감.
    // 걸어 올라갈 만한 경사는 따라가고, 벽·절벽·장애물은 그대로 막힘.
    public static class GroundTrace
    {
        // 한 번에 이 길이씩 끊어서 땅 높이를 다시 잼
        public const float StepLength = 0.75f;
        // 이 기울기(높이/거리)보다 가파르면 경사가 아니라 벽으로 봄 (약 60도)
        private const float MaxSlope = 1.75f;
        private const float MinHover = 0.4f;
        private const float MaxHover = 2.5f;

        // 출발점이 땅에서 얼마나 떠 있는지. 밑에 땅이 없으면 -1 (= 지형 안 따라가고 수평으로)
        public static float HoverOf(ISkillContext context, Vector3 origin)
        {
            if (!context.TryFindGround(origin, out Vector3 ground))
                return -1f;

            return Mathf.Clamp(origin.y - ground.y, MinHover, MaxHover);
        }

        // from 에서 수평 방향으로 distance 만큼 한 걸음. 도착점 높이는 그 자리 땅 + hover.
        // 벽·장애물에 막히면 true, next 는 막힌 자리
        public static bool Step(ISkillContext context, Vector3 from, Vector3 flatDirection, float distance, float hover,
            out Vector3 next)
        {
            next = from + flatDirection * distance;

            if (hover > 0f)
            {
                float climb = distance * MaxSlope + 0.1f;
                Vector3 probe = next + Vector3.up * climb;
                if (context.TryFindGround(probe, out Vector3 ground))
                {
                    float y = ground.y + hover;
                    // 너무 급하게 바뀌면(벽 위, 절벽 아래) 안 따라감 -> 수평으로 가다가 벽이면 아래 LineBlocked 에서 막힘
                    if (Mathf.Abs(y - from.y) <= climb)
                        next.y = y;
                }
            }

            if (context.LineBlocked(from, next, out Vector3 wall))
            {
                next = wall;
                return true;
            }

            return false;
        }

        // start 에서 range 만큼 지형을 타고 간 경로. points 는 start 포함, 마지막 점이 끝(또는 막힌 자리).
        // 막혔으면 true
        public static bool Trace(ISkillContext context, Vector3 start, Vector3 flatDirection, float range, List<Vector3> points)
        {
            points.Clear();
            points.Add(start);

            float hover = HoverOf(context, start);
            Vector3 position = start;
            float remaining = range;

            while (remaining > 0.0001f)
            {
                float step = Mathf.Min(StepLength, remaining);
                bool blocked = Step(context, position, flatDirection, step, hover, out position);
                points.Add(position);
                if (blocked)
                    return true;

                remaining -= step;
            }

            return false;
        }
    }
}

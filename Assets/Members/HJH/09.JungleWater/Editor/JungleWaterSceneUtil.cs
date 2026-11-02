using UnityEngine;

namespace Assets.Members.HJH.Environment.EditorTools
{
    // 씬에서 물 판 / 안개 판 찾기. 이름(Water, GroundFog)이 바뀌어도 되게 셰이더 이름으로 찾는다.
    internal static class JungleWaterSceneUtil
    {
        public static Renderer FindWaterSurface() => FindLargest("ShallowWater");
        public static Renderer FindFogPlane() => FindLargest("GroundFog");

        private static Renderer FindLargest(string shaderKeyword)
        {
            Renderer best = null;
            float bestArea = 0f;
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                Material m = r.sharedMaterial;
                if (m == null || m.shader == null || !m.shader.name.Contains(shaderKeyword))
                    continue;
                float area = r.bounds.size.x * r.bounds.size.z;
                if (area > bestArea)
                {
                    bestArea = area;
                    best = r;
                }
            }
            return best;
        }
    }
}

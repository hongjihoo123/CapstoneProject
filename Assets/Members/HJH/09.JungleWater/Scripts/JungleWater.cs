using UnityEngine;

namespace Assets.Members.HJH.Environment
{
    // 물 시스템의 바깥 창구. 스킬·캐릭터 코드는 이것만 부르면 된다.
    //  JungleWater.Splash(맞은 위치)  → 물 위면 큰 물결 + 물보라
    //  JungleWater.Ripple(위치, ...)   → 물결만
    //  JungleWater.IsInWater(위치)     → 그 위치가 물속인지
    public static class JungleWater
    {
        private static WaterRippleSimulation _simulation;
        private static WaterSplashPool _splashPool;

        public static bool HasWater => _simulation != null;
        public static float SurfaceHeight => _simulation != null ? _simulation.WaterHeight : float.NegativeInfinity;

        internal static void Register(WaterRippleSimulation simulation) => _simulation = simulation;
        internal static void Register(WaterSplashPool pool) => _splashPool = pool;

        internal static void Unregister(WaterRippleSimulation simulation)
        {
            if (_simulation == simulation)
                _simulation = null;
        }

        internal static void Unregister(WaterSplashPool pool)
        {
            if (_splashPool == pool)
                _splashPool = null;
        }

        // 발 위치처럼 "그 높이 자체"가 수면 아래인지
        public static bool IsInWater(Vector3 position, float tolerance = 0.05f) =>
            _simulation != null && _simulation.Contains(position) && position.y <= _simulation.WaterHeight + tolerance;

        public static void Ripple(Vector3 position, float radius, float strength)
        {
            if (_simulation != null)
                _simulation.AddDrop(position, radius, strength);
        }

        // 스킬 명중 위치처럼 공중일 수도 있는 지점: 아래 바닥이 물에 잠겨 있으면 튄다
        public static bool Splash(Vector3 position, float radius = 1.5f, float strength = 1f)
        {
            if (_simulation == null || !_simulation.IsOverWater(position))
                return false;

            var surface = new Vector3(position.x, _simulation.WaterHeight, position.z);
            _simulation.AddDrop(surface, radius, -strength);

            if (_splashPool != null)
                _splashPool.Spawn(surface, radius);

            return true;
        }
    }
}

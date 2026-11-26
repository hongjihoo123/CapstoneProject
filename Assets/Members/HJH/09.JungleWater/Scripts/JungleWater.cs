using UnityEngine;

namespace Assets.Members.HJH.Environment
{
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

        public static bool IsInWater(Vector3 position, float tolerance = 0.05f) =>
            _simulation != null && _simulation.Contains(position) && position.y <= _simulation.WaterHeight + tolerance;

        public static void Ripple(Vector3 position, float radius, float strength)
        {
            if (_simulation != null)
                _simulation.AddDrop(position, radius, strength);
        }

        public static bool Splash(Vector3 position, float radius = 1.5f, float strength = 1f, float rippleRadius = -1f)
        {
            if (_simulation == null || !_simulation.IsOverWater(position))
                return false;

            var surface = new Vector3(position.x, _simulation.WaterHeight, position.z);
            _simulation.AddDrop(surface, rippleRadius > 0f ? rippleRadius : radius, -strength);

            if (_splashPool != null)
                _splashPool.Spawn(surface, radius);

            return true;
        }
    }
}

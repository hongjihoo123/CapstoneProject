namespace RobotWeapons
{
    public interface IBurstWeapon
    {
        bool IsBursting { get; }
    }

    public interface IAttackSpeedScalable
    {
        float AttackSpeedMultiplier { get; set; }
    }

    public interface IReloadSpeedScalable
    {
        float ReloadSpeedMultiplier { get; set; }
    }

    public interface IUltimateWeapon
    {
        void ActivateUltimate(float duration);
    }

    public interface IComboWeapon
    {
        int ComboIndex { get; }
        int ComboCount { get; }
    }

    public interface IShotInterceptable
    {
        System.Func<UnityEngine.Vector3, UnityEngine.Vector3, bool> ShotInterceptor { get; set; }
        event System.Action ShotIntercepted;
    }

    public interface IAnimationTimedShots
    {
        bool WaitsForAnimation { get; }
        event System.Action ShotQueued;
        event System.Action FireFrame;
        void OnFireFrame();
    }

    public interface IMuzzleSource
    {
        UnityEngine.Vector3 CurrentMuzzle { get; }
    }

    public interface IEmpowerableShots
    {
        int EmpoweredShots { get; }
        void Empower(int shots, float damageMultiplier);
    }

    public interface IShotHitSource
    {
        event System.Action<IDamageable, bool> ShotHit;
    }

    public struct ShotVisual
    {
        public UnityEngine.Vector3 Start;
        public UnityEngine.Vector3 End;
        public bool Hit;
        public bool Blocked;
        public bool Empowered;
        public UnityEngine.Color Color;
        public bool Projectile;
    }

    public interface IShotVisualSource
    {
        event System.Action<ShotVisual> ShotFired;
        event System.Action<ShotProjectile> ProjectileLaunched;
    }

    public sealed class ShotProjectile
    {
        public UnityEngine.Vector3 Position;
        public UnityEngine.Vector3 Direction;
        public float Travelled;
        public bool Alive = true;
        public bool Hit;
        public bool Empowered;
        public UnityEngine.Color Color;
        public float Size;
    }

    public struct SwingVisual
    {
        public UnityEngine.Transform Origin;
        public float Reach;
        public int Step;
        public int StepCount;
        public float Delay;
        public float Duration;
        public UnityEngine.Color Color;
        public SwingShape Shape;
    }

    [System.Serializable]
    public class SwingShape
    {
        [UnityEngine.Tooltip("Arc length in degrees.")]
        public float arc = 150f;
        [UnityEngine.Tooltip("Sweep direction seen from above (flat arc): true = left to right.")]
        public bool clockwise = true;
        [UnityEngine.Tooltip("Turns the arc's center left(-)/right(+) around the character. 0 = centered on aim.")]
        public float yaw;
        [UnityEngine.Tooltip("Tilts the slash plane around the aim direction. 0 = flat, 45 = diagonal, 90 = vertical (overhead) slash.")]
        public float roll;
        [UnityEngine.Tooltip("Tips the slash plane forward(+)/back(-).")]
        public float pitch;
        [UnityEngine.Tooltip("Arc radius. 0 = reach of the hit box.")]
        public float radius;
        [UnityEngine.Tooltip("Height of the arc center above the aim origin (AimOrigin, chest height).")]
        public float height = 0.9f;
        public float width = 0.6f;
        [UnityEngine.Tooltip("Shifts when the slash starts, in seconds at attack speed 1 (+ = later than hitStart).")]
        public float delayOffset;
        [UnityEngine.Tooltip("Sweep time relative to the hit window (hitEnd - hitStart).")]
        public float durationScale = 1.3f;
        [UnityEngine.Tooltip("Ribbon always faces the camera instead of lying in the slash plane (helps steep slashes read from top-down).")]
        public bool faceCamera;

        public UnityEngine.Quaternion PlaneRotation => UnityEngine.Quaternion.Euler(pitch, yaw, roll);
    }

    public struct SwingHitVisual
    {
        public int Step;
        public int StepCount;
        public int Targets;
        public float ShakeForce;
    }

    public interface ISwingVisualSource
    {
        event System.Action<SwingVisual> Swung;
        event System.Action<SwingHitVisual> SwingHit;
    }
}

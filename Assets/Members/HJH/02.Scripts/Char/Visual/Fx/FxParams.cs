using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // Runtime values an effect prefab can't know by itself (where a beam ends, how big a blast is...).
    // Fx fills only what the effect needs; each driver component reads the fields it cares about.
    public struct FxParams
    {
        public Color Tint;
        public Vector3 Start;
        public Vector3 End;
        public float Radius;
        public float Width;
        // <= 0: the effect holds until its handle is killed.
        public float Duration;
        public float Delay;
        public float Value;
        public Transform Follow;
        public Vector3 Offset;
        public SwingShape Shape;
    }

    // A component on an effect prefab that needs runtime values. Play is called right after spawning.
    public interface IFxDriver
    {
        void Play(in FxParams parameters);
        // The prefab returns to the pool once no driver is playing and every particle has died.
        bool IsPlaying { get; }
        void Stop();
    }

    // Optional: drivers that react to a value changing while the effect runs (fill, stacks, charge...).
    public interface IFxValueReceiver
    {
        void SetValue(float value);
    }

    public interface IFxSpeedReceiver
    {
        void SetSpeed(float value);
    }

    // Optional: line drivers that can be re-aimed every frame (aim guides).
    public interface IFxPointsReceiver
    {
        void SetPoints(Vector3 start, Vector3 end);
    }

    public interface IFxTintReceiver
    {
        void SetTint(Color tint);
    }
}

using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    public struct FxParams
    {
        public Color Tint;
        public Vector3 Start;
        public Vector3 End;
        public float Radius;
        public float Width;
        public float Duration;
        public float Delay;
        public float Value;
        public Transform Follow;
        public Vector3 Offset;
        public SwingShape Shape;
        public string Text;
        public DamageNumberStyle NumberStyle;
        public float MaxRise;
    }

    public enum DamageNumberStyle
    {
        Normal,
        Crit,
        Taken,
        Magic,
        Weak
    }

    public interface IFxDriver
    {
        void Play(in FxParams parameters);
        bool IsPlaying { get; }
        void Stop();
    }

    public interface IFxValueReceiver
    {
        void SetValue(float value);
    }

    public interface IFxSpeedReceiver
    {
        void SetSpeed(float value);
    }

    public interface IFxPointsReceiver
    {
        void SetPoints(Vector3 start, Vector3 end);
    }

    public interface IFxTintReceiver
    {
        void SetTint(Color tint);
    }
}

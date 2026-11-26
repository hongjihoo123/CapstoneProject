using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Audio
{
    public sealed class SoundVoice
    {
        public AudioSource Source;
        public SoundCue Cue;
        public SoundPriority Priority;
        public float StartTime;
        public float BaseVolume;
        public bool Punchable;
        public Transform Follow;
        public float StopAt = -1f;
        public int Token;

        public bool IsActive => Source != null && Source.isPlaying;
    }

    public readonly struct SoundHandle
    {
        private readonly SoundVoice _voice;
        private readonly int _token;

        public SoundHandle(SoundVoice voice)
        {
            _voice = voice;
            _token = voice != null ? voice.Token : 0;
        }

        private bool Valid => _voice != null && _voice.Token == _token;

        public bool IsPlaying => Valid && _voice.IsActive;

        public void Stop()
        {
            if (!Valid)
                return;
            _voice.Token++;
            _voice.Follow = null;
            _voice.StopAt = -1f;
            if (_voice.Source != null)
                _voice.Source.Stop();
        }

        public void StopAfter(float seconds)
        {
            if (Valid)
                _voice.StopAt = Time.unscaledTime + Mathf.Max(0f, seconds);
        }

        public void Follow(Transform target)
        {
            if (Valid)
                _voice.Follow = target;
        }
    }
}

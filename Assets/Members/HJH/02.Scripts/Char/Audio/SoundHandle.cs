using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Audio
{
    // 소리 통 하나 (SoundManager 가 돌려 씀). 밖에서는 SoundHandle 로만 만짐.
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
        // 통이 다른 소리에 넘어가면 올라감 → 예전 핸들이 새 소리를 건드리지 않게
        public int Token;

        public bool IsActive => Source != null && Source.isPlaying;
    }

    // Play 가 돌려주는 표. 한 번 나고 끝나는 소리는 무시해도 됨.
    // 반복 소리·지속 소리·움직이는 대상을 따라가는 소리에 씀. 통이 이미 다른 소리로 넘어갔으면 아무 일도 안 함.
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
            _voice.Token++;   // 이 핸들은 여기서 끝
            _voice.Follow = null;
            _voice.StopAt = -1f;
            if (_voice.Source != null)
                _voice.Source.Stop();
        }

        // seconds 초 뒤에 끔 (장판 지속시간 같은 것). 실제 시간 기준이라 히트스톱 중에도 정확함
        public void StopAfter(float seconds)
        {
            if (Valid)
                _voice.StopAt = Time.unscaledTime + Mathf.Max(0f, seconds);
        }

        // 이 대상을 따라다니게 (적이 움직여도 소리가 따라감). null 이면 멈춤
        public void Follow(Transform target)
        {
            if (Valid)
                _voice.Follow = target;
        }
    }
}

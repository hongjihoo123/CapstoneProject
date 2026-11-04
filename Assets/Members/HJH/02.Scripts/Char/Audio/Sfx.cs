using System;
using System.Collections.Generic;
using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Audio
{
    // 효과음 하나 = 클립 여러 개(랜덤으로 하나) + 볼륨·피치 흔들기 + 너무 자주 안 울리게 하는 간격.
    [Serializable]
    public class SfxCue
    {
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 0.8f;
        [Tooltip("피치를 이 범위에서 랜덤으로 (같은 소리 반복이 덜 지루하게)")]
        public Vector2 pitch = new(0.94f, 1.06f);
        [Tooltip("이 시간(초) 안에 또 부르면 무시 (연사·장판이 소리를 도배하지 않게)")]
        public float minInterval = 0.03f;

        [NonSerialized] public float LastTime = -999f;

        public bool HasClips => clips != null && clips.Length > 0;
    }

    [Serializable]
    public class ElementSfx
    {
        public ElementType element;
        public SfxCue cast = new();
        public SfxCue impact = new();
    }

    // 효과음 재생. 소리 낼 곳에서 Sfx.Play(Sfx.Bank.swing, 위치) 만 부르면 됨.
    //  - AudioSource 를 미리 여러 개 만들어 돌려 씀 (매번 만들지 않음)
    //  - 탑다운이라 거리 감쇠 대신 화면 좌우 위치로 살짝 패닝
    //  - 히트스톱(시간 느려짐) 중에도 소리는 제 속도로
    public static class Sfx
    {
        private const string BankPath = "HJH/SfxBank";
        private const int Voices = 24;

        private static SfxBank _bank;
        private static bool _bankLoaded;
        private static readonly List<AudioSource> Sources = new();
        private static int _next;
        private static Transform _root;

        public static float MasterVolume = 1f;
        // 이번 플레이에서 실제로 울린 횟수 (테스트 확인용)
        public static int PlayedCount { get; private set; }

        public static SfxBank Bank
        {
            get
            {
                if (!_bankLoaded)
                {
                    _bankLoaded = true;
                    _bank = Resources.Load<SfxBank>(BankPath);
                }
                return _bank;
            }
        }

        // 어떤 칸의 소리를 내려고 했는지 (비어 있거나 꺼져 있어도 알림). 소리 고르기 창이 그 칸으로 따라감
        public static event Action<SfxCue> Requested;

        // 실제로는 다른 칸(대신 쓰는 소리)을 냈지만, 원래 이 칸을 위한 소리였다고 알림
        public static void Announce(SfxCue cue)
        {
            if (cue != null)
                Requested?.Invoke(cue);
        }

        public static void Play(SfxCue cue, Vector3 position, float volumeScale = 1f, float pitchScale = 1f)
        {
            if (cue == null)
                return;
            Requested?.Invoke(cue);
            if (!cue.HasClips || volumeScale <= 0f || Bank == null || Bank.muted)
                return;

            float now = Time.unscaledTime;
            // 이전 플레이의 값이 남아 있으면(now 가 더 작음) 막지 않음
            if (now >= cue.LastTime && now - cue.LastTime < cue.minInterval)
                return;
            cue.LastTime = now;

            AudioClip clip = cue.clips[UnityEngine.Random.Range(0, cue.clips.Length)];
            if (clip == null)
                return;

            AudioSource source = NextSource();
            if (source == null)
                return;

            source.clip = clip;
            source.volume = Mathf.Clamp01(cue.volume * volumeScale * MasterVolume);
            source.pitch = UnityEngine.Random.Range(cue.pitch.x, cue.pitch.y) * pitchScale;
            source.panStereo = PanOf(position);
            source.Play();
            PlayedCount++;
        }

        private static float PanOf(Vector3 position)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return 0f;
            Vector3 view = cam.WorldToViewportPoint(position);
            return Mathf.Clamp((view.x - 0.5f) * 1.2f, -0.6f, 0.6f);
        }

        // 쉬고 있는 소리 통을 찾고, 다 바쁘면 가장 오래된 걸 끊고 씀
        private static AudioSource NextSource()
        {
            if (_root == null)
            {
                Sources.Clear();
                var go = new GameObject("[Sfx]");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _root = go.transform;
                for (int i = 0; i < Voices; i++)
                {
                    var source = go.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.spatialBlend = 0f;
                    source.ignoreListenerPause = false;
                    Sources.Add(source);
                }
            }

            for (int i = 0; i < Sources.Count; i++)
            {
                int index = (_next + i) % Sources.Count;
                if (!Sources[index].isPlaying)
                {
                    _next = (index + 1) % Sources.Count;
                    return Sources[index];
                }
            }

            AudioSource oldest = Sources[_next];
            _next = (_next + 1) % Sources.Count;
            oldest.Stop();
            return oldest;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _bank = null;
            _bankLoaded = false;
            _root = null;
            Sources.Clear();
            _next = 0;
            PlayedCount = 0;
        }
    }
}

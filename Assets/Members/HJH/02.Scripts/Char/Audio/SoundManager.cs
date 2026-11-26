using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Assets.Members.HJH._02.Scripts.Char.Audio
{
    public class SoundManager : MonoBehaviour
    {
        private const string LibraryPath = "HJH/SfxBank";
        private const string MixerPath = "HJH/MainMixer";
        private const int Voices = 32;
        private const string VolumeKeyPrefix = "Sound.Vol.";

        private static readonly SoundSpatial DefaultSpatial = new();
        private static readonly string[] BusNames = Enum.GetNames(typeof(SoundBus));

        private static SoundManager _instance;
        private static bool _quitting;
        private static SoundLibrary _library;
        private static bool _libraryLoaded;

        public static float MasterVolume = 1f;
        public static int PlayedCount { get; private set; }

        public static SoundLibrary Library
        {
            get
            {
                if (!_libraryLoaded)
                {
                    _libraryLoaded = true;
                    _library = Resources.Load<SoundLibrary>(LibraryPath);
                }
                return _library;
            }
        }

        public static SoundSpatial Spatial => Library != null && Library.spatial != null ? Library.spatial : DefaultSpatial;

        public static SoundHandle Play(SoundCue cue, Vector3 position, float volumeScale = 1f, float pitchScale = 1f)
        {
            if (cue == null || !cue.HasClips || volumeScale <= 0f)
                return default;
            SoundLibrary library = Library;
            if (library == null || library.muted)
                return default;
            SoundManager manager = Instance;
            return manager == null ? default : manager.PlayInternal(cue, position, volumeScale, pitchScale);
        }

        private SoundHandle PlayInternal(SoundCue cue, Vector3 position, float volumeScale, float pitchScale)
        {
            float now = Time.unscaledTime;
            if (cue.minInterval > 0f && now >= cue.LastTime && now - cue.LastTime < cue.minInterval)
                return default;

            bool is3D = cue.space == SoundSpace.World3D;
            if (is3D && !SoundListener.IsAudible(position, Spatial, cue.rangeScale))
                return default;

            AudioClip clip = cue.clips[UnityEngine.Random.Range(0, cue.clips.Length)];
            if (clip == null)
                return default;

            SoundVoice voice = Acquire(cue);
            if (voice == null)
                return default;
            cue.LastTime = now;

            AudioSource source = voice.Source;
            voice.Token++;
            voice.Cue = cue;
            voice.Priority = cue.priority;
            voice.StartTime = now;
            voice.Follow = null;
            voice.StopAt = -1f;
            voice.Punchable = cue.bus == SoundBus.Sfx || cue.bus == SoundBus.Enemy;
            voice.BaseVolume = cue.volume * volumeScale * MasterVolume;

            source.clip = clip;
            source.loop = cue.loop;
            source.outputAudioMixerGroup = GroupOf(cue.bus);
            source.volume = Mathf.Clamp01(voice.BaseVolume * (voice.Punchable ? PunchGain : 1f)) * BusGain(cue.bus);
            source.pitch = UnityEngine.Random.Range(cue.pitch.x, cue.pitch.y) * pitchScale;
            if (is3D)
            {
                source.transform.position = position;
                Spatial.Apply(source, cue.rangeScale);
            }
            else
            {
                source.spatialBlend = 0f;
            }
            source.Play();
            PlayedCount++;
            return new SoundHandle(voice);
        }

        private SoundVoice Acquire(SoundCue cue)
        {
            if (cue.maxVoices > 0)
            {
                int count = 0;
                SoundVoice oldestSame = null;
                foreach (SoundVoice v in _voices)
                {
                    if (!v.IsActive || v.Cue != cue)
                        continue;
                    count++;
                    if (oldestSame == null || v.StartTime < oldestSame.StartTime)
                        oldestSame = v;
                }
                if (count >= cue.maxVoices)
                    return Reclaim(oldestSame);
            }

            for (int i = 0; i < _voices.Count; i++)
            {
                int index = (_next + i) % _voices.Count;
                if (!_voices[index].IsActive)
                {
                    _next = (index + 1) % _voices.Count;
                    return _voices[index];
                }
            }

            SoundVoice victim = null;
            foreach (SoundVoice v in _voices)
            {
                if (v.Priority > cue.priority)
                    continue;
                if (victim == null || v.Priority < victim.Priority ||
                    (v.Priority == victim.Priority && v.StartTime < victim.StartTime))
                    victim = v;
            }
            return victim == null ? null : Reclaim(victim);
        }

        private static SoundVoice Reclaim(SoundVoice voice)
        {
            voice.Token++;
            voice.Source.Stop();
            return voice;
        }

        public static AudioMixerGroup GroupOf(SoundBus bus)
        {
            SoundManager manager = Instance;
            return manager == null ? null : manager._groups[(int)bus];
        }

        public static void SetBusVolume(SoundBus bus, float linear) => SetVolume(bus.ToString(), linear);
        public static void SetMasterBusVolume(float linear)
        {
            linear = Mathf.Clamp01(linear);
            PlayerPrefs.SetFloat(VolumeKeyPrefix + "Master", linear);
            AudioListener.volume = linear;
        }

        public static float GetBusVolume(SoundBus bus) => PlayerPrefs.GetFloat(VolumeKeyPrefix + bus, 1f);
        public static float GetMasterBusVolume() => PlayerPrefs.GetFloat(VolumeKeyPrefix + "Master", 1f);

        public static float BusGain(SoundBus bus) => GroupOf(bus) != null ? 1f : GetBusVolume(bus);

        private static void SetVolume(string name, float linear)
        {
            linear = Mathf.Clamp01(linear);
            PlayerPrefs.SetFloat(VolumeKeyPrefix + name, linear);
            SoundManager manager = Instance;
            if (manager == null)
                return;
            manager.ApplyVolume(name, linear);
            manager.RefreshVolumes(false);
        }

        private void ApplyVolume(string name, float linear)
        {
            if (_mixer == null)
                return;
            float db = linear <= 0.0001f ? -80f : Mathf.Clamp(20f * Mathf.Log10(linear), -80f, 0f);
            _mixer.SetFloat("Vol_" + name, db);
        }

        private static float _punchGain = 1f;
        private static float _punchStart = -999f;
        private static float _punchHold;
        private static float _punchRelease;

        public static void Punch(float gain, float hold, float release)
        {
            if (gain <= 1f)
                return;
            if (PunchGain > gain)
                return;

            _punchGain = gain;
            _punchStart = Time.unscaledTime;
            _punchHold = Mathf.Max(0f, hold);
            _punchRelease = Mathf.Max(0.01f, release);
            if (_instance != null)
                _instance.RefreshVolumes(true);
        }

        public static float PunchGain
        {
            get
            {
                float t = Time.unscaledTime - _punchStart;
                if (t < 0f || t >= _punchHold + _punchRelease)
                    return 1f;
                if (t <= _punchHold)
                    return _punchGain;
                return Mathf.Lerp(_punchGain, 1f, Mathf.SmoothStep(0f, 1f, (t - _punchHold) / _punchRelease));
            }
        }

        private void RefreshVolumes(bool punchOnly)
        {
            float gain = PunchGain;
            foreach (SoundVoice v in _voices)
            {
                if (!v.IsActive || v.Cue == null || (punchOnly && !v.Punchable))
                    continue;
                v.Source.volume = Mathf.Clamp01(v.BaseVolume * (v.Punchable ? gain : 1f)) * BusGain(v.Cue.bus);
            }
        }

        private readonly List<SoundVoice> _voices = new();
        private readonly AudioMixerGroup[] _groups = new AudioMixerGroup[BusNames.Length];
        private AudioMixer _mixer;
        private int _next;
        private bool _wasPunching;

        private static SoundManager Instance
        {
            get
            {
                if (_instance == null && Application.isPlaying && !_quitting)
                    Create();
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _quitting = false;
            _library = null;
            _libraryLoaded = false;
            _punchGain = 1f;
            _punchStart = -999f;
            PlayedCount = 0;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        private static void OnQuitting() => _quitting = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => _ = Instance;

        private static void Create()
        {
            var go = new GameObject("[SoundManager]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SoundManager>();
        }

        private void Awake()
        {
            _mixer = Resources.Load<AudioMixer>(MixerPath);
            if (_mixer != null)
            {
                for (int i = 0; i < BusNames.Length; i++)
                {
                    AudioMixerGroup[] found = _mixer.FindMatchingGroups("Master/" + BusNames[i]);
                    _groups[i] = found.Length > 0 ? found[0] : null;
                    ApplyVolume(BusNames[i], GetBusVolume((SoundBus)i));
                }
            }
            AudioListener.volume = GetMasterBusVolume();

            for (int i = 0; i < Voices; i++)
            {
                var child = new GameObject($"Voice_{i}");
                child.transform.SetParent(transform, false);
                AudioSource source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                _voices.Add(new SoundVoice { Source = source });
            }
        }

        private void Update()
        {
            bool punching = PunchGain > 1f;
            if (punching || _wasPunching)
                RefreshVolumes(true);
            _wasPunching = punching;

            float now = Time.unscaledTime;
            foreach (SoundVoice v in _voices)
            {
                if (!v.IsActive)
                    continue;
                if (v.Follow != null)
                    v.Source.transform.position = v.Follow.position;
                if (v.StopAt >= 0f && now >= v.StopAt)
                {
                    v.Token++;
                    v.StopAt = -1f;
                    v.Source.Stop();
                }
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}

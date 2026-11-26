using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Assets.Members.HJH._02.Scripts.Char.Audio
{
    // 효과음 재생 창구. 씬에 자동으로 하나 생김 (배치 필요 없음 — 어느 씬에서 시작해도 동작).
    //   사용: SoundManager.Play(cue, 위치)  →  SoundHandle (반복·지속·따라가기 소리에만 필요)
    //   - 소리 통(SoundVoice) 32개를 미리 만들어 돌려 씀. 가득 차면 우선순위가 낮은 것부터 끊음
    //   - 같은 소리 동시 개수 제한(maxVoices), 너무 멀어 안 들리는 소리는 통을 쓰지 않음
    //   - 믹서 그룹(SoundBus)으로 나가서 옵션 음량·리미터가 먹음. 믹서가 없으면 그냥 재생 (Resources/HJH/MainMixer)
    //   - 3D 설정은 SoundLibrary.spatial, "귀"는 SoundListener (플레이어 발밑)
    //   - 히트스톱(시간 느려짐) 중에도 소리는 제 속도로
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
        // 이번 플레이에서 실제로 울린 횟수 (테스트 확인용)
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

        // 3D 설정 (라이브러리가 없으면 기본값)
        public static SoundSpatial Spatial => Library != null && Library.spatial != null ? Library.spatial : DefaultSpatial;

        // ------------------------------------------------ 재생

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
                return default;   // 이전 플레이의 값이 남아 있으면(now 가 더 작음) 막지 않음

            bool is3D = cue.space == SoundSpace.World3D;
            if (is3D && !SoundListener.IsAudible(position, Spatial, cue.rangeScale))
                return default;   // 너무 멀어서 안 들림 — 소리 통 아끼기

            AudioClip clip = cue.clips[UnityEngine.Random.Range(0, cue.clips.Length)];
            if (clip == null)
                return default;

            SoundVoice voice = Acquire(cue);
            if (voice == null)
                return default;   // 가득 찼고 전부 더 중요한 소리
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

        // 통 고르기: 같은 소리가 상한이면 그중 가장 오래된 것 → 쉬는 통 → 더 덜 중요한 통 순
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
            voice.Token++;   // 이 통을 가진 예전 핸들 무효화
            voice.Source.Stop();
            return voice;
        }

        // ------------------------------------------------ 믹서·음량

        // 믹서 그룹. 믹서가 없거나 그룹이 없으면 null (그냥 기본 출력)
        public static AudioMixerGroup GroupOf(SoundBus bus)
        {
            SoundManager manager = Instance;
            return manager == null ? null : manager._groups[(int)bus];
        }

        // 0~1 슬라이더 값 (설정 창). 저장하고 바로 반영
        //  - 전체: AudioListener.volume (SoundManager 를 안 거치는 소리까지 전부)
        //  - 종류별: 믹서가 있으면 Vol_<이름> 파라미터에 dB 로, 없으면 소리 통 음량에 곱함
        public static void SetBusVolume(SoundBus bus, float linear) => SetVolume(bus.ToString(), linear);
        public static void SetMasterBusVolume(float linear)
        {
            linear = Mathf.Clamp01(linear);
            PlayerPrefs.SetFloat(VolumeKeyPrefix + "Master", linear);
            AudioListener.volume = linear;
        }

        public static float GetBusVolume(SoundBus bus) => PlayerPrefs.GetFloat(VolumeKeyPrefix + bus, 1f);
        public static float GetMasterBusVolume() => PlayerPrefs.GetFloat(VolumeKeyPrefix + "Master", 1f);

        // 소리에 직접 곱할 종류별 음량. 믹서 그룹이 있으면 믹서가 줄이므로 1
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
            _mixer.SetFloat("Vol_" + name, db);   // 믹서에 노출 안 된 파라미터면 조용히 false
        }

        // ------------------------------------------------ 볼륨 펀치
        // 강한 처치 같은 순간에 효과음 전체를 gain 배로 확 키웠다가(hold 동안 유지) release 초 동안 부드럽게 원래대로.
        // 지금 울리고 있는 소리 + 그동안 새로 나는 소리 모두에 적용. 시간은 실제 시간 (슬로모션과 상관없이)
        private static float _punchGain = 1f;
        private static float _punchStart = -999f;
        private static float _punchHold;
        private static float _punchRelease;

        public static void Punch(float gain, float hold, float release)
        {
            if (gain <= 1f)
                return;
            // 센 쪽이 이김
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

        // 울리고 있는 소리의 음량 다시 계산 (punchOnly = 펀치가 먹는 소리만)
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

        // ------------------------------------------------ 인스턴스

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
                RefreshVolumes(true);   // 끝난 뒤 한 번 더 → 정확히 원래 볼륨으로
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

using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Members.HJH._02.Scripts.Char.Character;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Audio
{
    // 스킬별 소리(SkillSoundData)를 제때 재생. 씬에 자동으로 하나 생김.
    //  - 몸 동작 소리: 클립의 AnimationEvent PlaySkillSound(eventName) → PlayerAnimationEventRelay → 여기
    //    (같은 클립을 여러 스킬이 같이 쓰니까, 지금 쓰는 스킬의 목록에서 그 이름을 찾아 냄)
    //  - 파티클 시점 소리: 스킬 이펙트 칸이 터질 때(ElementSkillCast.FxPlayed) / 스킬 쓴 순간 / 범위 공격이 닿은 순간 + time 초
    //  - 캐릭터 모델이 없으면(애니 이벤트가 안 옴) Animation 소리는 fallbackDelay 로 대신
    // 소리 쪽 오류가 스킬 코드로 올라가지 않게 듣는 함수는 전부 막아 둠.
    public class SkillSoundPlayer : MonoBehaviour
    {
        private const string Folder = "HJH/SkillSounds";
        private const int Voices = 16;
        private const float AnimEventWindow = 4f;   // 스킬 쓴 뒤 이 시간 안에 온 애니 이벤트만 그 스킬 것으로 봄

        private sealed class Caster
        {
            public SkillData Skill;
            public float CastTime = -999f;
            public Transform Transform;
            public Action<SkillUsedInfo> Handler;
        }

        private struct Pending
        {
            public float At;
            public SkillSoundEntry Entry;
            public Vector3 Position;
        }

        private static SkillSoundPlayer _instance;
        private static Dictionary<SkillData, SkillSoundData> _bySkill;

        private readonly Dictionary<SkillStateModule, Caster> _casters = new();
        private readonly List<Pending> _pending = new();
        private readonly List<AudioSource> _sources = new();
        private readonly Dictionary<AudioSource, float> _stopAt = new();
        private int _next;
        private float _nextScan;

        public static int PlayedCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null)
                return;
            var go = new GameObject("[SkillSoundPlayer]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SkillSoundPlayer>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _bySkill = null;
            PlayedCount = 0;
        }

        public static SkillSoundData Of(SkillData skill)
        {
            if (skill == null)
                return null;
            // SfxBank 의 skillSounds 가 꺼져 있으면 전용 소리 없음 → 재생도 안 하고, 공통 소리도 안 막음
            if (Sfx.Bank == null || !Sfx.Bank.skillSounds)
                return null;
            if (_bySkill == null)
            {
                _bySkill = new Dictionary<SkillData, SkillSoundData>();
                foreach (SkillSoundData data in Resources.LoadAll<SkillSoundData>(Folder))
                    if (data != null && data.skill != null)
                        _bySkill[data.skill] = data;
            }
            return _bySkill.TryGetValue(skill, out SkillSoundData found) ? found : null;
        }

        // SkillPresentation 이 공통 시전음/폭발음을 낼지 물어봄
        public static bool ReplacesGenericCast(SkillData skill) => Of(skill) is { replacesGenericCast: true };
        public static bool ReplacesGenericImpact(SkillData skill) => Of(skill) is { replacesGenericImpact: true };

        // ------------------------------------------------ 듣기

        private void OnEnable()
        {
            SkillImpacts.ImpactedBy += OnImpact;
            ElementSkillCast.FxPlayed += OnFx;
        }

        private void OnDisable()
        {
            SkillImpacts.ImpactedBy -= OnImpact;
            ElementSkillCast.FxPlayed -= OnFx;
            foreach (var pair in _casters)
                if (pair.Key != null)
                    pair.Key.SkillCasting -= pair.Value.Handler;
            _casters.Clear();
        }

        // 애니 이벤트에서 부름 (PlayerAnimationEventRelay.PlaySkillSound)
        public static void OnAnimationEvent(SkillStateModule module, string eventName)
        {
            if (_instance == null || module == null || string.IsNullOrEmpty(eventName))
                return;
            try
            {
                if (!_instance._casters.TryGetValue(module, out Caster caster) || caster.Skill == null)
                    return;
                if (Time.time - caster.CastTime > AnimEventWindow)
                    return;
                SkillSoundData data = Of(caster.Skill);
                if (data == null)
                    return;
                foreach (SkillSoundEntry e in data.entries)
                    if (e.trigger == SkillSoundTrigger.Animation && e.eventName == eventName)
                        _instance.Play(e, caster.Transform.position);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void OnCast(Caster caster, SkillUsedInfo info)
        {
            try
            {
                caster.Skill = info.Data;
                caster.CastTime = Time.time;
                SkillSoundData data = Of(info.Data);
                if (data == null)
                    return;
                bool animated = HasAnimator(caster.Transform);
                Vector3 at = caster.Transform.position;
                foreach (SkillSoundEntry e in data.entries)
                {
                    if (e.trigger == SkillSoundTrigger.Cast)
                        Schedule(e, e.time, at);
                    else if (e.trigger == SkillSoundTrigger.Animation && !animated)
                        Schedule(e, e.fallbackDelay, at);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void OnFx(ElementSkillBase skill, FxSlot slot, Vector3 position)
        {
            try
            {
                SkillSoundData data = Of(skill);
                if (data == null)
                    return;
                string name = SlotName(skill, slot);
                if (name == null)
                    return;
                foreach (SkillSoundEntry e in data.entries)
                    if (e.trigger == SkillSoundTrigger.Fx && e.fxSlot == name)
                        Schedule(e, e.time, position);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void OnImpact(Vector3 position, float radius, SkillData skill)
        {
            if (radius < 0.9f)
                return;   // 적 하나 맞힌 건 hitFx 소리가 맡음
            try
            {
                SkillSoundData data = Of(skill);
                if (data == null)
                    return;
                foreach (SkillSoundEntry e in data.entries)
                    if (e.trigger == SkillSoundTrigger.Impact)
                        Schedule(e, e.time, position);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        // ------------------------------------------------ 재생

        private void Schedule(SkillSoundEntry e, float delay, Vector3 position)
        {
            if (delay <= 0f)
                Play(e, position);
            else
                _pending.Add(new Pending { At = Time.time + delay, Entry = e, Position = position });
        }

        private void Play(SkillSoundEntry e, Vector3 position)
        {
            if (e.clip == null)
                return;
            SfxBank bank = Sfx.Bank;
            if (bank != null && bank.muteFxSounds)
                return;
            float now = Time.unscaledTime;
            if (now >= e.LastPlayed && now - e.LastPlayed < e.minInterval)
                return;
            e.LastPlayed = now;

            AudioSource source = NextSource();
            source.clip = e.clip;
            source.loop = e.loop;
            source.volume = Mathf.Clamp01(e.volume * Sfx.MasterVolume);
            source.pitch = 1f + UnityEngine.Random.Range(-e.pitchRandom, e.pitchRandom);
            source.panStereo = PanOf(position);
            source.Play();
            if (e.holdTime > 0f)
                _stopAt[source] = Time.time + e.holdTime;
            else
                _stopAt.Remove(source);
            PlayedCount++;
        }

        private void Update()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < _pending[i].At)
                    continue;
                Pending p = _pending[i];
                _pending.RemoveAt(i);
                Play(p.Entry, p.Position);
            }

            if (_stopAt.Count > 0)
            {
                List<AudioSource> done = null;
                foreach (var pair in _stopAt)
                    if (pair.Key == null || Time.time >= pair.Value)
                        (done ??= new List<AudioSource>()).Add(pair.Key);
                if (done != null)
                    foreach (AudioSource s in done)
                    {
                        _stopAt.Remove(s);
                        if (s != null)
                            s.Stop();
                    }
            }

            ScanCasters();
        }

        // 플레이어(스킬 모듈)가 늦게 생기거나 바뀌어도 잡히게 가끔 다시 찾음
        private void ScanCasters()
        {
            if (Time.unscaledTime < _nextScan)
                return;
            _nextScan = Time.unscaledTime + 1f;
            foreach (SkillStateModule m in FindObjectsByType<SkillStateModule>(FindObjectsSortMode.None))
            {
                if (_casters.ContainsKey(m))
                    continue;
                var caster = new Caster { Transform = m.transform };
                caster.Handler = info => OnCast(caster, info);
                _casters.Add(m, caster);
                m.SkillCasting += caster.Handler;
            }
        }

        private static bool HasAnimator(Transform caster)
        {
            CharacterModel model = caster.GetComponentInParent<CharacterModel>();
            if (model == null)
                model = caster.GetComponentInChildren<CharacterModel>();
            return model != null && model.Animator != null && model.Animator.isActiveAndEnabled;
        }

        private AudioSource NextSource()
        {
            if (_sources.Count == 0)
            {
                for (int i = 0; i < Voices; i++)
                {
                    var s = gameObject.AddComponent<AudioSource>();
                    s.playOnAwake = false;
                    s.spatialBlend = 0f;
                    _sources.Add(s);
                }
            }
            for (int i = 0; i < _sources.Count; i++)
            {
                int index = (_next + i) % _sources.Count;
                if (!_sources[index].isPlaying)
                {
                    _next = (index + 1) % _sources.Count;
                    return _sources[index];
                }
            }
            AudioSource oldest = _sources[_next];
            _next = (_next + 1) % _sources.Count;
            oldest.Stop();
            return oldest;
        }

        private static float PanOf(Vector3 position)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return 0f;
            Vector3 view = cam.WorldToViewportPoint(position);
            return Mathf.Clamp((view.x - 0.5f) * 1.2f, -0.6f, 0.6f);
        }

        // 이펙트 칸 객체 → 필드 이름 (castFx, impactFx ...)
        private static string SlotName(ElementSkillBase skill, FxSlot slot)
        {
            if (skill == null || slot == null)
                return null;
            for (Type t = skill.GetType(); t != null && t != typeof(ScriptableObject); t = t.BaseType)
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (f.FieldType == typeof(FxSlot) && ReferenceEquals(f.GetValue(skill), slot))
                        return f.Name;
            return null;
        }
    }
}

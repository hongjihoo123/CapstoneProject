using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Members.HJH._02.Scripts.Char.Character;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Audio
{
    public class SkillSoundPlayer : MonoBehaviour
    {
        private const string Folder = "HJH/SkillSounds";
        private const float AnimEventWindow = 4f;

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
            if (SoundManager.Library == null || !SoundManager.Library.skillSounds)
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

        public static bool ReplacesGenericCast(SkillData skill) => Of(skill) is { replacesGenericCast: true };
        public static bool ReplacesGenericImpact(SkillData skill) => Of(skill) is { replacesGenericImpact: true };

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
                return;
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
            SoundLibrary library = SoundManager.Library;
            if (library != null && library.muteFxSounds)
                return;

            SoundHandle handle = SoundManager.Play(e.Cue, position);
            if (!handle.IsPlaying)
                return;
            if (e.holdTime > 0f)
                handle.StopAfter(e.holdTime);
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

            ScanCasters();
        }

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

using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Audio;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public class SkillPresentation : MonoBehaviour
    {
        private const float CooldownPerWeight = 6f;
        private const float MinWeight = 0.6f, MaxWeight = 2.2f;

        private readonly Dictionary<SkillStateModule, System.Action<SkillUsedInfo>> _modules = new();
        private readonly List<(Vector3 pos, float radius, float time)> _recent = new();
        private float _nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<SkillPresentation>() == null)
                new GameObject("[SkillPresentation]").AddComponent<SkillPresentation>();
        }

        public static float Weight(SkillData skill) =>
            skill == null ? 1f : Mathf.Clamp(skill.Cooldown / CooldownPerWeight, MinWeight, MaxWeight);

        private void OnEnable() => SkillImpacts.ImpactedBy += OnImpact;

        private void OnDisable()
        {
            SkillImpacts.ImpactedBy -= OnImpact;
            foreach (var pair in _modules)
            {
                if (pair.Key != null)
                    pair.Key.SkillCasting -= pair.Value;
            }
            _modules.Clear();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScan)
                return;
            _nextScan = Time.unscaledTime + 2f;

            foreach (SkillStateModule m in FindObjectsByType<SkillStateModule>(FindObjectsSortMode.None))
            {
                if (_modules.ContainsKey(m))
                    continue;
                SkillStateModule caster = m;
                System.Action<SkillUsedInfo> handler = info => OnCast(caster, info);
                _modules.Add(m, handler);
                m.SkillCasting += handler;
            }
        }

        private void OnCast(SkillStateModule caster, SkillUsedInfo info)
        {
            SkillData skill = info.Data;
            if (skill == null || caster == null)
                return;

            float w = Weight(skill);
            Color color = skill.FxColor;
            Vector3 feet = caster.transform.position;

            LightFlash.Flash(feet + Vector3.up * 1.2f, color, 3f + 2f * w, 2f + 2f * w, 0.25f);
            PlayCastSound(skill, feet, w);

            if (w >= 1.2f)
            {
                Fx.Shockwave(feet + Vector3.up * 0.05f, color, 1.2f * w);
                HitFeel.Zoom(-0.03f * w, 0.4f);
                ScreenPulse.Play(color, 0.12f * w, 0.3f);
            }

            if (w >= 1.8f)
                HitFeel.Shake(0.05f * w, 0.25f);
        }

        private void OnImpact(Vector3 position, float radius, SkillData skill)
        {
            if (radius < 1f)
                return;

            float now = Time.unscaledTime;
            _recent.RemoveAll(r => now - r.time > 0.3f);
            foreach (var r in _recent)
            {
                if (Vector3.Distance(r.pos, position) < r.radius)
                    return;
            }
            _recent.Add((position, radius, now));

            float w = Weight(skill);
            Color color = skill != null ? skill.FxColor : Color.white;

            LightFlash.Flash(position + Vector3.up * 1.2f, color, radius * 2.5f + 3f, 3f + 3f * w, 0.32f);
            PlayImpactSound(skill, position, w);
            ScreenPulse.Play(color, Mathf.Clamp(0.12f * w + radius * 0.05f, 0.15f, 0.7f), 0.3f);

            if (skill is not ElementSkillBase)
                Fx.Shockwave(position, color, radius * 1.2f);

            if (w >= 1.3f && radius >= 1.5f)
            {
                HitFeel.Zoom(0.035f * w, 0.32f);
                HitFeel.Shake(0.1f * w, 0.28f);
            }

            if (w >= 1.9f && radius >= 2f)
                HitFeel.SlowMo(0.4f, 0.25f);
        }

        private static readonly Dictionary<GameObject, bool> OwnAudio = new();

        private static bool HasOwnAudio(FxSlot slot)
        {
            if (slot == null || slot.prefab == null)
                return false;
            if (!OwnAudio.TryGetValue(slot.prefab, out bool has))
                OwnAudio[slot.prefab] = has = slot.prefab.GetComponentInChildren<AudioSource>(true) != null;
            return has;
        }

        private static void PlayCastSound(SkillData skill, Vector3 at, float w)
        {
            SoundLibrary bank = SoundManager.Library;
            if (bank == null || skill is ElementSkillBase e && HasOwnAudio(e.CastFx) || SkillSoundPlayer.ReplacesGenericCast(skill))
                return;
            SoundCue own = skill is ElementSkillBase ? bank.Of(skill.Element)?.cast : null;
            SoundCue cue = own != null && own.HasClips ? own : null;
            SoundManager.Play(cue ?? bank.skillCast, at, Mathf.Clamp(0.6f + 0.25f * w, 0.6f, 1f), PitchOf(w));
        }

        private static void PlayImpactSound(SkillData skill, Vector3 at, float w)
        {
            SoundLibrary bank = SoundManager.Library;
            if (bank == null || skill is ElementSkillBase e && HasOwnAudio(e.ImpactFx) || SkillSoundPlayer.ReplacesGenericImpact(skill))
                return;
            SoundCue own = skill is ElementSkillBase ? bank.Of(skill.Element)?.impact : null;
            SoundCue cue = own != null && own.HasClips ? own : null;
            SoundManager.Play(cue ?? bank.skillImpact, at, Mathf.Clamp(0.65f + 0.2f * w, 0.65f, 1f), PitchOf(w));
            if (w >= 1.5f)
                SoundManager.Play(bank.heavyImpact, at, Mathf.Clamp01(w - 1f));
        }

        private static float PitchOf(float w) => Mathf.Clamp(1.12f - 0.12f * w, 0.85f, 1.08f);
    }
}

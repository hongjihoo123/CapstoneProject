using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Audio;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // 모든 스킬 공통 '무게감' 연출. 스킬 종류와 상관없이, 쿨다운이 긴(= 중요한) 스킬일수록 크게.
    //  쓸 때   : 발밑 빛 + 링. 큰 스킬은 카메라가 살짝 물러나며 "큰 거 온다"
    //  터질 때 : 빛 + 화면 번쩍. 큰 스킬은 카메라가 확 다가가며 흔들림, 아주 큰 스킬은 짧은 슬로모션
    // 스킬 코드는 몰라도 됨: SkillStateModule.SkillCasting 과 SkillImpacts 를 듣기만 한다.
    // 씬에 자동으로 하나 생긴다.
    public class SkillPresentation : MonoBehaviour
    {
        // 쿨다운 6초 = 1배, 13초 이상 = 최대 2.2배
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
            // 플레이어(스킬 모듈)가 늦게 생기거나 바뀌어도 잡히게 가끔 다시 찾음
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

        // ------------------------------------------------ 쓸 때

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
                HitFeel.Zoom(-0.03f * w, 0.4f);   // 살짝 물러남 → 터질 때 다가가는 것과 대비
                ScreenPulse.Play(color, 0.12f * w, 0.3f);
            }

            if (w >= 1.8f)
                HitFeel.Shake(0.05f * w, 0.25f);
        }

        // ------------------------------------------------ 터질 때

        private void OnImpact(Vector3 position, float radius, SkillData skill)
        {
            if (radius < 1f)
                return;   // 적 하나 맞힌 건 피격 연출(번쩍임·섬광)이 맡음

            // 장판·연속 판정이 매 프레임 같은 자리를 부르면 도배되니 걸러냄
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

            // 원소 스킬은 원소별 겹 연출(ElementFlair)이 따로 있어서 기본 링은 원소 아닌 스킬만
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

        // ------------------------------------------------ 소리
        // 원소 스킬은 원소별 소리, 그 밖(궁극기·캐릭터 전용 스킬)은 공통 소리. 무거운 스킬일수록 크고 낮게

        // 이펙트 칸에 타임라인 연출(소리 포함)이 들어 있으면 그게 소리를 내니까 공통 소리는 안 냄 (겹침 방지)
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
            SfxBank bank = Sfx.Bank;
            if (bank == null || skill is ElementSkillBase e && HasOwnAudio(e.CastFx) || SkillSoundPlayer.ReplacesGenericCast(skill))
                return;   // 이 스킬 전용 소리(SkillSoundData)가 있으면 그게 냄
            SfxCue own = skill is ElementSkillBase ? bank.Of(skill.Element)?.cast : null;
            SfxCue cue = own != null && own.HasClips ? own : null;   // 원소 칸이 비어 있으면 공통 시전음
            Sfx.Play(cue ?? bank.skillCast, at, Mathf.Clamp(0.6f + 0.25f * w, 0.6f, 1f), PitchOf(w));
            Sfx.Announce(own);   // 소리 고르기 창: 대신 쓴 공통 소리가 아니라 이 원소 칸으로 따라가게
        }

        private static void PlayImpactSound(SkillData skill, Vector3 at, float w)
        {
            SfxBank bank = Sfx.Bank;
            if (bank == null || skill is ElementSkillBase e && HasOwnAudio(e.ImpactFx) || SkillSoundPlayer.ReplacesGenericImpact(skill))
                return;
            SfxCue own = skill is ElementSkillBase ? bank.Of(skill.Element)?.impact : null;
            SfxCue cue = own != null && own.HasClips ? own : null;   // 원소 칸이 비어 있으면 공통 폭발음
            Sfx.Play(cue ?? bank.skillImpact, at, Mathf.Clamp(0.65f + 0.2f * w, 0.65f, 1f), PitchOf(w));
            if (w >= 1.5f)
                Sfx.Play(bank.heavyImpact, at, Mathf.Clamp01(w - 1f));
            Sfx.Announce(own);
        }

        private static float PitchOf(float w) => Mathf.Clamp(1.12f - 0.12f * w, 0.85f, 1.08f);
    }
}

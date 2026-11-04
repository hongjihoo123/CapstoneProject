using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Audio
{
    // 소리가 언제 나나
    //  Animation : 몸 동작 클립의 AnimationEvent PlaySkillSound(eventName) 가 올 때 (time = 클립 안 시각)
    //  Cast      : 스킬 쓴 순간부터 time 초 뒤
    //  Fx        : 스킬의 이펙트 칸(fxSlot, 예: impactFx)이 터진 순간부터 time 초 뒤 (파티클 burst·startDelay 에 맞춘 값)
    //  Impact    : 범위 공격이 닿은 순간(SkillImpacts)부터 time 초 뒤
    public enum SkillSoundTrigger { Animation, Cast, Fx, Impact }

    [Serializable]
    public class SkillSoundEntry
    {
        public string eventName;
        public SkillSoundTrigger trigger;
        [Tooltip("Fx 일 때 이펙트 칸 이름 (castFx, impactFx, zoneFx ...)")]
        public string fxSlot;
        [Tooltip("Animation: 클립 안 시각(초) / 나머지: 기준 순간부터 몇 초 뒤")]
        public float time;
        [Tooltip("Animation 인데 캐릭터 모델(애니메이터)이 없을 때: 스킬 쓴 뒤 몇 초에 대신 울릴지")]
        public float fallbackDelay;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.8f;
        [Range(0f, 0.5f), Tooltip("피치를 1 ± 이 값 안에서 랜덤")] public float pitchRandom = 0.05f;
        public bool loop;
        [Tooltip("0보다 크면 이 시간(초) 뒤에 끔 (반복 소리·장판 지속시간)")] public float holdTime;
        [Tooltip("이 시간(초) 안에 또 오면 무시 (장판·여러 명 맞힘이 소리를 도배하지 않게)")] public float minInterval = 0.06f;

        [Header("만든 기록")]
        [TextArea] public string prompt;
        public float promptDuration;
        [Tooltip("타이밍 추정이 애매했던 소리")] public bool ambiguous;
        [TextArea] public string note;

        [NonSerialized] public float LastPlayed = -999f;
    }

    // 스킬 하나의 소리 목록. Resources/HJH/SkillSounds/<스킬 이름>.asset
    // 만드는 곳: HJH/Sfx/스킬 소리/3. 에셋·애니 이벤트 만들기 (SkillSoundBuilder)
    [CreateAssetMenu(menuName = "HJH/Skill Sound Data", fileName = "SkillSound")]
    public class SkillSoundData : ScriptableObject
    {
        public string skillId;
        public SkillData skill;
        [Tooltip("켜면 이 스킬은 공통 원소 시전음(SfxBank)을 안 냄 — 여기 시전 소리가 대신")]
        public bool replacesGenericCast = true;
        [Tooltip("켜면 이 스킬은 공통 원소 폭발음(SfxBank)을 안 냄 — 여기 폭발 소리가 대신")]
        public bool replacesGenericImpact = true;
        public List<SkillSoundEntry> entries = new();
    }
}

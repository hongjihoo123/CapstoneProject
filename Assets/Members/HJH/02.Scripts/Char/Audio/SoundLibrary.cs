using System;
using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Audio
{
    // 원소별 시전음·폭발음
    [Serializable]
    public class ElementSfx
    {
        public ElementType element;
        public SoundCue cast = new();
        public SoundCue impact = new();
    }

    // 주인이 없는 공용 소리 + 기본값. Resources/HJH/SfxBank.asset 하나만 둠 (에셋 이름은 예전 그대로).
    //  - 적·무기·스킬 전용 소리는 여기 말고 그 객체의 SO 에 SoundCue 필드로 넣을 것 (소리는 그 이벤트의 주인이 가짐)
    //  - 객체의 칸이 비어 있으면 여기 기본값으로 대신 (새 적을 만들어도 조용하지 않게)
    [CreateAssetMenu(menuName = "HJH/Sound Library", fileName = "SfxBank")]
    public class SoundLibrary : ScriptableObject
    {
        [Tooltip("켜면 효과음 전부 꺼짐")]
        public bool muted = true;

        [Tooltip("이펙트 프리팹에 붙은 소리(타임라인·FxSound)만 끄기. 공통 효과음(muted)과 따로")]
        public bool muteFxSounds;

        [Tooltip("스킬별 전용 소리(Resources/HJH/SkillSounds) 켜기. 끄면 원소 공통 소리만 남음")]
        public bool skillSounds;

        [Header("3D 소리 (거리·방향)")]
        public SoundSpatial spatial = new();

        [Header("평타 (근접)")]
        public SoundCue swing = new();
        public SoundCue swingHit = new();
        public SoundCue finisherHit = new();

        [Header("평타 (원거리)")]
        public SoundCue shot = new();
        public SoundCue empoweredShot = new();

        [Header("맞았을 때 (모든 공격 공통)")]
        public SoundCue hit = new();
        public SoundCue weakpointHit = new();
        public SoundCue kill = new() { priority = SoundPriority.High };

        [Tooltip("플레이어가 맞았을 때 (비어 있으면 hit 소리를 씀)")]
        public SoundCue playerHurt = new() { priority = SoundPriority.Critical, maxVoices = 2 };

        [Header("이동")]
        public SoundCue dash = new();

        [Header("스킬 (원소가 아닌 스킬: 궁극기·캐릭터 전용 스킬)")]
        public SoundCue skillCast = new() { priority = SoundPriority.High };
        public SoundCue skillImpact = new() { priority = SoundPriority.High };
        [Tooltip("무거운 스킬(쿨타임 긴 것)이 터질 때 위에 한 겹 더")]
        public SoundCue heavyImpact = new() { priority = SoundPriority.High };

        [Header("원소 스킬")]
        public ElementSfx[] elements = Array.Empty<ElementSfx>();

        public ElementSfx Of(ElementType element)
        {
            foreach (ElementSfx entry in elements)
                if (entry != null && entry.element == element)
                    return entry;
            return null;
        }
    }
}

using System;
using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Audio
{
    // 게임 전체 효과음 목록. Resources/HJH/SfxBank.asset 하나만 둠.
    // 소리를 바꾸려면 이 에셋에서 클립만 갈아 끼우면 됨 (코드는 안 건드림).
    [CreateAssetMenu(menuName = "HJH/Sfx Bank", fileName = "SfxBank")]
    public class SfxBank : ScriptableObject
    {
        [Tooltip("켜면 효과음 전부 꺼짐 (2026-11-02: 소리가 안 어울려서 임시로 끔)")]
        public bool muted = true;

        [Tooltip("이펙트 프리팹에 붙은 소리(타임라인·FxSound)만 끄기. 공통 효과음(muted)과 따로 (2026-11-03: Mixkit 불 소리는 켜 둠)")]
        public bool muteFxSounds;

        [Header("평타 (근접)")]
        public SfxCue swing = new();
        public SfxCue swingHit = new();
        public SfxCue finisherHit = new();

        [Header("평타 (원거리)")]
        public SfxCue shot = new();
        public SfxCue empoweredShot = new();

        [Header("맞았을 때 (모든 공격 공통)")]
        public SfxCue hit = new();
        public SfxCue weakpointHit = new();
        public SfxCue kill = new();

        [Header("이동")]
        public SfxCue dash = new();

        [Header("스킬 (원소가 아닌 스킬: 궁극기·캐릭터 전용 스킬)")]
        public SfxCue skillCast = new();
        public SfxCue skillImpact = new();
        [Tooltip("무거운 스킬(쿨타임 긴 것)이 터질 때 위에 한 겹 더")]
        public SfxCue heavyImpact = new();

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

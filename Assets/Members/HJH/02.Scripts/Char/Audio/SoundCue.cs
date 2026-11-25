using System;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Audio
{
    // 소리 하나 = "무엇을 어떻게 낼지". 언제 낼지는 쓰는 쪽(스킬 항목·코드)이 정함.
    //  - 적·무기·스킬 SO 에 이 필드를 직렬화로 넣고, SoundManager.Play(cue, 위치) 로 요청
    //  - 클립 여러 개 중 랜덤 + 볼륨·피치 흔들기 (같은 소리 반복이 지루하지 않게)
    [Serializable]
    public class SoundCue
    {
        public AudioClip[] clips;

        [Tooltip("어느 믹서 그룹으로 나갈지 (적은 Enemy, UI 는 Ui)")]
        public SoundBus bus = SoundBus.Sfx;

        [Range(0f, 1f)] public float volume = 0.8f;

        [Tooltip("피치를 이 범위에서 랜덤으로")]
        public Vector2 pitch = new(0.94f, 1.06f);

        [Tooltip("소리 통이 가득 찼을 때 끊기는 순서. 낮은 것부터 끊김")]
        public SoundPriority priority = SoundPriority.Normal;

        [Min(0), Tooltip("이 소리가 동시에 몇 개까지 나는지. 넘으면 가장 오래된 같은 소리를 끊음 (0 = 제한 없음)")]
        public int maxVoices = 4;

        [Min(0f), Tooltip("이 시간(초) 안에 또 부르면 무시. 같은 칸을 쓰는 모든 객체가 공유하니 여러 마리가 같이 내는 소리는 0 에 가깝게")]
        public float minInterval = 0.03f;

        [Tooltip("World3D = 난 위치에서 들림, Flat2D = 위치 없음 (UI·음악)")]
        public SoundSpace space = SoundSpace.World3D;

        [Min(0.1f), Tooltip("들리는 거리 배율 (1 = 기본, 보스 포효는 2 처럼)")]
        public float rangeScale = 1f;

        [Tooltip("반복 재생. 멈추려면 Play 가 돌려주는 SoundHandle.Stop")]
        public bool loop;

        [NonSerialized] public float LastTime = -999f;

        public bool HasClips => clips != null && clips.Length > 0;
    }
}

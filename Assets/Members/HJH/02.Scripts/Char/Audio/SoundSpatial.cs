using System;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Audio
{
    // 효과음의 3D 설정 한 벌 (SoundLibrary.spatial). 3D 소리의 AudioSource 는 전부 이걸로 세팅됨.
    //  - 듣는 위치는 카메라가 아니라 "플레이어 발밑" (SoundListener) → 화면 안 소리는 크게, 멀리 있는 소리는 작게
    //  - 거리 감쇠는 직선 (minDistance 까지 100%, maxDistance 에서 0%) — 값 잡기 쉽게
    [Serializable]
    public class SoundSpatial
    {
        [Range(0f, 1f), Tooltip("0 = 방향·거리 없는 2D, 1 = 완전 3D")]
        public float blend = 1f;

        [Tooltip("이 거리(m) 안에서는 항상 최대 볼륨")]
        public float minDistance = 6f;

        [Tooltip("이 거리(m) 밖에서는 안 들림")]
        public float maxDistance = 40f;

        [Range(0f, 360f), Tooltip("스테레오 클립이 퍼지는 각도. 클수록 가까울 때 좌우로 덜 쏠림 (모노 클립은 영향 적음)")]
        public float spread = 60f;

        // rangeScale: SoundCue.rangeScale (들리는 거리 배율)
        public void Apply(AudioSource source, float rangeScale = 1f)
        {
            source.spatialBlend = blend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = Mathf.Max(0.01f, minDistance * rangeScale);
            source.maxDistance = Mathf.Max(source.minDistance + 0.01f, maxDistance * rangeScale);
            source.spread = spread;
            source.dopplerLevel = 0f;   // 날아가는 탄·카메라 흔들림에 피치가 변하지 않게
        }
    }
}

using System;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Audio
{
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

        public void Apply(AudioSource source, float rangeScale = 1f)
        {
            source.spatialBlend = blend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = Mathf.Max(0.01f, minDistance * rangeScale);
            source.maxDistance = Mathf.Max(source.minDistance + 0.01f, maxDistance * rangeScale);
            source.spread = spread;
            source.dopplerLevel = 0f;
        }
    }
}

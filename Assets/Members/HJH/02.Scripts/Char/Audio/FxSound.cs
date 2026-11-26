using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Audio
{
    // 이펙트 프리팹에 붙는 소리 하나: 이펙트가 켜질 때(풀에서 꺼낼 때 포함) delay 초 뒤에 재생.
    //  - 반복(loop) 소리는 이펙트가 꺼질 때 같이 꺼짐 (오브젝트가 비활성화되면 AudioSource 도 멈춤)
    //  - 음소거는 FxPrefabInstance 가 SoundLibrary.muteFxSounds 로 함
    //  - 3D 설정(거리·방향)과 믹서 그룹(bus)은 켜질 때 자동으로 입힘 → 프리팹마다 따로 설정할 필요 없음
    //    (적 이펙트의 소리는 bus 를 Enemy 로)
    //  만드는 곳: HJH/Sfx/불 소리 자동 배치 (FxAutoSound)
    [RequireComponent(typeof(AudioSource))]
    public class FxSound : MonoBehaviour
    {
        [SerializeField, Tooltip("이펙트가 켜진 뒤 몇 초 뒤에 (애니메이션·폭발 타이밍 맞추기)")] private float delay;
        [SerializeField, Tooltip("어느 믹서 그룹으로 나갈지 (적 이펙트는 Enemy)")] private SoundBus bus = SoundBus.Sfx;

        private AudioSource _source;
        private float _baseVolume;

        private void OnEnable()
        {
            if (_source == null)
            {
                _source = GetComponent<AudioSource>();
                _baseVolume = _source.volume;
            }
            _source.Stop();
            SoundManager.Spatial.Apply(_source);
            AudioMixerGroupHelper.Assign(_source, bus);
            _source.volume = _baseVolume * SoundManager.BusGain(bus);   // 설정 창의 종류별 음량
            if (delay <= 0f)
                _source.Play();
            else
                _source.PlayDelayed(delay);
        }
    }

    internal static class AudioMixerGroupHelper
    {
        // 믹서가 없으면 그대로 둠 (프리팹에 지정해 둔 출력 유지)
        public static void Assign(AudioSource source, SoundBus bus)
        {
            var group = SoundManager.GroupOf(bus);
            if (group != null)
                source.outputAudioMixerGroup = group;
        }
    }
}

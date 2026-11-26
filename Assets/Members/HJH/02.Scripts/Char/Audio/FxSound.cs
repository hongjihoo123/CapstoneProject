using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Audio
{
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
            _source.volume = _baseVolume * SoundManager.BusGain(bus);
            if (delay <= 0f)
                _source.Play();
            else
                _source.PlayDelayed(delay);
        }
    }

    internal static class AudioMixerGroupHelper
    {
        public static void Assign(AudioSource source, SoundBus bus)
        {
            var group = SoundManager.GroupOf(bus);
            if (group != null)
                source.outputAudioMixerGroup = group;
        }
    }
}

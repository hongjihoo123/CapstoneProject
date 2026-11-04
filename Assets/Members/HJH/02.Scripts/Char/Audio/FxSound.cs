using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Audio
{
    // 이펙트 프리팹에 붙는 소리 하나: 이펙트가 켜질 때(풀에서 꺼낼 때 포함) delay 초 뒤에 재생.
    //  - 반복(loop) 소리는 이펙트가 꺼질 때 같이 꺼짐 (오브젝트가 비활성화되면 AudioSource 도 멈춤)
    //  - 음소거는 FxPrefabInstance 가 SfxBank.muteFxSounds 로 함
    //  만드는 곳: HJH/Sfx/불 소리 자동 배치 (FxAutoSound)
    [RequireComponent(typeof(AudioSource))]
    public class FxSound : MonoBehaviour
    {
        [SerializeField, Tooltip("이펙트가 켜진 뒤 몇 초 뒤에 (애니메이션·폭발 타이밍 맞추기)")] private float delay;

        private AudioSource _source;

        private void OnEnable()
        {
            _source ??= GetComponent<AudioSource>();
            _source.Stop();
            if (delay <= 0f)
                _source.Play();
            else
                _source.PlayDelayed(delay);
        }
    }
}

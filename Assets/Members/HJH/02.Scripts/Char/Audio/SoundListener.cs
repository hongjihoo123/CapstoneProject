using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.TopDown;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Audio
{
    // "귀" 위치를 카메라(16m 위, 흔들림·줌 있음)가 아니라 플레이어 발밑으로 옮김. 씬에 자동으로 하나 생김.
    //  - 방향(좌우)은 카메라가 보는 방향 기준이라 화면 왼쪽 소리는 왼쪽에서 들림
    //  - 카메라 흔들림·기울임이 소리 볼륨/좌우에 안 섞이게 yaw(좌우 회전)만 따라감
    //  - 탑다운 카메라(TopDownCameraFollow)가 있는 씬에서만 켬. 메뉴 등 없는 씬은 원래 카메라 귀를 그대로 씀
    //  - 켜는 동안 다른 AudioListener 는 꺼 둠 (2개 이상이면 유니티가 경고). 끝나면 되돌림
    public class SoundListener : MonoBehaviour
    {
        private const float ScanInterval = 1f;

        // 지금 "귀" 위치 (탑다운 카메라가 없는 씬에서는 없음)
        public static bool HasPosition { get; private set; }
        public static Vector3 Position { get; private set; }

        // 이 위치의 소리가 들릴 만한 거리 안인지. 안 들리면 소리 통을 쓰지 않게 (적이 많을 때 먼 소리가 가까운 소리를 끊는 것 방지)
        public static bool IsAudible(Vector3 at, SoundSpatial spatial, float rangeScale = 1f)
        {
            if (!HasPosition || spatial.blend < 0.999f)
                return true;
            float range = spatial.maxDistance * rangeScale;
            return (at - Position).sqrMagnitude <= range * range;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => HasPosition = false;

        private AudioListener _own;
        private TopDownCameraFollow _follow;
        private float _nextScan;
        private bool _engaged;
        private readonly List<AudioListener> _silenced = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<SoundListener>() != null)
                return;
            var go = new GameObject("[SoundListener]");
            DontDestroyOnLoad(go);
            go.AddComponent<SoundListener>();
        }

        private void LateUpdate()
        {
            if (_follow == null && Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + ScanInterval;
                _follow = FindFirstObjectByType<TopDownCameraFollow>();
            }

            if (_follow == null || !_follow.isActiveAndEnabled || _follow.Target == null)
            {
                Disengage();
                return;
            }

            if (!_engaged)
                Engage();
            else if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + ScanInterval;
                SilenceOthers();   // 씬에 새로 생긴 카메라 귀
            }

            transform.SetPositionAndRotation(
                _follow.Target.position,
                Quaternion.Euler(0f, _follow.transform.eulerAngles.y, 0f));
            Position = transform.position;
            HasPosition = true;
        }

        private void OnDestroy() => Disengage();

        private void Engage()
        {
            _engaged = true;
            SilenceOthers();
            if (_own == null)
                _own = gameObject.AddComponent<AudioListener>();
            _own.enabled = true;
        }

        private void Disengage()
        {
            HasPosition = false;
            if (!_engaged)
                return;
            _engaged = false;
            if (_own != null)
                _own.enabled = false;
            foreach (AudioListener listener in _silenced)
                if (listener != null)
                    listener.enabled = true;
            _silenced.Clear();
        }

        private void SilenceOthers()
        {
            foreach (AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                if (listener == _own || !listener.enabled)
                    continue;
                listener.enabled = false;
                _silenced.Add(listener);
            }
        }
    }
}

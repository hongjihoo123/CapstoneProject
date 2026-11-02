using System.Collections.Generic;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 터지는 자리에 잠깐 켜졌다 꺼지는 점광원. 어두운 맵에서 폭발이 주변(땅·나무·적)을 비춰서 훨씬 화려해 보임.
    // 미리 만든 몇 개를 돌려 쓰고, 다 쓰고 있으면 제일 오래된 걸 다시 씀.
    public class LightFlash : MonoBehaviour
    {
        private const int MaxLights = 12;

        private static readonly List<LightFlash> Pool = new();
        private static Transform _root;

        private Light _light;
        private float _start;
        private float _duration;
        private float _peak;

        public static void Flash(Vector3 position, Color color, float range, float intensity, float duration)
        {
            LightFlash flash = Take();
            flash.transform.position = position;
            flash._light.color = color;
            flash._light.range = range;
            flash._peak = intensity;
            flash._light.intensity = intensity;
            flash._start = Time.unscaledTime;
            flash._duration = Mathf.Max(0.05f, duration);
            flash._light.enabled = true;
            flash.enabled = true;
        }

        private static LightFlash Take()
        {
            Pool.RemoveAll(f => f == null);

            LightFlash oldest = null;
            foreach (LightFlash f in Pool)
            {
                if (!f.enabled)
                    return f;
                if (oldest == null || f._start < oldest._start)
                    oldest = f;
            }

            if (Pool.Count < MaxLights)
            {
                if (_root == null)
                    _root = new GameObject("[LightFlash]").transform;
                var go = new GameObject("Flash");
                go.transform.SetParent(_root, false);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                var flash = go.AddComponent<LightFlash>();
                flash._light = light;
                Pool.Add(flash);
                return flash;
            }

            return oldest;
        }

        private void Update()
        {
            float t = (Time.unscaledTime - _start) / _duration;
            if (t >= 1f)
            {
                _light.enabled = false;
                enabled = false;
                return;
            }

            float k = 1f - t;
            _light.intensity = _peak * k * k;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Pool.Clear();
            _root = null;
        }
    }
}

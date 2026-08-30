using UnityEngine;

namespace RobotWeapons
{
    [RequireComponent(typeof(LineRenderer))]
    public class LaserBeamVisual : MonoBehaviour
    {
        [SerializeField] private LineRenderer innerCore;
        [SerializeField] private GameObject muzzleEffect;
        [SerializeField] private GameObject impactEffect;

        [Header("번개 지글거림 (F3DLightning의 Oscillate 대체)")]
        [SerializeField] private bool oscillate = true;
        [SerializeField] private int oscillatePoints = 8;
        [SerializeField] private float amplitude = 0.3f;
        [SerializeField] private float oscillateInterval = 0.05f;

        [Header("텍스처 스크롤 (F3DLightning의 AnimateUV 대체)")]
        [SerializeField] private bool animateUV = true;
        [SerializeField] private float uvSpeed = -6f;

        private LineRenderer line;
        private bool wasActive;
        private float oscillateTimer;
        private float uvOffsetSeed;

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.enabled = false;

            if (innerCore != null)
            {
                innerCore.useWorldSpace = false;
                innerCore.enabled = false;
            }

            uvOffsetSeed = Random.Range(0f, 5f);
        }

        public void UpdateBeam(bool active, Vector3 start, Vector3 end)
        {
            if (active && !wasActive) SetEffectsActive(true);
            else if (!active && wasActive) SetEffectsActive(false);
            wasActive = active;

            line.enabled = active;
            if (innerCore != null) innerCore.enabled = active;
            if (!active) return;

            transform.position = start;
            transform.rotation = Quaternion.LookRotation(end - start);
            float length = Vector3.Distance(start, end);

            oscillateTimer += Time.deltaTime;
            bool refreshNoise = !oscillate || oscillateTimer >= oscillateInterval;
            if (refreshNoise) oscillateTimer = 0f;

            ApplyBeamShape(line, length, refreshNoise);
            if (innerCore != null) ApplyBeamShape(innerCore, length, refreshNoise);

            if (animateUV)
            {
                Vector2 uv = new Vector2(Time.time * uvSpeed + uvOffsetSeed, 0f);
                line.material.SetVector("_Offset", uv);
                if (innerCore != null) innerCore.material.SetVector("_Offset", uv);
            }

            if (muzzleEffect != null) muzzleEffect.transform.position = start;
            if (impactEffect != null) impactEffect.transform.position = end;
        }

        private void ApplyBeamShape(LineRenderer lr, float length, bool refreshNoise)
        {
            if (!oscillate)
            {
                lr.positionCount = 2;
                lr.SetPosition(0, Vector3.zero);
                lr.SetPosition(1, new Vector3(0f, 0f, length));
                return;
            }

            if (!refreshNoise) return;

            lr.positionCount = oscillatePoints;
            lr.SetPosition(0, Vector3.zero);
            for (int i = 1; i < oscillatePoints - 1; i++)
            {
                float z = length * i / (oscillatePoints - 1);
                lr.SetPosition(i, new Vector3(Random.Range(-amplitude, amplitude), Random.Range(-amplitude, amplitude), z));
            }
            lr.SetPosition(oscillatePoints - 1, new Vector3(0f, 0f, length));
        }

        private void SetEffectsActive(bool state)
        {
            if (muzzleEffect != null)
            {
                muzzleEffect.SetActive(state);
                muzzleEffect.BroadcastMessage(state ? "OnSpawned" : "OnDespawned", SendMessageOptions.DontRequireReceiver);
            }
            if (impactEffect != null)
            {
                impactEffect.SetActive(state);
                impactEffect.BroadcastMessage(state ? "OnSpawned" : "OnDespawned", SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}

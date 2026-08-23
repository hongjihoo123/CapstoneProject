using System.Collections;
using UnityEngine;

namespace RobotWeapons
{
    // 저격 발사 등 즉발 판정에 짧게 반짝이는 라인(트레이서)을 그려줌.
    [RequireComponent(typeof(LineRenderer))]
    public class TracerVisual : MonoBehaviour
    {
        [SerializeField] private float flashDuration = 0.05f;

        private LineRenderer line;
        private Coroutine activeFlash;

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.enabled = false;
        }

        public void Fire(Vector3 start, Vector3 end)
        {
            if (activeFlash != null) StopCoroutine(activeFlash);
            activeFlash = StartCoroutine(FlashRoutine(start, end));
        }

        private IEnumerator FlashRoutine(Vector3 start, Vector3 end)
        {
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.enabled = true;
            yield return new WaitForSeconds(flashDuration);
            line.enabled = false;
        }
    }
}

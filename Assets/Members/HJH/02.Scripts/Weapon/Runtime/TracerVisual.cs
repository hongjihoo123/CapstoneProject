using System.Collections;
using UnityEngine;

namespace RobotWeapons
{
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

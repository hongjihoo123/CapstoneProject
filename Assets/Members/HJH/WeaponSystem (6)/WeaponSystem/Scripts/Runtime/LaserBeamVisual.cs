using UnityEngine;

namespace RobotWeapons
{
    [RequireComponent(typeof(LineRenderer))]
    public class LaserBeamVisual : MonoBehaviour
    {
        private LineRenderer line;

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.positionCount = 2;
            line.enabled = false;
        }

        public void UpdateBeam(bool active, Vector3 start, Vector3 end)
        {
            line.enabled = active;
            if (!active) return;

            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }
    }
}

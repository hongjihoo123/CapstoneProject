using Members.KYR._01_Scripts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Members.HJH._02.Scripts.Char.TopDown
{
    [DefaultExecutionOrder(-50)]
    public class TopDownAimController : MonoBehaviour
    {
        [SerializeField] private PlayerAgent player;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float aimPlaneHeight = 1f;
        [SerializeField] private float turnSpeed = 1080f;

        public Vector3 AimPoint { get; private set; }

        private void Start()
        {
            if (viewCamera == null)
                viewCamera = Camera.main;

            player.Mover.MoveReference = viewCamera.transform;
        }

        private void Update()
        {
            if (!player.IsAlive || !player.ControlFsm.IsGameplayAlive)
                return;

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            Ray ray = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
            var plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y + aimPlaneHeight, 0f));
            if (!plane.Raycast(ray, out float distance))
                return;

            AimPoint = ray.GetPoint(distance);

            Vector3 toAim = AimPoint - transform.position;
            toAim.y = 0f;
            if (toAim.sqrMagnitude < 0.04f)
                return;

            player.SetAimDirection(toAim.normalized);

            Quaternion target = Quaternion.LookRotation(toAim);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }
    }
}

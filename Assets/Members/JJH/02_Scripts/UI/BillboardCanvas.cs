using UnityEngine;

namespace Members.JJH._02_Scripts.UI
{
    public class BillboardCanvas : MonoBehaviour
    {
        private Transform cam;

        private void Awake()
        {
            cam = Camera.main.transform;
        }

        private void LateUpdate()
        {
            transform.rotation = Quaternion.LookRotation(cam.forward);
        }
    }
}
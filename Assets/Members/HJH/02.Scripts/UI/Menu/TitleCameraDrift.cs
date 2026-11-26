using DG.Tweening;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.UI
{
    public class TitleCameraDrift : MonoBehaviour
    {
        [SerializeField] private Camera menuCamera;
        [Header("카메라 흔들림")]
        [SerializeField, Min(0f)] private float driftMove = 1.2f;
        [SerializeField, Min(0f)] private float driftTurn = 1.2f;
        [SerializeField, Min(1f)] private float driftTime = 9f;

        private void Start()
        {
            if (menuCamera == null || (driftMove <= 0f && driftTurn <= 0f))
                return;
            Transform cam = menuCamera.transform;
            Vector3 basePos = cam.position;
            Quaternion baseRot = cam.rotation;
            Vector3 offset = (cam.right * 0.6f + cam.up * 0.25f) * driftMove;
            cam.position = basePos - offset;
            cam.rotation = baseRot * Quaternion.Euler(0f, -driftTurn, 0f);
            cam.DOMove(basePos + offset, driftTime).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(cam.gameObject);
            cam.DORotateQuaternion(baseRot * Quaternion.Euler(0f, driftTurn, 0f), driftTime).SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(cam.gameObject);
        }
    }
}

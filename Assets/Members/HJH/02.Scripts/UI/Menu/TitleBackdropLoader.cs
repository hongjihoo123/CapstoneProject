using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Members.HJH._02.Scripts.UI
{
    public class TitleBackdropLoader : MonoBehaviour
    {
        [SerializeField, Tooltip("빌드 설정에 있어야 함")] private string backdropScene = "TitleBackdrop";
        [SerializeField] private Camera menuCamera;
        [SerializeField, Tooltip("켜면 시작할 때 카메라를 TitleCameraAnchor 자리로 옮김 (끄면 카메라에 저장된 값 그대로)")]
        private bool snapToAnchor;
        [Header("카메라 흔들림")]
        [SerializeField, Min(0f)] private float driftMove = 1.2f;
        [SerializeField, Min(0f)] private float driftTurn = 1.2f;
        [SerializeField, Min(1f)] private float driftTime = 9f;

        private IEnumerator Start()
        {
            Scene backdrop = SceneManager.GetSceneByName(backdropScene);
            if (!backdrop.isLoaded)
            {
                if (string.IsNullOrEmpty(backdropScene) || !Application.CanStreamedLevelBeLoaded(backdropScene))
                    yield break;
                yield return SceneManager.LoadSceneAsync(backdropScene, LoadSceneMode.Additive);
                backdrop = SceneManager.GetSceneByName(backdropScene);
                if (!backdrop.IsValid())
                    yield break;
            }
            SceneManager.SetActiveScene(backdrop);

            if (menuCamera == null)
                yield break;
            Transform cam = menuCamera.transform;

            if (snapToAnchor)
            {
                TitleCameraAnchor anchor = null;
                foreach (GameObject root in backdrop.GetRootGameObjects())
                    if (anchor == null)
                        anchor = root.GetComponentInChildren<TitleCameraAnchor>(true);
                if (anchor != null)
                {
                    cam.SetPositionAndRotation(anchor.transform.position, anchor.transform.rotation);
                    menuCamera.fieldOfView = anchor.FieldOfView;
                }
            }

            if (driftMove <= 0f && driftTurn <= 0f)
                yield break;
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

using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 타이틀 뒤에 실제 맵 장면(TitleBackdrop = 맵 복사본에서 플레이어/적/포탈을 뺀 것)을 겹쳐서 띄움.
    //  - 카메라 위치/각도/FOV 는 이 씬(MainMenu)의 Main Camera 에 저장된 값 그대로 (코드가 옮기지 않음 → 인스펙터에서 직접 조정).
    //    에디터에서 보면서 맞추려면 메뉴 'HJH/UI/타이틀 배경 장면 같이 열기' 로 TitleBackdrop 을 함께 열어 둠
    //  - 재생 시 TitleBackdrop 이 이미 열려 있으면 다시 읽지 않음
    //  - 카메라는 저장된 자리 둘레를 천천히 흔들림 (unscaled). 흔들림 크기는 아래 값 (0 이면 가만히)
    //  - 장면이 없으면 아무 일도 안 함 → 타이틀 UI 만 나옴
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
            SceneManager.SetActiveScene(backdrop); // 하늘/주변광 설정을 맵 쪽 것으로

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

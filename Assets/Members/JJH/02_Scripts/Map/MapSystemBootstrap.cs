using UnityEngine;

namespace Members.JJH._02_Scripts.Map
{
    // 맵 UI(Map_Canvas)와 씬 전환(Scene_Transition_Canvas)은 Start 씬에서 DontDestroyOnLoad 로 넘어온다.
    // 맵 씬을 바로 Play 하면 둘 다 없어서 포탈에서 F 를 눌러도 맵이 안 뜬다 → 없을 때만 프리팹으로 만든다.
    // Start 에서 확인하는 이유: 같은 씬에 놓인 Map_Canvas 의 Awake 가 먼저 끝난 뒤에 봐야 중복으로 만들지 않는다.
    public class MapSystemBootstrap : MonoBehaviour
    {
        [SerializeField] private GameObject mapCanvasPrefab;
        [SerializeField] private GameObject sceneTransitionCanvasPrefab;

        private void Start()
        {
            if (MapUI.Instance == null && mapCanvasPrefab != null)
                Instantiate(mapCanvasPrefab).name = mapCanvasPrefab.name;

            if (SceneTransitionManager.Instance == null && sceneTransitionCanvasPrefab != null)
                Instantiate(sceneTransitionCanvasPrefab).name = sceneTransitionCanvasPrefab.name;
        }
    }
}

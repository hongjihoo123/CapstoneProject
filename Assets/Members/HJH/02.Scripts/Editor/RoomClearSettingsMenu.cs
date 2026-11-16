using Assets.Members.HJH._02.Scripts.Quest;
using Members.JJH._02_Scripts.Agents.Enemies;
using Members.KYR._01.Scripts.Spawner;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HJH.EditorTools
{
    // 지금 열려 있는 씬의 "전체 적의 수"를 세어서 RoomClearSettings 에 적어 둔다 (없으면 만들어서).
    // 꺼져 있는 적(세트로 켜지는 적)과 스포너 웨이브 합계도 포함. 씬은 저장하지 않으니 확인하고 직접 저장하세요.
    // 퀘스트가 시작부터 "0 / 21" 로 고정되어 뜨고, 뒤늦게 나오는 적 때문에 성공이 먼저 뜨는 일이 없어짐.
    public static class RoomClearSettingsMenu
    {
        [MenuItem("HJH/퀘스트/이 씬의 적 수 기록")]
        public static void Record()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[RoomClear] 플레이 중에는 할 수 없습니다.");
                return;
            }

            int placed = 0;
            foreach (AbstractEnemy enemy in Object.FindObjectsByType<AbstractEnemy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (EditorUtility.IsPersistent(enemy))
                    continue;   // 프리팹 에셋은 제외, 씬에 있는 것만
                placed++;
            }

            int fromSpawners = 0;
            foreach (EnemySpawner spawner in Object.FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                fromSpawners += spawner.TotalEnemyCount;

            int total = placed + fromSpawners;

            RoomClearSettings settings = Object.FindFirstObjectByType<RoomClearSettings>(FindObjectsInactive.Include);
            if (settings == null)
            {
                var go = new GameObject("RoomClearSettings");
                settings = go.AddComponent<RoomClearSettings>();
                Undo.RegisterCreatedObjectUndo(go, "Create RoomClearSettings");
            }

            var so = new SerializedObject(settings);
            so.FindProperty("expectedTotal").intValue = total;
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(settings.gameObject.scene);
            Selection.activeObject = settings.gameObject;

            Debug.Log($"[RoomClear] 이 씬의 적 {total}마리 기록 (씬에 놓인 적 {placed} + 스포너 웨이브 {fromSpawners}). 씬을 저장하세요.");
        }
    }
}

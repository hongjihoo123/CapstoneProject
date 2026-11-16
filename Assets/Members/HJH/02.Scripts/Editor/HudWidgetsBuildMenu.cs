using Assets.Members.HJH._02.Scripts.UI;
using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using UnityEditor;
using UnityEngine;

namespace HJH.EditorTools
{
    // PlayerRig 의 HUD_Canvas 에 새 HUD 위젯(체력/골드/타이머/목표/스탯/죽음 화면)을 빈 오브젝트로 붙인다. 이미 있으면 건너뜀.
    // 위젯 모양은 각 스크립트가 실행할 때 만들어서, 여기선 오브젝트와 연결만 함.
    public static class HudWidgetsBuildMenu
    {
        private const string PrefabPath = "Assets/Members/HJH/05.Data/Prefab/PlayerRig.prefab";
        private const string StatsChannelPath = "Assets/Members/JJH/03_Datas/01_SO/EventChannels/PlayerStats_EventChannel.asset";

        [MenuItem("HJH/UI/새 HUD 위젯 붙이기")]
        public static void Build()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform canvas = null;
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == "HUD_Canvas") { canvas = t; break; }
                if (canvas == null) { Debug.LogWarning("[HudWidgets] HUD_Canvas 를 못 찾음"); return; }

                Add<PlayerHpHud>(canvas, "PlayerHpHud");
                Add<StageTimerHud>(canvas, "StageTimerHud");
                Add<KillQuestHud>(canvas, "KillQuestHud");
                Add<GoldHud>(canvas, "GoldHud");
                Add<DeathOverlay>(canvas, "DeathOverlay");
                StatsMiniHud stats = Add<StatsMiniHud>(canvas, "StatsMiniHud");

                var channel = AssetDatabase.LoadAssetAtPath<EventChannelSO>(StatsChannelPath);
                if (channel == null)
                    Debug.LogWarning("[HudWidgets] 스탯 채널을 못 찾음: " + StatsChannelPath);
                else
                {
                    var so = new SerializedObject(stats);
                    so.FindProperty("playerStatsChannel").objectReferenceValue = channel;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                HideOldGoldPlate(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[HudWidgets] 완료");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static T Add<T>(Transform parent, string name) where T : Component
        {
            Transform existing = parent.Find(name);
            if (existing != null && existing.TryGetComponent(out T found))
                return found;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<T>();
        }

        // 예전 골드 판(GoldPlate)은 새 GoldHud 로 대체. 오브젝트는 지우지 않고 꺼서
        // 같은 프리팹의 골드 지급(RunGoldGrant)은 그대로 둠. 이전에 내가 옮겨 둔 위치/그림은 원래대로 되돌림.
        private static void HideOldGoldPlate(GameObject root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "GoldPlate")
                    continue;

                var rect = (RectTransform)t;
                rect.anchoredPosition = new Vector2(24f, -24f);
                rect.sizeDelta = new Vector2(180f, 56f);
                if (t.TryGetComponent(out UnityEngine.UI.Image img))
                {
                    img.sprite = null;
                    img.type = UnityEngine.UI.Image.Type.Simple;
                }
                t.gameObject.SetActive(false);
                break;
            }
        }
    }
}

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HJH.EditorTools
{
    // PlayerRig 의 미니맵을 크게 + 새 링 프레임 붙이기. 이미 했으면 크기/위치만 다시 맞춤.
    public static class HudMinimapSkinMenu
    {
        private const string PrefabPath = "Assets/Members/HJH/05.Data/Prefab/PlayerRig.prefab";
        private const string RingPath = "Assets/Members/HJH/05.Data/Resources/HJH/UI/minimap_ring.png";
        private const string FrameName = "MiniMapFrame";

        [MenuItem("HJH/UI/미니맵 새 디자인 적용")]
        public static void Apply()
        {
            var ring = AssetDatabase.LoadAssetAtPath<Sprite>(RingPath);
            if (ring == null)
            {
                Debug.LogWarning("[MinimapSkin] 링 스프라이트 없음. 먼저 HJH/UI/HUD 에셋 가져오기 실행");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform map = null;
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == "MiniMap") { map = t; break; }
                if (map == null) { Debug.LogWarning("[MinimapSkin] MiniMap 을 못 찾음"); return; }

                // 캔버스 기준 해상도 기준으로 우상단 여백 30 에 맞춤 (이 HUD 는 1280x720 기준이라 화면에선 1.5배)
                var scaler = map.GetComponentInParent<CanvasScaler>();
                Vector2 refRes = scaler != null ? scaler.referenceResolution : new Vector2(1920f, 1080f);
                // 미니맵 오브젝트에 localScale 이 걸려 있어서(1.5배) 화면에서 보이는 크기(size) 기준으로 계산
                const float size = 190f;
                const float margin = 40f;
                var mapRect = (RectTransform)map;
                float scale = Mathf.Max(0.01f, mapRect.localScale.x);
                mapRect.sizeDelta = Vector2.one * (size / scale);
                mapRect.anchoredPosition = new Vector2(refRes.x * 0.5f - margin - size * 0.5f, refRes.y * 0.5f - margin - size * 0.5f);
                foreach (RectTransform child in map)
                    child.sizeDelta = Vector2.one * (size / scale);

                Transform parent = map.parent;
                Transform old = parent.Find(FrameName);
                GameObject frame = old != null ? old.gameObject : new GameObject(FrameName, typeof(RectTransform), typeof(Image));
                frame.transform.SetParent(parent, false);
                var fr = (RectTransform)frame.transform;
                fr.anchorMin = fr.anchorMax = mapRect.anchorMin;
                fr.pivot = mapRect.pivot;
                fr.anchoredPosition = mapRect.anchoredPosition;
                fr.sizeDelta = Vector2.one * (size + 4f) / 0.906f;   // 링 그림 반지름이 이미지의 0.453 배
                fr.SetSiblingIndex(map.GetSiblingIndex() + 1);
                var img = frame.GetComponent<Image>();
                img.sprite = ring;
                img.raycastTarget = false;
                img.color = Color.white;

                // 원 안쪽 가장자리 어둡게 + 옅은 거리 링/십자선 (마스크 안에 들어가는 자식)
                var vignetteSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Members/HJH/05.Data/Resources/HJH/UI/minimap_vignette.png");
                if (vignetteSprite != null && map.Find("Vignette") == null)
                {
                    var vgo = new GameObject("Vignette", typeof(RectTransform), typeof(Image));
                    vgo.transform.SetParent(map, false);
                    var vr = (RectTransform)vgo.transform;
                    vr.anchorMin = vr.anchorMax = new Vector2(0.5f, 0.5f);
                    vr.anchoredPosition = Vector2.zero;
                    vr.sizeDelta = Vector2.one * (size / scale);
                    var vimg = vgo.GetComponent<Image>();
                    vimg.sprite = vignetteSprite;
                    vimg.raycastTarget = false;
                }
                else if (map.Find("Vignette") is RectTransform existingV)
                    existingV.sizeDelta = Vector2.one * (size / scale);

                // 아이콘 모양 바꾸는 부품
                if (!map.TryGetComponent(out Assets.Members.HJH._02.Scripts.UI.MinimapIconSkin skin))
                    skin = map.gameObject.AddComponent<Assets.Members.HJH._02.Scripts.UI.MinimapIconSkin>();
                var skinSo = new SerializedObject(skin);
                skinSo.FindProperty("playerSize").floatValue = 6.5f;
                skinSo.FindProperty("enemySize").floatValue = 4f;
                skinSo.FindProperty("bossSize").floatValue = 7f;
                skinSo.FindProperty("portalSize").floatValue = 6.5f;
                skinSo.ApplyModifiedPropertiesWithoutUndo();

                // 미니맵 카메라 배경: 하늘색 -> HUD 에 맞는 어두운 남색 (PlayerRig 안 인스턴스만, 원본 프리팹은 그대로)
                foreach (Camera cam in root.GetComponentsInChildren<Camera>(true))
                {
                    if (cam.name != "MinimapCamera") continue;
                    cam.backgroundColor = new Color(0.045f, 0.075f, 0.12f, 1f);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[MinimapSkin] 적용 완료");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}

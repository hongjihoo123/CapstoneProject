using System.Collections.Generic;
using System.IO;
using Assets.Members.HJH._02.Scripts.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HJH.EditorTools
{
    // 타이틀(MainMenu 씬) 꾸미기. 이미 구워 둔 버튼/OnClick 은 건드리지 않고 겉모습/배치만 입힘:
    //   - MenuCanvas/Backdrop (어두운 덮개, 안개, 반딧불이, 비네트) + TitleAmbience 연결
    //   - 메인 카메라에 TitleBackdropLoader 연결, UI 전용 오버레이 카메라(UiCamera) 분리
    //   - 제목 그림자, 그리고 TitleMood 로 SoD 식 왼쪽 정렬 배치
    // 먼저 'HJH/UI/타이틀 배경 장면 만들기' 로 TitleBackdrop 씬이 있어야 함.
    // 다시 실행하면 UI 장식과 배치가 처음 상태로 돌아가므로, 인스펙터에서 고친 UI 값은 사라짐 (카메라 위치는 건드리지 않음).
    // 'HJH/UI/코드 UI 굽기' 로 MainMenu 를 다시 구우면 이 꾸밈이 사라지니, 그때 한 번 더 실행
    public static class TitleDecorMenu
    {
        private const string SpriteDir = "Assets/Members/HJH/05.Data/UI/Title";
        private const int UiLayer = 5;

        private static readonly Color Teal = new(0.30f, 0.95f, 0.75f, 1f);
        private static readonly Color Gold = new(0.84f, 0.72f, 0.47f, 1f);

        [MenuItem("HJH/UI/타이틀/UI 장식 다시 입히기 (카메라는 유지, UI 손본 값은 초기화)")]
        public static void Decorate()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[TitleDecor] 플레이 중에는 실행하지 않음");
                return;
            }
            if (!File.Exists(TitleBackdropMenu.BackdropPath))
            {
                Debug.LogWarning("[TitleDecor] TitleBackdrop 씬이 없음 → 'HJH/UI/타이틀 배경 장면 만들기' 를 먼저 실행");
                return;
            }

            Directory.CreateDirectory(SpriteDir);
            Sprite gradient = MakeSprite("title_gradient", 4, 256, GradientPixel);
            Sprite glow = MakeSprite("title_glow", 128, 128, GlowPixel); // soft_glow.png 는 어두운 그림자용이라 흰색 빛을 따로 만듦
            Sprite vignette = MakeSprite("title_vignette", 256, 256, VignettePixel);

            Scene scene = SceneManager.GetSceneByPath(TitleBackdropMenu.MainMenuPath);
            bool opened = !scene.isLoaded;
            if (opened)
                scene = EditorSceneManager.OpenScene(TitleBackdropMenu.MainMenuPath, OpenSceneMode.Additive);
            try
            {
                MainMenuScreen menu = null;
                foreach (var s in Object.FindObjectsByType<MainMenuScreen>(FindObjectsSortMode.None))
                    if (s.gameObject.scene == scene)
                        menu = s;
                if (menu == null)
                {
                    Debug.LogError("[TitleDecor] MainMenuScreen 을 찾지 못함");
                    return;
                }

                Decorate(menu, menu.transform.Find("MenuCanvas"), gradient, glow, vignette);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[TitleDecor] 완료");
            }
            finally
            {
                if (opened)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void Decorate(MainMenuScreen menu, Transform canvas, Sprite gradient, Sprite glow, Sprite vignette)
        {
            DestroyChild(canvas, "Backdrop");
            RectTransform back = NewRect("Backdrop", canvas);
            Stretch(back);
            back.SetSiblingIndex(0);

            Image grad = NewImage("Gradient", back, gradient, new Color(0f, 0.02f, 0.06f, 0.3f));
            Stretch(grad.rectTransform);

            Image coreGlow = NewImage("CoreGlow", back, glow, new Color(Teal.r, Teal.g, Teal.b, 0.05f));
            Place(coreGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1500f, 1000f));
            Image warmGlow = NewImage("WarmGlow", back, glow, new Color(Gold.r, Gold.g, Gold.b, 0.07f));
            Place(warmGlow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1900f, 520f));

            var mists = new List<RectTransform>();
            Image mistA = NewImage("MistA", back, glow, new Color(0.55f, 1f, 0.85f, 0.045f));
            Place(mistA.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(2400f, 360f));
            Image mistB = NewImage("MistB", back, glow, new Color(0.55f, 1f, 0.85f, 0.03f));
            Place(mistB.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 320f), new Vector2(2600f, 300f));
            mists.Add(mistA.rectTransform);
            mists.Add(mistB.rectTransform);

            var flies = new List<RectTransform>();
            RectTransform flyRoot = NewRect("Fireflies", back);
            Stretch(flyRoot);
            var rng = new System.Random(7);
            for (int i = 0; i < 16; i++)
            {
                float size = 16f + (float)rng.NextDouble() * 22f;
                Color c = i % 3 != 0 ? new Color(1f, 0.9f, 0.5f, 0.85f) : new Color(0.6f, 1f, 0.8f, 0.85f);
                Image fly = NewImage("Firefly" + i, flyRoot, glow, c);
                Place(fly.rectTransform, new Vector2(0.5f, 0.5f),
                    new Vector2((float)(rng.NextDouble() * 2 - 1) * 880f, (float)(rng.NextDouble() * 2 - 1) * 460f), new Vector2(size, size));
                flies.Add(fly.rectTransform);
            }

            Image vig = NewImage("Vignette", back, vignette, new Color(0f, 0.02f, 0.02f, 0.55f));
            Stretch(vig.rectTransform);

            TitleAmbience ambience = back.gameObject.GetComponent<TitleAmbience>() ?? back.gameObject.AddComponent<TitleAmbience>();
            var amb = new SerializedObject(ambience);
            SetArray(amb.FindProperty("fireflies"), flies);
            SetArray(amb.FindProperty("mists"), mists);
            SetArray(amb.FindProperty("glows"), new List<Image> { coreGlow, warmGlow });
            amb.ApplyModifiedPropertiesWithoutUndo();

            Transform oldGlow = canvas.Find("BackGlow");
            if (oldGlow != null && oldGlow.TryGetComponent(out Image og))
                og.color = new Color(Teal.r, Teal.g, Teal.b, 0.10f);

            WireBackdrop(menu);
            AddTitleShadows(canvas);
            TitleMood.AddSettingsButton(menu, canvas);
            TitleMood.Apply(menu, canvas);

            // 나중에 만든 오브젝트도 UI 카메라(UI 레이어)에 보이게
            foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = UiLayer;
        }

        // 메인 카메라를 맵 장면 위로: 로더 연결 + 후처리(Volume) 켜기 + UI 전용 오버레이 카메라
        private static void WireBackdrop(MainMenuScreen menu)
        {
            Camera cam = null;
            foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (c.gameObject.scene == menu.gameObject.scene && c.CompareTag("MainCamera"))
                    cam = c;
            if (cam == null)
                return;

            var loader = menu.GetComponent<TitleBackdropLoader>() ?? menu.gameObject.AddComponent<TitleBackdropLoader>();
            var so = new SerializedObject(loader);
            so.FindProperty("menuCamera").objectReferenceValue = cam;
            so.ApplyModifiedPropertiesWithoutUndo();

            UniversalAdditionalCameraData baseData = cam.GetUniversalAdditionalCameraData();
            baseData.renderPostProcessing = true;

            // 물/폭포 셰이더가 UI 보다 늦게 그려져서 글자를 덮음 → UI 는 오버레이 카메라(카메라 스택)로 항상 맨 위에
            Transform old = menu.transform.Find("UiCamera");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var uiGo = new GameObject("UiCamera", typeof(Camera));
            uiGo.transform.SetParent(menu.transform, false);
            var uiCam = uiGo.GetComponent<Camera>();
            uiCam.clearFlags = CameraClearFlags.Depth;
            uiCam.cullingMask = 1 << UiLayer;
            uiCam.nearClipPlane = 0.1f;
            uiCam.farClipPlane = 10f;
            UniversalAdditionalCameraData uiData = uiCam.GetUniversalAdditionalCameraData();
            uiData.renderType = CameraRenderType.Overlay;
            uiData.renderPostProcessing = false;
            baseData.cameraStack.Clear();
            baseData.cameraStack.Add(uiCam);
            cam.cullingMask &= ~(1 << UiLayer);

            Transform canvas = menu.transform.Find("MenuCanvas");
            if (canvas != null && canvas.TryGetComponent(out Canvas canvasComp))
            {
                canvasComp.worldCamera = uiCam;
                canvasComp.planeDistance = 1f;
            }
            foreach (Transform t in menu.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = UiLayer;
            uiGo.layer = 0;
        }

        // 제목 뒤 그림자: 같은 글자를 어둡게 한 번 더 (위치/색은 TitleMood 가 맞춤)
        private static void AddTitleShadows(Transform canvas)
        {
            foreach (string page in new[] { "Question", "Menu" })
            {
                Transform p = canvas.Find(page);
                if (p == null)
                    continue;
                DestroyChild(p, "TitleShadow");
                Transform title = p.Find("Title");
                if (title == null || !title.TryGetComponent(out TMP_Text t))
                    continue;

                t.fontStyle = FontStyles.Bold;
                t.enableVertexGradient = true;
                if (page == "Menu")
                    t.characterSpacing = 18f;
                GameObject shadow = Object.Instantiate(title.gameObject, p);
                shadow.name = "TitleShadow";
                shadow.transform.SetSiblingIndex(title.GetSiblingIndex());
                shadow.GetComponent<TMP_Text>().enableVertexGradient = false;
            }
        }

        // ------------------------------------------------------------ 그림 만들기

        private static Color GlowPixel(int x, int y, int w, int h)
        {
            float u = (x + 0.5f) / w * 2f - 1f;
            float v = (y + 0.5f) / h * 2f - 1f;
            float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
            return new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
        }

        // 가운데는 투명, 가장자리로 갈수록 어둡게
        private static Color VignettePixel(int x, int y, int w, int h)
        {
            float u = (x + 0.5f) / w * 2f - 1f;
            float v = (y + 0.5f) / h * 2f - 1f;
            float d = Mathf.Sqrt(u * u * 0.8f + v * v);
            return new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.35f, d)));
        }

        private static Color GradientPixel(int x, int y, int w, int h)
        {
            float v = (y + 0.5f) / h;                 // 0(아래)..1(위)
            return new Color(1f, 1f, 1f, Mathf.Pow(1f - v, 1.2f));
        }

        // ------------------------------------------------------------ 도우미 (TitleMood 도 씀)

        internal delegate Color PixelFunc(int x, int y, int w, int h);

        internal static Sprite MakeSprite(string name, int w, int h, PixelFunc pixel)
        {
            string path = $"{SpriteDir}/{name}.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var colors = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    colors[y * w + x] = pixel(x, y, w, h);
            tex.SetPixels(colors);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void SetArray<T>(SerializedProperty prop, List<T> items) where T : Object
        {
            prop.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        private static void DestroyChild(Transform parent, string name)
        {
            Transform c = parent.Find(name);
            while (c != null)
            {
                Object.DestroyImmediate(c.gameObject);
                c = parent.Find(name);
            }
        }

        internal static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        internal static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            RectTransform rect = NewRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        internal static void Place(RectTransform rect, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }
    }
}

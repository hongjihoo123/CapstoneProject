using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Assets.Members.HJH._02.Scripts.EditorTools
{
    // 에디터가 켜져 있는 동안 Temp/FxBridge/cmd.txt 를 1초마다 확인해서 명령을 실행함 (창이 뒤에 있어도 됨).
    // 이펙트 확인용 이미지 뽑기 + 에셋 새로고침용. 결과는 Temp/FxBridge/out.txt, 이미지는 Temp/FxBridge/*.jpg
    //   refresh                         → AssetDatabase.Refresh (새 스크립트/에셋 읽기)
    //   sheet <이름> <경로|경로@#rrggbb> ... → 프리팹마다 한 줄, 시간대별 칸으로 찍은 이미지
    //   skill <이름> <스킬에셋경로> ...   → 그 스킬이 쓰는 이펙트 프리팹 전부를 원소 색으로 찍음
    //   menu <메뉴 경로>                  → 에디터 메뉴 실행
    // Temp 폴더라 git 에 안 올라감. 이 파일을 지우면 기능도 사라짐.
    [InitializeOnLoad]
    public static class FxPreviewBridge
    {
        private const string Dir = "Temp/FxBridge";
        private const string CommandPath = Dir + "/cmd.txt";
        private const string OutputPath = Dir + "/out.txt";
        private const int Cell = 256;

        private static readonly float[] Times = { 0.08f, 0.2f, 0.4f, 0.7f, 1.1f, 1.8f };
        private static readonly string[] SlotNames =
            { "castFx", "windupFx", "travelFx", "streamFx", "zoneFx", "barrierFx", "auraFx", "selfFx", "impactFx", "hitFx", "blockFx", "statusFx" };

        private static double _nextCheck;

        static FxPreviewBridge()
        {
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextCheck)
                return;
            _nextCheck = EditorApplication.timeSinceStartup + 1.0;

            if (!File.Exists(CommandPath) || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            string[] lines = File.ReadAllLines(CommandPath);
            File.Delete(CommandPath);

            var log = new List<string>();
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;
                try
                {
                    log.Add(Run(line));
                }
                catch (Exception e)
                {
                    log.Add($"FAIL {line}\n{e}");
                }
            }

            log.Add($"done {DateTime.Now:HH:mm:ss}");
            Directory.CreateDirectory(Dir);
            File.WriteAllLines(OutputPath, log);
        }

        private static string Run(string line)
        {
            string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            switch (parts[0])
            {
                case "refresh":
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    return "refreshed";
                case "menu":
                    return EditorApplication.ExecuteMenuItem(line.Substring(5).Trim()) ? "menu ok" : "menu not found";
                case "sheet":
                    return Sheet(parts[1], parts.Skip(2).Select(ParseRow).ToList());
                case "pickups":
                    return Pickups(parts[1], parts[2]);
                case "combofix":
                    return EditScene(parts[1], ComboFix);
                case "skill":
                    return Sheet(parts[1], parts.Skip(2).SelectMany(SkillRows).ToList());
                default:
                    return "unknown " + parts[0];
            }
        }

        private readonly struct Row
        {
            public readonly string Label;
            public readonly GameObject Prefab;
            public readonly Color Tint;
            public readonly float Height;

            public Row(string label, GameObject prefab, Color tint, float height = 0f)
            {
                Label = label;
                Prefab = prefab;
                Tint = tint;
                Height = height;
            }
        }

        private static Row ParseRow(string token)
        {
            // 경로^높이 → 바닥에서 그만큼 띄워서 (몸 높이 이펙트)
            float height = 0f;
            int caret = token.IndexOf('^');
            if (caret > 0)
            {
                float.TryParse(token.Substring(caret + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out height);
                token = token.Substring(0, caret);
            }

            string path = token;
            Color tint = Color.white;
            int at = token.IndexOf('@');
            if (at > 0)
            {
                path = token.Substring(0, at);
                ColorUtility.TryParseHtmlString(token.Substring(at + 1), out tint);
            }
            return new Row(Path.GetFileNameWithoutExtension(path), AssetDatabase.LoadAssetAtPath<GameObject>(path), tint, height);
        }

        // 스킬 에셋의 이펙트 칸들 (tint 켜진 칸은 스킬 색으로)
        private static IEnumerable<Row> SkillRows(string skillPath)
        {
            var skill = AssetDatabase.LoadAssetAtPath<ScriptableObject>(skillPath);
            if (skill == null)
                yield break;

            Color color = Color.white;
            PropertyInfo fxColor = skill.GetType().GetProperty("FxColor", BindingFlags.Instance | BindingFlags.Public);
            if (fxColor != null)
                color = (Color)fxColor.GetValue(skill);

            var serialized = new SerializedObject(skill);
            foreach (string slot in SlotNames)
            {
                SerializedProperty prefab = serialized.FindProperty(slot + ".prefab");
                if (prefab?.objectReferenceValue is not GameObject go)
                    continue;
                bool tint = serialized.FindProperty(slot + ".tint")?.boolValue ?? false;
                float height = slot switch { "statusFx" => 2.1f, "hitFx" or "castFx" or "auraFx" or "travelFx" or "streamFx" or "selfFx" => 1.1f, _ => 0f };
                yield return new Row($"{skill.name}.{slot}", go, tint ? color : Color.white, height);
            }
        }

        // ---------- 스킬 구슬 배치 ----------
        // 씬의 SkillPickups 아래에 폴더 속 스킬마다 구슬이 하나씩 있게 함 (없는 건 첫 구슬을 복사).
        // 원소별로 한 줄씩 다시 정렬. 씬이 열려 있고 저장 안 된 변경이 있으면 안 건드림.
        private static readonly string[] ElementOrder = { "Fire_", "Water_", "Wind_", "Earth_", "Electric_" };

        private static string Pickups(string scenePath, string folder)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (wasLoaded && scene.isDirty)
                return $"pickups: {scenePath} 에 저장 안 된 변경이 있어서 건너뜀";
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            try
            {
                GameObject group = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "SkillPickups");
                if (group == null)
                    return "pickups: SkillPickups 없음";

                var pickups = new List<Component>();
                foreach (Transform child in group.transform)
                {
                    Component pickup = child.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().Name == "SkillPickup");
                    if (pickup != null)
                        pickups.Add(pickup);
                }
                if (pickups.Count == 0)
                    return "pickups: 복사할 구슬 없음";

                var bySkill = new Dictionary<Object, Component>();
                foreach (Component pickup in pickups)
                {
                    Object skill = new SerializedObject(pickup).FindProperty("skill").objectReferenceValue;
                    if (skill != null)
                        bySkill[skill] = pickup;
                }

                List<ScriptableObject> skills = AssetDatabase.FindAssets("t:ScriptableObject", new[] { folder })
                    .Select(g => AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(s => s != null)
                    .OrderBy(s => Array.FindIndex(ElementOrder, p => s.name.StartsWith(p)))
                    .ThenBy(s => s.name)
                    .ToList();

                int added = 0;
                foreach (ScriptableObject skill in skills)
                {
                    if (bySkill.ContainsKey(skill))
                        continue;
                    GameObject copy = Object.Instantiate(pickups[0].gameObject, group.transform);
                    copy.name = "Pickup_" + skill.name;
                    Component pickup = copy.GetComponents<Component>().First(c => c != null && c.GetType().Name == "SkillPickup");
                    var so = new SerializedObject(pickup);
                    so.FindProperty("skill").objectReferenceValue = skill;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    bySkill[skill] = pickup;
                    added++;
                }

                // 원소별 한 줄 (가로 2.4m, 줄 간격 2.6m, 플레이어 앞쪽부터 뒤로)
                var rowCount = new int[ElementOrder.Length + 1];
                var rowSize = new int[ElementOrder.Length + 1];
                foreach (ScriptableObject skill in skills)
                    rowSize[RowOf(skill)]++;
                foreach (ScriptableObject skill in skills)
                {
                    int row = RowOf(skill);
                    int column = rowCount[row]++;
                    float x = (column - (rowSize[row] - 1) * 0.5f) * 2.4f;
                    bySkill[skill].transform.localPosition = new Vector3(x, 0f, -row * 2.6f);
                    bySkill[skill].transform.SetSiblingIndex(skills.IndexOf(skill));
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                return $"pickups: {skills.Count}개 스킬, {added}개 새로 놓음, 저장함";
            }
            finally
            {
                if (!wasLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        // 씬 열고(이미 열려 있으면 그대로, 저장 안 된 변경 있으면 안 함) edit 돌리고 저장
        private static string EditScene(string scenePath, Func<Scene, string> edit)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (wasLoaded && scene.isDirty)
                return $"{scenePath} 에 저장 안 된 변경이 있어서 건너뜀";
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                string result = edit(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                return result + " / 저장함";
            }
            finally
            {
                if (!wasLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static IEnumerable<Component> ComponentsNamed(Scene scene, string typeName) =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Component>(true))
                .Where(c => c != null && c.GetType().Name == typeName);

        // JJH 콤보 UI: 비어 있는 피니시 다이아 스프라이트를 JJH 씬과 같은 걸로, 콤보 표의 테스트용 불-불 / 불-불-불-불 제거
        private static string ComboFix(Scene scene)
        {
            const string stackSheet = "Assets/Members/JJH/04_Assets/UI/Finish_Stack.png";
            long[] ids = { -777106112, 187347594 }; // 0: 꺼짐, 1: 켜짐 (JJH Element System Scene 값)
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(stackSheet).OfType<Sprite>().ToArray();
            var pick = new Sprite[ids.Length];
            foreach (Sprite sprite in sprites)
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string _, out long id))
                    continue;
                int index = Array.IndexOf(ids, id);
                if (index >= 0)
                    pick[index] = sprite;
            }
            if (pick.Any(s => s == null))
                return "Finish_Stack 스프라이트를 못 찾음";

            int views = 0;
            foreach (Component view in ComponentsNamed(scene, "ElementStackView"))
            {
                var so = new SerializedObject(view);
                SerializedProperty array = so.FindProperty("finisherStackSprites");
                if (array.arraySize >= 2)
                    continue;
                array.arraySize = 2;
                array.GetArrayElementAtIndex(0).objectReferenceValue = pick[0];
                array.GetArrayElementAtIndex(1).objectReferenceValue = pick[1];
                so.ApplyModifiedPropertiesWithoutUndo();
                views++;
            }

            int removed = 0;
            foreach (Component controller in ComponentsNamed(scene, "ElementBuffController"))
            {
                var so = new SerializedObject(controller);
                SerializedProperty combos = so.FindProperty("combos");
                for (int i = combos.arraySize - 1; i >= 0; i--)
                {
                    string comboName = combos.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                    if (comboName is "불-불" or "불-불-불-불")
                    {
                        combos.DeleteArrayElementAtIndex(i);
                        removed++;
                    }
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return $"combofix: 다이아 스프라이트 {views}곳, 콤보 {removed}개 지움 / " + LinkFinisher(scene);
        }

        // JJH 콤보 시스템이 켜져 있는 씬에 피니시 로직 붙이기:
        //  켜진 ElementBuffController 옆에 ElementComboChain(버프는 JJH 쪽이 보내니 끔) + FinisherViewLink,
        //  예전 HJH 콤보 HUD(ElementComboHud)는 끔 (JJH UI 를 씀)
        private static string LinkFinisher(Scene scene)
        {
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)).Where(c => c != null).ToList();
            var controller = all.OfType<global::Members.JJH._02_Scripts.ElementsSystem.ElementBuffController>().FirstOrDefault(c => c.gameObject.activeInHierarchy);
            if (controller == null)
                return "켜진 ElementBuffController 없음";

            var chains = all.OfType<Assets.Members.HJH._02.Scripts.Element.ElementComboChain>().ToList();
            var live = chains.FirstOrDefault(c => c.gameObject.activeInHierarchy && c.enabled);
            var old = chains.FirstOrDefault();
            if (live == null)
            {
                live = controller.gameObject.AddComponent<Assets.Members.HJH._02.Scripts.Element.ElementComboChain>();
                var so = new SerializedObject(live);
                if (old != null)
                {
                    var from = new SerializedObject(old);
                    so.FindProperty("skills").objectReferenceValue = from.FindProperty("skills").objectReferenceValue;
                    so.FindProperty("systemChannel").objectReferenceValue = from.FindProperty("systemChannel").objectReferenceValue;
                }
                so.FindProperty("buffController").objectReferenceValue = controller;
                so.FindProperty("sendBuffEvents").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (live.GetComponent<Assets.Members.HJH._02.Scripts.Element.FinisherViewLink>() == null)
            {
                var link = live.gameObject.AddComponent<Assets.Members.HJH._02.Scripts.Element.FinisherViewLink>();
                var so = new SerializedObject(link);
                so.FindProperty("chain").objectReferenceValue = live;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (var hud in all.OfType<Assets.Members.HJH._02.Scripts.Hud.PlayerHudSource>())
            {
                var so = new SerializedObject(hud);
                so.FindProperty("comboChain").objectReferenceValue = live;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            int hidden = 0;
            foreach (var comboUi in all.Where(c => c.GetType().Name == "ComboUI"))
            {
                if (!comboUi.gameObject.activeSelf)
                    continue;
                comboUi.gameObject.SetActive(false);
                hidden++;
            }

            var skills = new SerializedObject(live).FindProperty("skills").objectReferenceValue;
            return $"피니시 로직 → {live.gameObject.name} (skills {(skills != null ? "연결됨" : "없음!")}), 예전 콤보 HUD {hidden}개 끔";
        }

        private static int RowOf(Object skill)
        {
            int index = Array.FindIndex(ElementOrder, p => skill.name.StartsWith(p));
            return index < 0 ? ElementOrder.Length : index;
        }

        // ---------- 찍기 ----------
        // 한 줄 = 프리팹 하나. 칸 = 재생 후 Times 초. 게임 카메라 각도(55도 내려봄)
        // 이름:숫자 → 화면 반폭(m) 고정 (기본 3.5 = 가로 약 7m, 행끼리 크기 비교 가능)
        private static string Sheet(string name, List<Row> rows)
        {
            float extent = 3.5f;
            int colon = name.IndexOf(':');
            if (colon > 0)
            {
                float.TryParse(name.Substring(colon + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out extent);
                name = name.Substring(0, colon);
            }

            if (rows.Count == 0)
                return $"sheet {name}: no rows";

            Scene scene = EditorSceneManager.NewPreviewScene();
            var cameraGo = new GameObject("PreviewCamera");
            SceneManager.MoveGameObjectToScene(cameraGo, scene);
            var camera = cameraGo.AddComponent<Camera>();
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.22f, 0.24f, 0.23f);
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 200f;
            var target = new RenderTexture(Cell, Cell, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;

            // 바닥 (크기 감 잡기용 1m 격자)
            GameObject ground = MakeGround(scene);

            var sheet = new Texture2D(Times.Length * Cell, rows.Count * Cell, TextureFormat.RGB24, false);
            var readback = new Texture2D(Cell, Cell, TextureFormat.RGB24, false);
            var report = new List<string> { $"sheet {name}: rows top→bottom, columns {string.Join("/", Times)}s" };

            for (int r = 0; r < rows.Count; r++)
            {
                Row row = rows[r];
                report.Add($"  {r}: {row.Label} {(row.Prefab == null ? "(missing)" : "")}");
                for (int c = 0; c < Times.Length; c++)
                {
                    GameObject instance = Simulate(row, scene, Times[c], out _);
                    float frame = extent;
                    Vector3 look = new Vector3(0f, 0.5f, 0f);
                    cameraGo.transform.position = look + Quaternion.Euler(55f, 0f, 0f) * Vector3.back * (frame / Mathf.Tan(20f * Mathf.Deg2Rad) + 1f);
                    cameraGo.transform.LookAt(look);
                    Capture(camera, target, readback);
                    sheet.SetPixels(c * Cell, (rows.Count - 1 - r) * Cell, Cell, Cell, readback.GetPixels());

                    if (instance != null)
                        Object.DestroyImmediate(instance);
                }
                report.Add($"     view size ~{extent * 2f:0.#}m");
            }

            sheet.Apply();
            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{name}.jpg";
            File.WriteAllBytes(path, sheet.EncodeToJPG(88));
            Object.DestroyImmediate(sheet);
            Object.DestroyImmediate(readback);
            Object.DestroyImmediate(ground);
            camera.targetTexture = null;
            target.Release();
            EditorSceneManager.ClosePreviewScene(scene);
            report.Add($"  -> {path}");
            return string.Join("\n", report);
        }

        private static GameObject MakeGround(Scene scene)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.localScale = Vector3.one * 6f;
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            var grid = new Texture2D(64, 64);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
                grid.SetPixel(x, y, x < 2 || y < 2 ? new Color(0.36f, 0.38f, 0.37f) : new Color(0.26f, 0.28f, 0.27f));
            grid.Apply();
            grid.wrapMode = TextureWrapMode.Repeat;
            material.mainTexture = grid;
            material.mainTextureScale = Vector2.one * 60f;
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", grid);
                material.SetTextureScale("_BaseMap", Vector2.one * 60f);
            }
            ground.GetComponent<Renderer>().sharedMaterial = material;
            return ground;
        }

        private static GameObject Simulate(Row row, Scene scene, float time, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.one);
            if (row.Prefab == null)
                return null;

            GameObject instance = Object.Instantiate(row.Prefab);
            SceneManager.MoveGameObjectToScene(instance, scene);
            instance.transform.SetPositionAndRotation(Vector3.up * row.Height, Quaternion.identity);

            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem system in systems)
            {
                if (row.Tint != Color.white)
                {
                    ParticleSystem.MainModule main = system.main;
                    ParticleSystem.MinMaxGradient start = main.startColor;
                    start.color *= row.Tint;
                    start.colorMin *= row.Tint;
                    start.colorMax *= row.Tint;
                    main.startColor = start;
                }
            }

            ParticleSystem[] roots = systems.Where(s => s.transform.parent == null || s.transform.parent.GetComponentInParent<ParticleSystem>() == null).ToArray();
            foreach (ParticleSystem system in roots)
                system.Simulate(time, true, true, true);

            bool any = false;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer psr && psr.GetComponent<ParticleSystem>().particleCount == 0)
                    continue;
                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return instance;
        }

        private static void Capture(Camera camera, RenderTexture target, Texture2D readback)
        {
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
                RenderPipeline.SubmitRenderRequest(camera, request);
            else
                camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0);
            readback.Apply();
            RenderTexture.active = previous;
        }
    }
}

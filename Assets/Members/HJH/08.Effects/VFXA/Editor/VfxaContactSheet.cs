using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Assets.Members.HJH.Effects.VFXA.EditorTools
{
    // Renders every FxLibrary slot of the default library next to the VFXA library at a few moments of its life,
    // into one image (Temp/VfxaContactSheet.png) to compare old vs new without playing every skill.
    // Effects drawn by drivers at runtime (FxLine beams/tracers, text) stay empty here.
    public static class VfxaContactSheet
    {
        private const int Cell = 160;
        private static readonly float[] Times = { 0.12f, 0.35f, 0.8f };

        [MenuItem("HJH/VFXA/Render Old vs New Contact Sheet")]
        public static void Render()
        {
            FxLibrary oldLibrary = Resources.Load<FxLibrary>(FxLibrary.ResourcePath);
            var newLibrary = AssetDatabase.LoadAssetAtPath<FxLibrary>(VfxaSwapBuilder.Root + "/Swapped/HJH_FxLibrary_VFXA.asset");
            FieldInfo[] slots = typeof(FxLibrary).GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => f.FieldType == typeof(GameObject)).ToArray();

            Scene scene = EditorSceneManager.NewPreviewScene();
            var cameraGo = new GameObject("SheetCamera");
            SceneManager.MoveGameObjectToScene(cameraGo, scene);
            var camera = cameraGo.AddComponent<Camera>();
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.32f, 0.33f, 0.35f);
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            cameraGo.transform.position = new Vector3(0f, 5.2f, -4.2f);
            cameraGo.transform.LookAt(new Vector3(0f, 0.6f, 0f));

            var target = new RenderTexture(Cell, Cell, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;

            int columns = Times.Length * 2;
            var rows = new List<string>();
            foreach (FieldInfo slot in slots)
                rows.Add(slot.Name);

            var sheet = new Texture2D(columns * Cell + 8, rows.Count * Cell, TextureFormat.RGB24, false);
            var readback = new Texture2D(Cell, Cell, TextureFormat.RGB24, false);
            var log = new StringBuilder("rows (top->bottom); columns: old t=" + string.Join("/", Times) + " | new t=" + string.Join("/", Times) + "\n");

            for (int row = 0; row < slots.Length; row++)
            {
                FieldInfo slot = slots[row];
                var oldPrefab = slot.GetValue(oldLibrary) as GameObject;
                var newPrefab = newLibrary != null ? slot.GetValue(newLibrary) as GameObject : null;
                log.AppendLine($"{row + 1,2}. {slot.Name}: {(oldPrefab ? oldPrefab.name : "-")} -> {(newPrefab ? newPrefab.name : "-")}");

                for (int side = 0; side < 2; side++)
                {
                    GameObject prefab = side == 0 ? oldPrefab : newPrefab;
                    for (int i = 0; i < Times.Length; i++)
                    {
                        int x = (side * Times.Length + i) * Cell + side * 8;
                        int y = (slots.Length - 1 - row) * Cell;
                        Shot(prefab, scene, camera, target, readback, Times[i]);
                        sheet.SetPixels(x, y, Cell, Cell, readback.GetPixels());
                    }
                }
            }

            sheet.Apply();
            Directory.CreateDirectory("Temp");
            File.WriteAllBytes("Temp/VfxaContactSheet.png", sheet.EncodeToPNG());
            File.WriteAllText("Temp/VfxaContactSheet.txt", log.ToString());

            camera.targetTexture = null;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(sheet);
            Object.DestroyImmediate(readback);
            EditorSceneManager.ClosePreviewScene(scene);
            Debug.Log("[VFXA] Contact sheet written to Temp/VfxaContactSheet.png");
        }

        // Every pack prefab as authored (no wrapper), farther camera: for picking which pack effect fits a slot.
        [MenuItem("HJH/VFXA/Render Pack Prefabs Sheet")]
        public static void RenderPack()
        {
            float[] times = { 0.1f, 0.3f, 0.6f, 1.0f, 1.6f };
            string[] prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { VfxaSwapBuilder.Root })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => !p.Contains("/Swapped/")).OrderBy(p => p).ToArray();

            Scene scene = EditorSceneManager.NewPreviewScene();
            var cameraGo = new GameObject("SheetCamera");
            SceneManager.MoveGameObjectToScene(cameraGo, scene);
            var camera = cameraGo.AddComponent<Camera>();
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.32f, 0.33f, 0.35f);
            camera.fieldOfView = 40f;
            camera.farClipPlane = 200f;
            cameraGo.transform.position = new Vector3(0f, 13f, -10.5f);
            cameraGo.transform.LookAt(new Vector3(0f, 1f, 0f));
            var target = new RenderTexture(Cell, Cell, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;

            var sheet = new Texture2D(times.Length * Cell, prefabs.Length * Cell, TextureFormat.RGB24, false);
            var readback = new Texture2D(Cell, Cell, TextureFormat.RGB24, false);
            var log = new StringBuilder("rows (top->bottom); columns t=" + string.Join("/", times) + "\n");
            for (int row = 0; row < prefabs.Length; row++)
            {
                log.AppendLine($"{row + 1,2}. {prefabs[row].Substring(VfxaSwapBuilder.Root.Length + 1)}");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabs[row]);
                for (int i = 0; i < times.Length; i++)
                {
                    Shot(prefab, scene, camera, target, readback, times[i]);
                    sheet.SetPixels(i * Cell, (prefabs.Length - 1 - row) * Cell, Cell, Cell, readback.GetPixels());
                }
            }

            sheet.Apply();
            File.WriteAllBytes("Temp/VfxaPackSheet.png", sheet.EncodeToPNG());
            File.WriteAllText("Temp/VfxaPackSheet.txt", log.ToString());
            camera.targetTexture = null;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(sheet);
            Object.DestroyImmediate(readback);
            EditorSceneManager.ClosePreviewScene(scene);
            Debug.Log("[VFXA] Pack sheet written to Temp/VfxaPackSheet.png");
        }

        private static void Shot(GameObject prefab, Scene scene, Camera camera, RenderTexture target, Texture2D readback, float time)
        {
            GameObject instance = null;
            if (prefab != null)
            {
                instance = Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(instance, scene);
                instance.transform.SetPositionAndRotation(new Vector3(0f, 0.5f, 0f), Quaternion.identity);
                foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
                    ps.Simulate(time, false, true, true);
            }

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

            if (instance != null)
                Object.DestroyImmediate(instance);
        }
    }
}

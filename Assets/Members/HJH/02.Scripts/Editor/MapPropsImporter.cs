using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.EditorTools
{
    // 메뉴 HJH > 맵 > Tripo 프랍 반입 (FBX 설정 + 머티리얼 + 프리팹)
    //
    // 05.Data/MapProps/Models 의 FBX 8종(Blender 로 정상화한 Tripo 프랍)을 쓸 수 있게 만든다.
    //  1. FBX 임포터: 머티리얼 안 가져옴(직접 만든 걸 씀), 법선 가져오기, 접선 계산
    //  2. 크기 점검: Blender 에서 미터로 맞춘 크기와 임포트 결과가 100배 다르면 자동으로 globalScale 보정
    //  3. URP Lit 머티리얼(베이스 색 텍스처). 랜턴·꽃·덩굴은 파랗게 빛나는 부분만 뽑은 발광 맵을 따로 만듦
    //  4. 프리팹: 루트(바닥 중앙 피벗) > 모델. 바위·뿌리·통나무는 볼록 메시 콜라이더(렌더러와 같은 오브젝트),
    //     랜턴·꽃·덩굴은 콜라이더 없음 (대시 걸림 방지)
    //  5. 바위·통나무는 반투명 복사본(Resources/HJH/RockFade/<머티리얼>_Fade) 도 만들어 JungleOcclusionFader 가 투명 처리하게 함
    // 프리팹은 05.Data/MapProps/Prefabs. 다시 실행해도 같은 에셋을 덮어쓰기만 한다.
    // 처음 한 번은 컴파일 직후 자동으로 돈다(프리팹이 하나도 없을 때만).
    [InitializeOnLoad]
    public static class MapPropsImporter
    {
        private const string Root = "Assets/Members/HJH/05.Data/MapProps";
        private const string RockFadeFolder = "Assets/Members/HJH/05.Data/Resources/HJH/RockFade";

        private sealed class Spec
        {
            public string Name;
            public bool Collider;       // 볼록 메시 콜라이더
            public bool Emissive;       // 파란 발광 맵
            public bool RockFade;       // 반투명 복사본
            public float ExpectedMax;   // Blender 에서 맞춘 가장 긴 변(m), 크기 점검용
        }

        private static readonly Spec[] Specs =
        {
            new Spec { Name = "Rock_A",      Collider = true,  RockFade = true,  ExpectedMax = 2.4f },
            new Spec { Name = "Rock_B",      Collider = true,  RockFade = true,  ExpectedMax = 3.0f },
            new Spec { Name = "Roots_A",     Collider = true,                    ExpectedMax = 3.2f },
            new Spec { Name = "Roots_B",     Collider = true,                    ExpectedMax = 3.8f },
            new Spec { Name = "FallenLog",   Collider = true,  RockFade = true,  ExpectedMax = 4.0f },
            new Spec { Name = "Lantern",     Emissive = true,                    ExpectedMax = 3.2f },
            new Spec { Name = "GlowFlowers", Emissive = true,                    ExpectedMax = 1.6f },
            new Spec { Name = "GlowVine",    Emissive = true,                    ExpectedMax = 1.8f },
        };

        static MapPropsImporter()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!File.Exists(Root + "/Models/Rock_A.fbx")) return;
                if (File.Exists(Root + "/Prefabs/Rock_A.prefab")) return;
                Run();
            };
        }

        [MenuItem("HJH/맵/Tripo 프랍 반입 (FBX 설정+머티리얼+프리팹)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[MapProps] 플레이 중에는 안 돕니다.");
                return;
            }

            AssetDatabase.Refresh();
            var log = new List<string>();
            foreach (var s in Specs)
            {
                try { Build(s, log); }
                catch (System.Exception e) { log.Add($"{s.Name}: 실패 - {e.Message}"); }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MapProps] 반입 결과\n" + string.Join("\n", log));
        }

        private static void Build(Spec s, List<string> log)
        {
            string fbx = $"{Root}/Models/{s.Name}.fbx";
            string texPath = $"{Root}/Textures/{s.Name}_BaseColor.jpg";
            if (!File.Exists(fbx) || !File.Exists(texPath))
            {
                log.Add($"{s.Name}: 파일 없음");
                return;
            }

            ConfigureTexture(texPath, true);
            ConfigureModel(fbx);

            // 크기 점검 (100배 어긋나면 보정)
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            float max = LongestSide(model);
            string scaleNote = "";
            if (max > 0.0001f)
            {
                float ratio = max / s.ExpectedMax;
                if (ratio > 50f || ratio < 0.02f)
                {
                    var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
                    imp.globalScale *= s.ExpectedMax / max;
                    imp.SaveAndReimport();
                    model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                    scaleNote = $" (크기 보정 x{s.ExpectedMax / max:0.###})";
                    max = LongestSide(model);
                }
            }

            // 머티리얼
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            var mat = MakeMaterial($"{Root}/Materials/{s.Name}_Mat.mat", tex);
            if (s.Emissive)
            {
                var em = MakeEmissionMap(texPath, $"{Root}/Textures/{s.Name}_Emission.png");
                if (em != null)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetTexture("_EmissionMap", em);
                    mat.SetColor("_EmissionColor", new Color(1.6f, 1.6f, 1.6f));
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
            }
            EditorUtility.SetDirty(mat);

            // 프리팹
            var rootGo = new GameObject(s.Name);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, rootGo.transform);
            inst.name = "Model";
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
            if (s.Collider)
            {
                foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = true;
                }
            }
            string prefabPath = $"{Root}/Prefabs/{s.Name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(rootGo, prefabPath);
            Object.DestroyImmediate(rootGo);

            if (s.RockFade) MakeFadeCopy(mat, tex);
            log.Add($"{s.Name}: OK  최장변 {max:0.##}m{scaleNote}  콜라이더={(s.Collider ? "볼록" : "없음")}  발광={s.Emissive}");
        }

        private static float LongestSide(GameObject model)
        {
            var rs = model.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return 0f;
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return Mathf.Max(b.size.x, b.size.y, b.size.z);
        }

        private static void ConfigureModel(string path)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            bool changed = false;
            if (imp.materialImportMode != ModelImporterMaterialImportMode.None) { imp.materialImportMode = ModelImporterMaterialImportMode.None; changed = true; }
            if (imp.importNormals != ModelImporterNormals.Import) { imp.importNormals = ModelImporterNormals.Import; changed = true; }
            if (imp.importTangents != ModelImporterTangents.CalculateMikk) { imp.importTangents = ModelImporterTangents.CalculateMikk; changed = true; }
            if (imp.importCameras || imp.importLights) { imp.importCameras = false; imp.importLights = false; changed = true; }
            if (imp.meshCompression != ModelImporterMeshCompression.Off) { imp.meshCompression = ModelImporterMeshCompression.Off; changed = true; }
            if (changed) imp.SaveAndReimport();
        }

        private static void ConfigureTexture(string path, bool srgb)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp == null) return;
            bool changed = false;
            if (imp.sRGBTexture != srgb) { imp.sRGBTexture = srgb; changed = true; }
            if (!imp.mipmapEnabled) { imp.mipmapEnabled = true; changed = true; }
            if (imp.maxTextureSize != 2048) { imp.maxTextureSize = 2048; changed = true; }
            if (changed) imp.SaveAndReimport();
        }

        private static Material MakeMaterial(string path, Texture2D tex)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.12f);
            m.SetFloat("_Metallic", 0f);
            return m;
        }

        // 베이스 색에서 파랗게 빛나는 부분(청록·파랑이고 밝은 픽셀)만 남기고 나머지는 검게 → 발광 맵
        private static Texture2D MakeEmissionMap(string srcPath, string dstPath)
        {
            var tmp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tmp.LoadImage(File.ReadAllBytes(srcPath))) return null;
            var px = tmp.GetPixels32();
            var outPx = new Color32[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                float r = px[i].r / 255f, g = px[i].g / 255f, b = px[i].b / 255f;
                float lum = Mathf.Max(r, Mathf.Max(g, b));
                float blueish = b - r;                       // 파랑·청록은 b 가 r 보다 큼
                float k = Mathf.SmoothStep(0.15f, 0.45f, blueish) * Mathf.SmoothStep(0.5f, 0.85f, lum);
                outPx[i] = new Color32((byte)(px[i].r * k), (byte)(px[i].g * k), (byte)(px[i].b * k), 255);
            }
            var o = new Texture2D(tmp.width, tmp.height, TextureFormat.RGBA32, false);
            o.SetPixels32(outPx);
            File.WriteAllBytes(dstPath, o.EncodeToPNG());
            Object.DestroyImmediate(tmp); Object.DestroyImmediate(o);
            AssetDatabase.ImportAsset(dstPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(dstPath);
            if (imp != null && imp.maxTextureSize != 1024) { imp.maxTextureSize = 1024; imp.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(dstPath);
        }

        // JungleOcclusionFader 는 Resources/HJH/RockFade/<머티리얼 이름>_Fade 를 찾는다. 기존 M_Rock_Fade 를 베이스로 복사.
        private static void MakeFadeCopy(Material orig, Texture2D tex)
        {
            string dest = $"{RockFadeFolder}/{orig.name}_Fade.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(dest) == null)
            {
                if (!AssetDatabase.CopyAsset(RockFadeFolder + "/M_Rock_Fade.mat", dest))
                {
                    Debug.LogWarning("[MapProps] 반투명 복사본을 못 만듦: " + dest);
                    return;
                }
            }
            var fade = AssetDatabase.LoadAssetAtPath<Material>(dest);
            fade.SetTexture("_BaseMap", tex);
            fade.SetTexture("_BumpMap", null);
            fade.DisableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(fade);
        }
    }
}

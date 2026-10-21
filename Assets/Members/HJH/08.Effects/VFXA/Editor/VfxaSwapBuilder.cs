using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Assets.Members.HJH.Effects.VFXA.EditorTools
{
    // Tools for the VFX Apprentice pack (Effects/VFXA):
    //   Report        - measure the pack prefabs (size, length, shaders) into Temp/VfxaReport.txt
    //   Fix Materials - pack materials on Built-in shaders (pink in URP) -> URP Particles/Unlit
    //   Build         - one FX prefab per FxLibrary slot (pack effect + our pooling/drivers), a second library
    //                   that uses them, and a copy of SkillSwapTest that plays that library (F8 flips back to the old one).
    // The default library in Resources and the existing scenes are never touched.
    public static class VfxaSwapBuilder
    {
        // Paths are looked up, not hard-coded, so moving the HJH folders around does not break the tool.
        // Root = the VFXA folder this script sits in (…/VFXA/Editor/VfxaSwapBuilder.cs).
        public static string Root
        {
            get
            {
                string script = AssetDatabase.FindAssets("VfxaSwapBuilder t:MonoScript").Select(AssetDatabase.GUIDToAssetPath)
                    .First(p => p.EndsWith("/VfxaSwapBuilder.cs"));
                return Path.GetDirectoryName(Path.GetDirectoryName(script)).Replace('\\', '/');
            }
        }

        private static string Out => Root + "/Swapped";
        private static string LibraryPath => Out + "/HJH_FxLibrary_VFXA.asset";
        private static string SourceScene => FindScene("SkillSwapTest");
        private static string TestScene => Path.GetDirectoryName(SourceScene).Replace('\\', '/') + "/VFXASwapTest.unity";

        private static string FindScene(string name) =>
            AssetDatabase.FindAssets($"{name} t:Scene", new[] { "Assets/Members/HJH" }).Select(AssetDatabase.GUIDToAssetPath)
                .First(p => Path.GetFileNameWithoutExtension(p) == name);

        private const string Spells = "VFXApprentice/Core/Spells/Default Spells";
        private const string Emitters = "VFXApprentice/VFX/00_Simple_Emitters/Prefabs";
        private const string Mbr = "MBR/Prefabs";

        private enum Mode { OneShot, Held }

        // One FxLibrary slot -> one pack prefab. scale = pack prefab size inside our "authored for 1" root.
        private sealed class Swap
        {
            public string Slot, Pack, Name;
            public float Scale = 1f;
            public Mode Mode = Mode.OneShot;
            public bool Tinted;
            public int MaxInstances = 12;
            public Vector3 Offset, Euler;
            public System.Action<GameObject, GameObject> Setup; // (root, pack child)
        }

        private static readonly Swap[] Swaps =
        {
            // Combat (every character)
            new Swap { Slot = "hit", Name = "FX_Hit_VFXA", Pack = Spells + "/Spell Melee/VFX/VFX_Melee_Impact.prefab", Scale = 1f, Tinted = true },
            new Swap { Slot = "muzzle", Name = "FX_Muzzle_VFXA", Pack = Emitters + "/PF_10_Directional_Sparks.prefab", Scale = 0.9f, Tinted = true },
            new Swap { Slot = "explosion", Name = "FX_Explosion_VFXA", Pack = "VFXApprentice/VFX/02_Wispy_Cloud/Prefabs/PF_Nether_Explosion.prefab", Scale = 0.15f, MaxInstances = 6 },
            new Swap { Slot = "shockwave", Name = "FX_Shockwave_VFXA", Pack = Emitters + "/PF_08_Mesh_RadialDisk.prefab", Scale = 0.9f, Tinted = true },
            new Swap { Slot = "groundSlam", Name = "FX_GroundSlam_VFXA", Pack = "VFXApprentice/VFX/03_Lightning_Zap/Prefabs/PF_DEV_Lightning_Strike.prefab", Scale = 0.25f, MaxInstances = 6 },
            new Swap { Slot = "characterSwap", Name = "FX_CharacterSwap_VFXA", Pack = Spells + "/Spell Instant/VFX/VFX_Instant_Area.prefab", Scale = 0.5f },

            // Weapons and projectiles
            new Swap { Slot = "orb", Name = "FX_Orb_VFXA", Pack = Spells + "/Spell Ranged/VFX/VFX_Ranged_Projectile.prefab", Scale = 1.1f, Mode = Mode.Held },
            new Swap { Slot = "projectileImpact", Name = "FX_ProjectileImpact_VFXA", Pack = Spells + "/Spell Ranged/VFX/VFX_Ranged_Impact.prefab", Scale = 0.5f },

            // Movement
            new Swap { Slot = "blinkOut", Name = "FX_BlinkOut_VFXA", Pack = "VFXApprentice/VFX/03_Lightning_Zap/Prefabs/PF_DEV_Lightning_WindUp_2.prefab", Scale = 0.5f },
            new Swap { Slot = "blinkIn", Name = "FX_BlinkIn_VFXA", Pack = "VFXApprentice/VFX/02_Wispy_Cloud/Prefabs/PF_Nether_WindUp.prefab", Scale = 0.5f, Offset = Vector3.up * 0.9f },

            // Skills
            new Swap
            {
                Slot = "charge", Name = "FX_Charge_VFXA", Pack = Mbr + "/PS_Lightning_Lure_WindUp.prefab", Scale = 0.4f, Mode = Mode.Held,
                // Grows with the charge like the old one did.
                Setup = (root, child) => SetRef(root.AddComponent<FxScaleByValue>(), "target", child.transform, ("minScale", 0.45f), ("maxScale", 1f)),
            },
            new Swap { Slot = "cannonMuzzle", Name = "FX_CannonMuzzle_VFXA", Pack = Emitters + "/PF_11_LightRays.prefab", Scale = 0.7f, Tinted = true },
            new Swap { Slot = "runeCast", Name = "FX_RuneCast_VFXA", Pack = Spells + "/Spell Instant/VFX/VFX_Instant_Cast.prefab", Scale = 0.5f },
            new Swap { Slot = "runeGain", Name = "FX_RuneGain_VFXA", Pack = Emitters + "/PF_07_Burst_2.prefab", Scale = 0.9f },

            // Passives
            new Swap { Slot = "frenzyStack", Name = "FX_FrenzyStack_VFXA", Pack = Emitters + "/PF_07_Burst_1.prefab", Scale = 0.9f },
            new Swap
            {
                Slot = "frenzyAura", Name = "FX_FrenzyAura_VFXA", Pack = Emitters + "/PF_04_GPUTightOrbit.prefab", Scale = 0.35f, Mode = Mode.Held, Tinted = true,
                Setup = (root, child) =>
                {
                    root.AddComponent<FxFollow>();
                    var emission = root.AddComponent<FxEmissionByValue>();
                    SetArray(emission, "systems", child.GetComponentsInChildren<ParticleSystem>(true).Where(ps => ps.emission.enabled).ToArray());
                    SetFloat(emission, "ratePerValue", 6f);
                },
            },
        };

        // Slots whose look comes from a driver (FxReach) that shapes the old particle system at runtime:
        // keep that system, only give it a pack material.
        private static readonly (string slot, string name, string material)[] Restyles =
        {
            ("pull", "FX_Pull_VFXA", "VFXApprentice/VFX/Shared/Materials/MT_Motes_Add.mat"),
            ("pullBurst", "FX_PullBurst_VFXA", "VFXApprentice/VFX/Shared/Materials/MT_Motes_Add.mat"),
            ("lifesteal", "FX_Lifesteal_VFXA", "VFXApprentice/VFX/Shared/Materials/MT_Ball_01_Add.mat"),
        };

        [MenuItem("HJH/VFXA/Build Swapped FX + Test Scene")]
        public static void BuildAll()
        {
            FixMaterials();
            FxLibrary library = BuildLibrary();
            BuildScene(library);
            Debug.Log($"[VFXA] Done. Library: {LibraryPath}  Scene: {TestScene}");
        }

        [MenuItem("HJH/VFXA/Fix Pack Materials (Built-in -> URP)")]
        public static void FixMaterials()
        {
            Shader urpUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var log = new StringBuilder();
            foreach (string path in AssetDatabase.FindAssets("t:Material", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                string shader = material.shader != null ? material.shader.name : "";
                bool builtIn = shader.StartsWith("Mobile/") || shader.StartsWith("Legacy Shaders/") || shader.StartsWith("Particles/") ||
                               shader == "Standard" || shader == "Hidden/InternalErrorShader" || material.shader == null;
                if (!builtIn)
                    continue;

                Texture texture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                Color color = material.HasProperty("_TintColor") ? material.GetColor("_TintColor") * 2f
                    : material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                int blend = shader.Contains("Additive") ? 2 : shader.Contains("Premultiply") ? 1 : 0; // URP: 0 alpha, 1 premultiply, 2 additive

                material.shader = urpUnlit;
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", color);
                SetupUrpTransparent(material, blend);
                EditorUtility.SetDirty(material);
                log.AppendLine($"{path}: {shader} -> URP Particles/Unlit ({(blend == 2 ? "Additive" : blend == 1 ? "Premultiply" : "Alpha")})");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[VFXA] Fixed materials:\n" + (log.Length > 0 ? log.ToString() : "none"));
        }

        private static void SetupUrpTransparent(Material material, int blend)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", blend);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            switch (blend)
            {
                case 1:
                    material.SetFloat("_SrcBlend", (float)BlendMode.One);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    break;
                case 2:
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.One);
                    break;
                default:
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    break;
            }

            if (material.HasProperty("_SrcBlendAlpha")) material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (material.HasProperty("_DstBlendAlpha")) material.SetFloat("_DstBlendAlpha", material.GetFloat("_DstBlend"));
        }

        private static FxLibrary BuildLibrary()
        {
            Directory.CreateDirectory(Out);
            AssetDatabase.Refresh();

            FxLibrary source = Resources.Load<FxLibrary>(FxLibrary.ResourcePath);
            var library = AssetDatabase.LoadAssetAtPath<FxLibrary>(LibraryPath);
            if (library == null)
            {
                library = Object.Instantiate(source);
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            else
            {
                EditorUtility.CopySerialized(source, library);
            }

            library.name = Path.GetFileNameWithoutExtension(LibraryPath);
            var serialized = new SerializedObject(library);

            foreach (Swap swap in Swaps)
                serialized.FindProperty(swap.Slot).objectReferenceValue = BuildPrefab(swap);

            foreach (var restyle in Restyles)
            {
                // The copy still holds the old prefab in this slot: restyle that one.
                SerializedProperty slot = serialized.FindProperty(restyle.slot);
                slot.objectReferenceValue = Restyle((GameObject)slot.objectReferenceValue, restyle.name, restyle.material);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }

        private static GameObject BuildPrefab(Swap swap)
        {
            var pack = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/{swap.Pack}");
            if (pack == null)
            {
                Debug.LogError($"[VFXA] Missing pack prefab {swap.Pack}");
                return null;
            }

            var root = new GameObject(swap.Name);
            var child = (GameObject)PrefabUtility.InstantiatePrefab(pack);
            PrefabUtility.UnpackPrefabInstance(child, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = swap.Offset;
            child.transform.localRotation = Quaternion.Euler(swap.Euler);
            child.transform.localScale = Vector3.one * swap.Scale;

            foreach (Transform t in child.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

            ParticleSystem[] systems = child.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem ps in systems)
            {
                ParticleSystem.MainModule main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy; // our root is scaled by radius/size at runtime
                main.playOnAwake = false;                               // the pool plays it once it is placed
                main.stopAction = ParticleSystemStopAction.None;
                if (swap.Mode == Mode.OneShot)
                {
                    main.loop = false;                                  // one-shots must end so the pool gets them back
                    if (main.duration > 5f)
                        main.duration = 1.2f;                           // the pack's demo emitters run 10 s
                }

                // Empty container systems in the pack have no material: never draw them (they would be pink).
                if (ps.TryGetComponent(out ParticleSystemRenderer r) && r.sharedMaterials.All(m => m == null))
                    r.enabled = false;
            }

            var instance = root.AddComponent<FxPrefabInstance>();
            var settings = new SerializedObject(instance);
            settings.FindProperty("maxInstances").intValue = swap.MaxInstances;
            SerializedProperty untinted = settings.FindProperty("untinted");
            ParticleSystem[] keep = swap.Tinted ? new ParticleSystem[0] : systems;
            untinted.arraySize = keep.Length;
            for (int i = 0; i < keep.Length; i++)
                untinted.GetArrayElementAtIndex(i).objectReferenceValue = keep[i];
            settings.ApplyModifiedPropertiesWithoutUndo();

            swap.Setup?.Invoke(root, child);

            string path = $"{Out}/{swap.Name}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject Restyle(GameObject source, string name, string materialPath)
        {
            string path = $"{Out}/{name}.prefab";
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/{materialPath}");
            GameObject copy = Object.Instantiate(source);
            copy.name = name;
            foreach (ParticleSystemRenderer r in copy.GetComponentsInChildren<ParticleSystemRenderer>(true))
                r.sharedMaterial = material;
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(copy, path);
            Object.DestroyImmediate(copy);
            return saved;
        }

        // A copy of SkillSwapTest (as saved on disk) with an FxLibraryOverride that plays the VFXA library.
        private static void BuildScene(FxLibrary library)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScene) == null && !AssetDatabase.CopyAsset(SourceScene, TestScene))
            {
                Debug.LogError($"[VFXA] Could not copy {SourceScene}");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(TestScene);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            if (!wasOpen)
                scene = EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Additive);

            GameObject holder = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "FX Library Override (VFXA)");
            if (holder == null)
            {
                holder = new GameObject("FX Library Override (VFXA)");
                SceneManager.MoveGameObjectToScene(holder, scene);
            }

            if (!holder.TryGetComponent(out FxLibraryOverride overrider))
                overrider = holder.AddComponent<FxLibraryOverride>();
            SetRef(overrider, "library", library);

            if (!holder.TryGetComponent(out FxShowcase _))
                holder.AddComponent<FxShowcase>();

            // Showcase spot: halfway between the player and the test dummies, facing the dummies.
            GameObject[] roots = scene.GetRootGameObjects();
            GameObject player = roots.FirstOrDefault(go => go.name == "Player");
            Vector3[] dummies = roots.Where(go => go.name.StartsWith("TestDummy")).Select(go => go.transform.position).ToArray();
            if (player != null && dummies.Length > 0)
            {
                Vector3 target = dummies.Aggregate(Vector3.zero, (sum, p) => sum + p) / dummies.Length;
                Vector3 from = player.transform.position;
                Vector3 spot = Vector3.Lerp(from, target, 0.5f);
                spot.y = from.y;
                Vector3 facing = target - from;
                facing.y = 0f;
                holder.transform.SetPositionAndRotation(spot, facing.sqrMagnitude > 0.01f ? Quaternion.LookRotation(facing) : Quaternion.identity);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!wasOpen)
                EditorSceneManager.CloseScene(scene, true);
        }

        private static void SetRef(Object target, string field, Object value, params (string name, float value)[] floats)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            foreach (var (name, v) in floats)
                so.FindProperty(name).floatValue = v;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty array = so.FindProperty(field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("HJH/VFXA/Report Pack Prefabs")]
        public static void Report()
        {
            var sb = new StringBuilder();
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { Root });
            foreach (string path in guids.Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                GameObject go = Object.Instantiate(prefab);
                go.transform.position = Vector3.zero;
                ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);
                float end = 0f;
                bool loops = false;
                var modes = new HashSet<string>();
                foreach (ParticleSystem ps in systems)
                {
                    ParticleSystem.MainModule main = ps.main;
                    loops |= main.loop;
                    modes.Add(main.scalingMode.ToString());
                    end = Mathf.Max(end, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
                }

                int missing = go.GetComponentsInChildren<Component>(true).Count(c => c == null);
                var shaders = new HashSet<string>();
                int nullMaterials = 0;
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r.enabled) continue;
                    foreach (Material m in r.sharedMaterials)
                    {
                        if (m == null) { nullMaterials++; continue; }
                        bool broken = m.shader == null || !m.shader.isSupported || ShaderUtil.ShaderHasError(m.shader);
                        shaders.Add((m.shader != null ? m.shader.name : "<null shader>") + (broken ? " [BROKEN]" : ""));
                    }
                }

                sb.AppendLine($"{path.Substring(Root.Length + 1)} | PS={systems.Length} loop={loops} end={end:0.0}s scaling={string.Join("/", modes)} missingScripts={missing} enabledNullMaterials={nullMaterials}");
                sb.AppendLine("    shaders: " + string.Join(", ", shaders));
                Object.DestroyImmediate(go);
            }

            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/VfxaReport.txt", sb.ToString());
            Debug.Log("[VFXA] Report written to Temp/VfxaReport.txt");
        }
    }
}

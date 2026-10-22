using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Assets.Members.HJH.Effects.Element.EditorTools
{
    // Makes the fireball / wind cutter flying effects once, then they are ordinary prefabs to edit by hand
    // (08.Effects/Element). Also hooks them into the Fire_Fireball / Wind_WindCutter skills and renders a preview
    // of them flying (Temp/ElementFxPreview.png).
    // ⚠️ Running it again OVERWRITES hand edits on FX_Fireball / FX_WindCutter. Edit the prefabs, not this.
    public static class ElementFxBuilder
    {
        private const string Folder = "Assets/Members/HJH/08.Effects/Element";
        private const string LearnMaterials = "Assets/Members/HJH/08.Effects/Learn/Materials/";
        private const string SkillFolder = "Assets/Members/HJH/05.Data/Skill/Element/";
        private const string PreviewPath = "Temp/ElementFxPreview.png";

        [MenuItem("HJH/Element FX/Build Fireball + Wind Cutter")]
        public static void BuildAll()
        {
            EnsureFolder();
            Material bladeMaterial = WindBladeMaterial();

            GameObject fireball = Save(BuildFireball(), "Fire/FX_Fireball");
            GameObject cutter = Save(BuildWindCutter(bladeMaterial), "Wind/FX_WindCutter");

            UseAsTravelFx("Fire_Fireball", fireball);
            UseAsTravelFx("Wind_WindCutter", cutter);
            AssetDatabase.SaveAssets();

            var oldOrb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Members/HJH/08.Effects/VFXA/Swapped/FX_Orb_VFXA.prefab");
            RenderPreview(new[] { fireball, cutter, oldOrb });
            Debug.Log($"[ElementFx] Built FX_Fireball / FX_WindCutter, preview at {PreviewPath}");
        }

        // ---------- 화염구 ----------
        // 하얗게 뜨거운 심 + 일렁이는 불꽃 + 뒤로 남는 불 꼬리 + 검은 연기 + 위로 튀는 불똥
        private static GameObject BuildFireball()
        {
            Transform root = new GameObject("FX_Fireball").transform;

            ParticleSystem glow = NewSystem(root, "Glow", "M_FX_Glow", world: false);
            Main(glow, life: 0.2f, size: 2.4f, color: C(1f, 0.36f, 0.06f, 0.5f));
            Rate(glow, 20f);
            ColorOverLife(glow, (0f, C(1, 1, 1, 0)), (0.3f, C(1, 1, 1, 1)), (1f, C(1, 1, 1, 0)));

            ParticleSystem flames = NewSystem(root, "Flames", "M_FX_Glow", world: false);
            Main(flames, life: new ParticleSystem.MinMaxCurve(0.15f, 0.25f), size: new ParticleSystem.MinMaxCurve(0.45f, 0.75f),
                color: Color.white, speed: new ParticleSystem.MinMaxCurve(0.4f, 1f));
            RandomRotation(flames);
            Sphere(flames, 0.15f);
            Rate(flames, 80f);
            ColorOverLife(flames, (0f, C(1f, 0.92f, 0.55f, 1f)), (0.35f, C(1f, 0.5f, 0.1f, 0.9f)), (1f, C(0.7f, 0.12f, 0.02f, 0f)));
            SizeOverLife(flames, 1f, 0.35f);
            Noise(flames, 0.5f, 3f);

            ParticleSystem core = NewSystem(root, "Core", "M_FX_Glow", world: false);
            Main(core, life: 0.1f, size: new ParticleSystem.MinMaxCurve(0.7f, 0.85f), color: C(1f, 0.96f, 0.82f, 1f));
            Rate(core, 30f);

            ParticleSystem trail = NewSystem(root, "FireTrail", "M_FX_Glow", world: true);
            Main(trail, life: new ParticleSystem.MinMaxCurve(0.28f, 0.4f), size: new ParticleSystem.MinMaxCurve(0.65f, 0.9f), color: Color.white);
            RandomRotation(trail);
            Sphere(trail, 0.12f);
            RateOverDistance(trail, 20f);
            ColorOverLife(trail, (0f, C(1f, 0.7f, 0.25f, 0.95f)), (0.25f, C(1f, 0.32f, 0.05f, 0.85f)),
                (0.65f, C(0.55f, 0.07f, 0.02f, 0.45f)), (1f, C(0.25f, 0.03f, 0.02f, 0f)));
            SizeOverLife(trail, 1f, 0.15f);
            Noise(trail, 0.7f, 2f);

            ParticleSystem smoke = NewSystem(root, "Smoke", "M_FX_Smoke", world: true);
            Main(smoke, life: new ParticleSystem.MinMaxCurve(0.5f, 0.8f), size: new ParticleSystem.MinMaxCurve(0.5f, 0.8f), color: Color.white);
            RandomRotation(smoke);
            RateOverDistance(smoke, 7f);
            ColorOverLife(smoke, (0f, C(0.2f, 0.14f, 0.1f, 0f)), (0.15f, C(0.2f, 0.14f, 0.1f, 0.5f)), (1f, C(0.12f, 0.1f, 0.1f, 0f)));
            SizeOverLife(smoke, 0.6f, 1.7f);
            Rise(smoke, 0.8f);
            smoke.GetComponent<ParticleSystemRenderer>().sortingFudge = 50f; // drawn behind the fire

            ParticleSystem embers = NewSystem(root, "Embers", "M_FX_Spark", world: true);
            Main(embers, life: new ParticleSystem.MinMaxCurve(0.3f, 0.6f), size: new ParticleSystem.MinMaxCurve(0.05f, 0.1f),
                color: Color.white, speed: new ParticleSystem.MinMaxCurve(0.8f, 2.5f));
            ParticleSystem.MainModule emberMain = embers.main;
            emberMain.gravityModifier = -0.25f; // 불똥은 위로
            Sphere(embers, 0.25f);
            Rate(embers, 40f);
            ColorOverLife(embers, (0f, C(1f, 0.9f, 0.5f, 1f)), (0.5f, C(1f, 0.45f, 0.08f, 1f)), (1f, C(0.8f, 0.15f, 0.02f, 0f)));
            Noise(embers, 1f, 2f);
            Stretch(embers, 0.06f, 1.2f);

            return root.gameObject;
        }

        // ---------- 바람 칼날 ----------
        // 바닥에 눕힌 초승달 칼날(민트 + 흰 날) + 칼끝에서 남는 바람 줄기 + 옅은 바람 + 흩날리는 입자
        private static GameObject BuildWindCutter(Material bladeMaterial)
        {
            Transform root = new GameObject("FX_WindCutter").transform;

            ParticleSystem blade = FlatBlade(root, "Blade", bladeMaterial, new ParticleSystem.MinMaxCurve(2.2f, 2.4f), C(0.5f, 1f, 0.82f, 0.9f), 0.09f);
            ColorOverLife(blade, (0f, C(1, 1, 1, 1)), (1f, C(1, 1, 1, 0)));
            ParticleSystem edge = FlatBlade(root, "BladeEdge", bladeMaterial, new ParticleSystem.MinMaxCurve(2.1f, 2.2f), C(1f, 1f, 1f, 0.8f), 0.05f);
            edge.transform.localPosition = new Vector3(0f, 0.01f, 0.05f);

            Material trailMaterial = AssetDatabase.LoadAssetAtPath<Material>(LearnMaterials + "M_FX_Trail.mat");
            AddTrail(root, "TrailLeft", new Vector3(-0.85f, 0f, -0.15f), trailMaterial, 0.14f);
            AddTrail(root, "TrailRight", new Vector3(0.85f, 0f, -0.15f), trailMaterial, 0.14f);
            AddTrail(root, "TrailCenter", new Vector3(0f, 0f, 0.3f), trailMaterial, 0.2f);

            // 칼날 폭에서 뒤로 흘러가는 바람 줄기 (속도 방향으로 늘린 가는 선)
            ParticleSystem streaks = NewSystem(root, "Streaks", "M_FX_Glow", world: true);
            Main(streaks, life: new ParticleSystem.MinMaxCurve(0.18f, 0.28f), size: new ParticleSystem.MinMaxCurve(0.06f, 0.1f), color: C(0.7f, 1f, 0.9f, 1f));
            Box(streaks, new Vector3(1.8f, 0.1f, 0.3f));
            Rate(streaks, 60f);
            ColorOverLife(streaks, (0f, C(1, 1, 1, 0.8f)), (1f, C(1, 1, 1, 0f)));
            ParticleSystem.VelocityOverLifetimeModule velocity = streaks.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = 0f;
            velocity.y = 0f;
            velocity.z = -4f;
            Stretch(streaks, 0.15f, 2f);

            ParticleSystem motes = NewSystem(root, "Motes", "M_FX_Spark", world: true);
            Main(motes, life: new ParticleSystem.MinMaxCurve(0.25f, 0.4f), size: new ParticleSystem.MinMaxCurve(0.04f, 0.08f),
                color: C(0.75f, 1f, 0.9f, 1f), speed: new ParticleSystem.MinMaxCurve(0.5f, 1.5f));
            Box(motes, new Vector3(1.4f, 0.1f, 0.2f));
            Rate(motes, 40f);
            ColorOverLife(motes, (0f, C(1, 1, 1, 1)), (1f, C(1, 1, 1, 0)));
            Noise(motes, 1.2f, 2.5f);

            return root.gameObject;
        }

        // A flat quad lying on the ground, turned with the effect (Mesh mode + Local alignment), so the crescent points forward.
        private static ParticleSystem FlatBlade(Transform root, string name, Material material, ParticleSystem.MinMaxCurve size, Color color, float life)
        {
            ParticleSystem system = NewSystem(root, name, null, world: false);
            system.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Main(system, life: life, size: size, color: color);
            Rate(system, 50f);

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            renderer.alignment = ParticleSystemRenderSpace.Local;
            return system;
        }

        private static void AddTrail(Transform root, string name, Vector3 position, Material material, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = 0.18f;
            trail.minVertexDistance = 0.08f;
            trail.widthCurve = AnimationCurve.Linear(0f, width, 1f, 0f);
            trail.colorGradient = MakeGradient((0f, C(0.85f, 1f, 0.95f, 0.85f)), (0.4f, C(0.45f, 1f, 0.8f, 0.5f)), (1f, C(0.3f, 0.9f, 0.7f, 0f)));
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.textureMode = LineTextureMode.Stretch;
        }

        // ---------- 초승달 텍스처 ----------
        // 큰 원에서 아래로 내린 원을 빼면 초승달. 앞쪽(바깥) 날이 밝고 안쪽·양 끝은 흐려짐. 텍스처 위쪽 = 앞쪽.
        private static Material WindBladeMaterial()
        {
            string texturePath = Folder + "/T_WindCrescent.png";
            const int sizePx = 256;
            var texture = new Texture2D(sizePx, sizePx, TextureFormat.RGBA32, false);
            var outerCenter = new Vector2(0.5f, 0.1f);
            var innerCenter = new Vector2(0.5f, 0f);
            const float outerRadius = 0.55f, innerRadius = 0.55f;

            for (int y = 0; y < sizePx; y++)
            for (int x = 0; x < sizePx; x++)
            {
                var p = new Vector2((x + 0.5f) / sizePx, (y + 0.5f) / sizePx);
                float insideOuter = outerRadius - Vector2.Distance(p, outerCenter);
                float outsideInner = Vector2.Distance(p, innerCenter) - innerRadius;
                float alpha = Mathf.SmoothStep(0f, 1f, insideOuter / 0.02f) * Mathf.SmoothStep(0f, 1f, outsideInner / 0.05f);
                alpha *= Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.3f, 0.48f, Mathf.Abs(p.x - 0.5f))); // 양 끝 흐리게
                alpha *= Mathf.Lerp(1f, 0.3f, Mathf.Clamp01(insideOuter / 0.15f));                       // 바깥 날 밝게
                alpha = Mathf.Clamp01(alpha);
                texture.SetPixel(x, y, new Color(alpha, alpha, alpha, alpha));
            }

            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            string materialPath = Folder + "/M_FX_WindBlade.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>(LearnMaterials + "M_FX_Glow.mat"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- 파티클 만들기 도우미 ----------
        private static ParticleSystem NewSystem(Transform parent, string name, string material, bool world)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = system.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = true;
            main.startSpeed = 0f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.maxParticles = 300;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (material != null)
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(LearnMaterials + material + ".mat");
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        private static void Main(ParticleSystem system, ParticleSystem.MinMaxCurve life, ParticleSystem.MinMaxCurve size, Color color,
            ParticleSystem.MinMaxCurve? speed = null)
        {
            ParticleSystem.MainModule main = system.main;
            main.startLifetime = life;
            main.startSize = size;
            main.startColor = color;
            if (speed.HasValue)
                main.startSpeed = speed.Value;
        }

        private static void Rate(ParticleSystem system, float perSecond)
        {
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = perSecond;
        }

        // 움직인 거리만큼 뿜기 → 빨리 날아가도 꼬리가 끊기지 않음
        private static void RateOverDistance(ParticleSystem system, float perMeter)
        {
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = perMeter;
        }

        private static void Sphere(ParticleSystem system, float radius)
        {
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
        }

        private static void Box(ParticleSystem system, Vector3 scale)
        {
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = scale;
        }

        private static void RandomRotation(ParticleSystem system)
        {
            ParticleSystem.MainModule main = system.main;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        }

        private static void ColorOverLife(ParticleSystem system, params (float time, Color color)[] keys)
        {
            ParticleSystem.ColorOverLifetimeModule module = system.colorOverLifetime;
            module.enabled = true;
            module.color = new ParticleSystem.MinMaxGradient(MakeGradient(keys));
        }

        private static void SizeOverLife(ParticleSystem system, float from, float to)
        {
            ParticleSystem.SizeOverLifetimeModule module = system.sizeOverLifetime;
            module.enabled = true;
            module.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, from, 1f, to));
        }

        private static void Noise(ParticleSystem system, float strength, float frequency)
        {
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = frequency;
            noise.scrollSpeed = 1f;
        }

        private static void Rise(ParticleSystem system, float upSpeed)
        {
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = 0f;
            velocity.y = upSpeed;
            velocity.z = 0f;
        }

        private static void Stretch(ParticleSystem system, float velocityScale, float lengthScale)
        {
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = velocityScale;
            renderer.lengthScale = lengthScale;
        }

        private static Gradient MakeGradient(params (float time, Color color)[] keys)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                keys.Select(k => new GradientColorKey(k.color, k.time)).ToArray(),
                keys.Select(k => new GradientAlphaKey(k.color.a, k.time)).ToArray());
            return gradient;
        }

        private static Color C(float r, float g, float b, float a = 1f) => new(r, g, b, a);

        // ---------- 저장 / 스킬에 연결 ----------
        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Members/HJH/08.Effects", "Element");
        }

        private static GameObject Save(GameObject root, string name)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        // 날아가는 이펙트 칸에 넣고, 색은 프리팹에 직접 칠했으니 Tint는 끔
        private static void UseAsTravelFx(string skillName, GameObject prefab)
        {
            var skill = AssetDatabase.LoadAssetAtPath<ScriptableObject>(SkillFolder + skillName + ".asset");
            var serialized = new SerializedObject(skill);
            serialized.FindProperty("travelFx.prefab").objectReferenceValue = prefab;
            serialized.FindProperty("travelFx.tint").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skill);
        }

        // ---------- 미리보기 ----------
        // Each row: one effect flying from left to right at 18 m/s, seen from the game camera angle.
        // Columns: 0.15s / 0.3s / 0.45s in flight, then 0.25s and 0.9s after it was stopped.
        private const int Cell = 300;

        private static void RenderPreview(GameObject[] prefabs)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            var cameraGo = new GameObject("PreviewCamera");
            SceneManager.MoveGameObjectToScene(cameraGo, scene);
            var camera = cameraGo.AddComponent<Camera>();
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.26f, 0.28f, 0.27f);
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            var target = new RenderTexture(Cell, Cell, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;

            float[] flight = { 0.15f, 0.3f, 0.45f };
            float[] afterStop = { 0.25f, 0.9f };
            int columns = flight.Length + afterStop.Length;
            var sheet = new Texture2D(columns * Cell, prefabs.Length * Cell, TextureFormat.RGB24, false);
            var readback = new Texture2D(Cell, Cell, TextureFormat.RGB24, false);

            for (int row = 0; row < prefabs.Length; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    float flyTime = column < flight.Length ? flight[column] : flight[^1];
                    float stopTime = column < flight.Length ? 0f : afterStop[column - flight.Length];
                    GameObject instance = Fly(prefabs[row], scene, flyTime, stopTime, out Vector3 head);

                    Vector3 look = head + Vector3.left * 0.6f;
                    cameraGo.transform.position = look + Quaternion.Euler(55f, 0f, 0f) * Vector3.back * 7f;
                    cameraGo.transform.LookAt(look);
                    Capture(camera, target, readback);
                    sheet.SetPixels(column * Cell, (prefabs.Length - 1 - row) * Cell, Cell, Cell, readback.GetPixels());

                    if (instance != null)
                        Object.DestroyImmediate(instance);
                }
            }

            sheet.Apply();
            File.WriteAllBytes(PreviewPath, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Object.DestroyImmediate(readback);
            camera.targetTexture = null;
            target.Release();
            EditorSceneManager.ClosePreviewScene(scene);
        }

        private static GameObject Fly(GameObject prefab, Scene scene, float flyTime, float stopTime, out Vector3 head)
        {
            const float speed = 18f, step = 1f / 60f;
            var start = new Vector3(-flyTime * speed * 0.5f, 1f, 0f);
            head = start;
            if (prefab == null)
                return null;

            GameObject instance = Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(instance, scene);
            instance.transform.SetPositionAndRotation(start, Quaternion.LookRotation(Vector3.right));
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem system in systems)
            {
                ParticleSystem.MainModule main = system.main;
                main.ringBufferMode = ParticleSystemRingBufferMode.Disabled;
                system.Simulate(0f, false, true, true);
            }

            for (float t = 0f; t < flyTime; t += step)
            {
                instance.transform.position += Vector3.right * speed * step;
                foreach (ParticleSystem system in systems)
                    system.Simulate(step, false, false, false);
            }

            head = instance.transform.position;
            if (stopTime > 0f)
            {
                foreach (ParticleSystem system in systems)
                    system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                for (float t = 0f; t < stopTime; t += step)
                    foreach (ParticleSystem system in systems)
                        system.Simulate(step, false, false, false);
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

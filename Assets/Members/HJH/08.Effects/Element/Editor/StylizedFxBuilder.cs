using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Members.HJH.EditorTools
{
    // 메뉴: HJH > Element FX > 스타일 이펙트 만들기
    // 스타일 불꽃·바람결 셰이더(SG_StylizedFire / SG_StylizedWind)로 파티클 프리팹 4개를 만든다.
    //  FX_Fire_StylizedBurst : 구가 부풀며 타서 사라지는 폭발 + 위로 솟는 불꽃 혀 + 번쩍 심지
    //  FX_Fire_StylizedHit   : 작은 불꽃 덩어리
    //  FX_Wind_StylizedSwirl : 납작한 고리 3개가 돌며 퍼지는 회오리
    //  FX_Wind_StylizedHit   : 작은 고리 2개
    // Resources/HJH/StylizedFx 에 저장 → ElementFlair 가 이름으로 불러 씀. 다시 실행하면 덮어씀.
    public static class StylizedFxBuilder
    {
        private const string GraphMenu = "HJH/Element FX/스타일 이펙트 셰이더 그래프 만들기";
        private const string StylizedFolder = "Assets/Members/HJH/08.Effects/Stylized";
        private const string FireMaterialPath = StylizedFolder + "/M_StylizedFire.mat";
        private const string WindMaterialPath = StylizedFolder + "/M_StylizedWind.mat";
        private const string RingMeshPath = StylizedFolder + "/SM_StylizedRing.asset";
        private const string OutFolder = "Assets/Members/HJH/05.Data/Resources/HJH/StylizedFx";
        private const string LearnMaterials = "Assets/Members/HJH/08.Effects/Learn/Materials";
        private const string SoftMaterialPath = "Assets/Members/HJH/09.JungleWater/Generated/Materials/M_WaterSplash.mat";

        // 배경(어두운 정글)에 맞추는 보조 입자: 연기·불씨·나뭇잎 부스러기·흙먼지·섬광
        private static Material _smoke, _spark, _glow, _soft;

        [MenuItem("HJH/Element FX/스타일 이펙트 만들기")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(FireMaterialPath) == null || AssetDatabase.LoadAssetAtPath<Material>(WindMaterialPath) == null)
                EditorApplication.ExecuteMenuItem(GraphMenu);

            var fire = AssetDatabase.LoadAssetAtPath<Material>(FireMaterialPath);
            var wind = AssetDatabase.LoadAssetAtPath<Material>(WindMaterialPath);
            if (fire == null || wind == null)
            {
                Debug.LogError("[ElementFx] 스타일 이펙트 머티리얼이 없습니다. '" + GraphMenu + "' 를 먼저 실행해 주세요.");
                return;
            }

            _smoke = AssetDatabase.LoadAssetAtPath<Material>(LearnMaterials + "/M_FX_Smoke.mat");
            _spark = AssetDatabase.LoadAssetAtPath<Material>(LearnMaterials + "/M_FX_Spark.mat");
            _glow = AssetDatabase.LoadAssetAtPath<Material>(LearnMaterials + "/M_FX_Glow.mat");
            _soft = AssetDatabase.LoadAssetAtPath<Material>(SoftMaterialPath);

            EnsureFolder(OutFolder);
            Mesh sphere = BuiltinMesh(PrimitiveType.Sphere);
            Mesh ring = RingMesh();

            Save(FireBurst(fire, sphere), "FX_Fire_StylizedBurst");
            Save(FireHit(fire, sphere), "FX_Fire_StylizedHit");
            Save(WindSwirl(wind, ring, rings: 3, size: 2.2f, life: 0.65f, debris: 18), "FX_Wind_StylizedSwirl");
            Save(WindSwirl(wind, ring, rings: 2, size: 0.9f, life: 0.38f, debris: 6), "FX_Wind_StylizedHit");
            Save(HitFlare(), "FX_HitFlare");

            AssetDatabase.SaveAssets();
            Debug.Log("[ElementFx] 스타일 이펙트 5개 생성 완료: " + OutFolder);
        }

        // ---------------------------------------------------------------- 불

        private static GameObject FireBurst(Material mat, Mesh sphere)
        {
            var root = new GameObject("FX_Fire_StylizedBurst");

            // 부풀며 타서 사라지는 불덩어리
            ParticleSystem blobs = NewSystem(root.transform, "Blobs", mat);
            Main(blobs, life: (0.7f, 1.0f), speed: (1.8f, 4.2f), size: (1.1f, 2.3f), gravity: -0.4f);
            Burst(blobs, 7);
            Shape(blobs, ParticleSystemShapeType.Sphere, 0.45f);
            Grow(blobs, 0.5f, 1.35f);
            FadeOut(blobs);
            AsMesh(blobs, sphere);

            // 위로 솟는 불꽃 혀
            ParticleSystem tongues = NewSystem(root.transform, "Tongues", mat);
            Main(tongues, life: (0.5f, 0.85f), speed: (3.2f, 5.5f), size: (0.65f, 1.2f), gravity: -0.2f);
            Burst(tongues, 10);
            Shape(tongues, ParticleSystemShapeType.Cone, 0.4f, angle: 28f);
            Grow(tongues, 1f, 0.4f);
            FadeOut(tongues);
            var tr = tongues.GetComponent<ParticleSystemRenderer>();
            tr.renderMode = ParticleSystemRenderMode.Stretch;
            tr.lengthScale = 1.6f;
            tr.velocityScale = 0.05f;

            // 처음 한 번 번쩍 커지는 심지
            ParticleSystem core = NewSystem(root.transform, "Core", mat);
            Main(core, life: (0.26f, 0.26f), speed: (0f, 0f), size: (3f, 3f), gravity: 0f);
            Burst(core, 1);
            Shape(core, ParticleSystemShapeType.Sphere, 0.01f);
            Grow(core, 0.35f, 1f);
            FadeOut(core);
            AsMesh(core, sphere);

            // 타고 남은 연기: 어둡고 느리게 피어오름 (배경과 어울리게 하는 핵심)
            Smoke(root.transform, count: 6, size: (1.6f, 3f), life: (1.3f, 2f), color: new Color(0.16f, 0.13f, 0.11f, 0.6f));
            // 위로 흩날리는 불씨
            Embers(root.transform, count: 22, color: new Color(1f, 0.55f, 0.15f, 1f));

            return root;
        }

        private static GameObject FireHit(Material mat, Mesh sphere)
        {
            var root = new GameObject("FX_Fire_StylizedHit");
            ParticleSystem blobs = NewSystem(root.transform, "Blobs", mat);
            Main(blobs, life: (0.35f, 0.55f), speed: (1.5f, 3f), size: (0.45f, 0.9f), gravity: -0.3f);
            Burst(blobs, 4);
            Shape(blobs, ParticleSystemShapeType.Sphere, 0.2f);
            Grow(blobs, 0.6f, 1.3f);
            FadeOut(blobs);
            AsMesh(blobs, sphere);
            Smoke(root.transform, count: 2, size: (0.6f, 1.1f), life: (0.7f, 1.1f), color: new Color(0.18f, 0.15f, 0.13f, 0.45f));
            Embers(root.transform, count: 6, color: new Color(1f, 0.6f, 0.2f, 1f));
            return root;
        }

        // ---------------------------------------------------------------- 보조 입자

        private static void Smoke(Transform parent, int count, (float, float) size, (float, float) life, Color color)
        {
            if (_smoke == null)
                return;
            ParticleSystem smoke = NewSystem(parent, "Smoke", _smoke);
            Main(smoke, life: life, speed: (0.4f, 1.2f), size: size, gravity: -0.12f);
            smoke.GetComponent<ParticleSystemRenderer>().sortingFudge = 10f;   // 불 뒤에 그려지게
            var main = smoke.main;
            main.startColor = color;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Burst(smoke, count);
            Shape(smoke, ParticleSystemShapeType.Sphere, 0.5f);
            Grow(smoke, 0.6f, 1.5f);
            Fade(smoke, 0.1f, 0.3f);
        }

        private static void Embers(Transform parent, int count, Color color)
        {
            if (_spark == null)
                return;
            ParticleSystem embers = NewSystem(parent, "Embers", _spark);
            Main(embers, life: (0.7f, 1.4f), speed: (2f, 5.5f), size: (0.04f, 0.1f), gravity: -0.25f);
            var main = embers.main;
            main.startColor = color;
            Burst(embers, count);
            Shape(embers, ParticleSystemShapeType.Cone, 0.5f, angle: 45f);
            var noise = embers.noise;   // 바람에 흩날리듯
            noise.enabled = true;
            noise.strength = 1.2f;
            noise.frequency = 0.8f;
            Fade(embers, 0f, 0.6f);
        }

        // 나뭇잎·흙 부스러기: 회오리를 따라 돌며 날아감 (정글 배경)
        private static void Debris(Transform parent, int count, float radius)
        {
            if (_soft == null || count <= 0)
                return;
            ParticleSystem debris = NewSystem(parent, "Debris", _soft);
            Main(debris, life: (0.6f, 1.1f), speed: (1.2f, 3f), size: (0.12f, 0.26f), gravity: 0.4f);
            var main = debris.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.18f, 0.3f, 0.12f, 1f), new Color(0.33f, 0.25f, 0.14f, 1f));
            Burst(debris, count);
            Shape(debris, ParticleSystemShapeType.Circle, radius * 0.5f);
            var orbit = debris.velocityOverLifetime;
            orbit.enabled = true;
            orbit.space = ParticleSystemSimulationSpace.Local;
            orbit.orbitalY = new ParticleSystem.MinMaxCurve(5f, 8f);
            orbit.radial = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            orbit.y = new ParticleSystem.MinMaxCurve(1f, 2.5f);
            Fade(debris, 0f, 0.7f);
        }

        // 바닥 흙먼지
        private static void Dust(Transform parent, int count, float size)
        {
            if (_smoke == null || count <= 0)
                return;
            ParticleSystem dust = NewSystem(parent, "Dust", _smoke);
            Main(dust, life: (0.6f, 1f), speed: (1.5f, 3f), size: (size * 0.5f, size), gravity: 0f);
            var main = dust.main;
            main.startColor = new Color(0.42f, 0.38f, 0.3f, 0.35f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Burst(dust, count);
            var shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;   // 바닥에서 바깥으로
            shape.radius = 0.3f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            Grow(dust, 0.5f, 1.4f);
            Fade(dust, 0.05f, 0.4f);
        }

        // 맞은 지점 섬광: 짧은 빛줄기 + 플레어 (Fx.Spawn 색으로 물듦)
        private static GameObject HitFlare()
        {
            var root = new GameObject("FX_HitFlare");
            if (_spark != null)
            {
                ParticleSystem streaks = NewSystem(root.transform, "Streaks", _spark);
                Main(streaks, life: (0.08f, 0.15f), speed: (9f, 16f), size: (0.05f, 0.09f), gravity: 0f);
                Burst(streaks, 9);
                Shape(streaks, ParticleSystemShapeType.Sphere, 0.05f);
                var r = streaks.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.lengthScale = 3.5f;
                r.velocityScale = 0.04f;
                Fade(streaks, 0f, 0.4f);
            }
            if (_glow != null)
            {
                ParticleSystem flare = NewSystem(root.transform, "Flare", _glow);
                Main(flare, life: (0.1f, 0.1f), speed: (0f, 0f), size: (1.4f, 1.4f), gravity: 0f);
                Burst(flare, 1);
                Shape(flare, ParticleSystemShapeType.Sphere, 0.01f);
                Grow(flare, 1f, 0.3f);
                Fade(flare, 0f, 0.2f);
            }
            return root;
        }

        // 알파: 처음 hold 비율까지 유지하다가 끝에 0. fadeIn 이 0 보다 크면 그만큼 나타남
        private static void Fade(ParticleSystem ps, float fadeIn, float hold)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var g = new Gradient();
            var alphas = fadeIn > 0f
                ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(1f, Mathf.Max(fadeIn, hold)), new GradientAlphaKey(0f, 1f) }
                : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, hold), new GradientAlphaKey(0f, 1f) };
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alphas);
            color.color = g;
        }

        // ---------------------------------------------------------------- 바람

        private static GameObject WindSwirl(Material mat, Mesh ring, int rings, float size, float life, int debris)
        {
            var root = new GameObject("FX_Wind_Stylized");
            ParticleSystem swirl = NewSystem(root.transform, "Rings", mat);
            Main(swirl, life: (life * 0.85f, life * 1.1f), speed: (0.4f, 1.2f), size: (size * 0.75f, size * 1.1f), gravity: -0.1f);

            var emission = swirl.emission;
            var bursts = new ParticleSystem.Burst[rings];
            for (int i = 0; i < rings; i++)
                bursts[i] = new ParticleSystem.Burst(i * 0.05f, (short)1);
            emission.SetBursts(bursts);

            var shape = swirl.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;   // 살짝 위로 떠오르며 퍼짐
            shape.angle = 0f;
            shape.radius = 0.01f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            Grow(swirl, 0.55f, 1.35f);
            FadeOut(swirl);

            // 고리를 바닥에 눕히고 Y 축으로 빙글빙글
            var main = swirl.main;
            main.startRotation3D = true;
            main.startRotationX = 0f;
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = 0f;
            var rot = swirl.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = 0f;
            rot.z = 0f;
            rot.y = new ParticleSystem.MinMaxCurve(7f, 13f);   // 라디안/초

            AsMesh(swirl, ring);
            swirl.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.World;

            // 고리는 옅게 (애니 느낌 줄이기): 시작 알파 0.7 → 셰이더가 처음부터 조금 갉아먹은 상태
            var startColor = swirl.main;
            startColor.startColor = new Color(1f, 1f, 1f, 0.7f);

            Debris(root.transform, debris, size);
            Dust(root.transform, Mathf.Max(2, debris / 3), size * 0.9f);
            return root;
        }

        // ---------------------------------------------------------------- 파티클 도우미

        private static ParticleSystem NewSystem(Transform parent, string name, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        private static void Main(ParticleSystem ps, (float min, float max) life, (float min, float max) speed, (float min, float max) size, float gravity)
        {
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.min, life.max);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.min, speed.max);
            main.startSize = new ParticleSystem.MinMaxCurve(size.min, size.max);
            main.startColor = Color.white;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 64;
            main.stopAction = ParticleSystemStopAction.None;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
        }

        private static void Burst(ParticleSystem ps, int count)
        {
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        }

        private static void Shape(ParticleSystem ps, ParticleSystemShapeType type, float radius, float angle = 25f)
        {
            var shape = ps.shape;
            shape.shapeType = type;
            shape.radius = radius;
            shape.angle = angle;
            if (type == ParticleSystemShapeType.Cone)
                shape.rotation = new Vector3(-90f, 0f, 0f);   // 위로
        }

        private static void Grow(ParticleSystem ps, float from, float to)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, from, 1f, to));
        }

        // 알파가 줄면 셰이더가 가장자리부터 갉아먹음 → 처음엔 꽉 차 있다가 뒤로 갈수록 빠르게
        private static void FadeOut(ParticleSystem ps)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.35f), new GradientAlphaKey(0f, 1f) });
            color.color = g;
        }

        private static void AsMesh(ParticleSystem ps, Mesh mesh)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = mesh;
        }

        // ---------------------------------------------------------------- 메시 / 저장

        private static Mesh BuiltinMesh(PrimitiveType type)
        {
            var temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            return mesh;
        }

        // 납작한 고리 (반지름 1, 폭 0.45). UV.x = 둘레(3바퀴 반복), UV.y = 안쪽 0 → 바깥 1
        private static Mesh RingMesh()
        {
            const int segments = 48;
            const float inner = 0.55f, outer = 1f;
            var vertices = new Vector3[(segments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float a = t * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices[i * 2] = dir * inner;
                vertices[i * 2 + 1] = dir * outer;
                uvs[i * 2] = new Vector2(t * 3f, 0f);
                uvs[i * 2 + 1] = new Vector2(t * 3f, 1f);
                normals[i * 2] = normals[i * 2 + 1] = Vector3.up;
            }

            for (int i = 0; i < segments; i++)
            {
                int v = i * 2, k = i * 6;
                triangles[k] = v;
                triangles[k + 1] = v + 1;
                triangles[k + 2] = v + 2;
                triangles[k + 3] = v + 1;
                triangles[k + 4] = v + 3;
                triangles[k + 5] = v + 2;
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(RingMeshPath);
            bool isNew = mesh == null;
            if (isNew)
                mesh = new Mesh { name = "SM_StylizedRing" };
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            if (isNew)
                AssetDatabase.CreateAsset(mesh, RingMeshPath);
            else
                EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void Save(GameObject root, string name)
        {
            root.name = name;
            PrefabUtility.SaveAsPrefabAsset(root, $"{OutFolder}/{name}.prefab");
            Object.DestroyImmediate(root);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Members.HJH.EditorTools
{
    // 메뉴: HJH > Element FX > PBR 이펙트 만들기
    // 배경(PBR 다크 판타지 정글)에 맞는 사실적인 불·바람 이펙트.
    //  1) 텍스처 생성: 피어오르는 연기 시트(8x8 프레임, 밀도 + 노멀), 불꽃 시트(8x8, 열)
    //  2) 머티리얼: HJH/FX/LitSmoke(빛 받는 연기), HJH/FX/Fire(열 → 색), 파편(URP Lit)
    //  3) 프리팹 6개 (Resources/HJH/PbrFx): 불/바람 × 시전/폭발/피격
    //  4) 불·바람 스킬 에셋의 castFx / impactFx / hitFx 를 이걸로 교체, 납작한 기본 연출(impactStyle) 끔
    // 다시 실행하면 텍스처·프리팹을 덮어씀.
    public static partial class PbrFxBuilder
    {
        private const string Root = "Assets/Members/HJH/08.Effects/PBR";
        private const string OutFolder = "Assets/Members/HJH/05.Data/Resources/HJH/PbrFx";
        private const string SkillFolder = "Assets/Members/HJH/05.Data/Skill/Element";
        private const string SparkMaterialPath = "Assets/Members/HJH/08.Effects/Learn/Materials/M_FX_Spark.mat";
        private const string LeafMaterialPath = "Assets/Members/HJH/09.JungleWater/Generated/Materials/M_WaterSplash.mat";

        private const int Grid = 8;          // 8 x 8 = 64 프레임
        private const int FrameSize = 128;   // 한 프레임 128px → 1024px 텍스처

        private static Material _smoke, _dust, _fire, _spark, _leaf, _debris;
        private static Mesh _rock;

        [MenuItem("HJH/Element FX/PBR 이펙트 만들기")]
        public static void Build()
        {
            EnsureFolder(Root + "/Textures");
            EnsureFolder(Root + "/Materials");
            EnsureFolder(OutFolder);

            EditorUtility.DisplayProgressBar("PBR 이펙트", "연기 시트 생성", 0.1f);
            try
            {
                Texture2D smokeTex = SmokeFlipbook(out Texture2D smokeNormal);
                EditorUtility.DisplayProgressBar("PBR 이펙트", "불꽃 시트 생성", 0.5f);
                Texture2D fireTex = FireFlipbook();

                EditorUtility.DisplayProgressBar("PBR 이펙트", "머티리얼·프리팹", 0.8f);
                _smoke = Mat("M_FX_PbrSmoke", "HJH/FX/LitSmoke", m =>
                {
                    m.SetTexture("_MainTex", smokeTex);
                    m.SetTexture("_NormalTex", smokeNormal);
                    m.SetColor("_Color", new Color(0.32f, 0.3f, 0.28f, 1f));
                });
                _dust = Mat("M_FX_PbrDust", "HJH/FX/LitSmoke", m =>
                {
                    m.SetTexture("_MainTex", smokeTex);
                    m.SetTexture("_NormalTex", smokeNormal);
                    m.SetColor("_Color", new Color(0.68f, 0.62f, 0.5f, 1f));
                    m.SetFloat("_Density", 1.4f);
                    m.SetFloat("_SoftDistance", 0.25f);   // 바닥에 붙어 다니므로 짧게 (길면 지워짐)
                });
                _fire = Mat("M_FX_PbrFire", "HJH/FX/Fire", m =>
                {
                    m.SetTexture("_MainTex", fireTex);
                    m.SetColor("_CoreColor", new Color(1.4f, 0.9f, 0.4f));   // 하얗게 날아가지 않게
                });
                _debris = Mat("M_FX_PbrDebris", "Universal Render Pipeline/Lit", m =>
                {
                    m.SetColor("_BaseColor", new Color(0.24f, 0.2f, 0.16f, 1f));
                    m.SetFloat("_Smoothness", 0.15f);
                });
                _spark = Mat("M_FX_Streak", "HJH/FX/Streak", m => { });   // 불티·바람줄기 (늘어난 빌보드)
                _leaf = AssetDatabase.LoadAssetAtPath<Material>(LeafMaterialPath);
                _rock = RockMesh();
                PrepareKit();   // 입체 메시 이펙트 재료 (PbrFxBuilder.Skills.cs)

                var prefabs = new Dictionary<string, GameObject>
                {
                    ["FireCast"] = Save(FireCast(), "FX_Fire_PbrCast"),
                    ["FireBurst"] = Save(FireBurst(), "FX_Fire_PbrBurst"),
                    ["FireHit"] = Save(FireHit(), "FX_Fire_PbrHit"),
                    ["WindCast"] = Save(WindCast(), "FX_Wind_PbrCast"),
                    ["WindBurst"] = Save(WindBurst(), "FX_Wind_PbrBurst"),
                    ["WindHit"] = Save(WindHit(), "FX_Wind_PbrHit"),
                };

                int skills = AssignToSkills(prefabs);
                BuildSkillFx(prefabs);   // 스킬별 전용 이펙트 + 스킬 수치 (PbrFxBuilder.Skills.cs)
                AssetDatabase.SaveAssets();
                Debug.Log($"[ElementFx] PBR 이펙트 6개 생성 + 불·바람 스킬 {skills}개에 연결 완료 ({OutFolder})");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // =====================================================================
        // 텍스처 생성
        // =====================================================================

        // 피어오르는 연기: 시간이 갈수록 부풀고 옅어짐. 알파 = 밀도, 노멀 = 밀도의 경사
        private static Texture2D SmokeFlipbook(out Texture2D normal)
        {
            int size = Grid * FrameSize;
            var density = new float[size, size];
            for (int f = 0; f < Grid * Grid; f++)
            {
                float t = f / (float)(Grid * Grid - 1);
                int ox = (f % Grid) * FrameSize, oy = (Grid - 1 - f / Grid) * FrameSize;   // 위 왼쪽부터 (파티클 시트 순서)
                for (int y = 0; y < FrameSize; y++)
                for (int x = 0; x < FrameSize; x++)
                {
                    float u = (x + 0.5f) / FrameSize * 2f - 1f, v = (y + 0.5f) / FrameSize * 2f - 1f;
                    float wx = Fbm(u * 1.5f + 7f, v * 1.5f, t * 0.8f, 3) - 0.5f, wy = Fbm(u * 1.5f, v * 1.5f + 13f, t * 0.8f, 3) - 0.5f;   // 뭉게뭉게 찌그러진 외곽
                    float r = Mathf.Sqrt((u + wx * 0.6f) * (u + wx * 0.6f) + (v + wy * 0.6f) * (v + wy * 0.6f)) / (0.72f + 0.22f * t);
                    float shape = 1f - Smooth(0.45f, 1f, r);
                    float n = Fbm(u * 2.2f, v * 2.2f - t * 0.8f, t * 1.6f + 3f, 5);
                    float d = Mathf.Clamp01(Mathf.Clamp01((n - 0.3f - 0.12f * t) * 3.2f) * shape * 1.5f);
                    density[ox + x, oy + y] = d * (1f - 0.25f * t);
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var nrm = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var px = new Color[size * size];
            var npx = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = density[x, y];
                px[y * size + x] = new Color(1f, 1f, 1f, d);

                // 같은 프레임 안에서만 경사 계산 (옆 프레임 섞이지 않게)
                int fx = x % FrameSize, fy = y % FrameSize;
                float dl = fx > 0 ? density[x - 1, y] : d, dr = fx < FrameSize - 1 ? density[x + 1, y] : d;
                float dd = fy > 0 ? density[x, y - 1] : d, du = fy < FrameSize - 1 ? density[x, y + 1] : d;
                var n = new Vector3((dl - dr) * 3f, (dd - du) * 3f, 1f).normalized;
                npx[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
            tex.SetPixels(px);
            nrm.SetPixels(npx);

            normal = WriteTexture(nrm, "T_FX_SmokeNormal_8x8", srgb: false);
            return WriteTexture(tex, "T_FX_Smoke_8x8", srgb: false);
        }

        // 불꽃: 위로 흐르며 일렁이는 열. 초반에 크고 뜨겁다가 식으며 줄어듦. 알파 = 열
        private static Texture2D FireFlipbook()
        {
            int size = Grid * FrameSize;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var px = new Color[size * size];
            for (int f = 0; f < Grid * Grid; f++)
            {
                float t = f / (float)(Grid * Grid - 1);
                int ox = (f % Grid) * FrameSize, oy = (Grid - 1 - f / Grid) * FrameSize;
                for (int y = 0; y < FrameSize; y++)
                for (int x = 0; x < FrameSize; x++)
                {
                    float u = (x + 0.5f) / FrameSize * 2f - 1f, v = (y + 0.5f) / FrameSize * 2f - 1f;
                    // 아래가 넓고 위로 갈수록 좁아지는 불꽃 모양 + 시간이 지나면 위로 떠오르며 작아짐
                    float lift = v - t * 0.35f;
                    float width = 0.7f + 0.25f * Mathf.Clamp01(-lift);
                    float r = Mathf.Sqrt((u / width) * (u / width) + (lift / 0.9f) * (lift / 0.9f)) / (1f - 0.3f * t);
                    float shape = 1f - Smooth(0.35f, 1f, r);
                    float n = Fbm(u * 3f, v * 3f - t * 4f, t * 2f, 4);
                    float heat = Mathf.Clamp01(Mathf.Clamp01((n * 1.4f - 0.25f) * 2.4f) * shape * 1.3f) * (1f - 0.4f * t);
                    px[(oy + y) * size + ox + x] = new Color(1f, 1f, 1f, heat);
                }
            }
            tex.SetPixels(px);
            return WriteTexture(tex, "T_FX_Fire_8x8", srgb: false);
        }

        private static Texture2D WriteTexture(Texture2D tex, string name, bool srgb)
        {
            string path = $"{Root}/Textures/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---- 3D 값 노이즈 (연기·불꽃 모양용) ----
        private static float Hash(int x, int y, int z)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + z * 2147483647;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffffff) / (float)0xffffff;
            }
        }

        private static float ValueNoise(float x, float y, float z)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y), iz = Mathf.FloorToInt(z);
            float fx = x - ix, fy = y - iy, fz = z - iz;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            fz = fz * fz * (3f - 2f * fz);
            float Lerp3(int dz)
            {
                float a = Mathf.Lerp(Hash(ix, iy, iz + dz), Hash(ix + 1, iy, iz + dz), fx);
                float b = Mathf.Lerp(Hash(ix, iy + 1, iz + dz), Hash(ix + 1, iy + 1, iz + dz), fx);
                return Mathf.Lerp(a, b, fy);
            }
            return Mathf.Lerp(Lerp3(0), Lerp3(1), fz);
        }

        private static float Fbm(float x, float y, float z, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += ValueNoise(x, y, z) * amp;
                norm += amp;
                x = x * 2.03f + 11.7f;
                y = y * 2.03f + 5.3f;
                z = z * 1.7f;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        private static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        // =====================================================================
        // 불
        // =====================================================================

        private static GameObject FireCast()
        {
            var root = new GameObject("FX_Fire_PbrCast");
            Fireballs(root.transform, count: 2, size: (0.7f, 1.1f), life: (0.3f, 0.45f), speed: (0.5f, 1.5f), radius: 0.15f);
            Smoke(root.transform, _smoke, count: 2, size: (0.6f, 1f), life: (0.8f, 1.2f), color: new Color(1f, 1f, 1f, 0.6f), delay: 0.05f);
            Embers(root.transform, 8, (1.5f, 3.5f));
            return root;
        }

        // 폭발: 화염구 → 바위 파편 → 바닥 흙먼지 충격파 → 오래 남는 연기 + 불씨
        private static GameObject FireBurst()
        {
            var root = new GameObject("FX_Fire_PbrBurst");
            Fireballs(root.transform, count: 6, size: (1.6f, 2.8f), life: (0.55f, 0.85f), speed: (0.8f, 2.4f), radius: 0.5f);
            Debris(root.transform, count: 14, speed: (4.5f, 9f));
            DustRing(root.transform, count: 16, speed: (4f, 7f), size: (1.8f, 3f));
            Smoke(root.transform, _smoke, count: 9, size: (2.2f, 3.8f), life: (1.8f, 2.8f), color: new Color(1f, 1f, 1f, 0.9f), delay: 0.08f);
            Embers(root.transform, 30, (2.5f, 7f));
            Shell(root.transform, _heatShell, 4.6f, 0.45f);   // 입체: 열 껍질 + 불기둥 + 바닥 불
            Pillar(root.transform, "Flare", _flameMesh, new Vector3(1.4f, 3.4f, 1.4f), 0.55f, 0f);
            GroundDisc(root.transform, "Scorch", _flameGround, 4.2f, 1.2f, 0.3f);
            Sparks(root.transform, 24, (5f, 8f));
            return root;
        }

        private static GameObject FireHit()
        {
            var root = new GameObject("FX_Fire_PbrHit");
            Fireballs(root.transform, count: 3, size: (0.6f, 1.1f), life: (0.3f, 0.5f), speed: (0.8f, 2f), radius: 0.2f);
            Smoke(root.transform, _smoke, count: 2, size: (0.8f, 1.4f), life: (0.9f, 1.4f), color: new Color(1f, 1f, 1f, 0.7f), delay: 0.05f);
            Embers(root.transform, 10, (2f, 5f));
            Shell(root.transform, _heatShell, 1.6f, 0.3f);
            Sparks(root.transform, 14, (4f, 7f));
            return root;
        }

        // =====================================================================
        // 바람
        // =====================================================================

        private static GameObject WindCast()
        {
            var root = new GameObject("FX_Wind_PbrCast");
            DustRing(root.transform, count: 6, speed: (2.5f, 4f), size: (0.9f, 1.5f));
            Gusts(root.transform, count: 6, speed: (8f, 12f));
            Leaves(root.transform, count: 6, radius: 0.6f);
            return root;
        }

        // 바람 폭발: 바닥을 쓸고 나가는 흙먼지 충격파 + 돌며 날아가는 나뭇잎 + 빠른 바람줄기 + 소용돌이 먼지
        private static GameObject WindBurst()
        {
            var root = new GameObject("FX_Wind_PbrBurst");
            DustRing(root.transform, count: 22, speed: (5f, 9f), size: (1.8f, 3.2f));
            Swirl(root.transform);
            Leaves(root.transform, count: 24, radius: 1.2f);
            Gusts(root.transform, count: 16, speed: (12f, 18f));
            Debris(root.transform, count: 6, speed: (3f, 6f));
            Shell(root.transform, _windMesh, 4.6f, 0.5f);   // 입체: 바람 껍질이 부풀고 바닥 소용돌이
            GroundDisc(root.transform, "Swirl", _windGround, 4.4f, 0.8f, 0.3f);
            return root;
        }

        private static GameObject WindHit()
        {
            var root = new GameObject("FX_Wind_PbrHit");
            DustRing(root.transform, count: 5, speed: (2f, 3.5f), size: (0.9f, 1.5f));
            Leaves(root.transform, count: 8, radius: 0.4f);
            Gusts(root.transform, count: 6, speed: (8f, 12f));
            Shell(root.transform, _windMesh, 1.8f, 0.3f);
            return root;
        }

        // =====================================================================
        // 입자 층
        // =====================================================================

        private static void Fireballs(Transform parent, int count, (float, float) size, (float, float) life, (float, float) speed, float radius)
        {
            ParticleSystem ps = NewSystem(parent, "Fireballs", _fireVol);
            Main(ps, (life.Item1 * 1.5f, life.Item2 * 1.5f), speed, size, gravity: -0.35f);   // 시트가 불 → 그을음 연기까지 보여줌
            Random3DRotation(ps, true);
            Burst(ps, 0f, count);
            Shape(ps, ParticleSystemShapeType.Sphere, radius);
            Grow(ps, 0.6f, 1.25f);
            Fade(ps, 0f, 0.6f);
            Flipbook(ps);
        }

        private static void Smoke(Transform parent, Material mat, int count, (float, float) size, (float, float) life, Color color, float delay)
        {
            ParticleSystem ps = NewSystem(parent, "Smoke", mat);
            Main(ps, life, (0.4f, 1.4f), size, gravity: -0.12f);
            var main = ps.main;
            main.startColor = color;
            Random3DRotation(ps, true);
            Burst(ps, delay, count);
            Shape(ps, ParticleSystemShapeType.Sphere, 0.6f);
            Grow(ps, 0.55f, 1.6f);
            Fade(ps, 0.12f, 0.35f);
            Flipbook(ps);
            ps.GetComponent<ParticleSystemRenderer>().sortingFudge = 20f;   // 불 뒤에
        }

        // 바닥을 따라 바깥으로 퍼지는 흙먼지 고리 (충격파)
        private static void DustRing(Transform parent, int count, (float, float) speed, (float, float) size)
        {
            ParticleSystem ps = NewSystem(parent, "DustRing", _dust);
            Main(ps, (0.7f, 1.2f), speed, size, gravity: 0f);
            var main = ps.main;
            main.startColor = new Color(1f, 1f, 1f, 0.95f);
            Random3DRotation(ps, true);
            Burst(ps, 0f, count);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.4f;
            shape.radiusThickness = 0f;
            shape.position = new Vector3(0f, 0.5f, 0f);   // 바닥에서 띄워야 소프트 페이드에 안 지워짐
            shape.rotation = new Vector3(90f, 0f, 0f);
            ps.GetComponent<ParticleSystemRenderer>().sortingFudge = 30f;   // 흙먼지는 불·연기 뒤에
            var drag = ps.limitVelocityOverLifetime;   // 확 퍼졌다가 멈춤
            drag.enabled = true;
            drag.drag = 3f;
            drag.limit = 100f;
            drag.multiplyDragByParticleSize = false;
            drag.multiplyDragByParticleVelocity = false;
            Grow(ps, 0.6f, 1.5f);
            Fade(ps, 0.05f, 0.3f);
            Flipbook(ps);
        }

        // 바위 파편: 빛 받는 작은 돌이 튀었다 떨어짐 (묵직한 임팩트)
        private static void Debris(Transform parent, int count, (float, float) speed)
        {
            ParticleSystem ps = NewSystem(parent, "Debris", _debris);
            Main(ps, (0.7f, 1.2f), speed, (0.08f, 0.22f), gravity: 2.2f);
            Random3DRotation(ps, false);
            Burst(ps, 0f, count);
            Shape(ps, ParticleSystemShapeType.Cone, 0.3f, angle: 50f);
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
            rot.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
            rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            Fade(ps, 0f, 0.8f);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = _rock;
            r.shadowCastingMode = ShadowCastingMode.On;
        }

        private static void Embers(Transform parent, int count, (float, float) speed)
        {
            if (_spark == null)
                return;
            ParticleSystem ps = NewSystem(parent, "Embers", _spark);
            Main(ps, (0.8f, 1.6f), speed, (0.03f, 0.08f), gravity: -0.3f);
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.1f, 1f), new Color(1f, 0.75f, 0.35f, 1f));
            Burst(ps, 0f, count);
            Shape(ps, ParticleSystemShapeType.Cone, 0.4f, angle: 55f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 1.4f;
            noise.frequency = 0.9f;
            Fade(ps, 0f, 0.6f);
        }

        private static void Leaves(Transform parent, int count, float radius)
        {
            if (_leaf == null)
                return;
            ParticleSystem ps = NewSystem(parent, "Leaves", _leaf);
            Main(ps, (0.7f, 1.3f), (1.5f, 3.5f), (0.25f, 0.5f), gravity: 0.5f);
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.32f, 0.12f, 1f), new Color(0.36f, 0.28f, 0.14f, 1f));
            Burst(ps, 0f, count);
            Shape(ps, ParticleSystemShapeType.Circle, radius);
            var orbit = ps.velocityOverLifetime;
            orbit.enabled = true;
            orbit.space = ParticleSystemSimulationSpace.Local;
            orbit.orbitalY = new ParticleSystem.MinMaxCurve(4f, 8f);
            orbit.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            orbit.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
            orbit.radial = new ParticleSystem.MinMaxCurve(1f, 2f);
            orbit.y = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            orbit.x = new ParticleSystem.MinMaxCurve(0f, 0f);   // x·y·z 는 같은 모드여야 함
            orbit.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            Fade(ps, 0f, 0.7f);
        }

        // 빠른 바람줄기: 옅은 빛줄기가 바깥으로 휙
        private static void Gusts(Transform parent, int count, (float, float) speed)
        {
            if (_spark == null)
                return;
            ParticleSystem ps = NewSystem(parent, "Gusts", _spark);
            Main(ps, (0.12f, 0.22f), speed, (0.05f, 0.09f), gravity: 0f);
            var main = ps.main;
            main.startColor = new Color(0.75f, 0.95f, 0.88f, 0.55f);
            Burst(ps, 0f, count);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            Fade(ps, 0f, 0.4f);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 5f;
            r.velocityScale = 0.05f;
        }

        // 소용돌이 먼지 기둥
        private static void Swirl(Transform parent)
        {
            ParticleSystem ps = NewSystem(parent, "Swirl", _dust);
            Main(ps, (0.9f, 1.4f), (0.5f, 1.2f), (0.9f, 1.6f), gravity: -0.4f);
            var main = ps.main;
            main.startColor = new Color(1f, 1f, 1f, 0.8f);
            Random3DRotation(ps, true);
            Burst(ps, 0.03f, 10);
            Shape(ps, ParticleSystemShapeType.Circle, 0.9f);
            var orbit = ps.velocityOverLifetime;
            orbit.enabled = true;
            orbit.space = ParticleSystemSimulationSpace.Local;
            orbit.orbitalY = new ParticleSystem.MinMaxCurve(3f, 5f);
            orbit.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            orbit.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
            orbit.y = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            orbit.x = new ParticleSystem.MinMaxCurve(0f, 0f);   // x·y·z 는 같은 모드여야 함
            orbit.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            Grow(ps, 0.6f, 1.4f);
            Fade(ps, 0.08f, 0.35f);
            Flipbook(ps);
        }

        // =====================================================================
        // 파티클 도우미
        // =====================================================================

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

        private static void Random3DRotation(ParticleSystem ps, bool billboard)
        {
            var main = ps.main;
            if (billboard)
            {
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                var rot = ps.rotationOverLifetime;   // 연기는 천천히 돌며 피어오름
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            }
            else
            {
                main.startRotation3D = true;
                main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            }
        }

        private static void Burst(ParticleSystem ps, float time, int count)
        {
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(time, (short)count) });
        }

        private static void Shape(ParticleSystem ps, ParticleSystemShapeType type, float radius, float angle = 25f)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = type;
            shape.radius = radius;
            shape.angle = angle;
            if (type == ParticleSystemShapeType.Cone)
                shape.rotation = new Vector3(-90f, 0f, 0f);
            else if (type == ParticleSystemShapeType.Circle)
                shape.rotation = new Vector3(90f, 0f, 0f);
        }

        private static void Grow(ParticleSystem ps, float from, float to)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, from, 1f, to));
        }

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

        // 8x8 시트를 수명 동안 한 번 재생 + 프레임 섞기용 버텍스 스트림
        private static void Flipbook(ParticleSystem ps)
        {
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = Grid;
            sheet.numTilesY = Grid;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            sheet.cycleCount = 1;

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Normal,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.UV2,
                ParticleSystemVertexStream.AnimBlend,
            });
        }

        // =====================================================================
        // 스킬 연결
        // =====================================================================

        // 불·바람 스킬의 시전/폭발/피격 이펙트를 PBR 프리팹으로, 납작한 기본 연출(impactStyle)은 끔
        private static int AssignToSkills(Dictionary<string, GameObject> prefabs)
        {
            int count = 0;
            foreach (string element in new[] { "Fire", "Wind" })
            {
                foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { $"{SkillFolder}/{element}" }))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                    if (asset == null)
                        continue;
                    var so = new SerializedObject(asset);
                    bool changed = false;
                    changed |= SetSlot(so, "castFx", prefabs[element + "Cast"]);
                    changed |= SetSlot(so, "impactFx", prefabs[element + "Burst"], onlyIfSet: true);
                    changed |= SetSlot(so, "hitFx", prefabs[element + "Hit"]);
                    SerializedProperty style = so.FindProperty("impactStyle");
                    if (style != null && style.enumValueIndex != 0)
                    {
                        style.enumValueIndex = 0;
                        changed = true;
                    }
                    if (changed)
                    {
                        so.ApplyModifiedPropertiesWithoutUndo();
                        count++;
                    }
                }
            }
            return count;
        }

        private static bool SetSlot(SerializedObject so, string field, GameObject prefab, bool onlyIfSet = false)
        {
            SerializedProperty slot = so.FindProperty(field);
            if (slot == null || prefab == null)
                return false;
            SerializedProperty p = slot.FindPropertyRelative("prefab");
            SerializedProperty tint = slot.FindPropertyRelative("tint");
            if (p == null || (onlyIfSet && p.objectReferenceValue == null))
                return false;
            p.objectReferenceValue = prefab;
            if (tint != null)
                { if (tint.propertyType == SerializedPropertyType.Boolean) tint.boolValue = false; else if (tint.propertyType == SerializedPropertyType.Float) tint.floatValue = 0f; else tint.intValue = 0; }
            return true;
        }

        // =====================================================================
        // 에셋 도우미
        // =====================================================================

        private static Material Mat(string name, string shaderName, System.Action<Material> setup)
        {
            string path = $"{Root}/Materials/{name}.mat";
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[ElementFx] 셰이더를 못 찾음: {shaderName}");
                return null;
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }
            setup(mat);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // 각진 작은 돌 (정이십면체를 찌그러뜨림)
        private static Mesh RockMesh()
        {
            string path = $"{Root}/Materials/SM_FX_Rock.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
                return mesh;

            float p = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3>
            {
                new(-1, p, 0), new(1, p, 0), new(-1, -p, 0), new(1, -p, 0),
                new(0, -1, p), new(0, 1, p), new(0, -1, -p), new(0, 1, -p),
                new(p, 0, -1), new(p, 0, 1), new(-p, 0, -1), new(-p, 0, 1),
            };
            int[] tris =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var rng = new System.Random(5);
            var verts = new List<Vector3>();
            var faces = new List<int>();
            for (int i = 0; i < tris.Length; i++)   // 면마다 꼭짓점을 따로 둬서 각진 그림자
            {
                Vector3 c = v[tris[i]].normalized * 0.5f;
                c.Scale(new Vector3(1f, 0.7f, 0.85f));
                c *= 0.85f + (float)rng.NextDouble() * 0.3f;
                verts.Add(c);
                faces.Add(i);
            }
            mesh = new Mesh { name = "SM_FX_Rock" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(faces, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static GameObject Save(GameObject root, string name)
        {
            root.name = name;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{OutFolder}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
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

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HJH.EditorTools
{
    // 바닥을 Shape of Dreams 처럼: 칠한 듯한 밝은/어두운 얼룩 패치 + 낙엽·잔가지 + 나뭇가지 그림자(태양 쿠키). 메뉴: HJH/맵/바닥/...
    //  1) 질감 만들기: 타일되는 텍스처 3장(어두운 바탕, 밝은 패치, 낙엽·잔가지)을 코드로 생성 → 터레인 레이어 3개 추가 → 플레이 영역에 패치 얼룩을 칠한다.
    //  2) 나뭇가지 그림자: 가지·잎 무늬 쿠키를 태양에 걸어 바닥과 프랍 위에 어른거리는 그림자를 준다 (끄기 메뉴 있음).
    public static class MapFloorStyle
    {
        private const string Dir = "Assets/Members/HJH/05.Data/Map/Floor/";

        // ───────────────────────── 타일 가능한 텍스처 생성 ─────────────────────────
        private static float Periodic(float x, float y, float s, float seed)
        {
            // 4점 블렌드로 이어 붙여도 티 안 나는 노이즈
            float a = Mathf.PerlinNoise(x + seed, y + seed);
            float b = Mathf.PerlinNoise(x - s + seed, y + seed);
            float c = Mathf.PerlinNoise(x + seed, y - s + seed);
            float d = Mathf.PerlinNoise(x - s + seed, y - s + seed);
            return ((s - x) * (s - y) * a + x * (s - y) * b + (s - x) * y * c + x * y * d) / (s * s);
        }

        private static void PutWrap(Color[] px, int N, int x, int y, Color col, float a)
        {
            x = ((x % N) + N) % N; y = ((y % N) + N) % N;
            px[y * N + x] = Color.Lerp(px[y * N + x], col, a);
        }

        private static void Ellipse(Color[] px, int N, Vector2 c, float len, float wid, float ang, Color col)
        {
            float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
            int r = Mathf.CeilToInt(len);
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    float u = (dx * ca + dy * sa) / len, v = (-dx * sa + dy * ca) / wid;
                    float d = u * u + v * v;
                    if (d <= 1f) PutWrap(px, N, Mathf.RoundToInt(c.x) + dx, Mathf.RoundToInt(c.y) + dy, col, Mathf.Clamp01((1f - d) * 3f));
                }
        }

        private static void Line(Color[] px, int N, Vector2 a, Vector2 b, float thick, Color col)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b) * 1.5f);
            for (int i = 0; i <= steps; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)Mathf.Max(steps, 1));
                int r = Mathf.CeilToInt(thick);
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                        if (dx * dx + dy * dy <= thick * thick) PutWrap(px, N, Mathf.RoundToInt(p.x) + dx, Mathf.RoundToInt(p.y) + dy, col, 0.85f);
            }
        }

        private static Texture2D Make(string name, int N, Color baseCol, Color varCol, bool litter, int seed)
        {
            string path = Dir + name + ".png";
            var px = new Color[N * N];
            float s = 8f; // 큰 얼룩 주기
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float fx = x / (float)N * s, fy = y / (float)N * s;
                    float n = Periodic(fx, fy, s, seed) * 0.6f + Periodic(fx * 3f % s, fy * 3f % s, s, seed + 9f) * 0.4f;
                    px[y * N + x] = Color.Lerp(baseCol, varCol, Mathf.SmoothStep(0.35f, 0.7f, n));
                }
            if (litter)
            {
                var rng = new System.Random(seed);
                float R() => (float)rng.NextDouble();
                Color[] leafCols = { new Color(0.30f, 0.36f, 0.22f), new Color(0.38f, 0.40f, 0.24f), new Color(0.22f, 0.32f, 0.30f), new Color(0.34f, 0.26f, 0.18f) };
                for (int i = 0; i < 110; i++)
                    Ellipse(px, N, new Vector2(R() * N, R() * N), Mathf.Lerp(14f, 34f, R()), Mathf.Lerp(5f, 11f, R()), R() * 6.28f, leafCols[rng.Next(leafCols.Length)] * Mathf.Lerp(0.8f, 1.15f, R()));
                var twig = new Color(0.20f, 0.16f, 0.14f);
                for (int i = 0; i < 26; i++)
                {
                    var p = new Vector2(R() * N, R() * N); float ang = R() * 6.28f;
                    for (int k = 0; k < 5; k++)     // 살짝 꺾이는 잔가지
                    {
                        ang += (R() - 0.5f) * 0.7f; var q = p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Mathf.Lerp(20f, 40f, R());
                        Line(px, N, p, q, Mathf.Lerp(1.2f, 2.4f, R()), twig); p = q;
                    }
                }
            }
            for (int i = 0; i < px.Length; i++) px[i].a = 1f;   // 알파가 곧 스무스니스라 항상 1 로
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true);
            tex.SetPixels(px); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.wrapMode = TextureWrapMode.Repeat; ti.mipmapEnabled = true; ti.sRGBTexture = true; ti.maxTextureSize = 1024;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static TerrainLayer Layer(string name, Texture2D tex, float tile, float smooth)
        {
            string path = Dir + name + ".terrainlayer";
            var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (l == null) { l = new TerrainLayer(); AssetDatabase.CreateAsset(l, path); }
            l.diffuseTexture = tex; l.tileSize = new Vector2(tile, tile); l.smoothness = smooth; l.metallic = 0f;
            EditorUtility.SetDirty(l);
            return l;
        }

        private static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - a) / (b - a)));

        // ───────────────────────── 1. 바닥 질감 ─────────────────────────
        [MenuItem("HJH/맵/바닥/1 바닥 질감 만들기 (얼룩 패치 + 낙엽·잔가지)")]
        private static void PaintFloor()
        {
            if (EditorApplication.isPlaying) return;
            var terrain = Terrain.activeTerrain;
            var playDist = StonePackImporter.PlayDistFn();
            if (terrain == null || playDist == null) { Debug.LogWarning("[Floor] Terrain 이 없거나 지원하지 않는 씬입니다 (Map_L_Normal / Map_Bridge_Normal)."); return; }
            Directory.CreateDirectory(Dir);

            var tBase = Make("T_Floor_Base", 512, new Color(0.13f, 0.19f, 0.21f), new Color(0.17f, 0.24f, 0.25f), false, 11);
            var tLight = Make("T_Floor_Light", 512, new Color(0.24f, 0.33f, 0.34f), new Color(0.31f, 0.41f, 0.40f), false, 23);
            var tLitter = Make("T_Floor_Litter", 1024, new Color(0.14f, 0.20f, 0.20f), new Color(0.18f, 0.24f, 0.23f), true, 37);
            var lBase = Layer("TL_Floor_Base", tBase, 8f, 0.05f);
            var lLight = Layer("TL_Floor_Light", tLight, 8f, 0.06f);
            var lLitter = Layer("TL_Floor_Litter", tLitter, 6f, 0.08f);

            var data = terrain.terrainData;
            Undo.RegisterCompleteObjectUndo(data, "Floor style");
            var layers = new System.Collections.Generic.List<TerrainLayer>(data.terrainLayers);
            bool first = !layers.Contains(lBase);
            foreach (var l in new[] { lBase, lLight, lLitter }) if (!layers.Contains(l)) layers.Add(l);
            data.terrainLayers = layers.ToArray();
            int iBase = layers.IndexOf(lBase), iLight = layers.IndexOf(lLight), iLitter = layers.IndexOf(lLitter);

            int res = data.alphamapResolution, nl = data.alphamapLayers;
            var map = data.GetAlphamaps(0, 0, res, res);
            var origin = terrain.transform.position; var size = data.size;
            int painted = 0;
            for (int zi = 0; zi < res; zi++)
            {
                if (zi % 64 == 0) EditorUtility.DisplayProgressBar("바닥 칠하기", zi + "/" + res, zi / (float)res);
                for (int xi = 0; xi < res; xi++)
                {
                    var p = new Vector2(origin.x + (xi + 0.5f) / res * size.x, origin.z + (zi + 0.5f) / res * size.z);
                    if (playDist(p) > 0.01f) continue;
                    // 칠한 듯한 얼룩: 저주파 노이즈 + 워프, 경계는 좁게 끊어서 붓 자국처럼
                    float warp = Mathf.PerlinNoise(p.x * 0.11f + 5f, p.y * 0.11f + 9f) * 3f;
                    float n = Mathf.PerlinNoise(p.x * 0.045f + warp * 0.2f + 20f, p.y * 0.045f + 40f) + 0.22f * Mathf.PerlinNoise(p.x * 0.17f, p.y * 0.17f + 3f);
                    float light = Smooth(0.60f, 0.64f, n);
                    float n2 = Mathf.PerlinNoise(p.x * 0.07f + 70f, p.y * 0.07f + 11f) + 0.2f * Mathf.PerlinNoise(p.x * 0.3f, p.y * 0.3f);
                    float litter = Smooth(0.62f, 0.67f, n2);
                    if (first) map[zi, xi, 0] *= 0.25f;                                  // 기존 이끼는 줄이고 새 바닥으로 대체 (처음 한 번만)
                    float b = (1f - light) * (1f - litter) * 0.95f;
                    map[zi, xi, iBase] = b; map[zi, xi, iLight] = light * (1f - litter) * 0.95f; map[zi, xi, iLitter] = litter * 0.95f;
                    float sum = 0f; for (int l = 0; l < nl; l++) sum += map[zi, xi, l];
                    if (sum > 0f) for (int l = 0; l < nl; l++) map[zi, xi, l] /= sum;
                    painted++;
                }
            }
            EditorUtility.ClearProgressBar();
            data.SetAlphamaps(0, 0, map);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Floor] 바닥 질감 적용: {painted}칸 (어두운 바탕 + 밝은 패치 + 낙엽·잔가지 패치). 얼룩 크기는 코드의 0.045/0.07(낮을수록 큼), 경계 선명도는 Smooth 폭으로 조정. 다시 실행해도 같은 결과.");
        }

        // ───────────────────────── 2. 나뭇가지 그림자 쿠키 ─────────────────────────
        [MenuItem("HJH/맵/바닥/2 나뭇가지 그림자 켜기 (태양 쿠키)")]
        private static void CookieOn()
        {
            if (EditorApplication.isPlaying) return;
            Directory.CreateDirectory(Dir);
            string path = Dir + "T_BranchCookie.png";
            if (!File.Exists(path)) MakeCookie(path);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var sun = RenderSettings.sun;
            if (sun == null) foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null || tex == null) { Debug.LogWarning("[Floor] 태양(Directional Light)을 못 찾았습니다."); return; }
            Undo.RecordObject(sun, "Cookie");
            sun.cookie = tex; sun.cookieSize2D = new Vector2(28f, 28f);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Floor] 태양에 가지 그림자 쿠키를 걸었습니다 (크기 28m 반복). 너무 진하면 MakeCookie 의 어둡기 값을 올리고 cookieSize2D 로 무늬 크기를 조절하세요.");
        }

        [MenuItem("HJH/맵/바닥/나뭇가지 그림자 끄기")]
        private static void CookieOff()
        {
            var sun = RenderSettings.sun;
            if (sun == null) return;
            Undo.RecordObject(sun, "Cookie"); sun.cookie = null;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static void MakeCookie(string path)
        {
            const int N = 1024;
            var px = new Color[N * N];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            var rng = new System.Random(77);
            float R() => (float)rng.NextDouble();
            Color shade = new Color(0.30f, 0.30f, 0.30f);   // 가지·잎 그림자 어둡기 (0=완전 검정, 1=없음)

            void Branch(Vector2 p, float ang, float len, float thick, int depth)
            {
                int segs = Mathf.Max(3, (int)(len / 14f));
                for (int i = 0; i < segs; i++)
                {
                    ang += (R() - 0.5f) * 0.35f;
                    var q = p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (len / segs);
                    Line(px, N, p, q, Mathf.Max(0.8f, thick * (1f - i / (float)segs * 0.6f)), shade);
                    if (depth > 0 && R() < 0.28f) Branch(q, ang + (R() < 0.5f ? -1f : 1f) * Mathf.Lerp(0.5f, 1.0f, R()), len * 0.45f, thick * 0.6f, depth - 1);
                    if (R() < 0.18f) Ellipse(px, N, q + new Vector2((R() - 0.5f) * 14f, (R() - 0.5f) * 14f), Mathf.Lerp(8f, 18f, R()), Mathf.Lerp(4f, 8f, R()), R() * 6.28f, shade);   // 잎
                    p = q;
                }
            }
            for (int i = 0; i < 16; i++) Branch(new Vector2(R() * N, R() * N), R() * 6.28f, Mathf.Lerp(160f, 320f, R()), Mathf.Lerp(3.5f, 6f, R()), 2);
            for (int i = 0; i < 40; i++) Ellipse(px, N, new Vector2(R() * N, R() * N), Mathf.Lerp(10f, 22f, R()), Mathf.Lerp(5f, 10f, R()), R() * 6.28f, shade); // 흩어진 잎 그림자

            // 가장자리를 부드럽게 (3x3 박스 블러 1회)
            var outPx = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float sum = 0f;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) sum += px[(((y + dy) % N + N) % N) * N + ((x + dx) % N + N) % N].r;
                    float v = sum / 9f; outPx[y * N + x] = new Color(v, v, v, v);
                }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            tex.SetPixels(outPx); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Cookie; ti.wrapMode = TextureWrapMode.Repeat; ti.alphaSource = TextureImporterAlphaSource.FromGrayScale; ti.mipmapEnabled = true;
            ti.SaveAndReimport();
        }
    }
}

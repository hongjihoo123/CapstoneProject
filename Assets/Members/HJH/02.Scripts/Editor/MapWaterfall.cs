using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HJH.EditorTools
{
    // 씬에 직접 만들어 둔 '폭포' 오브젝트(자식 Cube = 폭포 면)를 폭포로 만든다. 위치·크기·각도는 Cube 를 손으로 조절한 그대로.
    //   면(Cube)  : 흐르는 물 재질 + UV 스크롤(ScrollUV, 에디터에서도 움직임), 콜라이더 제거
    //   Drops     : 윗변에서 면을 따라 떨어지는 물방울 줄기 (늘어난 빌보드 파티클)
    //   Mist/Spray: 수면에 닿는 곳의 물안개(연기) 두 겹
    //   Splash    : 튀어 오르는 물보라,  Foam: 수면에 퍼지는 포말(수평 빌보드)
    //   Ripples   : 수면에 물결 (JungleWater 물결 시뮬레이션, 플레이 중)
    //   WF_Light  : 청록 점광원
    // 다시 돌리면 이전에 만든 WF_* 만 지우고 새로 만듦 (Cube 는 그대로). 수면 높이는 Map_Bridge_Normal 이면 그 맵의 물 높이, 아니면 Cube 아랫면.
    public static class MapWaterfall
    {
        private const string Dir = "Assets/Members/HJH/05.Data/Map/Waterfall/";
        private const string RootName = "폭포";

        [MenuItem("HJH/맵/폭포/'폭포' 오브젝트를 폭포로 만들기 (Cube 면 + 흐르는 UV + 파티클)")]
        private static void Build()
        {
            if (EditorApplication.isPlaying) return;
            MapPropTidy.BackupActiveScene();   // 씬을 바꾸기 전에 _RemovedBackup/<날짜>/ 로 복사
            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                Debug.LogWarning("[Waterfall] 씬에서 '폭포' 라는 이름의 오브젝트를 찾지 못했습니다. 그 밑에 폭포 면으로 쓸 Cube 를 넣어 두세요.");
                return;
            }

            // 예전에 내가 만든 절차형 폭포는 정리, 이번에 만든 WF_ 결과도 지우고 새로
            GameObject oldProc = GameObject.Find("Waterfall");
            if (oldProc != null) Undo.DestroyObjectImmediate(oldProc);
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Transform c = root.transform.GetChild(i);
                if (c.name.StartsWith("WF_")) Undo.DestroyObjectImmediate(c.gameObject);
            }

            var panels = new List<MeshRenderer>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                if (!r.name.StartsWith("WF_") && !IsInsideWF(r.transform, root.transform)) panels.Add(r);
            if (panels.Count == 0) { Debug.LogWarning("[Waterfall] '폭포' 밑에 Cube(MeshRenderer)가 없습니다."); return; }

            float waterY = float.NaN;
            Vector3? pool = null;
            if (EditorSceneManager.GetActiveScene().name == "Map_Bridge_Normal")
            {
                MapSpecBuilder.UseBridgeSpec();
                waterY = MapSpecBuilder.SpecWaterY;
                Vector2 cen = Vector2.zero;
                Vector2[] w = MapSpecBuilder.SpecWater();
                foreach (var v in w) cen += v;
                cen /= w.Length;
                pool = new Vector3(cen.x, waterY, cen.y);   // 면이 어느 쪽을 향해 흐를지 정하는 기준 (웅덩이 중심 쪽이 앞)
            }

            Directory.CreateDirectory(Dir);
            AssetDatabase.Refresh();
            Texture2D streak = MakeTexture(Dir + "T_WaterfallStreak.png", 64, 256, TextureWrapMode.Repeat, StreakPixel);
            Texture2D puff = MakeTexture(Dir + "T_MistPuff.png", 64, 64, TextureWrapMode.Clamp, PuffPixel);
            Material sheetMat = MakeMat(Dir + "M_WaterfallSheet.mat", streak, new Color(0.8f, 0.94f, 1f, 0.92f), false);
            Material mistMat = MakeMat(Dir + "M_WaterfallMist.mat", puff, new Color(0.85f, 0.95f, 1f, 0.5f), false);
            Material dropMat = MakeMat(Dir + "M_WaterfallDrop.mat", puff, new Color(0.9f, 0.97f, 1f, 0.8f), true);
            Material splashMat = MakeMat(Dir + "M_WaterfallSplash.mat", puff, new Color(1f, 1f, 1f, 0.9f), true);
            SheetMat2 = MakeMat(Dir + "M_WaterfallSheet2.mat", streak, new Color(1f, 1f, 1f, 0.45f), false);
            Stones = LoadStones("Stone_Big"); StonesSmall = LoadStones("Stone_Mid");
            PanelBounds = new List<Bounds>(); foreach (var p in panels) PanelBounds.Add(p.bounds);

            int idx = 0, built = 0;
            Vector3 baseSum = Vector3.zero, normalSum = Vector3.zero; float widthSum = 0f, topY = 0f;
            foreach (var mr in panels)
            {
                if (BuildPanel(root.transform, mr, idx++, waterY, pool, sheetMat, mistMat, dropMat, splashMat, out Vector3 basePos, out float width))
                {
                    built++; baseSum += basePos; widthSum += width; topY = Mathf.Max(topY, mr.bounds.max.y);
                    normalSum += root.transform.Find($"WF_Panel_{idx - 1}").forward;
                }
            }
            if (built == 0) { Debug.LogWarning("[Waterfall] 쓸 수 있는 면이 없습니다 (면은 세로로 선 얇은 Cube 여야 함)."); return; }

            // 조명: 물이 닿는 곳(밝게) + 폭포 중간 높이 앞쪽(물줄기와 암벽을 비춤)
            Vector3 mid = baseSum / built, front = normalSum.normalized;
            AddLight(root.transform, "WF_Light", mid + Vector3.up * 1.6f + front * 1.5f, Mathf.Max(9f, widthSum / built * 1.5f), 7f);
            AddLight(root.transform, "WF_LightHigh", mid + Vector3.up * (topY - mid.y) * 0.5f + front * 5f, Mathf.Max(14f, (topY - mid.y) * 0.9f), 5f);

            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Waterfall] '폭포' 면 {built}개를 폭포로 만들었습니다. 물이 위로 흐르면 면의 ScrollUV Speed.y 부호를 뒤집으세요. 파티클은 폭포를 선택하면 씬 뷰에서 미리 보입니다.");
        }

        private static bool IsInsideWF(Transform t, Transform stop)
        {
            for (; t != null && t != stop; t = t.parent)
                if (t.name.StartsWith("WF_")) return true;
            return false;
        }

        // ───────────────────────── 면 하나 ─────────────────────────
        private static bool BuildPanel(Transform root, MeshRenderer mr, int idx, float waterY, Vector3? pool,
            Material sheetMat, Material mistMat, Material dropMat, Material splashMat, out Vector3 basePos, out float width)
        {
            basePos = Vector3.zero; width = 0f;
            Transform t = mr.transform;
            Vector3 s = t.lossyScale;
            if (s.y < Mathf.Min(s.x, s.z)) { Debug.LogWarning($"[Waterfall] {t.name}: 세로로 선 얇은 면이 아니라 건너뜁니다."); return false; }

            bool thinX = s.x <= s.z;                       // 얇은 축 = 면의 법선
            Vector3 normal = thinX ? t.right : t.forward; normal.y = 0f; normal.Normalize();
            width = thinX ? s.z : s.x;
            float height = s.y, thick = thinX ? s.x : s.z;
            Bounds b = mr.bounds;

            Vector3 toPool = pool.HasValue ? new Vector3(pool.Value.x - b.center.x, 0f, pool.Value.z - b.center.z) : normal;
            if (Vector3.Dot(normal, toPool) < 0f) normal = -normal;   // 앞 = 웅덩이 쪽

            // Cube 는 위치·크기 안내용으로만 남긴다 (상자 모양이 그대로 보여서 렌더러는 끔). 실제 물줄기는 아래 WF_Sheet 가 그림
            mr.sharedMaterial = sheetMat;
            mr.enabled = false;
            foreach (var c in mr.GetComponents<Collider>()) Undo.DestroyObjectImmediate(c);
            var oldScroll = mr.GetComponent<Assets.Members.HJH.Environment.ScrollUV>();
            if (oldScroll != null) oldScroll.enabled = false;

            // 파티클 묶음: 수면에 닿는 바닥 중앙에, 앞(웅덩이 쪽)을 향하게
            float baseY = (float.IsNaN(waterY) ? b.min.y : Mathf.Max(b.min.y, waterY)) + 0.04f;
            Vector3 front = normal * (thick * 0.5f + 0.12f);
            basePos = new Vector3(b.center.x, baseY, b.center.z) + front;
            Quaternion face = Quaternion.LookRotation(normal, Vector3.up);   // 로컬 Z = 앞, X = 폭 방향

            var g = new GameObject($"WF_Panel_{idx}");
            g.transform.SetParent(root, true);
            g.transform.SetPositionAndRotation(basePos, face);

            float fall = Mathf.Max(0.8f, b.max.y - baseY);
            Vector3 topPos = new Vector3(b.center.x, b.max.y - 0.15f, b.center.z) + front;

            // 0) 물줄기 면 두 겹 (가장자리·윗변이 부드럽게 사라지고, 아래로 갈수록 앞으로 휘어 떨어짐) + 뒤 암벽 + 발치 바위
            BuildSheet(g.transform, idx, 0, width, fall, 0f, 1f, sheetMat, new Vector2(Mathf.Max(1f, width / 1.1f), Mathf.Max(1f, fall / 2f)), 1.6f);
            BuildSheet(g.transform, idx, 1, width * 0.96f, fall, 0.3f, 0.8f, SheetMat2, new Vector2(Mathf.Max(1f, width / 2.4f), Mathf.Max(1f, fall / 3.6f)), 2.3f);
            BuildRocks(g.transform, idx, width, fall, thick, b);
            // 윗변: 물이 넘어오는 턱의 흰 물보라
            {
                var ps = NewPS("WF_Crest", g.transform, topPos + Vector3.down * 0.2f, face);
                var main = ps.main;
                main.loop = true; main.duration = 3f; main.maxParticles = 200; main.prewarm = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
                main.gravityModifier = 0.5f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(0.95f, 0.99f, 1f, 0.5f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = ps.emission; em.rateOverTime = Mathf.Clamp(width * 6f, 12f, 90f);
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(width * 0.95f, 0.3f, 0.5f);
                FadeInOut(ps);
                Finish(ps, mistMat);
            }
            // 물줄기를 따라 같이 떨어지는 굵은 물덩이 + 옅은 물안개 장막 (면이 평평해 보이지 않게 부피를 줌)
            {
                var ps = NewPS("WF_Chunks", g.transform, topPos, face);
                var main = ps.main;
                main.loop = true; main.duration = 4f; main.maxParticles = 400; main.prewarm = true;
                main.startLifetime = Mathf.Sqrt(2f * fall / 9.81f) * 1.05f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
                main.gravityModifier = 1f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
                main.startColor = new Color(0.9f, 0.97f, 1f, 0.35f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = ps.emission; em.rateOverTime = Mathf.Clamp(width * 9f, 15f, 120f);
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(width * 0.9f, 0.05f, 0.3f);
                var r = ps.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.09f; r.lengthScale = 2.2f;
                Finish(ps, mistMat);
            }
            {
                var ps = NewPS("WF_Veil", g.transform, basePos + Vector3.up * fall * 0.5f + normal * 0.6f, face);
                var main = ps.main;
                main.loop = true; main.duration = 5f; main.maxParticles = 120; main.prewarm = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(3f, 5.5f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(0.85f, 0.95f, 1f, 0.1f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = ps.emission; em.rateOverTime = Mathf.Clamp(width * 2.2f, 6f, 30f);
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(width * 0.9f, fall * 0.8f, 0.6f);
                var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
                vel.x = 0f; vel.y = -1.6f; vel.z = 0f;
                FadeInOut(ps);
                Finish(ps, mistMat);
            }

            // 1) 떨어지는 물방울 줄기
            {
                var ps = NewPS("WF_Drops", g.transform, topPos, face);
                var main = ps.main;
                main.loop = true; main.duration = 4f; main.maxParticles = 700;
                main.startLifetime = Mathf.Sqrt(2f * fall / 9.81f) * 1.08f;
                main.startSpeed = 0.5f;
                main.gravityModifier = 1f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.13f);
                main.startColor = new Color(0.92f, 0.98f, 1f, 0.6f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = ps.emission; em.rateOverTime = Mathf.Clamp(width * 40f, 30f, 260f);
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(width * 0.95f, 0.04f, 0.12f);
                var r = ps.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.06f; r.lengthScale = 3f;
                Finish(ps, dropMat);
            }
            // 2) 물안개(연기) 두 겹: 낮고 진한 것 + 높고 옅은 것
            Mist("WF_Mist", g.transform, basePos + Vector3.up * 0.1f, face, width, 3.5f, new Vector2(2.6f, 4.6f), 0.36f, 0.35f, mistMat);
            Mist("WF_Spray", g.transform, basePos + Vector3.up * 0.5f, face, width, 1.6f, new Vector2(3.5f, 6f), 0.18f, 0.6f, mistMat);
            // 수면을 타고 웅덩이 쪽으로 깔려 나가는 물안개 (게임 카메라에서 폭포가 가까이 있다는 느낌)
            Mist("WF_MistRoll", g.transform, basePos + Vector3.up * 0.3f + normal * 1.5f, face, width, 1.8f, new Vector2(4f, 7f), 0.16f, 0.05f, mistMat, normal * 0.9f, 7f);
            // 3) 튀어 오르는 물보라
            {
                var ps = NewPS("WF_Splash", g.transform, basePos, face);
                var main = ps.main;
                main.loop = true; main.duration = 3f; main.maxParticles = 500;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
                main.gravityModifier = 1.4f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.28f);
                main.startColor = new Color(1f, 1f, 1f, 0.85f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = ps.emission; em.rateOverTime = Mathf.Clamp(width * 28f, 30f, 260f);
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(width * 0.9f, 0.05f, 0.35f); sh.rotation = new Vector3(-90f, 0f, 0f);   // 위로
                var size = ps.sizeOverLifetime; size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f)));
                Finish(ps, splashMat);
            }
            // 4) 수면에 퍼지는 포말 (수평 빌보드)
            {
                var ps = NewPS("WF_Foam", g.transform, basePos + normal * 1.1f, face);
                var main = ps.main;
                main.loop = true; main.duration = 4f; main.maxParticles = 160; main.prewarm = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(0.92f, 0.98f, 1f, 0.7f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = ps.emission; em.rateOverTime = Mathf.Clamp(width * 5f, 8f, 70f);
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(width * 0.95f, 0.02f, 2.4f);
                var size = ps.sizeOverLifetime; size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.5f)));
                FadeInOut(ps);
                var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
                Finish(ps, mistMat);
            }
            // 5) 수면 물결 (플레이 중)
            var ripple = g.AddComponent<Assets.Members.HJH.Environment.WaterfallRipples>();
            ripple.Configure(width * 0.45f);
            return true;
        }

        private static void Mist(string name, Transform parent, Vector3 pos, Quaternion face, float width, float ratePerMeter, Vector2 size, float alpha, float rise, Material mat, Vector3 drift = default, float life = 4f)
        {
            var ps = NewPS(name, parent, pos, face);
            var main = ps.main;
            main.loop = true; main.duration = 5f; main.maxParticles = 160; main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.75f, life * 1.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.85f, 0.94f, 1f, alpha);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = Mathf.Clamp(width * ratePerMeter, 6f, 60f);
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(width * 0.95f, 0.2f, 0.9f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.x = drift.x; vel.y = rise; vel.z = drift.z;
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.5f)));
            FadeInOut(ps);
            Finish(ps, mat);
        }

        // ───────────────────────── 물줄기 면·암벽·조명 ─────────────────────────
        private static Material SheetMat2;
        private static List<GameObject> Stones, StonesSmall;
        private static List<Bounds> PanelBounds;   // 옆에 다른 면이 붙어 있는 쪽에는 기둥 돌을 앞으로 내지 않기 위해
        private const string StoneDir = "Assets/Members/HJH/05.Data/MapProps/Stones/Prefabs";

        private static void AddLight(Transform root, string name, Vector3 pos, float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, true);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(0.55f, 0.88f, 1f);
            l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
            l.lightmapBakeType = LightmapBakeType.Mixed;
        }

        // 패널 로컬(X 폭, Y 높이, Z 앞) 기준 격자 면. 정점 알파로 좌우·윗변을 부드럽게 지우고, 아래로 갈수록 앞으로 휜다.
        private static void BuildSheet(Transform panel, int idx, int layer, float width, float fall, float zOffset, float alpha, Material mat, Vector2 tiling, float speed)
        {
            const int nx = 10, ny = 14;
            var verts = new Vector3[(nx + 1) * (ny + 1)]; var uvs = new Vector2[verts.Length]; var cols = new Color[verts.Length];
            for (int y = 0; y <= ny; y++)
                for (int x = 0; x <= nx; x++)
                {
                    float u = x / (float)nx, v = y / (float)ny; int i = y * (nx + 1) + x;
                    float bulge = 1.1f * (1f - v) * (1f - v) + 0.12f * Mathf.Sin(u * Mathf.PI * 3f + idx);   // 아래쪽이 앞으로, 폭 방향으로 살짝 굴곡
                    verts[i] = new Vector3((u - 0.5f) * width, v * fall, zOffset - 0.15f + bulge);
                    uvs[i] = new Vector2(u, v);
                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.Min(u, 1f - u) / 0.1f);
                    float top = Mathf.SmoothStep(0f, 1f, (1f - v) / 0.05f);
                    cols[i] = new Color(1f, 1f, 1f, alpha * edge * top);
                }
            var tris = new int[nx * ny * 6]; int k = 0;
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                {
                    int a = y * (nx + 1) + x, b2 = a + 1, c = a + nx + 1, d = c + 1;
                    tris[k++] = a; tris[k++] = c; tris[k++] = b2; tris[k++] = b2; tris[k++] = c; tris[k++] = d;
                }
            string path = $"{Dir}Waterfall_Sheet_{idx}_{layer}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) }; AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear();
            mesh.vertices = verts; mesh.uv = uvs; mesh.colors = cols; mesh.triangles = tris;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);

            var go = new GameObject($"WF_Sheet_{layer}");
            go.transform.SetParent(panel, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            var scroll = go.AddComponent<Assets.Members.HJH.Environment.ScrollUV>();
            var so = new SerializedObject(scroll);
            so.FindProperty("tiling").vector2Value = tiling;
            so.FindProperty("speed").vector2Value = new Vector2(0f, speed);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static List<GameObject> LoadStones(string prefix)
        {
            var list = new List<GameObject>();
            foreach (string guid in AssetDatabase.FindAssets(prefix + " t:Prefab", new[] { StoneDir }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go != null && go.name.StartsWith(prefix)) list.Add(go);
            }
            return list;
        }

        // 물줄기 뒤를 받치는 암벽(물이 허공에 떠 보이지 않게) + 윗변 턱 + 발치의 젖은 바위. 플레이 구역 밖이라 콜라이더는 끈다.
        private static void BuildRocks(Transform panel, int idx, float width, float fall, float thick, Bounds self)
        {
            if (Stones == null || Stones.Count == 0) { Debug.LogWarning("[Waterfall] 돌 프리팹(Stone_Big_*)을 못 찾아 암벽은 건너뜁니다: " + StoneDir); return; }
            var rng = new System.Random(977 * (idx + 1));
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var group = new GameObject("WF_Rocks");
            group.transform.SetParent(panel, false);

            void Place(List<GameObject> set, Vector3 local, float size)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(set[rng.Next(set.Count)], group.transform);
                go.transform.localRotation = Quaternion.Euler(R(-12f, 12f), R(0f, 360f), R(-12f, 12f));
                Bounds nb = RendererBounds(go);
                go.transform.localScale *= size / Mathf.Max(0.01f, Mathf.Max(nb.size.x, nb.size.z, nb.size.y * 0.8f));
                go.transform.position = panel.TransformPoint(local);
                go.transform.position += panel.TransformPoint(local) - RendererBounds(go).center;   // 돌 중심이 목표 지점에 오게
                foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }

            float step = 2.7f;
            int colsN = Mathf.CeilToInt((width + 5f) / step), rowsN = Mathf.CeilToInt((fall + 2.5f) / step);
            for (int r = 0; r < rowsN; r++)
                for (int c = 0; c < colsN; c++)
                {
                    float x = -(width + 5f) * 0.5f + (c + 0.5f) * step + R(-0.6f, 0.6f);
                    float y = -0.8f + r * step + R(-0.5f, 0.5f);
                    bool flank = Mathf.Abs(x) > width * 0.5f && !NextToOtherPanel(panel.TransformPoint(new Vector3(x, 0f, 0f)), self);   // 바깥쪽 끝의 기둥만 앞으로 나와 가장자리를 감쌈
                    float size = R(4.2f, 5.6f);
                    float z = flank ? R(-0.6f, 0.9f) : -(size * 0.45f + R(0.3f, 0.9f));   // 돌 앞면이 물줄기를 뚫고 나오지 않게 반지름만큼 뒤로
                    Place(Stones, new Vector3(x, y, z), size);
                }
            // 윗변 턱: 물이 바위 사이로 넘어오게 살짝 앞으로
            for (float x = -width * 0.5f; x <= width * 0.5f; x += 2.4f)
                Place(Stones, new Vector3(x + R(-0.4f, 0.4f), fall + R(0.6f, 1.2f), -R(1.3f, 1.9f)), R(2.8f, 3.8f));
            // 발치: 물을 맞는 바위 몇 개
            var small = StonesSmall != null && StonesSmall.Count > 0 ? StonesSmall : Stones;
            int feet = Mathf.Max(2, Mathf.RoundToInt(width / 3f));
            for (int i = 0; i < feet; i++)
                Place(small, new Vector3(R(-width * 0.45f, width * 0.45f), R(-0.5f, 0.1f), R(0.6f, 2.4f)), R(1.3f, 2.4f));
        }

        private static bool NextToOtherPanel(Vector3 p, Bounds self)
        {
            foreach (Bounds o in PanelBounds)
            {
                if (o.center == self.center) continue;
                if (p.x > o.min.x - 1.5f && p.x < o.max.x + 1.5f && p.z > o.min.z - 1.5f && p.z < o.max.z + 1.5f) return true;
            }
            return false;
        }

        private static Bounds RendererBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        private static void FadeInOut(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        private static ParticleSystem NewPS(string name, Transform parent, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(pos, rot);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // 설정 바꾸려면 멈춰 있어야 함
            return ps;
        }

        private static void Finish(ParticleSystem ps, Material mat)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play();
        }

        // ───────────────────────── 텍스처·머티리얼 ─────────────────────────
        private delegate Color32 PixelFn(float fx, float fy);

        private static Color32 StreakPixel(float fx, float fy)
        {
            // 세로로 긴 줄무늬 (위아래 이어붙여도 끊기지 않게 교차 보간)
            float N(float yy) => Mathf.PerlinNoise(fx * 9f + 3.1f, yy * 1.6f);
            float n = Mathf.Lerp(N(fy), N(fy - 1f), fy);
            float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.75f, n));
            float f2 = Mathf.PerlinNoise(fx * 22f + 9f, fy * 3.5f);
            float f2b = Mathf.PerlinNoise(fx * 22f + 9f, (fy - 1f) * 3.5f);
            float fine = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 0.85f, Mathf.Lerp(f2, f2b, fy))) * 0.7f;
            a = Mathf.Max(a, fine);
            a = Mathf.Lerp(0.4f, 1f, a);   // 바탕 물막(반투명) 위에 밝은 줄무늬. 얼룩처럼 구멍 나지 않게
            Color c = Color.Lerp(new Color(0.7f, 0.88f, 1f), Color.white, n);
            return new Color(c.r, c.g, c.b, a);
        }

        private static Color32 PuffPixel(float fx, float fy)
        {
            float dx = fx - 0.5f, dy = fy - 0.5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
            float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2f);
            a *= 0.75f + 0.25f * Mathf.PerlinNoise(fx * 5f, fy * 5f);
            return new Color(1f, 1f, 1f, a);
        }

        private static Texture2D MakeTexture(string path, int w, int h, TextureWrapMode wrap, PixelFn fn)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) px[y * w + x] = fn((x + 0.5f) / w, (y + 0.5f) / h);
            tex.SetPixels32(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.wrapMode = wrap;
            ti.mipmapEnabled = true;
            ti.sRGBTexture = true;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material MakeMat(string path, Texture2D tex, Color color, bool additive)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                mat = new Material(sh) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 1f);                         // 투명
            mat.SetFloat("_Blend", additive ? 2f : 0f);           // 0 알파, 2 가산
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);                            // 양면
            mat.SetFloat("_SoftParticlesEnabled", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent + 5;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }
    }
}

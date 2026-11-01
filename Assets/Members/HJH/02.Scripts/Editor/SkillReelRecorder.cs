using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Assets.Members.HJH._02.Scripts.EditorTools
{
    // 메뉴: HJH > Element FX > 스킬 영상 뽑기 (mp4)
    // 열린 씬(정글 맵) 위에서 불·바람 스킬 14개의 이펙트를 차례로 재생해 mp4 로 저장 (보여주기용)
    //  - 스킬 에셋에 연결된 이펙트를 게임과 같은 자리·크기·타이밍으로 재생 (시전, 날아감, 터짐, 장판, 오라, 돌진)
    //  - 시전자 = 회색 캡슐, 허수아비 = 빨간 캡슐. 화면 왼쪽 위에 스킬 이름
    //  - 데미지·밀치기·화면 흔들림은 빠짐 (이펙트만)
    // 결과: 프로젝트 폴더/Recordings/SkillReel.mp4
    public static class SkillReelRecorder
    {
        private const int Width = 1280, Height = 720, Fps = 30;
        private const float Dt = 1f / Fps;
        private const string SkillDir = "Assets/Members/HJH/05.Data/Skill/Element/";
        private static readonly Vector3 Body = Vector3.up * 1.1f;

        private static readonly string[] Order =
        {
            "Fire/Fire_Fireball", "Fire/Fire_FlameThrower", "Fire/Fire_FlameDash", "Fire/Fire_Inferno",
            "Fire/Fire_Meteor", "Fire/Fire_FireVeil", "Fire/Fire_Frenzy",
            "Wind/Wind_Gust", "Wind/Wind_WindCutter", "Wind/Wind_Tornado", "Wind/Wind_Leap",
            "Wind/Wind_Headwind", "Wind/Wind_Tailwind", "Wind/Wind_WindVeil",
        };

        [MenuItem("HJH/Element FX/스킬 영상 뽑기 (mp4)")]
        public static void RecordMenu() => Debug.Log("[SkillReel] " + Record("Recordings/SkillReel.mp4"));

        // ------------------------------------------------------------------ 재생 단위

        private sealed class Fx
        {
            public GameObject Go;
            public ParticleSystem[] Systems;
            public FxLocalMover Mover;
            public float Start, Stop = float.MaxValue;
            public Func<float, Vector3> Path;   // 켜지고 지난 시간 → 위치 (날아감, 따라다님)
            public bool Started, Stopped;
        }

        private sealed class Shot
        {
            public string Title;
            public float Length = 3f;
            public Vector3 Focus;
            public readonly List<Fx> Effects = new();
            public readonly List<(GameObject go, Func<float, Vector3> path)> Actors = new();
        }

        // ------------------------------------------------------------------ 녹화

        public static string Record(string path)
        {
            Renderer water = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(r => r.sharedMaterial != null && r.sharedMaterial.shader.name.Contains("ShallowWater"));
            Vector3 stage = water != null ? water.bounds.center : Vector3.zero;
            float floorY = water != null ? water.bounds.max.y : 0f;

            // 보여주기용이라 녹화하는 동안 안개는 끔 (이펙트가 뿌옇게 묻히지 않게). 끝나면 되돌림
            List<Renderer> fog = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(r => r.enabled && r.sharedMaterial != null && r.sharedMaterial.shader.name.Contains("GroundFog")).ToList();
            fog.ForEach(r => r.enabled = false);

            var made = new List<Object>();
            var camGo = new GameObject("[SkillReelCamera]") { hideFlags = HideFlags.DontSave };
            made.Add(camGo);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            cam.farClipPlane = 300f;
            TextMesh title = MakeTitle(camGo.transform, made);

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var frame = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            string full = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            int frames = 0;

            try
            {
                var attrs = new VideoTrackAttributes
                {
                    frameRate = new MediaRational(Fps),
                    width = Width,
                    height = Height,
                    includeAlpha = false,
                    bitRateMode = VideoBitrateMode.High,
                };
                using var encoder = new MediaEncoder(full, attrs);

                for (int s = 0; s < Order.Length; s++)
                {
                    var skill = AssetDatabase.LoadAssetAtPath<ScriptableObject>(SkillDir + Order[s] + ".asset");
                    if (skill == null)
                        continue;
                    EditorUtility.DisplayProgressBar("스킬 영상", skill.name, s / (float)Order.Length);

                    Shot shot = Build(skill, stage, floorY, made);
                    title.text = shot.Title;
                    cam.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
                    cam.transform.position = shot.Focus - cam.transform.forward * 17f;

                    for (float t = 0f; t < shot.Length; t += Dt)
                    {
                        Step(shot, t);
                        cam.targetTexture = rt;
                        cam.Render();
                        cam.targetTexture = null;
                        RenderTexture.active = rt;
                        frame.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                        frame.Apply();
                        RenderTexture.active = null;
                        encoder.AddFrame(frame);
                        frames++;
                        // 확인용: 스킬마다 가장 화려한 순간(0.5초) 한 장을 Temp/FxBridge 에
                        if (Mathf.Abs(t - Mathf.Min(0.6f + (shot.Length - 3f) * 0.6f, shot.Length * 0.5f)) < Dt * 0.5f)
                        {
                            Directory.CreateDirectory("Temp/FxBridge");
                            File.WriteAllBytes($"Temp/FxBridge/reel_{s:00}.jpg", frame.EncodeToJPG(80));
                        }
                    }

                    foreach (Fx fx in shot.Effects)
                        Object.DestroyImmediate(fx.Go);
                    foreach ((GameObject go, _) in shot.Actors)
                        Object.DestroyImmediate(go);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                fog.ForEach(r => r.enabled = true);
                foreach (Object o in made)
                    if (o != null)
                        Object.DestroyImmediate(o);
                Object.DestroyImmediate(frame);
                rt.Release();
            }
            return $"{frames} 프레임 ({frames / (float)Fps:0.0}초) -> {full}";
        }

        // 한 프레임: 켤 것 켜고, 움직일 것 옮기고, 끝난 건 방출 끄고, dt 만큼 시뮬레이션
        private static void Step(Shot shot, float t)
        {
            foreach ((GameObject go, Func<float, Vector3> p) in shot.Actors)
                if (p != null)
                    go.transform.position = p(t);

            foreach (Fx fx in shot.Effects)
            {
                if (t < fx.Start)
                    continue;
                float local = t - fx.Start;
                if (!fx.Started)
                {
                    fx.Started = true;
                    fx.Go.SetActive(true);
                    if (fx.Path != null)
                        fx.Go.transform.position = fx.Path(0f);
                    fx.Mover?.Evaluate(0f);
                    foreach (ParticleSystem ps in fx.Systems)
                        ps.Simulate(0f, false, true, false);
                }

                if (fx.Path != null)
                    fx.Go.transform.position = fx.Path(local);
                fx.Mover?.Evaluate(local);
                if (!fx.Stopped && t >= fx.Stop)
                {
                    fx.Stopped = true;
                    foreach (ParticleSystem ps in fx.Systems)
                    {
                        var emission = ps.emission;
                        emission.enabled = false;
                    }
                }
                foreach (ParticleSystem ps in fx.Systems)
                    ps.Simulate(Dt, false, false, false);
            }
        }

        // ------------------------------------------------------------------ 스킬별 연출 (스킬 코드와 같은 자리·크기)

        private static Shot Build(ScriptableObject skill, Vector3 stage, float floorY, List<Object> made)
        {
            var so = new SerializedObject(skill);
            string name = so.FindProperty("displayName")?.stringValue;
            var shot = new Shot { Title = string.IsNullOrEmpty(name) ? skill.name : name };

            Vector3 caster = Ground(stage + new Vector3(-3.5f, 0f, -1f), floorY);
            Vector3 dir = new Vector3(1f, 0f, 0.35f).normalized;
            Quaternion facing = Quaternion.LookRotation(dir);
            Vector3 hand = caster + Body + dir * 0.6f;
            GameObject casterGo = Capsule(caster, new Color(0.55f, 0.55f, 0.6f));
            shot.Actors.Add((casterGo, null));
            shot.Focus = caster + dir * 3.5f;

            Spawn(shot, Slot(so, "castFx"), hand, facing, 1.6f, 0f);
            GameObject hit = Slot(so, "hitFx");

            switch (skill.GetType().Name)
            {
                case "ProjectileSkill":
                {
                    float range = F(so, "range", 8f), speed = Mathf.Max(1f, F(so, "speed", 15f));
                    int count = Mathf.Max(1, (int)F(so, "count", 1f));
                    float spread = F(so, "spread", 0f), explode = F(so, "explodeRadius", 0f);
                    bool pierce = F(so, "pierce", 0f) > 0f;
                    float reach = pierce ? range : Mathf.Min(range, 6.5f);
                    Vector3 target = Ground(caster + dir * reach, floorY);
                    Dummies(shot, Ground(caster + dir * 6.5f, floorY), floorY, 1, dir);
                    for (int i = 0; i < count; i++)
                    {
                        float yaw = count > 1 ? Mathf.Lerp(-spread * 0.5f, spread * 0.5f, i / (float)(count - 1)) : 0f;
                        Vector3 d = Quaternion.Euler(0f, yaw, 0f) * dir;
                        Vector3 from = hand, to = Ground(caster + d * reach, floorY) + Body;
                        float time = Vector3.Distance(from, to) / speed;
                        Spawn(shot, Slot(so, "travelFx"), from, Quaternion.LookRotation(d), 1f, 0.05f, 0.05f + time,
                            l => Vector3.Lerp(from, to, Mathf.Clamp01(l / time)));
                        if (explode > 0f && i == count / 2)
                            Spawn(shot, Slot(so, "impactFx"), Ground(to, floorY), Quaternion.identity, explode / 2f * 1.3f, 0.05f + time);
                    }
                    float arrive = 0.05f + Vector3.Distance(hand, target + Body) / speed;
                    Spawn(shot, hit, Ground(caster + dir * 6.5f, floorY) + Body, facing, 1.6f, Mathf.Min(arrive, 6.5f / speed + 0.05f));
                    shot.Length = Mathf.Max(3f, arrive + 2f);
                    break;
                }
                case "ChannelSkill":
                {
                    float duration = F(so, "duration", 1.5f), tick = Mathf.Max(0.05f, F(so, "tickInterval", 0.15f));
                    Vector3 target = Ground(caster + dir * 4.5f, floorY);
                    Dummies(shot, target, floorY, 3, dir);
                    for (float t = 0f; t < duration; t += tick)
                        Spawn(shot, Slot(so, "streamFx"), caster + Body + dir * 0.5f, facing, 1f, t);
                    for (float t = 0.2f; t < duration; t += 0.45f)
                        Spawn(shot, hit, target + Body, facing, 1.6f, t);
                    shot.Length = duration + 1.8f;
                    break;
                }
                case "ConeSkill":
                {
                    Vector3 target = Ground(caster + dir * 4f, floorY);
                    Dummies(shot, target, floorY, 3, dir);
                    Spawn(shot, hit, target + Body, facing, 1.6f, 0.12f);
                    break;
                }
                case "DashSkill":
                {
                    float distance = F(so, "distance", 6f), dashTime = Mathf.Max(0.05f, F(so, "dashTime", 0.2f));
                    float land = F(so, "landRadius", 0f), path = F(so, "pathRadius", 0f);
                    Vector3 end = Ground(caster + dir * distance, floorY);
                    Vector3 start = caster;
                    Func<float, Vector3> move = t => Vector3.Lerp(start, end, Mathf.Clamp01((t - 0.1f) / dashTime));
                    shot.Actors[0] = (casterGo, t => move(t) + Vector3.up * 1f);
                    Spawn(shot, Slot(so, "dashFx"), start, facing, 1f, 0.1f, 0.1f + dashTime + 0.1f, l => move(l + 0.1f));
                    Vector3 mid = Ground(Vector3.Lerp(start, end, 0.55f), floorY);
                    Dummies(shot, path > 0f ? mid : end, floorY, 2, dir);
                    if (path > 0f)
                        Spawn(shot, hit, mid + Body, facing, 1.6f, 0.1f + dashTime * 0.55f);
                    if (land > 0f)
                    {
                        Spawn(shot, Slot(so, "impactFx"), end, Quaternion.identity, land / 2f * 1.3f, 0.1f + dashTime);
                        Spawn(shot, hit, end + Body + dir * 1f, facing, 1.6f, 0.1f + dashTime);
                    }
                    shot.Focus = Vector3.Lerp(start, end, 0.5f);
                    break;
                }
                case "CircleSkill":
                {
                    bool self = F(so, "aroundSelf", 0f) > 0f;
                    float radius = F(so, "radius", 2f), delay = F(so, "delay", 0f);
                    Vector3 center = self ? caster : Ground(caster + dir * Mathf.Min(F(so, "range", 8f), 6.5f), floorY);
                    Dummies(shot, center + dir * (self ? 2.2f : 0.8f), floorY, 3, dir);
                    if (delay > 0f)
                        Spawn(shot, Slot(so, "windupFx"), center, Quaternion.identity, 1f, 0.05f, 0.05f + delay);
                    Spawn(shot, Slot(so, "impactFx"), center, Quaternion.identity, radius / 2f * 1.3f, 0.05f + delay);
                    Spawn(shot, hit, center + dir * (self ? 2.2f : 0.8f) + Body, facing, 1.6f, 0.05f + delay);
                    shot.Focus = self ? caster : Vector3.Lerp(caster, center, 0.6f);
                    shot.Length = 3f + delay;
                    break;
                }
                case "ZoneSkill":
                {
                    bool self = F(so, "aroundSelf", 0f) > 0f;
                    float radius = F(so, "radius", 3f), interval = Mathf.Max(0.1f, F(so, "interval", 0.5f));
                    float time = Mathf.Min(F(so, "zoneTime", 4f), 3.2f);
                    Vector3 center = self ? caster : Ground(caster + dir * Mathf.Min(F(so, "range", 6f), 6f), floorY);
                    Spawn(shot, Slot(so, "zoneFx"), center, facing, radius / 2f, 0.05f, 0.05f + time);
                    Vector3 victim = center + dir * radius * 0.6f;
                    Dummies(shot, victim, floorY, 2, dir);
                    for (float t = 0.1f; t < time; t += interval * 2f)
                        Spawn(shot, hit, victim + Body, facing, 1.6f, t);
                    shot.Focus = self ? caster : Vector3.Lerp(caster, center, 0.6f);
                    shot.Length = time + 1.2f;
                    break;
                }
                case "SelfSkill":
                {
                    float explode = F(so, "explodeRadius", 0f);
                    Spawn(shot, Slot(so, "auraFx"), caster + Vector3.up, Quaternion.identity, 1f, 0.05f, 3f);
                    if (explode > 0f && F(so, "explodeDelay", 1f) <= 0.01f)
                    {
                        Dummies(shot, caster + dir * 2.5f, floorY, 2, dir);
                        Spawn(shot, Slot(so, "impactFx"), caster, Quaternion.identity, explode / 2f * 1.3f, 0.08f);
                        Spawn(shot, hit, caster + dir * 2.5f + Body, facing, 1.6f, 0.1f);
                    }
                    shot.Focus = caster;
                    shot.Length = 3.6f;
                    break;
                }
                case "BarrierSkill":
                {
                    float radius = F(so, "radius", 2.5f);
                    Spawn(shot, Slot(so, "barrierFx"), caster, facing, radius / 2f, 0.05f, 3f);
                    shot.Focus = caster;
                    shot.Length = 3.6f;
                    break;
                }
            }
            return shot;
        }

        // ------------------------------------------------------------------ 도우미

        private static void Spawn(Shot shot, GameObject prefab, Vector3 position, Quaternion rotation, float scale, float start,
            float stop = float.MaxValue, Func<float, Vector3> path = null)
        {
            if (prefab == null)
                return;
            GameObject go = Object.Instantiate(prefab, position, rotation);
            go.hideFlags = HideFlags.DontSave;
            go.transform.localScale = Vector3.one * scale;
            go.SetActive(false);
            shot.Effects.Add(new Fx
            {
                Go = go,
                Systems = go.GetComponentsInChildren<ParticleSystem>(true),
                Mover = go.GetComponent<FxLocalMover>(),
                Start = start,
                Stop = stop,
                Path = path,
            });
        }

        private static GameObject Slot(SerializedObject so, string field) =>
            so.FindProperty(field)?.FindPropertyRelative("prefab")?.objectReferenceValue as GameObject;

        private static float F(SerializedObject so, string field, float fallback)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
                return fallback;
            return p.propertyType switch
            {
                SerializedPropertyType.Float => p.floatValue,
                SerializedPropertyType.Integer => p.intValue,
                SerializedPropertyType.Boolean => p.boolValue ? 1f : 0f,
                _ => fallback,
            };
        }

        private static void Dummies(Shot shot, Vector3 center, float floorY, int count, Vector3 dir)
        {
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = Ground(center + side * ((i - (count - 1) * 0.5f) * 1.3f), floorY);
                shot.Actors.Add((Capsule(p, new Color(0.7f, 0.18f, 0.15f)), null));
            }
        }

        private static GameObject Capsule(Vector3 feet, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.hideFlags = HideFlags.DontSave;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = feet + Vector3.up;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            mat.SetColor("_BaseColor", color);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        // 바닥(또는 수면) 높이로
        private static Vector3 Ground(Vector3 p, float floorY)
        {
            float y = floorY;
            if (Physics.Raycast(new Vector3(p.x, p.y + 40f, p.z), Vector3.down, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore))
                y = Mathf.Max(y, hit.point.y);
            return new Vector3(p.x, y, p.z);
        }

        private static TextMesh MakeTitle(Transform cam, List<Object> made)
        {
            var go = new GameObject("[SkillReelTitle]") { hideFlags = HideFlags.DontSave };
            made.Add(go);
            go.transform.SetParent(cam, false);
            go.transform.localPosition = new Vector3(-1.95f, 1.0f, 3.2f);
            var text = go.AddComponent<TextMesh>();
            Font font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 64);
            made.Add(font);
            text.font = font;
            text.fontSize = 64;
            text.characterSize = 0.018f;
            text.anchor = TextAnchor.UpperLeft;
            text.color = Color.white;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return text;
        }
    }
}

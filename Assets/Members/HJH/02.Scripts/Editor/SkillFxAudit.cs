using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace HJH.EditorTools
{
    // 스킬별 이펙트 무게 점검 (읽기 전용). 결과: Captures/_fxaudit.txt
    // 스킬 에셋이 참조하는 모든 프리팹(의존성)을 모아서: 파티클 시스템 수, 예상 최대 파티클, 조명, 충돌, 서브 이미터, 트레일, 메시 파티클, VFX 그래프, Lit 머티리얼
    public static class SkillFxAudit
    {
        private sealed class Stat
        {
            public int Systems, MaxParticles, EstParticles, Collisions, WorldCollisions, LightModules, LightModuleMax, Lights, ShadowLights,
                SubEmitters, Trails, MeshParticles, MeshParticleVerts, Vfx, LitMats, Noise, Prefabs;
            public float MaxStartSize;
            public readonly HashSet<string> Shaders = new();

            public void Add(Stat o)
            {
                Systems += o.Systems; MaxParticles += o.MaxParticles; EstParticles += o.EstParticles; Collisions += o.Collisions;
                WorldCollisions += o.WorldCollisions; LightModules += o.LightModules; LightModuleMax += o.LightModuleMax; Lights += o.Lights;
                ShadowLights += o.ShadowLights; SubEmitters += o.SubEmitters; Trails += o.Trails; MeshParticles += o.MeshParticles;
                MeshParticleVerts += o.MeshParticleVerts; Vfx += o.Vfx; LitMats += o.LitMats; Noise += o.Noise; Prefabs += o.Prefabs;
                MaxStartSize = Mathf.Max(MaxStartSize, o.MaxStartSize);
                Shaders.UnionWith(o.Shaders);
            }
        }

        [MenuItem("HJH/Perf/스킬 이펙트 점검")]
        public static void Run()
        {
            var cache = new Dictionary<string, Stat>();
            var rows = new List<(string skill, Stat stat, string prefabs)>();

            foreach (string guid in AssetDatabase.FindAssets("t:SkillData", new[] { "Assets/Members/HJH/05.Data/Skill" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var total = new Stat();
                var names = new List<string>();
                foreach (string dep in AssetDatabase.GetDependencies(path, true))
                {
                    if (!dep.EndsWith(".prefab")) continue;
                    if (!cache.TryGetValue(dep, out Stat s))
                        cache[dep] = s = Measure(dep);
                    if (s.Systems == 0 && s.Lights == 0 && s.Vfx == 0) continue;
                    total.Add(s);
                    names.Add($"{Path.GetFileNameWithoutExtension(dep)}(p{s.EstParticles},L{s.Lights + s.LightModuleMax})");
                }
                rows.Add((Path.GetFileNameWithoutExtension(path), total, string.Join(" ", names)));
            }

            var sb = new StringBuilder();
            sb.AppendLine("skill | prefabs | systems | estParticles | maxParticles | lights(comp/shadow) | lightModuleMax | collision(world) | subEmit | trails | meshPS(verts) | vfx | litMats | noise | maxStartSize");
            foreach (var (skill, s, prefabs) in rows.OrderByDescending(r => r.stat.EstParticles))
                sb.AppendLine($"{skill} | {s.Prefabs} | {s.Systems} | {s.EstParticles} | {s.MaxParticles} | {s.Lights}/{s.ShadowLights} | {s.LightModuleMax} | {s.Collisions}({s.WorldCollisions}) | {s.SubEmitters} | {s.Trails} | {s.MeshParticles}({s.MeshParticleVerts}) | {s.Vfx} | {s.LitMats} | {s.Noise} | {s.MaxStartSize:F1}");
            sb.AppendLine();
            sb.AppendLine("## prefabs per skill");
            foreach (var (skill, _, prefabs) in rows.OrderBy(r => r.skill))
                sb.AppendLine($"{skill}: {prefabs}");
            sb.AppendLine();
            sb.AppendLine("## shaders");
            foreach (var kv in cache.Where(k => k.Value.Systems > 0).OrderBy(k => k.Key))
                sb.AppendLine($"{Path.GetFileNameWithoutExtension(kv.Key)}: {string.Join(", ", kv.Value.Shaders)}");

            Directory.CreateDirectory("Captures");
            File.WriteAllText("Captures/_fxaudit.txt", sb.ToString());
            Debug.Log("[SkillFxAudit] Captures/_fxaudit.txt");
        }

        private static Stat Measure(string path)
        {
            var s = new Stat { Prefabs = 1 };
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) return s;

            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                s.Systems++;
                var main = ps.main;
                s.MaxParticles += main.maxParticles;
                float life = main.startLifetime.constantMax;
                float rate = ps.emission.enabled ? ps.emission.rateOverTime.constantMax : 0f;
                int burst = 0;
                if (ps.emission.enabled)
                    for (int i = 0; i < ps.emission.burstCount; i++)
                    {
                        ParticleSystem.Burst b = ps.emission.GetBurst(i);
                        burst += Mathf.RoundToInt(b.count.constantMax) * Mathf.Max(1, b.cycleCount);
                    }
                s.EstParticles += Mathf.Min(main.maxParticles, Mathf.CeilToInt(rate * Mathf.Min(life, main.duration + life)) + burst);
                s.MaxStartSize = Mathf.Max(s.MaxStartSize, main.startSize.constantMax);

                if (ps.collision.enabled) { s.Collisions++; if (ps.collision.type == ParticleSystemCollisionType.World) s.WorldCollisions++; }
                if (ps.lights.enabled) { s.LightModules++; s.LightModuleMax += ps.lights.maxLights; }
                if (ps.subEmitters.enabled) s.SubEmitters += ps.subEmitters.subEmittersCount;
                if (ps.trails.enabled) s.Trails++;
                if (ps.noise.enabled) s.Noise++;

                var r = ps.GetComponent<ParticleSystemRenderer>();
                if (r != null)
                {
                    if (r.renderMode == ParticleSystemRenderMode.Mesh && r.mesh != null)
                    {
                        s.MeshParticles++;
                        s.MeshParticleVerts += r.mesh.vertexCount;
                    }
                    foreach (Material m in r.sharedMaterials)
                    {
                        if (m == null || m.shader == null) continue;
                        s.Shaders.Add(m.shader.name);
                        if (m.shader.name.Contains("Lit") && !m.shader.name.Contains("Unlit")) s.LitMats++;
                    }
                }
            }

            foreach (Light l in root.GetComponentsInChildren<Light>(true))
            {
                s.Lights++;
                if (l.shadows != LightShadows.None) s.ShadowLights++;
            }
            s.Vfx = root.GetComponentsInChildren<VisualEffect>(true).Length;
            return s;
        }
    }
}

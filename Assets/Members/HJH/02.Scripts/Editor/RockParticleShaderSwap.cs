using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEditor;
using UnityEngine;

namespace HJH.EditorTools
{
    // 땅 스킬 돌 메시 파티클 CPU 비용 시험 (2026-11-12 측정: RockWall/EarthSplitter CPU 메인 5ms → 10ms).
    // URP/Lit 은 메시 파티클 GPU 인스턴싱을 못 해서 CPU가 매 프레임 돌 정점(960개)을 만듦 → Particles/Lit 복사 머티리얼로 바꿔 봄.
    // 원본 M_FX_EarthRock 은 안 건드림 (JJH EnemySpawn_Effect 도 씀). 우리 땅 프리팹만 새 머티리얼로.
    // 돌 색이 원소 색으로 물들지 않게 바뀐 파티클은 FxPrefabInstance.untinted 에 넣음 (지금처럼 갈색).
    // 백업: 프로젝트 루트 Backups/RockFx (Assets 밖이라 임포트 안 됨). 되돌리기 메뉴로 복원.
    public static class RockParticleShaderSwap
    {
        private const string SourceMat = "Assets/Members/HJH/08.Effects/PBR/Materials/M_FX_EarthRock.mat";
        private const string NewMat = "Assets/Members/HJH/08.Effects/PBR/Materials/M_FX_EarthRock_Particle.mat";
        private const string BackupDir = "Backups/RockFx";
        private const string Log = "Captures/_rockswap.txt";

        private static IEnumerable<string> Prefabs()
        {
            string guid = AssetDatabase.AssetPathToGUID(SourceMat);
            return AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Members/HJH/05.Data/Resources/HJH/PbrFx" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => File.ReadAllText(p).Contains(guid));
        }

        [MenuItem("HJH/Perf/돌 파티클 셰이더 시험 적용")]
        public static void Apply()
        {
            var log = new StringBuilder();
            Directory.CreateDirectory(BackupDir);
            Directory.CreateDirectory("Captures");

            Material source = AssetDatabase.LoadAssetAtPath<Material>(SourceMat);
            Material particle = AssetDatabase.LoadAssetAtPath<Material>(NewMat);
            // HJH/FX/RockInstanced = Particles/Lit + 캐릭터 전용 조명 구분(_LIGHT_LAYERS) + 그림자 패스. 처음 시험한 Particles/Lit 은 돌이 밝게 떠서 바꿈
            Shader shader = Shader.Find("HJH/FX/RockInstanced");
            if (particle != null && particle.shader != shader)
            {
                particle.shader = shader;
                particle.SetFloat("_Surface", 0f);
                EditorUtility.SetDirty(particle);
                log.AppendLine($"{NewMat} shader -> {shader.name}");
            }
            if (particle == null)
            {
                particle = new Material(shader) { name = "M_FX_EarthRock_Particle", enableInstancing = true };
                particle.SetColor("_BaseColor", source.GetColor("_BaseColor"));
                particle.SetFloat("_Smoothness", source.GetFloat("_Smoothness"));
                particle.SetFloat("_Metallic", source.GetFloat("_Metallic"));
                particle.SetFloat("_Surface", 0f);   // 불투명 (지금과 같음)
                particle.SetFloat("_ColorMode", 0f); // 곱하기
                AssetDatabase.CreateAsset(particle, NewMat);
                log.AppendLine($"created {NewMat} shader={shader.name}");
            }

            foreach (string path in Prefabs().ToList())
            {
                string backup = Path.Combine(BackupDir, Path.GetFileName(path));
                if (!File.Exists(backup))
                    File.Copy(path, backup);

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                var swapped = new List<ParticleSystem>();
                foreach (ParticleSystemRenderer r in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    Material[] mats = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] == source) { mats[i] = particle; changed = true; }
                    if (!changed) continue;

                    r.sharedMaterials = mats;
                    if (r.renderMode == ParticleSystemRenderMode.Mesh)
                        r.enableGPUInstancing = true;
                    var ps = r.GetComponent<ParticleSystem>();
                    swapped.Add(ps);
                    var col = ps.colorOverLifetime;
                    log.AppendLine($"{Path.GetFileNameWithoutExtension(path)}/{r.name}: mode={r.renderMode} mesh={(r.mesh ? r.mesh.name + ":" + r.mesh.vertexCount : "-")} " +
                                   $"cast={r.shadowCastingMode} colorOverLifetime={col.enabled} start={ps.main.startColor.color} streams={r.activeVertexStreamsCount}");
                }

                if (swapped.Count > 0)
                {
                    if (!root.TryGetComponent(out FxPrefabInstance fx))
                        fx = root.AddComponent<FxPrefabInstance>();
                    var so = new SerializedObject(fx);
                    SerializedProperty list = so.FindProperty("untinted");
                    var existing = new HashSet<Object>();
                    for (int i = 0; i < list.arraySize; i++) existing.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);
                    foreach (ParticleSystem ps in swapped.Where(p => !existing.Contains(p)))
                    {
                        list.arraySize++;
                        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = ps;
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            File.WriteAllText(Log, log.ToString());
            Debug.Log("[RockParticleShaderSwap] applied\n" + log);
        }

        [MenuItem("HJH/Perf/돌 파티클 셰이더 되돌리기")]
        public static void Revert()
        {
            var log = new StringBuilder();
            if (Directory.Exists(BackupDir))
                foreach (string backup in Directory.GetFiles(BackupDir, "*.prefab"))
                {
                    string path = "Assets/Members/HJH/05.Data/Resources/HJH/PbrFx/" + Path.GetFileName(backup);
                    File.Copy(backup, path, true);
                    log.AppendLine("restored " + path);
                }
            AssetDatabase.Refresh();
            File.WriteAllText(Log, log.ToString());
            Debug.Log("[RockParticleShaderSwap] reverted\n" + log);
        }
    }
}

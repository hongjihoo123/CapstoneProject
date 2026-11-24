using System.Text;
using Assets.Members.HJH._02.Scripts.Element.Upgrade;
using Members.KYR._01.Scripts.Enchant.So;
using Members.KYR._01.Scripts.Player;
using RobotWeapons;
using UnityEditor;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Editor
{
    // 플레이 중 검증: 실제 PlayerAgent 의 피해 경로(ApplyDamageTo)에 치명이 걸리는지, 슬롯별 배율이 맞는지, 궁극기 연쇄가 도는지.
    // 쓰는 법: 메뉴 "치명 경로 플레이 검증 준비" -> capture.ps1 -Config skilltree_play.json (플레이어가 있는 씬을 플레이)
    // 결과: Captures/_skilltree_play.txt
    public static class SkillTreePlayCheck
    {
        private const string Key = "HJH.SkillTreePlayCheck";
        private const string TreePath = "Assets/Members/HJH/05.Data/Enchant/EnchantTree_Gunner.asset";

        [MenuItem("HJH/스킬 트리/치명 경로 플레이 검증 준비")]
        public static void Prepare() => EditorPrefs.SetBool(Key, true);

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !EditorPrefs.GetBool(Key, false))
                    return;
                EditorPrefs.DeleteKey(Key);
                double until = EditorApplication.timeSinceStartup + 4d;
                EditorApplication.CallbackFunction tick = null;
                tick = () =>
                {
                    if (!EditorApplication.isPlaying)
                    {
                        EditorApplication.update -= tick;
                        return;
                    }
                    if (EditorApplication.timeSinceStartup < until)
                        return;
                    EditorApplication.update -= tick;
                    Run();
                };
                EditorApplication.update += tick;
            };
        }

        private sealed class Punching : MonoBehaviour, IDamageable
        {
            public readonly System.Collections.Generic.List<float> Hits = new();
            public bool IsAlive => true;
            public void TakeDamage(float amount, GameObject source) => Hits.Add(amount);
        }

        private static void Run()
        {
            var log = new StringBuilder("[SkillTreePlayCheck]\n");
            int fails = 0;
            void Check(string name, bool ok, string detail)
            {
                if (!ok)
                    fails++;
                log.AppendLine($"  {(ok ? "OK  " : "FAIL")} {name}: {detail}");
            }

            var go = new GameObject("PlayCheckTarget");
            try
            {
                var player = Object.FindFirstObjectByType<PlayerAgent>();
                var tree = AssetDatabase.LoadAssetAtPath<EnchantTree>(TreePath);
                if (player == null || tree == null)
                {
                    log.AppendLine("  플레이어 또는 트리를 못 찾음 (플레이어가 있는 씬에서 돌려 주세요)");
                    fails++;
                    return;
                }

                SkillTreeProgress.WipeAll();
                SkillTreeProgress.Grant(2000);
                bool learned = true;
                for (int guard = 0; learned && guard < 1000; guard++)
                {
                    learned = false;
                    foreach (EnchantTreeNode node in tree.Nodes)
                        learned |= SkillTreeProgress.TryLearn(tree, node);
                }
                SkillTreeProgress.Apply(tree);
                CritSystem.Reset();

                var dummy = go.AddComponent<Punching>();

                // 평타 경로 (HitSource 기본 = Weapon -> Attack 슬롯). 확률 24.5%, 치명 배율 1.5 + 0.5 = 2.0
                for (int i = 0; i < 600; i++)
                    player.ApplyDamageTo(dummy, 100f);
                Stats(dummy, out float min, out float max, out int crits, out int n);
                float rate = (float)crits / n;
                float baseline = min;
                Check("평타 치명 비율 (24.5% + 방아쇠 중첩 10% = 약 34.5%. 한 프레임에 쏴서 가만히 보너스는 0)", rate > 0.25f && rate < 0.45f, $"{rate:P1} ({crits}/{n})");
                Check("평타 치명 배율 (2.0 ~ 급소 사격 +0.4 = 2.0~2.4)", max / min >= 1.99f && max / min <= 2.41f, $"{max:0.##}/{min:0.##} = {max / min:0.###}");

                // 충전탄 경로 (Passive -> Charged 슬롯). 확정 치명 + 배율 2.1
                dummy.Hits.Clear();
                for (int i = 0; i < 50; i++)
                {
                    HitSource.Push(HitSourceKind.Passive);
                    try { player.ApplyDamageTo(dummy, 100f); }
                    finally { HitSource.Pop(); }
                }
                Stats(dummy, out min, out max, out crits, out n);
                Check("충전탄은 전부 치명 (비치명의 2.1배)", Mathf.Abs(min / baseline - 2.1f) < 0.01f && Mathf.Abs(max / baseline - 2.1f) < 0.01f, $"{min / baseline:0.###} ~ {max / baseline:0.###}배 ({n}발)");

                // 지속 피해(화상 틱 등)는 치명 안 남
                dummy.Hits.Clear();
                CritSystem.Suppressed = true;
                for (int i = 0; i < 100; i++)
                    player.ApplyDamageTo(dummy, 100f);
                CritSystem.Suppressed = false;
                Stats(dummy, out min, out max, out crits, out n);
                Check("지속 피해 치명 없음", Mathf.Approximately(min, max), $"min {min:0.##} max {max:0.##}");

                // 표식: 표식 찍힌 적은 받는 피해 +18% (비치명끼리 비교)
                dummy.Hits.Clear();
                for (int i = 0; i < 200; i++)
                    player.ApplyDamageTo(dummy, 100f);
                Stats(dummy, out float unmarkedMin, out _, out _, out _);
                dummy.Hits.Clear();
                CritSystem.Mark(dummy);
                for (int i = 0; i < 200; i++)
                    player.ApplyDamageTo(dummy, 100f);
                Stats(dummy, out float markedMin, out _, out _, out _);
                Check("표식 적 받는 피해 (x1.18)", Mathf.Abs(markedMin / unmarkedMin - 1.18f) < 0.01f, (markedMin / unmarkedMin).ToString("0.###"));

                // 백스텝 장전: 대쉬 시전 직후 평타 3발은 치명 확정
                dummy.Hits.Clear();
                CritSystem.OnCast(player, Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.SkillSlotId.Dash);
                for (int i = 0; i < 3; i++)
                    player.ApplyDamageTo(dummy, 100f);
                bool allCrit = true;
                float lowest = float.MaxValue;
                foreach (float h in dummy.Hits)
                {
                    allCrit &= h >= baseline * 1.9f;
                    lowest = Mathf.Min(lowest, h);
                }
                Check("대쉬 직후 평타 3발 확정 치명", allCrit && dummy.Hits.Count == 3, dummy.Hits.Count + "발, 최소 " + lowest.ToString("0.##") + " (기준 " + baseline.ToString("0.##") + ")");

                // 정조준(가만히 2초 = +20%p) / 급소 사격(14m = 치명 배율 +0.4): 시간·거리 조건이라 값을 직접 놓고 비교
                var moveField = typeof(CritSystem).GetField("_lastMoveTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                moveField.SetValue(null, Time.time);
                float moving = CritSystem.ChanceFor(SkillUpgradeScope.Attack);
                moveField.SetValue(null, Time.time - 10f);
                float still = CritSystem.ChanceFor(SkillUpgradeScope.Attack);
                Check("정조준: 가만히 있으면 치명 확률 +20%p", Mathf.Abs(still - moving - 0.20f) < 1e-3f, (still - moving).ToString("0.###"));
                // 급소 사격은 정조준과 택일이라 이 테스트에서는 안 배움 -> 같은 값을 판에 임시로 올려서 읽는 쪽만 확인
                var probe = new object();
                SkillUpgrades.Board.Set(probe, new[] { new SkillUpgradeBoard.Entry(SkillUpgradeKind.CritFar, SkillUpgradeScope.Attack, 0.40f) });
                float nearMult = CritSystem.MultiplierFor(SkillUpgradeScope.Attack, null, 0f);
                float farMult = CritSystem.MultiplierFor(SkillUpgradeScope.Attack, null, 14f);
                Check("급소 사격: 14m 치명 배율 +0.4", Mathf.Abs(farMult - nearMult - 0.40f) < 1e-3f, (farMult - nearMult).ToString("0.###"));
                SkillUpgrades.Board.Clear(probe);

                // 궁극기 연쇄: 시전 알림 후 궁극기 슬롯 치명이 필요 횟수(1)를 채우면 연쇄 단계가 오름
                CritSystem.OnCast(player, Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.SkillSlotId.Ultimate);
                CritSystem.CurrentSlot = SkillUpgradeScope.Ultimate;
                for (int i = 0; i < 400 && CritSystem.UltChain == 0; i++)
                    player.ApplyDamageTo(dummy, 100f);
                CritSystem.CurrentSlot = SkillUpgradeScope.All;
                Check("궁극기 파편 치명 -> 연쇄 1단계", CritSystem.UltChain == 1, $"연쇄 {CritSystem.UltChain}");

                // 평타 공속 배율
                float speed = CritSystem.AttackSpeedMultiplier();
                Check("평타 공속 배율 (1 + 0.83, 5연속 치명 중이면 쾌감 사격 +0.30)", Mathf.Abs(speed - 1.83f) < 1e-3f || Mathf.Abs(speed - 2.13f) < 1e-3f, CritSystem.AttackSpeedMultiplier().ToString("0.###"));
            }
            finally
            {
                SkillTreeProgress.WipeAll();
                CritSystem.Reset();
                Object.Destroy(go);
                log.AppendLine(fails == 0 ? "ALL PASS" : $"FAILS = {fails}");
                System.IO.File.WriteAllText("Captures/_skilltree_play.txt", log.ToString(), new UTF8Encoding(true));
            }
        }

        private static void Stats(Punching dummy, out float min, out float max, out int crits, out int n)
        {
            min = float.MaxValue;
            max = 0f;
            foreach (float h in dummy.Hits)
            {
                min = Mathf.Min(min, h);
                max = Mathf.Max(max, h);
            }
            crits = 0;
            n = dummy.Hits.Count;
            foreach (float h in dummy.Hits)
            {
                if (h > min * 1.05f || (min == max && false))
                    crits++;
            }
        }
    }
}

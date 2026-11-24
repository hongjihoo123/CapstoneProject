using System.Collections.Generic;
using System.Text;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Element.Upgrade;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.KYR._01.Scripts.Enchant;
using Members.KYR._01.Scripts.Enchant.Editor;
using Members.KYR._01.Scripts.Enchant.So;
using UnityEditor;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Editor
{
    // 스킬 트리(강화 노드) 검증: 트리 에셋 -> EnchantModule 이 노드를 배움 -> SkillUpgrades 판에 올라감 -> 스킬 쪽 읽는 곳이 달라짐.
    // 플레이 중이 아닐 때만 돈다 (씬을 안 건드리고, 만든 오브젝트는 전부 지움).
    public static class SkillUpgradeSelfTest
    {
        private const string TreePath = "Assets/Members/HJH/05.Data/Enchant/EnchantTree_Gunner.asset";
        private const string DashPath = "Assets/Members/HJH/05.Data/Skill/Gunner/Gunner_Dash.asset";

        private sealed class Host : ISkillHost
        {
            public ISkillContext Context => null;
            public float CooldownReduction => 0f;
            public void OnSkillEntered() { }
        }

        [MenuItem("HJH/스킬 트리/강화 검증")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[SkillUpgradeSelfTest] 플레이 중에는 돌리지 않습니다.");
                return;
            }

            var log = new StringBuilder("[SkillUpgradeSelfTest]\n");
            int fails = 0;

            void Check(string name, float got, float want)
            {
                bool ok = Mathf.Abs(got - want) < 1e-4f;
                if (!ok)
                    fails++;
                log.AppendLine($"  {(ok ? "OK  " : "FAIL")} {name}: {got:0.####} (기대 {want:0.####})");
            }

            EnchantTree tree = AssetDatabase.LoadAssetAtPath<EnchantTree>(TreePath);
            if (tree == null)
            {
                Debug.LogError("[SkillUpgradeSelfTest] 트리 에셋을 못 찾음: " + TreePath);
                return;
            }

            foreach (EnchantTreeIssue issue in EnchantTreeValidator.Validate(tree))
            {
                log.AppendLine("  검증: " + issue.Message);
                if (issue.IsError)
                    fails++;
            }

            var go = new GameObject("SkillUpgradeSelfTest") { hideFlags = HideFlags.HideAndDontSave };
            EnchantModule module = null;
            try
            {
                module = go.AddComponent<EnchantModule>();
                var so = new SerializedObject(module);
                so.FindProperty("trees").arraySize = 1;
                so.FindProperty("trees").GetArrayElementAtIndex(0).objectReferenceValue = tree;
                so.FindProperty("ignoreCost").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                // 에디트 모드에서는 OnEnable/Start 가 안 돌아서, 캐릭터가 골라졌을 때와 같은 적용 경로를 직접 부른다
                void Reapply() => typeof(EnchantModule)
                    .GetMethod("ApplyStats", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(module, new object[] { tree });
                Reapply();

                SkillData dash = AssetDatabase.LoadAssetAtPath<SkillData>(DashPath);
                const SkillUpgradeScope Atk = SkillUpgradeScope.Attack;
                const SkillUpgradeScope Chg = SkillUpgradeScope.Charged;

                // 0) 아무것도 안 배움 = 보너스 없음
                Check("시작: 평타 치명 확률", SkillUpgrades.Bonus(SkillUpgradeKind.CritChance, Atk), 0f);

                // 1) 선행이 안 열린 노드(능력)는 못 배움
                Check("선행 없이 정조준", module.TryLearn(tree, tree.FindNode("atk1_a1")) ? 1f : 0f, 0f);

                // 2) 전부 최대 랭크까지 배움 (택일 노드는 먼저 나온 쪽만)
                var guard = 0;
                bool learned = true;
                while (learned && guard++ < 1000)
                {
                    learned = false;
                    foreach (EnchantTreeNode node in tree.Nodes)
                        learned |= module.TryLearn(tree, node);
                }

                Check("평타 치명 확률 (정밀 사격 입구 2% + 1.5%x5 x3)", SkillUpgrades.Exact(SkillUpgradeKind.CritChance, Atk), 0.245f);
                Check("평타 공속 (연발/치명환류 공속 + 난사 40%)", SkillUpgrades.Exact(SkillUpgradeKind.AttackSpeed, Atk), 0.83f);
                Check("평타 피해 배율 (난사 -25% + 2%x5)", SkillUpgrades.Multiplier(SkillUpgradeKind.Damage, Atk), 0.85f);
                Check("평타 치명 시 궁극기 쿨 감소 (입구 0.1 + 0.1x5 x2)", SkillUpgrades.Exact(SkillUpgradeKind.CritRefundUlt, Atk), 1.1f);
                Check("충전탄 치명 확률은 100%로 막힘 (확정 치명)", CritSystem.ChanceFor(Chg), 1f);
                Check("충전탄 치명 배율 (기본 1.5 + 6%x5 x2)", CritSystem.MultiplierFor(Chg), 2.1f);
                Check("충전 필요 발수 감소 (0.2 x5 x3)", SkillUpgrades.Exact(SkillUpgradeKind.ChargeNeed, Chg), 3f);
                Check("파편 수 (입구 1 + 5 + 5)", SkillUpgrades.Exact(SkillUpgradeKind.ShrapnelCount, SkillUpgradeScope.Ultimate), 11f);
                Check("궁극기 필요 치명 횟수 (8 - 7)", CritSystem.UltNeed(), 1f);
                Check("궁극기 연쇄 상한 (기본 1 + 끝없는 연쇄 1)", CritSystem.UltChainCap(), 2f);

                Check("정조준 (가만히 쏠 때 치명 확률 최대)", SkillUpgrades.Exact(SkillUpgradeKind.CritStill, Atk), 0.20f);
                Check("집속탄", SkillUpgrades.Exact(SkillUpgradeKind.ShrapnelDense, SkillUpgradeScope.Ultimate), 1f);
                Check("표식 적이 받는 피해 (입구 3% + 3%x5)", SkillUpgrades.Exact(SkillUpgradeKind.MarkTaken, SkillUpgradeScope.Shock), 0.18f);
                Check("충격파 사슬 (핵심)", SkillUpgrades.Exact(SkillUpgradeKind.ChainShock, SkillUpgradeScope.Shock), 0.5f);
                Check("이동속도 (2%x5)", SkillUpgrades.Exact(SkillUpgradeKind.MoveSpeed, SkillUpgradeScope.Dash), 0.10f);
                Check("대쉬 시 충전탄 게이지 (0.2x5 + 잔류 충전 2)", SkillUpgrades.Exact(SkillUpgradeKind.DashGauge, Chg), 3f);
                Check("백스텝 장전 (대쉬 직후 확정 치명 발수)", SkillUpgrades.Exact(SkillUpgradeKind.GuaranteedCrits, SkillUpgradeScope.Dash), 3f);

                // 3) 실제 읽는 곳이 바뀌었나
                var dashState = new GenericSkillState(new Host(), SkillSlotId.Dash, dash);
                float baseCooldown = dash.Cooldown;
                Check("대쉬 쿨 (3s x (1-0.45))", dashState.CooldownDuration, baseCooldown * 0.55f);
                Check("대쉬 충전 (기본 + 연속 대쉬 1)", dashState.MaxCharges, dash.Charges + 1f);

                // 치명 굴림: 충전탄 슬롯으로 100 -> 100 x 2.1 (확률 100%)
                CritSystem.Reset();
                CritSystem.CurrentSlot = Chg;
                float rolled = CritSystem.Apply(null, null, 100f, out bool crit);
                CritSystem.CurrentSlot = SkillUpgradeScope.All;
                Check("충전탄 치명 굴림 (100 -> 210)", rolled, 210f);
                Check("치명 판정 true", crit ? 1f : 0f, 1f);

                // 지속 피해는 치명 안 남
                CritSystem.Suppressed = true;
                CritSystem.CurrentSlot = Chg;
                Check("지속 피해는 치명 제외", CritSystem.Apply(null, null, 100f, out bool dotCrit), 100f);
                CritSystem.Suppressed = false;
                CritSystem.CurrentSlot = SkillUpgradeScope.All;
                Check("지속 피해 치명 false", dotCrit ? 1f : 0f, 0f);

                // 4) 리셋하면 전부 사라짐
                module.ResetTree(tree);
                Check("리셋 후 평타 치명", SkillUpgrades.Bonus(SkillUpgradeKind.CritChance, Atk), 0f);
            }
            finally
            {
                Object.DestroyImmediate(go);
                SkillUpgrades.Board.Clear(module);   // 에디트 모드는 OnDisable 이 안 돌아서 직접 지움
            }

            log.AppendLine(fails == 0 ? "ALL PASS" : $"FAILS = {fails}");
            System.IO.File.WriteAllText("Captures/_skilltree_selftest.txt", log.ToString(), new UTF8Encoding(true));   // 열린 에디터 로그가 다른 프로젝트 것일 때도 결과를 볼 수 있게
            if (fails == 0)
                Debug.Log(log.ToString());
            else
                Debug.LogError(log.ToString());
        }
    }
}


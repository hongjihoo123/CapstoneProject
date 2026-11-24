using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Character;
using Members.KYR._01.Scripts.Currency;
using Members.KYR._01.Scripts.Enchant;
using Members.KYR._01.Scripts.Enchant.Data;
using Members.KYR._01.Scripts.Enchant.So;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Element.Upgrade
{
    // 아웃게임 스킬 트리의 "내 진행도": 씬을 넘어 남고(PlayerPrefs), 지금 캐릭터의 강화를 SkillUpgrades 판에 올림.
    //  - 규칙은 인챈트 트리와 같음: 선행 중 하나라도 배웠으면 열림, 택일 그룹은 하나만, 입구(Entry)는 항상 배운 것으로 침
    //  - 소울(MetaSoul)로 사고, 되돌리면 비용의 70% 만 돌려줌 (반올림). 되돌려서 끊긴 아랫 노드는 같이 풀림(그 몫은 환급 없음)
    //  - 트리 에셋 없이도 판을 복원할 수 있게 "랭크 + 계산된 강화 목록"을 같이 저장함 (게임 씬에는 트리 참조가 없어도 됨)
    //  - 화면(SkillTreeScreen)은 이 클래스만 부르고, 스킬은 SkillUpgrades 판만 읽음
    public static class SkillTreeProgress
    {
        private const string SaveKey = "HJH.SkillTree.v1";
        private const int StartingSoul = 80;   // 첫 실행 때만. 소울 획득처가 생기기 전까지 화면을 써볼 수 있게 (임시)
        private static readonly object BoardSource = new();

        [Serializable]
        private sealed class TreeSave
        {
            public string tree;
            public List<string> ids = new();
            public List<int> ranks = new();
            public List<EntrySave> entries = new();
        }

        [Serializable]
        private struct EntrySave
        {
            public int kind;
            public int scope;
            public float value;
        }

        [Serializable]
        private sealed class Save
        {
            public bool started;
            public int soul;
            public string applied;   // 지금 판에 올라가 있는 트리 (이름)
            public List<TreeSave> trees = new();
        }

        private static Save _save;
        private static bool _hooked;

        public static event Action Changed;

        // ------------------------------------------------------------ 불러오기 / 저장

        public static void EnsureLoaded()
        {
            if (_save != null)
                return;

            _save = new Save();
            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try { JsonUtility.FromJsonOverwrite(json, _save); }
                catch (Exception e) { Debug.LogWarning("[SkillTree] 저장 파일을 못 읽어 새로 시작합니다: " + e.Message); _save = new Save(); }
            }

            if (!_save.started)
            {
                _save.started = true;
                _save.soul = StartingSoul;
                Persist();
            }

            SyncCurrency();
            PushApplied();
        }

        // 소울은 CurrencyService(MetaSoul)가 들고 있고, 저장본과 맞춘다. 서비스는 씬 시작 전에 만들어짐
        private static void SyncCurrency()
        {
            CurrencyService currency = GameBootstrap.Currency;
            if (currency == null)
                return;

            int diff = _save.soul - currency.Get(CurrencyType.MetaSoul);
            if (diff > 0)
                currency.Add(CurrencyType.MetaSoul, diff);
            else if (diff < 0)
                currency.TrySpend(CurrencyType.MetaSoul, -diff);

            if (!_hooked)
            {
                _hooked = true;
                currency.OnChanged += (type, value) =>
                {
                    if (type != CurrencyType.MetaSoul || _save == null || _save.soul == value)
                        return;
                    _save.soul = value;
                    Persist();
                    Changed?.Invoke();
                };
            }
        }

        private static void Persist()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(_save));
            PlayerPrefs.Save();
        }

        // 도메인 리로드 없이 다시 플레이해도 저장본을 새로 읽게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _save = null;
            _hooked = false;
        }

        // 씬이 뜨면 저장된 강화를 판에 올림 (픽 화면을 안 거치고 바로 시작한 테스트 씬에서도 적용되게)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() => EnsureLoaded();

        // ------------------------------------------------------------ 조회

        public static int Soul
        {
            get
            {
                EnsureLoaded();
                return _save.soul;
            }
        }

        public static int Rank(EnchantTree tree, EnchantTreeNode node)
        {
            if (tree == null || node == null || string.IsNullOrEmpty(node.id))
                return 0;
            EnsureLoaded();
            TreeSave save = Find(tree, false);
            if (save == null)
                return 0;
            int i = save.ids.IndexOf(node.id);
            return i < 0 ? 0 : save.ranks[i];
        }

        public static int SpentInTab(EnchantTree tree, string tab)
        {
            int sum = 0;
            foreach (EnchantTreeNode node in tree.Nodes)
            {
                if (node != null && SameTab(node.tab, tab))
                    sum += Rank(tree, node) * Mathf.Max(0, node.cost);
            }
            return sum;
        }

        public static bool IsOpen(EnchantTree tree, EnchantTreeNode node)
        {
            if (tree == null || node == null)
                return false;
            if (!tree.HasParents(node.id))
                return true;   // 선행이 없는 노드(계열 입구)는 처음부터 배울 수 있음

            foreach (EnchantTreeLink link in tree.Links)
            {
                if (link != null && link.childId == node.id && Rank(tree, tree.FindNode(link.parentId)) > 0)
                    return true;
            }
            return false;
        }

        public static bool IsBlockedByExclusive(EnchantTree tree, EnchantTreeNode node)
        {
            if (tree == null || node == null || string.IsNullOrEmpty(node.exclusiveGroup))
                return false;

            foreach (EnchantTreeNode other in tree.Nodes)
            {
                if (other != null && other.id != node.id && other.exclusiveGroup == node.exclusiveGroup && Rank(tree, other) > 0)
                    return true;
            }
            return false;
        }

        public static EnchantLearnResult CanLearn(EnchantTree tree, EnchantTreeNode node)
        {
            if (tree == null || node == null || string.IsNullOrEmpty(node.id))
                return EnchantLearnResult.Invalid;
            if (Rank(tree, node) >= Mathf.Max(1, node.maxRank))
                return EnchantLearnResult.MaxRank;
            if (!IsOpen(tree, node))
                return EnchantLearnResult.Locked;
            if (IsBlockedByExclusive(tree, node))
                return EnchantLearnResult.Exclusive;
            if (node.cost > 0 && Soul < node.cost)
                return EnchantLearnResult.NotEnoughCurrency;
            return EnchantLearnResult.Ok;
        }

        // 되돌리면 돌려받는 소울 (비용의 70%, 반올림. 5점 노드 = 3점)
        public static int RefundOf(EnchantTreeNode node) =>
            node == null ? 0 : Mathf.CeilToInt(Mathf.Max(0, node.cost) * 0.7f - 0.5f);

        public static bool CanRefund(EnchantTree tree, EnchantTreeNode node) =>
            tree != null && node != null && Rank(tree, node) > 0;

        // ------------------------------------------------------------ 배우기 / 되돌리기

        public static bool TryLearn(EnchantTree tree, EnchantTreeNode node)
        {
            if (CanLearn(tree, node) != EnchantLearnResult.Ok)
                return false;

            if (node.cost > 0 && !Spend(node.cost))
                return false;

            SetRank(Find(tree, true), node.id, Rank(tree, node) + 1);
            Commit(tree);
            return true;
        }

        public static bool TryRefund(EnchantTree tree, EnchantTreeNode node)
        {
            if (!CanRefund(tree, node))
                return false;

            TreeSave save = Find(tree, true);
            SetRank(save, node.id, Rank(tree, node) - 1);
            Refund(RefundOf(node));
            Prune(tree, save);   // 이 노드를 지나야만 열리던 아랫 노드는 같이 풀림
            Commit(tree);
            return true;
        }

        // 전부 되돌림 (환급 규칙은 같음)
        public static void RefundAll(EnchantTree tree)
        {
            if (tree == null)
                return;

            TreeSave save = Find(tree, false);
            if (save == null)
                return;

            int back = 0;
            for (int i = 0; i < save.ids.Count; i++)
            {
                EnchantTreeNode node = tree.FindNode(save.ids[i]);
                back += RefundOf(node) * save.ranks[i];
            }
            save.ids.Clear();
            save.ranks.Clear();
            Refund(back);
            Commit(tree);
        }

        // 지금 캐릭터의 트리를 판에 올림 (CharacterPick 의 게임 시작 때). tree 가 null 이면 비움
        public static void Apply(EnchantTree tree)
        {
            EnsureLoaded();
            _save.applied = tree != null ? tree.name : null;
            Persist();
            PushApplied();
        }

        // ------------------------------------------------------------ 안쪽

        private static bool Spend(int amount)
        {
            CurrencyService currency = GameBootstrap.Currency;
            if (currency != null)
                return currency.TrySpend(CurrencyType.MetaSoul, amount);   // OnChanged 가 저장본을 맞춤
            if (_save.soul < amount)
                return false;
            _save.soul -= amount;
            return true;
        }

        private static void Refund(int amount)
        {
            if (amount <= 0)
                return;
            CurrencyService currency = GameBootstrap.Currency;
            if (currency != null)
                currency.Add(CurrencyType.MetaSoul, amount);
            else
                _save.soul += amount;
        }

        private static void Prune(EnchantTree tree, TreeSave save)
        {
            // 입구에서 이어진 노드만 남김
            var reachable = new HashSet<string>();
            foreach (EnchantTreeNode node in tree.Nodes)
            {
                if (node != null && !tree.HasParents(node.id))
                    reachable.Add(node.id);
            }

            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (EnchantTreeLink link in tree.Links)
                {
                    if (link == null || reachable.Contains(link.childId) || !reachable.Contains(link.parentId))
                        continue;
                    if (RankIn(save, link.childId) > 0)
                    {
                        reachable.Add(link.childId);
                        changed = true;
                    }
                }
            }

            for (int i = save.ids.Count - 1; i >= 0; i--)
            {
                if (!reachable.Contains(save.ids[i]))
                {
                    Refund(RefundOf(tree.FindNode(save.ids[i])) * save.ranks[i]);   // 끊긴 노드는 환급하고 풀림
                    save.ids.RemoveAt(i);
                    save.ranks.RemoveAt(i);
                }
            }
        }

        private static int RankIn(TreeSave save, string id)
        {
            int i = save.ids.IndexOf(id);
            return i < 0 ? 0 : save.ranks[i];
        }

        private static void SetRank(TreeSave save, string id, int rank)
        {
            int i = save.ids.IndexOf(id);
            if (rank <= 0)
            {
                if (i >= 0)
                {
                    save.ids.RemoveAt(i);
                    save.ranks.RemoveAt(i);
                }
                return;
            }
            if (i < 0)
            {
                save.ids.Add(id);
                save.ranks.Add(rank);
            }
            else
            {
                save.ranks[i] = rank;
            }
        }

        // 트리 하나의 강화 목록을 다시 계산해 저장하고, 지금 올라가 있는 트리면 판도 갱신
        private static void Commit(EnchantTree tree)
        {
            TreeSave save = Find(tree, true);
            save.entries.Clear();
            for (int i = 0; i < save.ids.Count; i++)
            {
                EnchantTreeNode node = tree.FindNode(save.ids[i]);
                if (node == null)
                    continue;
                foreach (EnchantSkillEffect effect in node.SkillEffects)
                {
                    if (effect != null)
                        save.entries.Add(new EntrySave { kind = (int)effect.kind, scope = (int)effect.scope, value = effect.ValueAt(save.ranks[i]) });
                }
            }

            Persist();
            if (_save.applied == tree.name)
                PushApplied();
            Changed?.Invoke();
        }

        private static void PushApplied()
        {
            TreeSave save = string.IsNullOrEmpty(_save.applied) ? null : _save.trees.Find(t => t.tree == _save.applied);
            if (save == null)
            {
                SkillUpgrades.Board.Clear(BoardSource);
                return;
            }

            var entries = new List<SkillUpgradeBoard.Entry>();
            foreach (EntrySave e in save.entries)
                entries.Add(new SkillUpgradeBoard.Entry((SkillUpgradeKind)e.kind, (SkillUpgradeScope)e.scope, e.value));
            SkillUpgrades.Board.Set(BoardSource, entries);
        }

        private static TreeSave Find(EnchantTree tree, bool create)
        {
            EnsureLoaded();
            TreeSave save = _save.trees.Find(t => t.tree == tree.name);
            if (save == null && create)
            {
                save = new TreeSave { tree = tree.name };
                _save.trees.Add(save);
            }
            return save;
        }

        public static bool SameTab(string a, string b) => string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.Ordinal);

        // 트리의 탭 이름들 (노드에 처음 나온 순서). 탭이 하나도 안 적혀 있으면 빈 이름 하나
        public static List<string> TabsOf(EnchantTree tree)
        {
            var tabs = new List<string>();
            if (tree == null)
                return tabs;
            foreach (EnchantTreeNode node in tree.Nodes)
            {
                if (node != null && !tabs.Exists(t => SameTab(t, node.tab)))
                    tabs.Add(node.tab ?? string.Empty);
            }
            return tabs;
        }

        // 테스트/디버그용: 소울 지급
        public static void Grant(int amount)
        {
            EnsureLoaded();
            if (amount > 0)
                Refund(amount);
            Changed?.Invoke();
        }

        // 테스트/디버그용: 저장 전부 지우고 처음 상태로
        public static void WipeAll()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            SkillUpgrades.Board.Clear(BoardSource);
            CurrencyService currency = GameBootstrap.Currency;
            _save = null;
            if (currency != null)
                currency.TrySpend(CurrencyType.MetaSoul, currency.Get(CurrencyType.MetaSoul));
            EnsureLoaded();
            Changed?.Invoke();
        }
    }
}

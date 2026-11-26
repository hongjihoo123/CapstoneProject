using System;
using System.Collections.Generic;
using Members.KYR._01.Scripts.Currency;
using Members.KYR._01.Scripts.Enchant;
using Members.KYR._01.Scripts.Enchant.So;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Element.Upgrade
{
    public static class SkillTreeProgress
    {
        private const string SaveKey = "HJH.SkillTree.v1";
        private const int StartingSoul = 80;
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
            public string applied;
            public List<TreeSave> trees = new();
        }

        private static Save _save;
        private static bool _hooked;

        public static event Action Changed;

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _save = null;
            _hooked = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() => EnsureLoaded();

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
                return true;

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

        public static int RefundOf(EnchantTreeNode node) =>
            node == null ? 0 : Mathf.CeilToInt(Mathf.Max(0, node.cost) * 0.7f - 0.5f);

        public static bool CanRefund(EnchantTree tree, EnchantTreeNode node) =>
            tree != null && node != null && Rank(tree, node) > 0;

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
            Prune(tree, save);
            Commit(tree);
            return true;
        }

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

        public static void Apply(EnchantTree tree)
        {
            EnsureLoaded();
            _save.applied = tree != null ? tree.name : null;
            Persist();
            PushApplied();
        }

        private static bool Spend(int amount)
        {
            CurrencyService currency = GameBootstrap.Currency;
            if (currency != null)
                return currency.TrySpend(CurrencyType.MetaSoul, amount);
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
                    Refund(RefundOf(tree.FindNode(save.ids[i])) * save.ranks[i]);
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

        public static void Grant(int amount)
        {
            EnsureLoaded();
            if (amount > 0)
                Refund(amount);
            Changed?.Invoke();
        }

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

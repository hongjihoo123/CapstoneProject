using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Character;
using Members.JJH._02_Scripts.Systems.ModuleSystem;
using Members.KYR._01.Scripts.Currency;
using Members.KYR._01.Scripts.Enchant.Data;
using Members.KYR._01.Scripts.Enchant.So;
using Members.KYR._01.Scripts.Modules;
using Members.KYR._01.Scripts.Player;
using Members.KYR._01.Scripts.Stats;
using UnityEngine;

namespace Members.KYR._01.Scripts.Enchant
{
    public enum EnchantLearnResult
    {
        Ok,
        Invalid,
        MaxRank,
        Locked,
        Exclusive,
        NotEnoughCurrency
    }

    // 캐릭터별 인챈트 트리 진행도(노드별 랭크)를 들고, 지금 캐릭터 트리의 효과를 플레이어 스탯에 건다.
    // 플레이어 밑에 붙이면 모듈로 초기화되고, 따로 둬도 PlayerAgent/CharacterSwitcher를 찾아 쓴다.
    public class EnchantModule : AbstractModule
    {
        [SerializeField] private EnchantTree[] trees = Array.Empty<EnchantTree>();
        [SerializeField] private CharacterSwitcher switcher;
        [SerializeField] private PlayerAgent player;
        [SerializeField, Tooltip("테스트용: 비용 없이 배운다")] private bool ignoreCost;

        private readonly Dictionary<EnchantTree, Dictionary<string, int>> _ranks = new();
        private EnchantTree _applied;
        private bool _subscribed;

        public event Action<EnchantTree, EnchantTreeNode, int> RankChanged;
        public event Action<EnchantTree> ActiveTreeChanged;

        public IReadOnlyList<EnchantTree> Trees => trees;
        public CharacterData CurrentCharacter => switcher != null ? switcher.Current : null;
        public EnchantTree ActiveTree => TreeFor(CurrentCharacter);

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            if (player == null)
                player = owner as PlayerAgent;
        }

        private void Awake()
        {
            if (switcher == null)
                switcher = FindFirstObjectByType<CharacterSwitcher>();
            if (player == null)
                player = FindFirstObjectByType<PlayerAgent>();
        }

        private void OnEnable()
        {
            if (switcher != null && !_subscribed)
            {
                switcher.CharacterChanged += HandleCharacterChanged;
                _subscribed = true;
            }

            // 껐다 켠 경우 다시 건다. 처음 켜질 때는 스위처가 아직 캐릭터를 안 골랐으므로 건너뛴다.
            if (switcher != null && switcher.Current != null)
                ApplyStats(TreeFor(switcher.Current));
        }

        private void Start()
        {
            // 스위처가 없으면 첫 트리를 바로 건다. 있으면 스위처 Start의 CharacterChanged에서 건다.
            if (switcher == null)
                ApplyStats(ActiveTree);
        }

        private void OnDisable()
        {
            if (switcher != null && _subscribed)
            {
                switcher.CharacterChanged -= HandleCharacterChanged;
                _subscribed = false;
            }

            ApplyStats(null);
        }

        public EnchantTree TreeFor(CharacterData character)
        {
            if (trees == null)
                return null;

            EnchantTree first = null;
            for (int i = 0; i < trees.Length; i++)
            {
                EnchantTree tree = trees[i];
                if (tree == null)
                    continue;

                if (first == null)
                    first = tree;
                if (character != null && tree.Character == character)
                    return tree;
            }

            return character == null ? first : null;
        }

        public int GetRank(EnchantTree tree, string nodeId)
        {
            if (tree == null || string.IsNullOrEmpty(nodeId))
                return 0;

            return _ranks.TryGetValue(tree, out Dictionary<string, int> ranks) && ranks.TryGetValue(nodeId, out int rank)
                ? rank
                : 0;
        }

        // 입구 노드는 항상 열려 있고, 나머지는 선행 노드 중 하나라도 배웠으면 열린다.
        // 선행이 없는 일반 노드는 열리지 않는다 (트리는 반드시 입구에서 시작).
        public bool IsOpen(EnchantTree tree, EnchantTreeNode node)
        {
            if (tree == null || node == null)
                return false;
            if (node.kind == EnchantNodeKind.Entry)
                return true;

            foreach (EnchantTreeLink link in tree.Links)
            {
                if (link != null && link.childId == node.id && GetRank(tree, link.parentId) > 0)
                    return true;
            }

            return false;
        }

        public bool IsBlockedByExclusive(EnchantTree tree, EnchantTreeNode node)
        {
            if (tree == null || node == null || string.IsNullOrEmpty(node.exclusiveGroup))
                return false;

            foreach (EnchantTreeNode other in tree.Nodes)
            {
                if (other == null || other.id == node.id || other.exclusiveGroup != node.exclusiveGroup)
                    continue;
                if (GetRank(tree, other.id) > 0)
                    return true;
            }

            return false;
        }

        public EnchantLearnResult CanLearn(EnchantTree tree, EnchantTreeNode node)
        {
            if (tree == null || node == null || string.IsNullOrEmpty(node.id))
                return EnchantLearnResult.Invalid;
            if (GetRank(tree, node.id) >= Mathf.Max(1, node.maxRank))
                return EnchantLearnResult.MaxRank;
            if (!IsOpen(tree, node))
                return EnchantLearnResult.Locked;
            if (IsBlockedByExclusive(tree, node))
                return EnchantLearnResult.Exclusive;
            if (!ignoreCost && node.cost > 0)
            {
                CurrencyService currency = GameBootstrap.Currency;
                if (currency == null || !currency.CanAfford(tree.CostCurrency, node.cost))
                    return EnchantLearnResult.NotEnoughCurrency;
            }

            return EnchantLearnResult.Ok;
        }

        public bool TryLearn(EnchantTree tree, EnchantTreeNode node)
        {
            if (CanLearn(tree, node) != EnchantLearnResult.Ok)
                return false;

            if (!ignoreCost && node.cost > 0 && !GameBootstrap.Currency.TrySpend(tree.CostCurrency, node.cost))
                return false;

            int rank = GetRank(tree, node.id) + 1;
            RanksOf(tree)[node.id] = rank;

            if (tree == _applied)
                ApplyStats(tree);

            RankChanged?.Invoke(tree, node, rank);
            return true;
        }

        // 진행도를 통째로 지운다 (리셋/세이브 불러오기 전용, 비용 환급 없음).
        public void ResetTree(EnchantTree tree)
        {
            if (tree == null || !_ranks.Remove(tree))
                return;

            if (tree == _applied)
                ApplyStats(tree);

            RankChanged?.Invoke(tree, null, 0);
        }

        public IReadOnlyDictionary<string, int> RanksSnapshot(EnchantTree tree)
        {
            return new Dictionary<string, int>(RanksOf(tree));
        }

        public void LoadRanks(EnchantTree tree, IReadOnlyDictionary<string, int> ranks)
        {
            if (tree == null)
                return;

            Dictionary<string, int> target = RanksOf(tree);
            target.Clear();
            if (ranks != null)
            {
                foreach (KeyValuePair<string, int> pair in ranks)
                {
                    EnchantTreeNode node = tree.FindNode(pair.Key);
                    if (node != null && pair.Value > 0)
                        target[pair.Key] = Mathf.Min(pair.Value, Mathf.Max(1, node.maxRank));
                }
            }

            if (tree == _applied)
                ApplyStats(tree);

            RankChanged?.Invoke(tree, null, 0);
        }

        private void HandleCharacterChanged(CharacterData character)
        {
            EnchantTree tree = TreeFor(character);
            ApplyStats(tree);
            ActiveTreeChanged?.Invoke(tree);
        }

        // 트리 에셋 자체를 모디파이어 출처로 써서, 지울 때 그 트리 것만 정확히 빠진다.
        private void ApplyStats(EnchantTree tree)
        {
            PlayerStatsModule stats = player != null ? player.Stats : null;
            if (stats == null || stats.Tree == null)
            {
                _applied = tree;
                return;
            }

            if (_applied != null)
                stats.RemoveModifiers(_applied);
            if (tree != null && tree != _applied)
                stats.RemoveModifiers(tree);

            _applied = tree;
            if (tree == null || !_ranks.TryGetValue(tree, out Dictionary<string, int> ranks))
                return;

            foreach (KeyValuePair<string, int> pair in ranks)
            {
                EnchantTreeNode node = tree.FindNode(pair.Key);
                if (node == null || pair.Value <= 0)
                    continue;

                foreach (EnchantStatEffect effect in node.Effects)
                {
                    if (effect == null || effect.stat == PlayerStatId.None)
                        continue;

                    stats.AddModifier(effect.stat, new StatModifier(tree, effect.type, effect.ValueAt(pair.Value)));
                }
            }
        }

        private Dictionary<string, int> RanksOf(EnchantTree tree)
        {
            if (!_ranks.TryGetValue(tree, out Dictionary<string, int> ranks))
            {
                ranks = new Dictionary<string, int>();
                _ranks.Add(tree, ranks);
            }

            return ranks;
        }
    }
}

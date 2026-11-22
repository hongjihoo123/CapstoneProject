using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Character;
using Members.KYR._01.Scripts.Currency;
using Members.KYR._01.Scripts.Enchant.Data;
using Members.KYR._01.Scripts.Stats;
using UnityEngine;

namespace Members.KYR._01.Scripts.Enchant.So
{
    // 캐릭터 하나의 인챈트 트리. 연결은 선행이고, 한 노드로 선이 여러 개 들어오면 그중 하나만 있으면 된다.
    // 들어오는 선이 없는 노드는 처음부터 배울 수 있다.
    [CreateAssetMenu(fileName = "EnchantTree", menuName = "SO/Enchant/EnchantTree", order = 10)]
    public class EnchantTree : ScriptableObject
    {
        [SerializeField] private CharacterData character;
        [SerializeField] private CurrencyType costCurrency = CurrencyType.MetaSoul;
        [SerializeField] private List<EnchantTreeNode> nodes = new List<EnchantTreeNode>();
        [SerializeField] private List<EnchantTreeLink> links = new List<EnchantTreeLink>();

        public CharacterData Character
        {
            get => character;
            set => character = value;
        }

        public CurrencyType CostCurrency
        {
            get => costCurrency;
            set => costCurrency = value;
        }

        public List<EnchantTreeNode> Nodes
        {
            get
            {
                if (nodes == null)
                    nodes = new List<EnchantTreeNode>();
                return nodes;
            }
        }

        public List<EnchantTreeLink> Links
        {
            get
            {
                if (links == null)
                    links = new List<EnchantTreeLink>();
                return links;
            }
        }

        public EnchantTreeNode FindNode(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            List<EnchantTreeNode> list = Nodes;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].id == id)
                    return list[i];
            }

            return null;
        }

        public int IndexOf(string id)
        {
            List<EnchantTreeNode> list = Nodes;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].id == id)
                    return i;
            }

            return -1;
        }

        public bool HasParents(string id)
        {
            List<EnchantTreeLink> list = Links;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].childId == id)
                    return true;
            }

            return false;
        }

        // 지워진 노드의 ID가 다시 나오지 않도록 순번 대신 무작위 ID를 쓴다.
        public string NewNodeId()
        {
            string id;
            do
            {
                id = Guid.NewGuid().ToString("N").Substring(0, 8);
            } while (FindNode(id) != null);

            return id;
        }

        private void OnEnable()
        {
            if (nodes == null)
                nodes = new List<EnchantTreeNode>();
            if (links == null)
                links = new List<EnchantTreeLink>();
        }
    }

    [Serializable]
    public class EnchantTreeNode
    {
        public string id;
        public string displayName;
        public EnchantNodeKind kind = EnchantNodeKind.Stat;
        public int maxRank = 1;
        [Tooltip("랭크 1개당 비용")]
        public int cost = 1;
        [Tooltip("같은 그룹 노드는 하나만 배울 수 있다. 비우면 제한 없음")]
        public string exclusiveGroup;
        public string description;
        public List<EnchantStatEffect> effects = new List<EnchantStatEffect>();
        public Vector2 position;

        public List<EnchantStatEffect> Effects
        {
            get
            {
                if (effects == null)
                    effects = new List<EnchantStatEffect>();
                return effects;
            }
        }
    }

    [Serializable]
    public class EnchantStatEffect
    {
        public PlayerStatId stat = PlayerStatId.Damage;
        public StatModifierType type = StatModifierType.PercentAdd;
        [Tooltip("랭크 1개당 값. Flat은 그대로, PercentAdd/PercentMore는 0.05 = 5%")]
        public float valuePerRank = 0.05f;

        public float ValueAt(int rank) => valuePerRank * Mathf.Max(0, rank);
    }

    [Serializable]
    public class EnchantTreeLink
    {
        public string parentId;
        public string childId;
    }
}

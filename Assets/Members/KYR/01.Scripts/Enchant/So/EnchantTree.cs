using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Character;
using Members.KYR._01.Scripts.Enchant.Data;
using UnityEngine;

namespace Members.KYR._01.Scripts.Enchant.So
{
    // 캐릭터 하나의 인챈트 트리. 연결은 선행이고, 한 노드로 선이 여러 개 들어오면 그중 하나만 있으면 된다.
    [CreateAssetMenu(fileName = "EnchantTree", menuName = "SO/Enchant/EnchantTree", order = 10)]
    public class EnchantTree : ScriptableObject
    {
        [SerializeField] private CharacterData character;
        [SerializeField] private List<EnchantTreeNode> nodes = new List<EnchantTreeNode>();
        [SerializeField] private List<EnchantTreeLink> links = new List<EnchantTreeLink>();

        public CharacterData Character
        {
            get => character;
            set => character = value;
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
        public int cost = 1;
        public string exclusiveGroup;
        public string description;
        public Vector2 position;
    }

    [Serializable]
    public class EnchantTreeLink
    {
        public string parentId;
        public string childId;
    }
}

using Members.KYR._01.Scripts.Enchant.Data;
using Members.KYR._01.Scripts.Enchant.So;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Members.KYR._01.Scripts.Enchant.Editor
{
    public sealed class EnchantTreeNodeView : Node
    {
        private readonly EnchantTreeGraphView _graph;
        private readonly Label _info;

        public EnchantTreeNode Data { get; }

        public Port Input { get; }
        public Port Output { get; }

        public EnchantTreeNodeView(EnchantTreeGraphView graph, EnchantTreeNode data)
        {
            _graph = graph;
            Data = data;

            Input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            Output = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            Input.portName = string.Empty;
            Output.portName = string.Empty;
            inputContainer.Add(Input);
            outputContainer.Add(Output);
            capabilities &= ~Capabilities.Copiable;

            _info = new Label();
            _info.style.fontSize = 11;
            _info.style.color = new Color(0.9f, 0.9f, 0.88f);
            _info.style.whiteSpace = WhiteSpace.Normal;
            _info.style.paddingLeft = 8;
            _info.style.paddingRight = 8;
            _info.style.paddingTop = 4;
            _info.style.paddingBottom = 6;
            extensionContainer.Add(_info);

            style.width = 180;
            RefreshExpandedState();
            RefreshPorts();
            Refresh();
            SetPosition(new Rect(data.position, new Vector2(180f, 96f)));

            RegisterCallback<PointerUpEvent>(_ => _graph.EndMove());
            RegisterCallback<PointerCaptureOutEvent>(_ => _graph.EndMove());
        }

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
            if (_graph.MuteWrites || Data == null)
                return;

            Vector2 pos = newPos.position;
            if (Data.position == pos)
                return;

            _graph.RecordMove();
            Data.position = pos;
            _graph.DirtyTree();
        }

        public void Refresh()
        {
            title = string.IsNullOrEmpty(Data.displayName) ? Data.id : Data.displayName;
            titleContainer.style.backgroundColor = EnchantNodeKindStyle.ColorOf(Data.kind);

            Label titleLabel = this.Q<Label>("title-label");
            if (titleLabel != null)
                titleLabel.style.color = Color.white;

            string text = EnchantNodeKindStyle.LabelOf(Data.kind) + "   최대 " + Data.maxRank + "   비용 " + Data.cost;
            if (!string.IsNullOrEmpty(Data.exclusiveGroup))
                text += "\n배타 " + Data.exclusiveGroup;
            string effects = EnchantText.Effects(Data, -1);
            if (!string.IsNullOrEmpty(effects))
                text += "\n" + effects;
            _info.text = text;
        }
    }

    static class EnchantNodeKindStyle
    {
        public static string LabelOf(EnchantNodeKind kind)
        {
            switch (kind)
            {
                case EnchantNodeKind.Entry: return "입구";
                case EnchantNodeKind.Stat: return "스탯";
                case EnchantNodeKind.Ability: return "특성";
                case EnchantNodeKind.Key: return "핵심";
                case EnchantNodeKind.Cooldown: return "쿨";
                case EnchantNodeKind.Element: return "원소";
                default: return kind.ToString();
            }
        }

        public static Color ColorOf(EnchantNodeKind kind)
        {
            switch (kind)
            {
                case EnchantNodeKind.Entry: return new Color(0.62f, 0.46f, 0.16f);
                case EnchantNodeKind.Stat: return new Color(0.22f, 0.38f, 0.58f);
                case EnchantNodeKind.Ability: return new Color(0.48f, 0.28f, 0.56f);
                case EnchantNodeKind.Key: return new Color(0.62f, 0.30f, 0.16f);
                case EnchantNodeKind.Cooldown: return new Color(0.14f, 0.42f, 0.46f);
                case EnchantNodeKind.Element: return new Color(0.18f, 0.46f, 0.30f);
                default: return new Color(0.25f, 0.25f, 0.25f);
            }
        }
    }
}

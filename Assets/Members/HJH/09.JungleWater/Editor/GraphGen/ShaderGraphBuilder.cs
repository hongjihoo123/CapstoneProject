using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph;
using HlslSourceType = UnityEditor.ShaderGraph.Drawing.HlslSourceType;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using URPSG = UnityEditor.Rendering.Universal.ShaderGraph;

namespace Assets.Members.HJH.Environment.GraphGen
{
    // Shader Graph 를 코드로 만드는 작은 도우미.
    // Shader Graph 내부 API 를 쓰려고 이 폴더의 어셈블리 이름이 "Unity.VisualEffectGraph.EditorTests" 다.
    // (Shader Graph·URP 가 이 이름에만 내부 API 를 열어 둠. VFX Graph 패키지를 설치하면 이름이 겹치니 그때는 이 폴더를 지울 것)
    sealed class ShaderGraphBuilder
    {
        private const float ColumnWidth = 230f;
        private const float RowHeight = 150f;

        public readonly GraphData Graph;
        private readonly string _includeGuid;
        private int _nextOutputId = 1;

        private ShaderGraphBuilder(GraphData graph, string includeGuid)
        {
            Graph = graph;
            _includeGuid = includeGuid;
        }

        // ---------------- 그래프 만들기 ----------------

        public static ShaderGraphBuilder SubGraph(string includeGuid)
        {
            var graph = new GraphData { isSubGraph = true };
            var output = new SubGraphOutputNode();
            graph.AddNode(output);
            graph.outputNode = output;
            graph.path = "HJH/JungleWater";
            return new ShaderGraphBuilder(graph, includeGuid);
        }

        public static ShaderGraphBuilder UnlitTransparent(string includeGuid, bool bothFaces, bool needsLighting)
        {
            var target = (URPSG.UniversalTarget)Activator.CreateInstance(typeof(URPSG.UniversalTarget));
            target.TrySetActiveSubTarget(typeof(URPSG.UniversalUnlitSubTarget));
            target.surfaceType = URPSG.SurfaceType.Transparent;
            target.alphaMode = URPSG.AlphaMode.Alpha;
            target.renderFace = bothFaces ? URPSG.RenderFace.Both : URPSG.RenderFace.Front;
            target.zWriteControl = URPSG.ZWriteControl.ForceDisabled;
            target.castShadows = false;
            ((URPSG.UniversalUnlitSubTarget)target.activeSubTarget).keepLightingVariants = needsLighting;

            var graph = new GraphData();
            graph.AddContexts();
            graph.InitializeOutputs(new Target[] { target }, new[]
            {
                BlockFields.VertexDescription.Position,
                BlockFields.VertexDescription.Normal,
                BlockFields.VertexDescription.Tangent,
                BlockFields.SurfaceDescription.BaseColor,
                BlockFields.SurfaceDescription.Alpha,
            });
            graph.path = "HJH/JungleWater";
            return new ShaderGraphBuilder(graph, includeGuid);
        }

        // 빛·그림자를 받는 불투명 + 알파 잘라내기 (잎 카드용). 양면.
        public static ShaderGraphBuilder LitCutout(string includeGuid)
        {
            var target = (URPSG.UniversalTarget)Activator.CreateInstance(typeof(URPSG.UniversalTarget));
            target.TrySetActiveSubTarget(typeof(URPSG.UniversalLitSubTarget));
            target.surfaceType = URPSG.SurfaceType.Opaque;
            target.alphaClip = true;
            target.renderFace = URPSG.RenderFace.Both;
            target.castShadows = true;
            target.receiveShadows = true;

            var graph = new GraphData();
            graph.AddContexts();
            graph.InitializeOutputs(new Target[] { target }, new[]
            {
                BlockFields.VertexDescription.Position,
                BlockFields.VertexDescription.Normal,
                BlockFields.VertexDescription.Tangent,
                BlockFields.SurfaceDescription.BaseColor,
                BlockFields.SurfaceDescription.NormalTS,
                BlockFields.SurfaceDescription.Metallic,
                BlockFields.SurfaceDescription.Smoothness,
                BlockFields.SurfaceDescription.Emission,
                BlockFields.SurfaceDescription.Occlusion,
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
            });
            graph.path = "HJH/JungleWater";
            return new ShaderGraphBuilder(graph, includeGuid);
        }

        public void Save(string path)
        {
            Graph.AddCategory(CategoryData.DefaultCategory(Graph.properties.Cast<ShaderInput>().ToList()));
            Graph.ValidateGraph();
            FileUtilities.WriteShaderGraphToDisk(path, Graph);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        // ---------------- 노드 ----------------

        public T Node<T>(float col, float row, Action<T> setup = null) where T : AbstractMaterialNode, new()
        {
            var node = new T();
            setup?.Invoke(node);
            Place(node, col, row);
            Graph.AddNode(node);
            return node;
        }

        public SubGraphNode Sub(SubGraphAsset asset, float col, float row)
        {
            var node = new SubGraphNode();
            node.asset = asset;
            Place(node, col, row);
            Graph.AddNode(node);
            return node;
        }

        public CustomFunctionNode Func(string functionName, float col, float row,
            (string name, SlotValueType type)[] inputs, (string name, SlotValueType type)[] outputs)
        {
            var node = new CustomFunctionNode
            {
                sourceType = HlslSourceType.File,
                functionName = functionName,
                functionSource = _includeGuid,
            };

            int id = 0;
            foreach (var (name, type) in inputs)
                node.AddSlot(MaterialSlot.CreateMaterialSlot(type, id++, name, name, SlotType.Input, UnityEngine.Vector4.zero));
            foreach (var (name, type) in outputs)
                node.AddSlot(MaterialSlot.CreateMaterialSlot(type, id++, name, name, SlotType.Output, UnityEngine.Vector4.zero));

            Place(node, col, row);
            Graph.AddNode(node);
            return node;
        }

        public PropertyNode Prop(AbstractShaderProperty property, float col, float row)
        {
            var node = new PropertyNode();
            Place(node, col, row);
            Graph.AddNode(node);
            node.property = property;
            return node;
        }

        // 상수 벡터 노드 (Dynamic 슬롯에 벡터 상수를 직접 넣으면 차원이 꼬일 수 있어서 노드로 둔다)
        public Vector2Node Vec2(float x, float y, float col, float row) => Node<Vector2Node>(col, row, n =>
        {
            Set(n, "X", x);
            Set(n, "Y", y);
        });

        public Vector3Node Vec3(float x, float y, float z, float col, float row) => Node<Vector3Node>(col, row, n =>
        {
            Set(n, "X", x);
            Set(n, "Y", y);
            Set(n, "Z", z);
        });

        private static void Place(AbstractMaterialNode node, float col, float row)
        {
            DrawState state = node.drawState;
            state.position = new Rect(col * ColumnWidth, row * RowHeight, 0f, 0f);
            state.expanded = true;
            node.drawState = state;
        }

        // ---------------- 연결 ----------------

        // from 노드의 출력 → to 노드의 입력.  출력 이름이 null 이면 첫 번째 출력
        public void Link(AbstractMaterialNode from, string outName, AbstractMaterialNode to, string inName)
        {
            MaterialSlot outSlot = FindSlot(from, outName, input: false);
            MaterialSlot inSlot = FindSlot(to, inName, input: true);
            Graph.Connect(from.GetSlotReference(outSlot.id), to.GetSlotReference(inSlot.id));
        }

        public void Link(AbstractMaterialNode from, AbstractMaterialNode to, string inName) => Link(from, null, to, inName);

        // 입력 슬롯 기본값 (스칼라는 모든 성분에 같은 값)
        public static void Set(AbstractMaterialNode node, string inName, float value) =>
            Set(node, inName, new Vector4(value, value, value, value));

        public static void Set(AbstractMaterialNode node, string inName, Vector4 v)
        {
            MaterialSlot slot = FindSlot(node, inName, input: true);
            switch (slot)
            {
                case Vector1MaterialSlot s: s.value = v.x; break;
                case Vector2MaterialSlot s: s.value = v; break;
                case Vector3MaterialSlot s: s.value = v; break;
                case Vector4MaterialSlot s: s.value = v; break;
                case DynamicVectorMaterialSlot s: s.value = v; break;
                case DynamicValueMaterialSlot s:
                    Matrix4x4 m = Matrix4x4.zero;
                    m.SetRow(0, v);
                    s.value = m;
                    break;
                default:
                    throw new InvalidOperationException($"{node.name}.{inName}: 기본값을 넣을 수 없는 슬롯 ({slot.GetType().Name})");
            }
        }

        private static MaterialSlot FindSlot(AbstractMaterialNode node, string name, bool input)
        {
            var slots = new List<MaterialSlot>();
            if (input)
                node.GetInputSlots(slots);
            else
                node.GetOutputSlots(slots);

            if (name == null && slots.Count > 0)
                return slots[0];

            MaterialSlot found = slots.FirstOrDefault(s => s.RawDisplayName() == name)
                                 ?? slots.FirstOrDefault(s => s.shaderOutputName == name);
            if (found == null)
                throw new InvalidOperationException(
                    $"[JungleWater 그래프] {node.name}: '{name}' {(input ? "입력" : "출력")} 슬롯이 없음. 있는 것: {string.Join(", ", slots.Select(s => s.RawDisplayName()))}");
            return found;
        }

        // ---------------- 프로퍼티 ----------------

        public Vector1ShaderProperty FloatProp(string displayName, float value, string reference = null, Vector2? range = null)
        {
            var p = new Vector1ShaderProperty { displayName = displayName, value = value };
            if (range.HasValue)
            {
                p.floatType = FloatType.Slider;
                p.rangeValues = range.Value;
            }
            return AddInput(p, reference);
        }

        public ColorShaderProperty ColorProp(string displayName, Color value, string reference = null) =>
            AddInput(new ColorShaderProperty { displayName = displayName, value = value }, reference);

        public Vector2ShaderProperty Vec2Prop(string displayName, string reference = null) =>
            AddInput(new Vector2ShaderProperty { displayName = displayName }, reference);

        public Vector3ShaderProperty Vec3Prop(string displayName, string reference = null) =>
            AddInput(new Vector3ShaderProperty { displayName = displayName }, reference);

        public Vector4ShaderProperty Vec4Prop(string displayName, Vector4 value = default, string reference = null) =>
            AddInput(new Vector4ShaderProperty { displayName = displayName, value = value }, reference);

        public Texture2DShaderProperty TextureProp(string displayName, Texture texture, string reference = null) =>
            AddInput(new Texture2DShaderProperty
            {
                displayName = displayName,
                value = new SerializableTexture { texture = texture },
            }, reference);

        private T AddInput<T>(T property, string reference) where T : AbstractShaderProperty
        {
            Graph.AddGraphInput(property);
            if (!string.IsNullOrEmpty(reference))
                property.SetReferenceNameAndSanitizeForGraph(Graph, reference);
            return property;
        }

        // ---------------- Sub Graph 출력 ----------------

        public void Output(string name, SlotValueType type, AbstractMaterialNode from, string outName = null)
        {
            var output = (SubGraphOutputNode)Graph.outputNode;
            int id = _nextOutputId++;
            output.AddSlot(MaterialSlot.CreateMaterialSlot(type, id, name, NodeUtils.GetHLSLSafeName(name), SlotType.Input, UnityEngine.Vector4.zero));
            Link(from, outName, output, name);
        }

        public void PlaceOutput(float col, float row) => Place(Graph.outputNode, col, row);

        // ---------------- 메인 그래프 출력 블록 ----------------

        public void ToBlock(BlockFieldDescriptor descriptor, AbstractMaterialNode from, string outName = null)
        {
            BlockNode block = Graph.GetNodes<BlockNode>().First(b => b.descriptor == descriptor);
            var slots = new List<MaterialSlot>();
            block.GetInputSlots(slots);
            MaterialSlot outSlot = FindSlot(from, outName, input: false);
            Graph.Connect(from.GetSlotReference(outSlot.id), block.GetSlotReference(slots[0].id));
        }

        // 출력 블록에 노드 없이 값만 넣기 (예: Smoothness = 0.15)
        public void SetBlock(BlockFieldDescriptor descriptor, float value)
        {
            BlockNode block = Graph.GetNodes<BlockNode>().First(b => b.descriptor == descriptor);
            var slots = new List<MaterialSlot>();
            block.GetInputSlots(slots);
            if (slots[0] is Vector1MaterialSlot s)
                s.value = value;
        }

        // ---------------- 설명용 그룹 / 메모 ----------------

        public void Group(string title, params AbstractMaterialNode[] nodes)
        {
            var group = new GroupData(title, UnityEngine.Vector2.zero);
            Graph.CreateGroup(group);
            foreach (AbstractMaterialNode node in nodes)
                Graph.SetGroup(node, group);
        }

        public void Note(string title, string content, float col, float row, float width = 420f, float height = 160f) =>
            Graph.AddStickyNote(new StickyNoteData(title, content, new Rect(col * ColumnWidth, row * RowHeight, width, height)));
    }
}

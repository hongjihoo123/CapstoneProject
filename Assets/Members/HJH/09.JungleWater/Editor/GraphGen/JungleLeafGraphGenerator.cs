using UnityEditor;
using UnityEditor.ShaderGraph;
using UnityEngine;

namespace Assets.Members.HJH.Environment.GraphGen
{
    // 메뉴: HJH > Jungle Water > 잎 셰이더 그래프 만들기
    // 검정 배경의 잎 그림(T_LeafCluster.png) → 검정은 잘라내고, 바람에 흔들리는 잎 카드 셰이더.
    // 노드 약 15개. 반복문·시뮬레이션이 없어서 HLSL 이 필요 없다.
    public static class JungleLeafGraphGenerator
    {
        public const string MenuPath = "HJH/Jungle Water/잎 셰이더 그래프 만들기";
        private const string Root = "Assets/Members/HJH/09.JungleWater";
        private const string TexturePath = Root + "/Textures/T_LeafCluster.png";
        private const string GraphPath = Root + "/Shaders/Graph/SG_JungleLeaf.shadergraph";
        public const string MaterialPath = Root + "/Generated/Materials/M_JungleLeaf.mat";

        [MenuItem(MenuPath)]
        public static void Generate()
        {
            Texture2D texture = PrepareTexture();
            if (texture == null)
            {
                Debug.LogError($"[JungleWater] 잎 텍스처가 없습니다: {TexturePath}");
                return;
            }

            var b = ShaderGraphBuilder.LitCutout(null);
            Build(b, texture);
            b.Save(GraphPath);

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(GraphPath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            else
            {
                mat.shader = shader;
            }
            mat.SetTexture("_BaseMap", texture);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Debug.Log("[JungleWater] 잎 셰이더 그래프 + 머티리얼(M_JungleLeaf) 생성 완료");
        }

        // 가장자리가 반대편으로 번지지 않게 Clamp. 투명도는 셰이더가 검정으로 판단하므로 알파 채널 불필요
        private static Texture2D PrepareTexture()
        {
            if (AssetImporter.GetAtPath(TexturePath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        private static void Build(ShaderGraphBuilder b, Texture2D texture)
        {
            var baseMap = b.TextureProp("Leaf Texture", texture, "_BaseMap");
            var windStrength = b.FloatProp("Wind Strength", 0.4f, "_WindStrength", new Vector2(0f, 2f));
            var windSpeed = b.FloatProp("Wind Speed", 1.6f, "_WindSpeed", new Vector2(0f, 6f));

            // ================= ① 검정 지우기 (3개) =================
            // 잎은 초록 채널이 밝고 배경은 0 → G 가 어두우면 투명, Alpha Clip 으로 잘라냄
            var tex = b.Prop(baseMap, 0, 0);
            var sample = b.Node<SampleTexture2DNode>(1, 0);
            var cut = b.Node<SmoothstepNode>(2.3f, 0.8f, n =>
            {
                ShaderGraphBuilder.Set(n, "Edge1", 0.06f);
                ShaderGraphBuilder.Set(n, "Edge2", 0.16f);
            });

            b.Link(tex, sample, "Texture");
            b.Link(sample, "G", cut, "In");
            b.ToBlock(BlockFields.SurfaceDescription.BaseColor, sample, "RGBA");
            b.ToBlock(BlockFields.SurfaceDescription.Alpha, cut, "Out");
            b.SetBlock(BlockFields.SurfaceDescription.Smoothness, 0.15f);   // 잎이 번들거리지 않게

            // ================= ② 흔들림 (12개) =================
            // 오프셋 = sin(시간 × 속도 + 월드 x) × 세기 × (UV 중심에서의 거리)
            //  - 월드 x 를 더해서 카드마다, 카드 안에서도 박자가 달라짐 (물결처럼 흔들림)
            //  - 가운데(가지)는 고정, 가장자리일수록 크게
            const float r = 3f;
            var time = b.Node<TimeNode>(0, r);
            var speed = b.Prop(windSpeed, 0, r + 1);
            var t = b.Node<MultiplyNode>(1, r);
            var worldPos = b.Node<PositionNode>(1, r + 1.4f);
            var split = b.Node<SplitNode>(2, r + 1.4f);
            var phase = b.Node<AddNode>(3, r + 0.5f);
            var sine = b.Node<SineNode>(4, r + 0.5f);
            var strength = b.Prop(windStrength, 4, r + 1.5f);
            var sway = b.Node<MultiplyNode>(5, r + 0.8f);

            var uv = b.Node<UVNode>(4, r + 2.5f);
            var edge = b.Node<DistanceNode>(5, r + 2.5f, n => ShaderGraphBuilder.Set(n, "B", new Vector4(0.5f, 0.5f, 0f, 0f)));
            var amount = b.Node<MultiplyNode>(6, r + 1.5f);

            var offset = b.Node<Vector3Node>(7, r + 1.5f);
            var localPos = b.Node<PositionNode>(7, r + 3, n => n.spacePopup = new UnityEditor.ShaderGraph.Drawing.Controls.PopupList(new[] { "Object" }, 0));
            var final = b.Node<AddNode>(8.3f, r + 2.2f);

            b.Link(time, "Time", t, "A");
            b.Link(speed, t, "B");
            b.Link(worldPos, "Out", split, "In");
            b.Link(t, "Out", phase, "A");
            b.Link(split, "R", phase, "B");
            b.Link(phase, "Out", sine, "In");
            b.Link(sine, "Out", sway, "A");
            b.Link(strength, sway, "B");
            b.Link(uv, "Out", edge, "A");
            b.Link(sway, "Out", amount, "A");
            b.Link(edge, "Out", amount, "B");
            b.Link(amount, "Out", offset, "X");
            b.Link(localPos, "Out", final, "A");
            b.Link(offset, "Out", final, "B");
            b.ToBlock(BlockFields.VertexDescription.Position, final, "Out");

            b.Group("① 검정 지우기: 초록(G)이 어두우면 잘라냄", tex, sample, cut);
            b.Group("② 흔들림: sin(시간 + 위치) × 세기 × 가장자리일수록 크게", time, speed, t, worldPos, split, phase, sine, strength, sway, uv, edge, amount, offset, localPos, final);
            b.Note("정글 잎 카드 (노드만)",
                "① 잎은 초록 채널이 밝고 배경은 0 → Smoothstep 으로 투명도를 만들고 Alpha Clip 으로 잘라낸다.\n" +
                "② 꼭짓점을 sin 으로 좌우로 민다. 월드 x 를 더해 카드마다 박자가 다르고, UV 중심에서 멀수록(가장자리) 크게 흔들린다.\n" +
                "불투명 + Alpha Clip 이라 그림자도 잎 모양대로 떨어진다.", 0, -1.6f, 520f, 150f);
        }
    }
}

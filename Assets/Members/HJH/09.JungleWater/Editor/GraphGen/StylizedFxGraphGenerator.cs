using UnityEditor;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

namespace Assets.Members.HJH.Environment.GraphGen
{
    // 메뉴: HJH > Element FX > 스타일 이펙트 셰이더 그래프 만들기
    // 블렌더 스타일라이즈 이펙트 방식: 노이즈·보로노이를 시간에 따라 흘리고, 색을 몇 단계로 딱 끊고,
    // 파티클 수명(버텍스 컬러 알파)에 따라 가장자리부터 갉아먹듯 사라지게. 전부 노드.
    public static class StylizedFxGraphGenerator
    {
        public const string MenuPath = "HJH/Element FX/스타일 이펙트 셰이더 그래프 만들기";
        public const string Folder = "Assets/Members/HJH/08.Effects/Stylized";
        public const string FireMaterialPath = Folder + "/M_StylizedFire.mat";
        public const string WindMaterialPath = Folder + "/M_StylizedWind.mat";

        [MenuItem(MenuPath)]
        public static void Generate()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Members/HJH/08.Effects", "Stylized");

            var fire = ShaderGraphBuilder.UnlitTransparent(null, bothFaces: true, needsLighting: false);
            BuildFire(fire);
            fire.Save(Folder + "/SG_StylizedFire.shadergraph");
            MakeMaterial(Folder + "/SG_StylizedFire.shadergraph", FireMaterialPath);

            var wind = ShaderGraphBuilder.UnlitTransparent(null, bothFaces: true, needsLighting: false);
            BuildWind(wind);
            wind.Save(Folder + "/SG_StylizedWind.shadergraph");
            MakeMaterial(Folder + "/SG_StylizedWind.shadergraph", WindMaterialPath);

            AssetDatabase.SaveAssets();
            Debug.Log("[ElementFx] 스타일 이펙트 셰이더(불·바람) + 머티리얼 생성 완료");
        }

        private static void MakeMaterial(string graphPath, string materialPath)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(graphPath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, materialPath);
            }
            else
            {
                mat.shader = shader;
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
        }

        private static ColorShaderProperty Hdr(ShaderGraphBuilder b, string name, Color value, string reference)
        {
            ColorShaderProperty p = b.ColorProp(name, value, reference);
            p.colorMode = ColorMode.HDR;   // 1 넘는 밝기 → 블룸에 걸려서 빛남
            return p;
        }

        // ---------------------------------------------------------------- 공통 조각

        // 부드러운 단계: Edge1 ~ Edge2 사이에서 0 → 1 (Step 처럼 딱 끊지 않음)
        private static SmoothstepNode Ramp(ShaderGraphBuilder b, float col, float row, float from, float to) =>
            b.Node<SmoothstepNode>(col, row, n =>
            {
                ShaderGraphBuilder.Set(n, "Edge1", from);
                ShaderGraphBuilder.Set(n, "Edge2", to);
            });


        // UV(xy) + 시간 × 속도 (+ 늘리기) → 흐르는 UV
        private static AbstractMaterialNode ScrolledUV(ShaderGraphBuilder b, AbstractShaderProperty speed, AbstractShaderProperty tiling,
            out AbstractMaterialNode uv2, out AbstractMaterialNode time, float row)
        {
            var uv = b.Node<UVNode>(0, row);
            var su = b.Node<SplitNode>(1, row);
            var rg = b.Node<CombineNode>(2, row);
            b.Link(uv, "Out", su, "In");
            b.Link(su, "R", rg, "R");
            b.Link(su, "G", rg, "G");
            uv2 = rg;

            AbstractMaterialNode baseUV = rg;
            if (tiling != null)
            {
                var tile = b.Prop(tiling, 2, row + 0.8f);
                var tiled = b.Node<MultiplyNode>(3, row);
                b.Link(rg, "RG", tiled, "A");
                b.Link(tile, tiled, "B");
                baseUV = tiled;
            }

            var t = b.Node<TimeNode>(2, row + 1.6f);
            var spd = b.Prop(speed, 2, row + 2.3f);
            var scroll = b.Node<MultiplyNode>(3, row + 1.8f);
            b.Link(t, "Time", scroll, "A");
            b.Link(spd, scroll, "B");
            time = t;

            var uvScroll = b.Node<AddNode>(4, row + 0.6f);
            b.Link(baseUV, baseUV is CombineNode ? "RG" : "Out", uvScroll, "A");
            b.Link(scroll, "Out", uvScroll, "B");
            b.Group("흐르는 UV = UV + 시간 × 속도", uv, su, rg, t, spd, scroll, uvScroll);
            return uvScroll;
        }

        // 노이즈로 UV 를 살짝 일렁인 뒤 보로노이 → (1 - 거리) = 덩어리 모양
        private static AbstractMaterialNode Blobs(ShaderGraphBuilder b, AbstractMaterialNode uvScroll, AbstractMaterialNode time,
            AbstractShaderProperty noiseScale, AbstractShaderProperty distort, AbstractShaderProperty density, float row,
            out AbstractMaterialNode noise)
        {
            var ns = b.Prop(noiseScale, 5, row + 1f);
            var n = b.Node<NoiseNode>(5.5f, row);
            b.Link(uvScroll, "Out", n, "UV");
            b.Link(ns, n, "Scale");
            noise = n;

            var dis = b.Prop(distort, 6.5f, row + 1f);
            var nOff = b.Node<MultiplyNode>(7, row);
            var uvDist = b.Node<AddNode>(8, row);
            b.Link(n, "Out", nOff, "A");
            b.Link(dis, nOff, "B");
            b.Link(uvScroll, "Out", uvDist, "A");
            b.Link(nOff, "Out", uvDist, "B");

            var spin = b.Node<MultiplyNode>(8, row + 1.2f, n2 => ShaderGraphBuilder.Set(n2, "B", 2f));
            b.Link(time, "Time", spin, "A");
            var dens = b.Prop(density, 8, row + 2f);
            var vor = b.Node<VoronoiNode>(9, row);
            b.Link(uvDist, "Out", vor, "UV");
            b.Link(spin, "Out", vor, "AngleOffset");
            b.Link(dens, vor, "CellDensity");

            var inv = b.Node<OneMinusNode>(10, row);
            b.Link(vor, "Out", inv, "In");
            b.Group("노이즈로 일렁인 보로노이 → 덩어리 (블렌더 영상의 Noise + Voronoi)", ns, n, dis, nOff, uvDist, spin, dens, vor, inv);
            return inv;
        }

        // 파티클 수명 → 갉아먹기 기준값 (알파가 줄수록 기준이 올라감)
        private static AbstractMaterialNode Erosion(ShaderGraphBuilder b, float row, out AbstractMaterialNode vertexRgb)
        {
            var vc = b.Node<VertexColorNode>(10, row);
            var sv = b.Node<SplitNode>(11, row);
            var rgb = b.Node<CombineNode>(12, row + 0.8f);
            var erosion = b.Node<OneMinusNode>(12, row);
            b.Link(vc, "Out", sv, "In");
            b.Link(sv, "R", rgb, "R");
            b.Link(sv, "G", rgb, "G");
            b.Link(sv, "B", rgb, "B");
            b.Link(sv, "A", erosion, "In");
            vertexRgb = rgb;
            b.Group("파티클 수명(알파) → 갉아먹기 기준", vc, sv, rgb, erosion);
            return erosion;
        }

        // ---------------------------------------------------------------- 불

        private static void BuildFire(ShaderGraphBuilder b)
        {
            var core = Hdr(b, "Core Color", new Color(1.4f, 1.15f, 0.7f, 1f), "_CoreColor");
            var mid = Hdr(b, "Mid Color", new Color(1.15f, 0.45f, 0.1f, 1f), "_MidColor");
            var edge = Hdr(b, "Edge Color", new Color(0.45f, 0.1f, 0.03f, 1f), "_EdgeColor");
            var speed = b.Vec4Prop("Scroll Speed", new Vector4(0f, -1.3f, 0f, 0f), "_ScrollSpeed");
            var noiseScale = b.FloatProp("Noise Scale", 7f, "_NoiseScale");
            var distort = b.FloatProp("Distort", 0.12f, "_Distort", new Vector2(0f, 0.5f));
            var density = b.FloatProp("Cell Density", 4f, "_CellDensity");
            var softness = b.FloatProp("Edge Softness", 0.2f, "_Softness", new Vector2(0.001f, 0.3f));

            AbstractMaterialNode uvScroll = ScrolledUV(b, speed, null, out AbstractMaterialNode uv2, out AbstractMaterialNode time, 0);
            AbstractMaterialNode blobs = Blobs(b, uvScroll, time, noiseScale, distort, density, 0, out AbstractMaterialNode noise);

            // 가운데가 뜨겁게: 1 - |UV - 0.5| × 2
            var centered = b.Node<SubtractNode>(3, 4, n => ShaderGraphBuilder.Set(n, "B", 0.5f));
            var len = b.Node<LengthNode>(4, 4);
            var len2 = b.Node<MultiplyNode>(5, 4, n => ShaderGraphBuilder.Set(n, "B", 2f));
            var radial = b.Node<OneMinusNode>(6, 4);
            var radialSat = b.Node<SaturateNode>(7, 4);
            b.Link(uv2, "RG", centered, "A");
            b.Link(centered, "Out", len, "In");
            b.Link(len, "Out", len2, "A");
            b.Link(len2, "Out", radial, "In");
            b.Link(radial, "Out", radialSat, "In");

            // 열 = 가운데 × (노이즈 0.6 + 덩어리 0.6)
            var n06 = b.Node<MultiplyNode>(9, 2.5f, n => ShaderGraphBuilder.Set(n, "B", 0.6f));
            var b06 = b.Node<MultiplyNode>(11, 1.5f, n => ShaderGraphBuilder.Set(n, "B", 0.6f));
            var sum = b.Node<AddNode>(12, 2.5f);
            var heat = b.Node<MultiplyNode>(13, 3.2f);
            b.Link(noise, "Out", n06, "A");
            b.Link(blobs, "Out", b06, "A");
            b.Link(n06, "Out", sum, "A");
            b.Link(b06, "Out", sum, "B");
            b.Link(radialSat, "Out", heat, "A");
            b.Link(sum, "Out", heat, "B");
            b.Group("열 = 가운데일수록 × 노이즈·덩어리", centered, len, len2, radial, radialSat, n06, b06, sum, heat);

            // 갉아먹기: 열이 기준보다 높은 곳만 남김 (가장자리 살짝만 부드럽게)
            AbstractMaterialNode erosion = Erosion(b, 5.5f, out AbstractMaterialNode vertexRgb);
            var soft = b.Prop(softness, 13, 6.5f);
            var edge2 = b.Node<AddNode>(14, 6f);
            var alpha = b.Node<SmoothstepNode>(15, 5f);
            b.Link(erosion, "Out", edge2, "A");
            b.Link(soft, edge2, "B");
            b.Link(erosion, "Out", alpha, "Edge1");
            b.Link(edge2, "Out", alpha, "Edge2");
            b.Link(heat, "Out", alpha, "In");

            // 색 3단: 가장자리 → 중간(열 0.45↑) → 심지(열 0.7↑)
            var pEdge = b.Prop(edge, 13, 0);
            var pMid = b.Prop(mid, 13, 0.6f);
            var pCore = b.Prop(core, 13, 1.2f);
            var step1 = Ramp(b, 14, 1.8f, 0.3f, 0.6f);
            var step2 = Ramp(b, 14, 2.6f, 0.55f, 0.85f);
            var c1 = b.Node<LerpNode>(15, 0.5f);
            var c2 = b.Node<LerpNode>(16, 1.2f);
            var color = b.Node<MultiplyNode>(17, 2f);
            b.Link(heat, "Out", step1, "In");
            b.Link(heat, "Out", step2, "In");
            b.Link(pEdge, c1, "A");
            b.Link(pMid, c1, "B");
            b.Link(step1, "Out", c1, "T");
            b.Link(c1, "Out", c2, "A");
            b.Link(pCore, c2, "B");
            b.Link(step2, "Out", c2, "T");
            b.Link(c2, "Out", color, "A");
            b.Link(vertexRgb, "RGB", color, "B");

            b.ToBlock(BlockFields.SurfaceDescription.BaseColor, color, "Out");
            b.ToBlock(BlockFields.SurfaceDescription.Alpha, alpha, "Out");
            b.Group("색 3단을 부드럽게 이어 붙이기 (배경에 맞게 애니 느낌 줄임)", pEdge, pMid, pCore, step1, step2, c1, c2, color);
            b.Group("수명에 따라 가장자리부터 갉아먹기", soft, edge2, alpha);
            b.Note("스타일라이즈 불꽃 (노드만)",
                "블렌더 영상 방식: 노이즈·보로노이를 위로 흘리고 → 가운데일수록 뜨겁게 → 색을 3단으로 끊고 →\n" +
                "파티클이 늙을수록(알파↓) 기준을 올려서 가장자리부터 갉아먹듯 사라짐. 심지 색이 1보다 밝아 블룸에 걸려 빛난다.",
                0, -1.8f, 560f, 130f);
        }

        // ---------------------------------------------------------------- 바람

        private static void BuildWind(ShaderGraphBuilder b)
        {
            var core = Hdr(b, "Core Color", new Color(0.8f, 1.05f, 0.95f, 1f), "_CoreColor");
            var mid = Hdr(b, "Mid Color", new Color(0.5f, 0.85f, 0.72f, 1f), "_MidColor");
            var speed = b.Vec4Prop("Scroll Speed", new Vector4(1.8f, 0f, 0f, 0f), "_ScrollSpeed");
            var tiling = b.Vec4Prop("Tiling", new Vector4(1.5f, 5f, 0f, 0f), "_Tiling");
            var noiseScale = b.FloatProp("Noise Scale", 9f, "_NoiseScale");
            var distort = b.FloatProp("Distort", 0.1f, "_Distort", new Vector2(0f, 0.5f));
            var density = b.FloatProp("Cell Density", 3f, "_CellDensity");
            var softness = b.FloatProp("Edge Softness", 0.18f, "_Softness", new Vector2(0.001f, 0.3f));

            AbstractMaterialNode uvScroll = ScrolledUV(b, speed, tiling, out AbstractMaterialNode uv2, out AbstractMaterialNode time, 0);
            AbstractMaterialNode blobs = Blobs(b, uvScroll, time, noiseScale, distort, density, 0, out _);

            // 띠 가운데만 (UV.y 0~1 의 양 끝을 흐리게) → 고리·궤적 모양
            var sy = b.Node<SplitNode>(3, 4);
            var low = b.Node<SmoothstepNode>(4, 4, n =>
            {
                ShaderGraphBuilder.Set(n, "Edge1", 0f);
                ShaderGraphBuilder.Set(n, "Edge2", 0.3f);
            });
            var high = b.Node<SmoothstepNode>(4, 5, n =>
            {
                ShaderGraphBuilder.Set(n, "Edge1", 1f);
                ShaderGraphBuilder.Set(n, "Edge2", 0.7f);
            });
            var band = b.Node<MultiplyNode>(5, 4.5f);
            b.Link(uv2, "RG", sy, "In");
            b.Link(sy, "G", low, "In");
            b.Link(sy, "G", high, "In");
            b.Link(low, "Out", band, "A");
            b.Link(high, "Out", band, "B");

            // 줄무늬 = 덩어리² × 띠 (제곱해서 가늘게)
            var thin = b.Node<PowerNode>(11, 1.5f, n => ShaderGraphBuilder.Set(n, "B", 2f));
            var streak = b.Node<MultiplyNode>(12, 3f);
            b.Link(blobs, "Out", thin, "A");
            b.Link(thin, "Out", streak, "A");
            b.Link(band, "Out", streak, "B");
            b.Group("가로로 길게 늘인 보로노이 = 바람결, 띠 양 끝은 흐리게", sy, low, high, band, thin, streak);

            AbstractMaterialNode erosion = Erosion(b, 5.5f, out AbstractMaterialNode vertexRgb);
            var bias = b.Node<AddNode>(13, 5.5f, n => ShaderGraphBuilder.Set(n, "B", 0.12f));
            var soft = b.Prop(softness, 13, 6.5f);
            var edge2 = b.Node<AddNode>(14, 6f);
            var alpha = b.Node<SmoothstepNode>(15, 5f);
            b.Link(erosion, "Out", bias, "A");
            b.Link(bias, "Out", edge2, "A");
            b.Link(soft, edge2, "B");
            b.Link(bias, "Out", alpha, "Edge1");
            b.Link(edge2, "Out", alpha, "Edge2");
            b.Link(streak, "Out", alpha, "In");

            var pMid = b.Prop(mid, 13, 0);
            var pCore = b.Prop(core, 13, 0.6f);
            var step = Ramp(b, 14, 1.5f, 0.55f, 0.9f);
            var c = b.Node<LerpNode>(15, 0.6f);
            var color = b.Node<MultiplyNode>(16, 1.5f);
            b.Link(streak, "Out", step, "In");
            b.Link(pMid, c, "A");
            b.Link(pCore, c, "B");
            b.Link(step, "Out", c, "T");
            b.Link(c, "Out", color, "A");
            b.Link(vertexRgb, "RGB", color, "B");

            b.ToBlock(BlockFields.SurfaceDescription.BaseColor, color, "Out");
            b.ToBlock(BlockFields.SurfaceDescription.Alpha, alpha, "Out");
            b.Group("색: 민트 → 진한 곳만 흰빛 (부드럽게)", pMid, pCore, step, c, color);
            b.Group("수명에 따라 갉아먹기", bias, soft, edge2, alpha);
            b.Note("스타일라이즈 바람결 (노드만)",
                "보로노이를 가로로 5배 늘려 흘리면 바람 줄무늬가 된다. 고리 메시(UV.x = 둘레, UV.y = 폭)에 쓰면 회오리 링.\n" +
                "불꽃과 같은 방식으로 파티클이 늙을수록 줄무늬가 가늘어지며 사라진다.", 0, -1.8f, 560f, 120f);
        }
    }
}

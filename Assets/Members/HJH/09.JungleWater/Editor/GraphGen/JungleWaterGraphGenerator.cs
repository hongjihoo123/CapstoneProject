using System.IO;
using UnityEditor;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using V = UnityEditor.ShaderGraph.SlotValueType;

namespace Assets.Members.HJH.Environment.GraphGen
{
    // 메뉴: HJH > Jungle Water > 노드 버전 셰이더 그래프 만들기
    // HLSL 버전(.shader)과 같은 공식을 Shader Graph 로 만든다.
    //  - 눈으로 보며 조정하는 표현(색·깊이·굴절·거품·반사) = 노드 (Sub Graph 한 개 = 개념 한 개)
    //  - 노드로 못 하는 것(반복문, 라이트 색/그림자, 이전 프레임 시뮬레이션) = HLSL Custom Function
    public static class JungleWaterGraphGenerator
    {
        private const string Root = "Assets/Members/HJH/09.JungleWater";
        private const string IncludePath = Root + "/Shaders/Include/JungleWaterCommon.hlsl";
        private const string GraphDir = Root + "/Shaders/Graph";
        private const string SubDir = GraphDir + "/SubGraphs";
        private const string MatDir = Root + "/Generated/Materials";

        public const string WaterGraphPath = GraphDir + "/SG_JungleShallowWater.shadergraph";
        public const string FogGraphPath = GraphDir + "/SG_JungleGroundFog.shadergraph";
        public const string SplashGraphPath = GraphDir + "/SG_JungleSplashParticle.shadergraph";

        private static string _inc;

        [MenuItem("HJH/Jungle Water/노드 버전 셰이더 그래프 만들기")]
        public static void GenerateAll()
        {
            _inc = AssetDatabase.AssetPathToGUID(IncludePath);
            if (string.IsNullOrEmpty(_inc))
            {
                Debug.LogError($"[JungleWater] {IncludePath} 를 찾을 수 없습니다.");
                return;
            }

            EnsureFolder(SubDir);
            EnsureFolder(MatDir);

            // Sub Graph 는 쓰는 쪽보다 먼저 만들어야 한다 (만들자마자 불러와서 노드로 씀)
            SubGraphAsset sceneBed = Make("SGS_SceneBed", BuildSceneBed);
            SubGraphAsset waveHeight = Make("SGS_WaveHeight", BuildWaveHeight);
            SubGraphAsset surfaceWaves = Make("SGS_SurfaceWaves", b => BuildSurfaceWaves(b, waveHeight));
            SubGraphAsset ripple = Make("SGS_InteractiveRipple", BuildInteractiveRipple);
            SubGraphAsset combineNormal = Make("SGS_CombineNormal", BuildCombineNormal);
            SubGraphAsset refraction = Make("SGS_Refraction", b => BuildRefraction(b, sceneBed));
            SubGraphAsset mainLight = Make("SGS_MainLight", BuildMainLight);
            SubGraphAsset waterColor = Make("SGS_WaterColor", BuildWaterColor);
            SubGraphAsset reflection = Make("SGS_Reflection", BuildReflection);
            SubGraphAsset foam = Make("SGS_Foam", BuildFoam);
            SubGraphAsset specular = Make("SGS_Specular", BuildSpecular);
            SubGraphAsset composite = Make("SGS_FinalComposite", BuildFinalComposite);

            var water = ShaderGraphBuilder.UnlitTransparent(_inc, bothFaces: false, needsLighting: true);
            BuildWaterGraph(water, sceneBed, surfaceWaves, ripple, combineNormal, refraction, mainLight, waterColor, reflection, foam, specular, composite);
            water.Save(WaterGraphPath);

            var fog = ShaderGraphBuilder.UnlitTransparent(_inc, bothFaces: true, needsLighting: true);
            BuildFogGraph(fog, sceneBed, mainLight);
            fog.Save(FogGraphPath);

            var splash = ShaderGraphBuilder.UnlitTransparent(_inc, bothFaces: true, needsLighting: false);
            BuildSplashGraph(splash);
            splash.Save(SplashGraphPath);

            CreateNodeMaterials();
            AssetDatabase.SaveAssets();
            Debug.Log("[JungleWater] 노드 버전 그래프 생성 완료. 'HJH > Jungle Water > 셰이더 전환' 으로 씬의 물·안개를 바꿔 비교할 수 있습니다.");
        }

        private static SubGraphAsset Make(string name, System.Action<ShaderGraphBuilder> build)
        {
            string path = $"{SubDir}/{name}.shadersubgraph";
            var b = ShaderGraphBuilder.SubGraph(_inc);
            build(b);
            b.Save(path);
            var asset = AssetDatabase.LoadAssetAtPath<SubGraphAsset>(path);
            if (asset == null)
                throw new System.InvalidOperationException($"[JungleWater] Sub Graph 불러오기 실패: {path}");
            return asset;
        }

        // =====================================================================
        // Sub Graphs
        // =====================================================================

        // 깊이 텍스처 → 바닥 위치, 물 두께
        private static void BuildSceneBed(ShaderGraphBuilder b)
        {
            var screenUV = b.Vec4Prop("ScreenUV");

            var uv = b.Prop(screenUV, 0, 2);
            var pos = b.Node<PositionNode>(0, 0);
            var cam = b.Node<CameraNode>(0, 1);
            var ray = b.Node<SubtractNode>(1, 0);
            var surfEye = b.Node<DotProductNode>(2, 0);
            var depth = b.Node<SceneDepthNode>(1, 2, n => n.depthSamplingMode = DepthSamplingMode.Eye);
            var k = b.Node<DivideNode>(3, 1);
            var scaled = b.Node<MultiplyNode>(4, 0);
            var bed = b.Node<AddNode>(5, 0);
            var splitP = b.Node<SplitNode>(5, 2);
            var splitB = b.Node<SplitNode>(6, 1);
            var diff = b.Node<SubtractNode>(7, 2);
            var thick = b.Node<MaximumNode>(8, 2, n => ShaderGraphBuilder.Set(n, "B", 0f));

            b.Link(pos, "Out", ray, "A");
            b.Link(cam, "Position", ray, "B");
            b.Link(ray, "Out", surfEye, "A");
            b.Link(cam, "Direction", surfEye, "B");
            b.Link(uv, depth, "UV");
            b.Link(depth, "Out", k, "A");
            b.Link(surfEye, "Out", k, "B");
            b.Link(ray, "Out", scaled, "A");
            b.Link(k, "Out", scaled, "B");
            b.Link(cam, "Position", bed, "A");
            b.Link(scaled, "Out", bed, "B");
            b.Link(pos, "Out", splitP, "In");
            b.Link(bed, "Out", splitB, "In");
            b.Link(splitP, "G", diff, "A");
            b.Link(splitB, "G", diff, "B");
            b.Link(diff, "Out", thick, "A");

            b.Output("BedPositionWS", V.Vector3, bed, "Out");
            b.Output("Thickness", V.Vector1, thick, "Out");
            b.PlaceOutput(9.5f, 1);

            b.Group("카메라 광선 × 깊이 = 바닥 위치", pos, cam, ray, surfEye, depth, k, scaled, bed);
            b.Group("물 두께 = 수면 높이 - 바닥 높이", splitP, splitB, diff, thick);
            b.Note("SGS_SceneBed",
                "깊이 텍스처는 '카메라에서 얼마나 먼지'만 알려준다.\n" +
                "카메라→이 픽셀 광선을 (바닥 깊이 / 수면 깊이) 배만큼 늘리면 물 밑 바닥의 월드 위치.\n" +
                "수면 높이 - 바닥 높이 = 물 두께 → 얕은 곳/깊은 곳 판단의 기준.", 0, -1.4f);
        }

        // 두 겹의 흐르는 노이즈 = 잔물결 높이
        private static void BuildWaveHeight(ShaderGraphBuilder b)
        {
            var pProp = b.Vec2Prop("P");
            var tProp = b.FloatProp("T", 0f);

            var p = b.Prop(pProp, 0, 0);
            var t = b.Prop(tProp, 0, 2);
            var dir1 = b.Vec2(1f, 0.6f, 0, 1);
            var dir2 = b.Vec2(-1.3f, 0.8f, 0, 3);

            var t1 = b.Node<MultiplyNode>(1, 1);
            var p1 = b.Node<AddNode>(2, 0);
            var n1 = NoiseNode(b, 3, 0);
            var p2s = b.Node<MultiplyNode>(1, 2.2f, n => ShaderGraphBuilder.Set(n, "B", 2.1f));
            var t2 = b.Node<MultiplyNode>(1, 3);
            var p2 = b.Node<AddNode>(2, 2.5f);
            var n2 = NoiseNode(b, 3, 2.5f);
            var w1 = b.Node<MultiplyNode>(4.5f, 0, n => ShaderGraphBuilder.Set(n, "B", 0.6f));
            var w2 = b.Node<MultiplyNode>(4.5f, 2.5f, n => ShaderGraphBuilder.Set(n, "B", 0.4f));
            var h = b.Node<AddNode>(5.5f, 1.2f);

            b.Link(t, t1, "A");
            b.Link(dir1, "Out", t1, "B");
            b.Link(p, p1, "A");
            b.Link(t1, "Out", p1, "B");
            b.Link(p1, "Out", n1, "P");
            b.Link(p, p2s, "A");
            b.Link(t, t2, "A");
            b.Link(dir2, "Out", t2, "B");
            b.Link(p2s, "Out", p2, "A");
            b.Link(t2, "Out", p2, "B");
            b.Link(p2, "Out", n2, "P");
            b.Link(n1, "Noise", w1, "A");
            b.Link(n2, "Noise", w2, "A");
            b.Link(w1, "Out", h, "A");
            b.Link(w2, "Out", h, "B");

            b.Output("H", V.Vector1, h, "Out");
            b.PlaceOutput(6.8f, 1.2f);

            b.Group("노이즈 1: 큰 물결이 (1, 0.6) 방향으로 흐름", dir1, t1, p1, n1, w1);
            b.Group("노이즈 2: 2.1배 촘촘한 물결이 반대쪽으로 흐름", p2s, dir2, t2, p2, n2, w2);
            b.Note("SGS_WaveHeight",
                "두 겹의 노이즈를 서로 다른 방향·속도로 흘려 섞으면 반복이 안 보이는 잔물결 높이가 된다.\n" +
                "노이즈 함수만 HLSL(JungleValueNoise) — HLSL 버전과 무늬를 똑같이 맞추기 위해.", 0, -1.4f);
        }

        // 높이 → 기울기 (옆 칸과의 차이)
        private static void BuildSurfaceWaves(ShaderGraphBuilder b, SubGraphAsset waveHeight)
        {
            var scaleProp = b.FloatProp("WaveScale", 1.4f);
            var speedProp = b.FloatProp("WaveSpeed", 0.25f);
            var strengthProp = b.FloatProp("WaveStrength", 0.3f);
            const float e = 0.08f;

            var pos = b.Node<PositionNode>(0, 0);
            var split = b.Node<SplitNode>(1, 0);
            var xz = b.Node<CombineNode>(2, 0);
            var scale = b.Prop(scaleProp, 2, 1);
            var p = b.Node<MultiplyNode>(3, 0);
            var time = b.Node<TimeNode>(2, 2);
            var speed = b.Prop(speedProp, 2, 3);
            var t = b.Node<MultiplyNode>(3, 2);

            var offX = b.Vec2(e, 0f, 3, 3);
            var offZ = b.Vec2(0f, e, 3, 4);
            var px = b.Node<AddNode>(4, 3);
            var pz = b.Node<AddNode>(4, 4);
            var h0 = b.Sub(waveHeight, 5, 0);
            var hx = b.Sub(waveHeight, 5, 2);
            var hz = b.Sub(waveHeight, 5, 4);

            var gx = b.Node<SubtractNode>(6, 1);
            var gz = b.Node<SubtractNode>(6, 3);
            var g = b.Node<CombineNode>(7, 2);
            var div = b.Node<DivideNode>(8, 2, n => ShaderGraphBuilder.Set(n, "B", e));
            var strength = b.Prop(strengthProp, 8, 3);
            var outGrad = b.Node<MultiplyNode>(9, 2);

            b.Link(pos, "Out", split, "In");
            b.Link(split, "R", xz, "R");
            b.Link(split, "B", xz, "G");
            b.Link(xz, "RG", p, "A");
            b.Link(scale, p, "B");
            b.Link(time, "Time", t, "A");
            b.Link(speed, t, "B");
            b.Link(p, "Out", px, "A");
            b.Link(offX, "Out", px, "B");
            b.Link(p, "Out", pz, "A");
            b.Link(offZ, "Out", pz, "B");

            b.Link(p, "Out", h0, "P");
            b.Link(t, "Out", h0, "T");
            b.Link(px, "Out", hx, "P");
            b.Link(t, "Out", hx, "T");
            b.Link(pz, "Out", hz, "P");
            b.Link(t, "Out", hz, "T");

            b.Link(hx, "H", gx, "A");
            b.Link(h0, "H", gx, "B");
            b.Link(hz, "H", gz, "A");
            b.Link(h0, "H", gz, "B");
            b.Link(gx, "Out", g, "R");
            b.Link(gz, "Out", g, "G");
            b.Link(g, "RG", div, "A");
            b.Link(div, "Out", outGrad, "A");
            b.Link(strength, outGrad, "B");

            b.Output("Gradient", V.Vector2, outGrad, "Out");
            b.PlaceOutput(10.3f, 2);

            b.Group("월드 XZ 좌표, 시간", pos, split, xz, scale, p, time, speed, t);
            b.Group("높이 3번: 제자리 / x로 0.08 / z로 0.08", offX, offZ, px, pz, h0, hx, hz);
            b.Group("(옆 높이 - 내 높이) / 거리 = 기울기", gx, gz, g, div, strength, outGrad);
            b.Note("SGS_SurfaceWaves",
                "노멀(빛 반사 방향)은 표면의 기울기로 정해진다.\n" +
                "높이를 세 군데서 재서 x, z 방향 기울기를 구한다 → 노멀맵 텍스처 없이 잔물결.", 0, -1.4f);
        }

        // HLSL 파동 시뮬레이션 결과 읽기
        private static void BuildInteractiveRipple(ShaderGraphBuilder b)
        {
            var strengthProp = b.FloatProp("RippleStrength", 18f);
            var pos = b.Node<PositionNode>(0, 0);
            var strength = b.Prop(strengthProp, 0, 1);
            var fn = b.Func("JungleRipple", 1.5f, 0,
                new[] { ("PositionWS", V.Vector3), ("Strength", V.Vector1) },
                new[] { ("NormalWS", V.Vector3), ("Height", V.Vector1) });

            b.Link(pos, "Out", fn, "PositionWS");
            b.Link(strength, fn, "Strength");
            b.Output("NormalWS", V.Vector3, fn, "NormalWS");
            b.Output("Height", V.Vector1, fn, "Height");
            b.PlaceOutput(3.5f, 0);

            b.Note("SGS_InteractiveRipple  (HLSL)",
                "캐릭터·스킬 물결은 '이전 프레임 결과 + 이웃 픽셀'로 계산하는 파동 시뮬레이션이라 노드로 불가능.\n" +
                "JungleRippleSim.shader(HLSL)가 텍스처에 계산해 두고, 여기서는 그 텍스처를 읽어 노멀·높이로 바꾼다.", 0, -1.4f);
        }

        // 잔물결 + 상호작용 물결 → 최종 노멀
        private static void BuildCombineNormal(ShaderGraphBuilder b)
        {
            var gradProp = b.Vec2Prop("WaveGradient");
            var rippleProp = b.Vec3Prop("RippleNormal");

            var grad = b.Prop(gradProp, 0, 0);
            var ripple = b.Prop(rippleProp, 0, 1);
            var sg = b.Node<SplitNode>(1, 0);
            var sr = b.Node<SplitNode>(1, 1);
            var x = b.Node<SubtractNode>(2, 0);
            var z = b.Node<SubtractNode>(2, 1);
            var combine = b.Node<CombineNode>(3, 0.5f, n => ShaderGraphBuilder.Set(n, "G", 1f));
            var normalize = b.Node<NormalizeNode>(4, 0.5f);

            b.Link(grad, sg, "In");
            b.Link(ripple, sr, "In");
            b.Link(sr, "R", x, "A");
            b.Link(sg, "R", x, "B");
            b.Link(sr, "B", z, "A");
            b.Link(sg, "G", z, "B");
            b.Link(x, "Out", combine, "R");
            b.Link(z, "Out", combine, "B");
            b.Link(combine, "RGB", normalize, "In");

            b.Output("NormalWS", V.Vector3, normalize, "Out");
            b.PlaceOutput(5.2f, 0.5f);
            b.Note("SGS_CombineNormal",
                "노멀 = (물결 노멀.x - 잔물결 기울기.x, 1, 물결 노멀.z - 잔물결 기울기.z) 정규화.\n" +
                "오르막의 반대쪽으로 노멀이 기운다.", 0, -1.4f);
        }

        // 굴절: 화면 색을 노멀만큼 밀어서 가져오기
        private static void BuildRefraction(ShaderGraphBuilder b, SubGraphAsset sceneBed)
        {
            var normalProp = b.Vec3Prop("NormalWS");
            var screenProp = b.Vec4Prop("ScreenUV");
            var thickProp = b.FloatProp("Thickness", 0f);
            var strengthProp = b.FloatProp("RefractionStrength", 0.02f);

            var normal = b.Prop(normalProp, 0, 0);
            var sn = b.Node<SplitNode>(1, 0);
            var nxz = b.Node<CombineNode>(2, 0);
            var thick = b.Prop(thickProp, 0, 1);
            var thick2 = b.Node<MultiplyNode>(1, 1, n => ShaderGraphBuilder.Set(n, "B", 2f));
            var sat = b.Node<SaturateNode>(2, 1);
            var strength = b.Prop(strengthProp, 2, 2);
            var amount = b.Node<MultiplyNode>(3, 1);
            var offset = b.Node<MultiplyNode>(4, 0);

            var screen = b.Prop(screenProp, 0, 3);
            var su = b.Node<SplitNode>(1, 3);
            var uv = b.Node<CombineNode>(2, 3);
            var refrUV = b.Node<AddNode>(5, 1);

            var bedR = b.Sub(sceneBed, 6, 1);
            var pos = b.Node<PositionNode>(6, 3);
            var splitP = b.Node<SplitNode>(7, 3);
            var splitB = b.Node<SplitNode>(7, 1);
            var above = b.Node<ComparisonNode>(8, 2, n => n.comparisonType = ComparisonType.Greater);
            var finalUV = b.Node<BranchNode>(9, 1);
            var finalThick = b.Node<BranchNode>(9, 3);
            var sceneColor = b.Node<SceneColorNode>(10, 1);

            b.Link(normal, sn, "In");
            b.Link(sn, "R", nxz, "R");
            b.Link(sn, "B", nxz, "G");
            b.Link(thick, thick2, "A");
            b.Link(thick2, "Out", sat, "In");
            b.Link(sat, "Out", amount, "A");
            b.Link(strength, amount, "B");
            b.Link(nxz, "RG", offset, "A");
            b.Link(amount, "Out", offset, "B");
            b.Link(screen, su, "In");
            b.Link(su, "R", uv, "R");
            b.Link(su, "G", uv, "G");
            b.Link(uv, "RG", refrUV, "A");
            b.Link(offset, "Out", refrUV, "B");

            b.Link(refrUV, "Out", bedR, "ScreenUV");
            b.Link(pos, "Out", splitP, "In");
            b.Link(bedR, "BedPositionWS", splitB, "In");
            b.Link(splitB, "G", above, "A");
            b.Link(splitP, "G", above, "B");
            b.Link(above, "Out", finalUV, "Predicate");
            b.Link(uv, "RG", finalUV, "True");
            b.Link(refrUV, "Out", finalUV, "False");
            b.Link(above, "Out", finalThick, "Predicate");
            b.Link(thick, finalThick, "True");
            b.Link(bedR, "Thickness", finalThick, "False");
            b.Link(finalUV, "Out", sceneColor, "UV");

            b.Output("SceneColor", V.Vector3, sceneColor, "Out");
            b.Output("Thickness", V.Vector1, finalThick, "Out");
            b.PlaceOutput(11.3f, 2);

            b.Group("밀 거리 = 노멀.xz × 세기 × (얕으면 약하게)", normal, sn, nxz, thick, thick2, sat, strength, amount, offset);
            b.Group("밀린 화면 좌표", screen, su, uv, refrUV);
            b.Group("밀린 곳 바닥이 수면보다 위(= 물 밖 물체)면 굴절 취소", bedR, pos, splitP, splitB, above, finalUV, finalThick);
            b.Note("SGS_Refraction",
                "물속 바닥이 일렁여 보이는 효과. 불투명 물체를 다 그린 화면(Scene Color)을 노멀 방향으로 조금 밀어서 가져온다.\n" +
                "밀린 위치가 물 밖 물체(나무, 캐릭터)면 그걸 끌어오지 않도록 원래 위치를 쓴다.", 0, -1.4f);
        }

        // 메인 라이트 (방향, 색, 그림자, 주변광) + 확산광
        private static void BuildMainLight(ShaderGraphBuilder b)
        {
            var normalProp = b.Vec3Prop("NormalWS");
            var pos = b.Node<PositionNode>(0, 0);
            var normal = b.Prop(normalProp, 0, 1);
            var fn = b.Func("JungleMainLight", 1.5f, 0,
                new[] { ("PositionWS", V.Vector3), ("NormalWS", V.Vector3) },
                new[] { ("Direction", V.Vector3), ("Color", V.Vector3), ("Shadow", V.Vector1), ("Ambient", V.Vector3) });
            var ndl = b.Node<DotProductNode>(3, 1);
            var sat = b.Node<SaturateNode>(4, 1);
            var lit = b.Node<MultiplyNode>(5, 0.5f);
            var shadowed = b.Node<MultiplyNode>(6, 0.5f);
            var diffuse = b.Node<AddNode>(7, 1);

            b.Link(pos, "Out", fn, "PositionWS");
            b.Link(normal, fn, "NormalWS");
            b.Link(normal, ndl, "A");
            b.Link(fn, "Direction", ndl, "B");
            b.Link(ndl, "Out", sat, "In");
            b.Link(fn, "Color", lit, "A");
            b.Link(sat, "Out", lit, "B");
            b.Link(lit, "Out", shadowed, "A");
            b.Link(fn, "Shadow", shadowed, "B");
            b.Link(shadowed, "Out", diffuse, "A");
            b.Link(fn, "Ambient", diffuse, "B");

            b.Output("Direction", V.Vector3, fn, "Direction");
            b.Output("Color", V.Vector3, fn, "Color");
            b.Output("Shadow", V.Vector1, fn, "Shadow");
            b.Output("Diffuse", V.Vector3, diffuse, "Out");
            b.PlaceOutput(8.3f, 0.5f);

            b.Group("확산광 = 라이트 색 × saturate(N·L) × 그림자 + 주변광", ndl, sat, lit, shadowed, diffuse);
            b.Note("SGS_MainLight  (HLSL)",
                "Unlit 그래프에는 라이트 색·그림자 노드가 없다 → GetMainLight / SampleSH 를 HLSL(JungleMainLight)로 불러온다.\n" +
                "그 값으로 하는 계산(확산광)은 노드.", 0, -1.4f);
        }

        // 얕으면 바닥이 비치고 깊을수록 탁한 물 색
        private static void BuildWaterColor(ShaderGraphBuilder b)
        {
            var sceneProp = b.Vec3Prop("SceneColor");
            var thickProp = b.FloatProp("Thickness", 0f);
            var tintProp = b.Vec3Prop("ShallowTint");
            var deepProp = b.Vec3Prop("DeepColor");
            var murkProp = b.FloatProp("Murkiness", 3.5f);
            var diffuseProp = b.Vec3Prop("Diffuse");

            var thick = b.Prop(thickProp, 0, 0);
            var murkiness = b.Prop(murkProp, 0, 1);
            var murk = b.Node<MultiplyNode>(1, 0.5f);
            var neg = b.Node<MultiplyNode>(2, 0, n => ShaderGraphBuilder.Set(n, "B", -1f));
            var exp = b.Node<ExponentialNode>(3, 0);
            var absorb = b.Node<OneMinusNode>(4, 0);

            var murk2 = b.Node<MultiplyNode>(2, 1.3f, n => ShaderGraphBuilder.Set(n, "B", 2f));
            var tintT = b.Node<SaturateNode>(3, 1.3f);
            var white = b.Vec3(1f, 1f, 1f, 3, 2.2f);
            var tintColor = b.Prop(tintProp, 3, 3);
            var tint = b.Node<LerpNode>(4, 2);
            var scene = b.Prop(sceneProp, 4, 3.3f);
            var transmitted = b.Node<MultiplyNode>(5, 2.5f);

            var deep = b.Prop(deepProp, 4, 4.3f);
            var diffuse = b.Prop(diffuseProp, 4, 5.3f);
            var deepLit = b.Node<MultiplyNode>(5, 4.5f);
            var col = b.Node<LerpNode>(6, 2);

            b.Link(thick, murk, "A");
            b.Link(murkiness, murk, "B");
            b.Link(murk, "Out", neg, "A");
            b.Link(neg, "Out", exp, "In");
            b.Link(exp, "Out", absorb, "In");
            b.Link(murk, "Out", murk2, "A");
            b.Link(murk2, "Out", tintT, "In");
            b.Link(white, "Out", tint, "A");
            b.Link(tintColor, tint, "B");
            b.Link(tintT, "Out", tint, "T");
            b.Link(scene, transmitted, "A");
            b.Link(tint, "Out", transmitted, "B");
            b.Link(deep, deepLit, "A");
            b.Link(diffuse, deepLit, "B");
            b.Link(transmitted, "Out", col, "A");
            b.Link(deepLit, "Out", col, "B");
            b.Link(absorb, "Out", col, "T");

            b.Output("Color", V.Vector3, col, "Out");
            b.PlaceOutput(7.2f, 2);

            b.Group("흡수량 = 1 - e^(-두께×흐림)  (빛이 물에 먹히는 양)", thick, murkiness, murk, neg, exp, absorb);
            b.Group("비쳐 보이는 바닥 = 바닥색 × 얕은 물 색조", murk2, tintT, white, tintColor, tint, scene, transmitted);
            b.Group("탁한 물 색 (조명 받음)", deep, diffuse, deepLit);
            b.Note("SGS_WaterColor",
                "실제 물처럼 두꺼울수록 빛이 지수적으로 줄어든다(e^-x).\n" +
                "흡수량 0 → 바닥이 그대로, 1 → 탁한 물 색만. 얕은 늪 물의 핵심 표현.", 0, -1.4f);
        }

        // 비스듬히 볼수록 반사색 (프레넬)
        private static void BuildReflection(ShaderGraphBuilder b)
        {
            var colorProp = b.Vec3Prop("Color");
            var normalProp = b.Vec3Prop("NormalWS");
            var reflProp = b.Vec3Prop("ReflectionColor");
            var powerProp = b.FloatProp("FresnelPower", 4f);
            var strengthProp = b.FloatProp("ReflectionStrength", 0.5f);

            var normal = b.Prop(normalProp, 0, 0);
            var power = b.Prop(powerProp, 0, 1);
            var fresnel = b.Node<FresnelNode>(1, 0);
            var strength = b.Prop(strengthProp, 1, 1.5f);
            var amount = b.Node<MultiplyNode>(2, 0.5f);
            var color = b.Prop(colorProp, 2, 1.5f);
            var refl = b.Prop(reflProp, 2, 2.3f);
            var col = b.Node<LerpNode>(3, 1);

            b.Link(normal, fresnel, "Normal");
            b.Link(power, fresnel, "Power");
            b.Link(fresnel, "Out", amount, "A");
            b.Link(strength, amount, "B");
            b.Link(color, col, "A");
            b.Link(refl, col, "B");
            b.Link(amount, "Out", col, "T");

            b.Output("Color", V.Vector3, col, "Out");
            b.PlaceOutput(4.2f, 1);
            b.Note("SGS_Reflection",
                "수면을 비스듬히 볼수록 하늘이 더 많이 반사된다(프레넬). Fresnel Effect 노드 = pow(1 - N·V, 세기).", 0, -1.4f);
        }

        // 물가 거품 + 물결 마루 거품
        private static void BuildFoam(ShaderGraphBuilder b)
        {
            var thickProp = b.FloatProp("Thickness", 0f);
            var distProp = b.FloatProp("FoamDistance", 0.12f);
            var scaleProp = b.FloatProp("FoamNoiseScale", 3f);
            var speedProp = b.FloatProp("WaveSpeed", 0.25f);
            var rippleHProp = b.FloatProp("RippleHeight", 0f);
            var rippleFoamProp = b.FloatProp("RippleFoam", 2.5f);

            // 거품 노이즈
            var time = b.Node<TimeNode>(0, 0);
            var speed = b.Prop(speedProp, 0, 1);
            var t = b.Node<MultiplyNode>(1, 0.5f);
            var flow = b.Vec2(0.6f, -0.4f, 1, 1.5f);
            var tFlow = b.Node<MultiplyNode>(2, 1);
            var pos = b.Node<PositionNode>(0, 2.5f);
            var sp = b.Node<SplitNode>(1, 2.5f);
            var xz = b.Node<CombineNode>(2, 2.5f);
            var scale = b.Prop(scaleProp, 2, 3.5f);
            var scaled = b.Node<MultiplyNode>(3, 2.5f);
            var p = b.Node<AddNode>(4, 1.5f);
            var fbm = b.Func("JungleFbm", 5, 1.5f,
                new[] { ("P", V.Vector2) },
                new[] { ("Noise", V.Vector1) });

            // 물가
            var thick = b.Prop(thickProp, 4, 4.5f);
            var dist = b.Prop(distProp, 4, 5.5f);
            var ratio = b.Node<DivideNode>(5, 4.5f);
            var ratioSat = b.Node<SaturateNode>(6, 4.5f);
            var shore = b.Node<OneMinusNode>(7, 4.5f);
            var n06 = b.Node<MultiplyNode>(7, 3, n => ShaderGraphBuilder.Set(n, "B", 0.6f));
            var shoreN = b.Node<AddNode>(8, 3.8f);
            var shoreM = b.Node<SubtractNode>(9, 3.8f, n => ShaderGraphBuilder.Set(n, "B", 0.3f));
            var foamA = b.Node<SmoothstepNode>(10, 3.8f, n =>
            {
                ShaderGraphBuilder.Set(n, "Edge1", 0.5f);
                ShaderGraphBuilder.Set(n, "Edge2", 0.7f);
            });

            // 물결 마루
            var rippleH = b.Prop(rippleHProp, 6, 0);
            var abs = b.Node<AbsoluteNode>(7, 0);
            var rippleFoam = b.Prop(rippleFoamProp, 7, 1);
            var rh = b.Node<MultiplyNode>(8, 0.3f);
            var rhm = b.Node<SubtractNode>(9, 0.3f, n => ShaderGraphBuilder.Set(n, "B", 0.15f));
            var rhs = b.Node<SaturateNode>(10, 0.3f);
            var mask = b.Node<SmoothstepNode>(9, 1.6f, n =>
            {
                ShaderGraphBuilder.Set(n, "Edge1", 0.3f);
                ShaderGraphBuilder.Set(n, "Edge2", 0.6f);
            });
            var foamB = b.Node<MultiplyNode>(11, 1);

            var sum = b.Node<AddNode>(12, 2.5f);
            var foam = b.Node<SaturateNode>(13, 2.5f);

            b.Link(time, "Time", t, "A");
            b.Link(speed, t, "B");
            b.Link(t, "Out", tFlow, "A");
            b.Link(flow, "Out", tFlow, "B");
            b.Link(pos, "Out", sp, "In");
            b.Link(sp, "R", xz, "R");
            b.Link(sp, "B", xz, "G");
            b.Link(xz, "RG", scaled, "A");
            b.Link(scale, scaled, "B");
            b.Link(scaled, "Out", p, "A");
            b.Link(tFlow, "Out", p, "B");
            b.Link(p, "Out", fbm, "P");

            b.Link(thick, ratio, "A");
            b.Link(dist, ratio, "B");
            b.Link(ratio, "Out", ratioSat, "In");
            b.Link(ratioSat, "Out", shore, "In");
            b.Link(fbm, "Noise", n06, "A");
            b.Link(shore, "Out", shoreN, "A");
            b.Link(n06, "Out", shoreN, "B");
            b.Link(shoreN, "Out", shoreM, "A");
            b.Link(shoreM, "Out", foamA, "In");

            b.Link(rippleH, abs, "In");
            b.Link(abs, "Out", rh, "A");
            b.Link(rippleFoam, rh, "B");
            b.Link(rh, "Out", rhm, "A");
            b.Link(rhm, "Out", rhs, "In");
            b.Link(fbm, "Noise", mask, "In");
            b.Link(rhs, "Out", foamB, "A");
            b.Link(mask, "Out", foamB, "B");

            b.Link(foamA, "Out", sum, "A");
            b.Link(foamB, "Out", sum, "B");
            b.Link(sum, "Out", foam, "In");

            b.Output("Foam", V.Vector1, foam, "Out");
            b.PlaceOutput(14, 2.5f);

            b.Group("흐르는 거품 노이즈 (fBm 4겹 = HLSL JungleFbm)", time, speed, t, flow, tFlow, pos, sp, xz, scale, scaled, p, fbm);
            b.Group("물가 거품: 물이 얇을수록 + 노이즈로 들쭉날쭉", thick, dist, ratio, ratioSat, shore, n06, shoreN, shoreM, foamA);
            b.Group("물결 마루 거품: 캐릭터 물결이 높은 곳", rippleH, abs, rippleFoam, rh, rhm, rhs, mask, foamB);
            b.Note("SGS_Foam",
                "거품은 두 군데에서 생긴다. ① 물이 얇은 물가 ② 캐릭터·스킬 물결이 솟은 곳.\n" +
                "같은 노이즈를 써서 두 거품이 자연스럽게 이어진다.", 0, -1.4f);
        }

        // 하이라이트 (Blinn-Phong)
        private static void BuildSpecular(ShaderGraphBuilder b)
        {
            var normalProp = b.Vec3Prop("NormalWS");
            var dirProp = b.Vec3Prop("LightDirection");
            var colorProp = b.Vec3Prop("LightColor");
            var shadowProp = b.FloatProp("Shadow", 1f);
            var powerProp = b.FloatProp("SpecularPower", 180f);
            var strengthProp = b.FloatProp("SpecularStrength", 0.7f);
            var foamProp = b.FloatProp("Foam", 0f);

            var cam = b.Node<CameraNode>(0, 0);
            var pos = b.Node<PositionNode>(0, 1);
            var toCam = b.Node<SubtractNode>(1, 0.5f);
            var view = b.Node<NormalizeNode>(2, 0.5f);
            var dir = b.Prop(dirProp, 2, 1.5f);
            var half = b.Node<AddNode>(3, 1);
            var halfN = b.Node<NormalizeNode>(4, 1);
            var normal = b.Prop(normalProp, 4, 2);
            var ndh = b.Node<DotProductNode>(5, 1.5f);
            var sat = b.Node<SaturateNode>(6, 1.5f);
            var power = b.Prop(powerProp, 6, 2.5f);
            var pow = b.Node<PowerNode>(7, 1.5f);

            var color = b.Prop(colorProp, 7, 0);
            var m1 = b.Node<MultiplyNode>(8, 1);
            var strength = b.Prop(strengthProp, 8, 2);
            var m2 = b.Node<MultiplyNode>(9, 1);
            var shadow = b.Prop(shadowProp, 9, 2);
            var m3 = b.Node<MultiplyNode>(10, 1);
            var foam = b.Prop(foamProp, 10, 2);
            var noFoam = b.Node<OneMinusNode>(11, 2);
            var spec = b.Node<MultiplyNode>(12, 1);

            b.Link(cam, "Position", toCam, "A");
            b.Link(pos, "Out", toCam, "B");
            b.Link(toCam, "Out", view, "In");
            b.Link(dir, half, "A");
            b.Link(view, "Out", half, "B");
            b.Link(half, "Out", halfN, "In");
            b.Link(normal, ndh, "A");
            b.Link(halfN, "Out", ndh, "B");
            b.Link(ndh, "Out", sat, "In");
            b.Link(sat, "Out", pow, "A");
            b.Link(power, pow, "B");
            b.Link(color, m1, "A");
            b.Link(pow, "Out", m1, "B");
            b.Link(m1, "Out", m2, "A");
            b.Link(strength, m2, "B");
            b.Link(m2, "Out", m3, "A");
            b.Link(shadow, m3, "B");
            b.Link(foam, noFoam, "In");
            b.Link(m3, "Out", spec, "A");
            b.Link(noFoam, "Out", spec, "B");

            b.Output("Specular", V.Vector3, spec, "Out");
            b.PlaceOutput(13, 1);

            b.Group("하프 벡터 H = normalize(빛 방향 + 시선 방향)", cam, pos, toCam, view, dir, half, halfN);
            b.Group("반짝임 = (N·H)^세기", normal, ndh, sat, power, pow);
            b.Group("× 라이트 색 × 강도 × 그림자 × (거품 없는 곳만)", color, m1, strength, m2, shadow, m3, foam, noFoam, spec);
            b.Note("SGS_Specular",
                "해가 수면에 비쳐 반짝이는 점. 노멀이 빛과 시선의 중간 방향(H)을 향할수록 밝다.\n" +
                "거품 위에서는 반짝이지 않게 (1 - 거품)을 곱한다.", 0, -1.4f);
        }

        // 거품 얹기 + 하이라이트 더하기 + 투명도
        private static void BuildFinalComposite(ShaderGraphBuilder b)
        {
            var colorProp = b.Vec3Prop("Color");
            var foamProp = b.FloatProp("Foam", 0f);
            var foamColorProp = b.Vec4Prop("FoamColor");
            var diffuseProp = b.Vec3Prop("Diffuse");
            var specProp = b.Vec3Prop("Specular");
            var thickProp = b.FloatProp("Thickness", 0f);
            var edgeProp = b.FloatProp("EdgeFade", 0.05f);

            var foamColor = b.Prop(foamColorProp, 0, 0);
            var sf = b.Node<SplitNode>(1, 0);
            var foamRGB = b.Node<CombineNode>(2, 0);
            var diffuse = b.Prop(diffuseProp, 2, 1);
            var foamLit = b.Node<MultiplyNode>(3, 0.5f);
            var foam = b.Prop(foamProp, 1, 2);
            var foamAmt = b.Node<MultiplyNode>(2, 2);
            var color = b.Prop(colorProp, 3, 1.5f);
            var withFoam = b.Node<LerpNode>(4, 1);
            var spec = b.Prop(specProp, 4, 2.2f);
            var finalColor = b.Node<AddNode>(5, 1.5f);

            var thick = b.Prop(thickProp, 2, 3.2f);
            var edge = b.Prop(edgeProp, 2, 4);
            var ratio = b.Node<DivideNode>(3, 3.4f);
            var alphaSum = b.Node<AddNode>(4, 3.2f);
            var alpha = b.Node<SaturateNode>(5, 3.2f);

            b.Link(foamColor, sf, "In");
            b.Link(sf, "R", foamRGB, "R");
            b.Link(sf, "G", foamRGB, "G");
            b.Link(sf, "B", foamRGB, "B");
            b.Link(foamRGB, "RGB", foamLit, "A");
            b.Link(diffuse, foamLit, "B");
            b.Link(foam, foamAmt, "A");
            b.Link(sf, "A", foamAmt, "B");
            b.Link(color, withFoam, "A");
            b.Link(foamLit, "Out", withFoam, "B");
            b.Link(foamAmt, "Out", withFoam, "T");
            b.Link(withFoam, "Out", finalColor, "A");
            b.Link(spec, finalColor, "B");
            b.Link(thick, ratio, "A");
            b.Link(edge, ratio, "B");
            b.Link(ratio, "Out", alphaSum, "A");
            b.Link(foamAmt, "Out", alphaSum, "B");
            b.Link(alphaSum, "Out", alpha, "In");

            b.Output("Color", V.Vector3, finalColor, "Out");
            b.Output("Alpha", V.Vector1, alpha, "Out");
            b.PlaceOutput(6.3f, 2);

            b.Group("색 = 물 색 위에 거품 얹고 + 하이라이트", foamColor, sf, foamRGB, diffuse, foamLit, foam, foamAmt, color, withFoam, spec, finalColor);
            b.Group("투명도: 물가(두께≈0)에서 녹아 사라짐", thick, edge, ratio, alphaSum, alpha);
            b.Note("SGS_FinalComposite",
                "물가에서 물 판의 경계선이 보이지 않도록, 두께가 EdgeFade 보다 얇으면 투명해진다.", 0, -1.4f);
        }

        // =====================================================================
        // 메인 그래프
        // =====================================================================

        private static void BuildWaterGraph(ShaderGraphBuilder b,
            SubGraphAsset sceneBed, SubGraphAsset surfaceWaves, SubGraphAsset ripple, SubGraphAsset combineNormal,
            SubGraphAsset refraction, SubGraphAsset mainLight, SubGraphAsset waterColor, SubGraphAsset reflection,
            SubGraphAsset foam, SubGraphAsset specular, SubGraphAsset composite)
        {
            // HLSL 버전과 같은 이름 → 머티리얼 값을 그대로 복사해 비교 가능
            var shallowTint = b.ColorProp("Shallow Tint", new Color(0.78f, 0.86f, 0.68f, 1f), "_ShallowTint");
            var deepColor = b.ColorProp("Deep Color", new Color(0.09f, 0.16f, 0.11f, 1f), "_DeepColor");
            var murkiness = b.FloatProp("Murkiness", 3.5f, "_Murkiness", new Vector2(0.1f, 12f));
            var waveScale = b.FloatProp("Wave Scale", 1.4f, "_WaveScale");
            var waveSpeed = b.FloatProp("Wave Speed", 0.25f, "_WaveSpeed");
            var waveStrength = b.FloatProp("Wave Normal Strength", 0.3f, "_WaveStrength", new Vector2(0f, 2f));
            var refrStrength = b.FloatProp("Refraction", 0.02f, "_RefractionStrength", new Vector2(0f, 0.1f));
            var rippleStrength = b.FloatProp("Ripple Normal Strength", 18f, "_RippleStrength", new Vector2(0f, 60f));
            var rippleFoam = b.FloatProp("Ripple Foam", 2.5f, "_RippleFoam", new Vector2(0f, 10f));
            var foamColor = b.ColorProp("Foam Color", new Color(0.82f, 0.86f, 0.74f, 0.9f), "_FoamColor");
            var foamDistance = b.FloatProp("Shore Foam Distance", 0.12f, "_FoamDistance", new Vector2(0.01f, 1f));
            var foamNoiseScale = b.FloatProp("Foam Noise Scale", 3f, "_FoamNoiseScale");
            var edgeFade = b.FloatProp("Shore Edge Fade", 0.05f, "_EdgeFade", new Vector2(0.001f, 0.3f));
            var reflColor = b.ColorProp("Reflection Color", new Color(0.55f, 0.66f, 0.6f, 1f), "_ReflectionColor");
            var fresnelPower = b.FloatProp("Fresnel Power", 4f, "_FresnelPower", new Vector2(1f, 10f));
            var reflStrength = b.FloatProp("Reflection Strength", 0.5f, "_ReflectionStrength", new Vector2(0f, 1f));
            var specPower = b.FloatProp("Specular Power", 180f, "_SpecularPower", new Vector2(8f, 512f));
            var specStrength = b.FloatProp("Specular Strength", 0.7f, "_SpecularStrength", new Vector2(0f, 3f));

            // 1열: 입력
            var screen = b.Node<ScreenPositionNode>(-1, 0);
            var pWaveScale = b.Prop(waveScale, -1, 2);
            var pWaveSpeed = b.Prop(waveSpeed, -1, 2.6f);
            var pWaveStrength = b.Prop(waveStrength, -1, 3.2f);
            var pRippleStrength = b.Prop(rippleStrength, -1, 4.5f);

            // 2열: 표면 모양
            var bed = b.Sub(sceneBed, 0.5f, 0);
            var waves = b.Sub(surfaceWaves, 0.5f, 2);
            var rip = b.Sub(ripple, 0.5f, 4.5f);
            var normal = b.Sub(combineNormal, 2, 3);

            // 3열: 굴절 / 조명
            var pRefr = b.Prop(refrStrength, 2, 1.3f);
            var refr = b.Sub(refraction, 3.5f, 0.5f);
            var light = b.Sub(mainLight, 3.5f, 3.5f);

            // 4열: 색
            var pTint = b.Prop(shallowTint, 4.8f, -1.2f);
            var pDeep = b.Prop(deepColor, 4.8f, -0.6f);
            var pMurk = b.Prop(murkiness, 4.8f, 0f);
            var color = b.Sub(waterColor, 6, 0);
            var pRefl = b.Prop(reflColor, 6, 2);
            var pFresnel = b.Prop(fresnelPower, 6, 2.6f);
            var pReflStr = b.Prop(reflStrength, 6, 3.2f);
            var refl = b.Sub(reflection, 7.5f, 1);

            // 거품 / 하이라이트
            var pFoamDist = b.Prop(foamDistance, 4.8f, 5);
            var pFoamScale = b.Prop(foamNoiseScale, 4.8f, 5.6f);
            var pRippleFoam = b.Prop(rippleFoam, 4.8f, 6.2f);
            var fm = b.Sub(foam, 6, 5);
            var pSpecPow = b.Prop(specPower, 7.5f, 4.2f);
            var pSpecStr = b.Prop(specStrength, 7.5f, 4.8f);
            var spec = b.Sub(specular, 9, 3.5f);

            // 마지막
            var pFoamColor = b.Prop(foamColor, 9, 1.4f);
            var pEdge = b.Prop(edgeFade, 9, 2.1f);
            var final = b.Sub(composite, 10.5f, 1.5f);

            b.Link(screen, "Out", bed, "ScreenUV");
            b.Link(pWaveScale, waves, "WaveScale");
            b.Link(pWaveSpeed, waves, "WaveSpeed");
            b.Link(pWaveStrength, waves, "WaveStrength");
            b.Link(pRippleStrength, rip, "RippleStrength");
            b.Link(waves, "Gradient", normal, "WaveGradient");
            b.Link(rip, "NormalWS", normal, "RippleNormal");

            b.Link(normal, "NormalWS", refr, "NormalWS");
            b.Link(screen, "Out", refr, "ScreenUV");
            b.Link(bed, "Thickness", refr, "Thickness");
            b.Link(pRefr, refr, "RefractionStrength");
            b.Link(normal, "NormalWS", light, "NormalWS");

            b.Link(refr, "SceneColor", color, "SceneColor");
            b.Link(refr, "Thickness", color, "Thickness");
            b.Link(pTint, color, "ShallowTint");
            b.Link(pDeep, color, "DeepColor");
            b.Link(pMurk, color, "Murkiness");
            b.Link(light, "Diffuse", color, "Diffuse");

            b.Link(color, "Color", refl, "Color");
            b.Link(normal, "NormalWS", refl, "NormalWS");
            b.Link(pRefl, refl, "ReflectionColor");
            b.Link(pFresnel, refl, "FresnelPower");
            b.Link(pReflStr, refl, "ReflectionStrength");

            b.Link(refr, "Thickness", fm, "Thickness");
            b.Link(pFoamDist, fm, "FoamDistance");
            b.Link(pFoamScale, fm, "FoamNoiseScale");
            b.Link(pWaveSpeed, fm, "WaveSpeed");
            b.Link(rip, "Height", fm, "RippleHeight");
            b.Link(pRippleFoam, fm, "RippleFoam");

            b.Link(normal, "NormalWS", spec, "NormalWS");
            b.Link(light, "Direction", spec, "LightDirection");
            b.Link(light, "Color", spec, "LightColor");
            b.Link(light, "Shadow", spec, "Shadow");
            b.Link(pSpecPow, spec, "SpecularPower");
            b.Link(pSpecStr, spec, "SpecularStrength");
            b.Link(fm, "Foam", spec, "Foam");

            b.Link(refl, "Color", final, "Color");
            b.Link(fm, "Foam", final, "Foam");
            b.Link(pFoamColor, final, "FoamColor");
            b.Link(light, "Diffuse", final, "Diffuse");
            b.Link(spec, "Specular", final, "Specular");
            b.Link(refr, "Thickness", final, "Thickness");
            b.Link(pEdge, final, "EdgeFade");

            b.ToBlock(BlockFields.SurfaceDescription.BaseColor, final, "Color");
            b.ToBlock(BlockFields.SurfaceDescription.Alpha, final, "Alpha");

            b.Group("① 물 두께", screen, bed);
            b.Group("② 수면 노멀 (잔물결 + 캐릭터 물결)", pWaveScale, pWaveSpeed, pWaveStrength, pRippleStrength, waves, rip, normal);
            b.Group("③ 굴절 + 조명", pRefr, refr, light);
            b.Group("④ 물 색 + 반사", pTint, pDeep, pMurk, color, pRefl, pFresnel, pReflStr, refl);
            b.Group("⑤ 거품 + 하이라이트", pFoamDist, pFoamScale, pRippleFoam, fm, pSpecPow, pSpecStr, spec);
            b.Group("⑥ 합치기", pFoamColor, pEdge, final);
            b.Note("탑뷰 정글 얕은 물 (노드 버전)",
                "Sub Graph 하나 = 개념 하나. 더블클릭하면 안을 볼 수 있다.\n" +
                "HLSL을 쓴 곳(노드로 불가능한 것):\n" +
                " · 캐릭터 물결 = 이전 프레임을 쓰는 파동 시뮬레이션 (JungleRippleSim.shader)\n" +
                " · 라이트 색/그림자 = Unlit 그래프에 노드 없음\n" +
                " · 노이즈 = HLSL 버전과 무늬를 맞추려고 같은 함수 사용", -1, -3.2f, 520f, 190f);
        }

        private static void BuildFogGraph(ShaderGraphBuilder b, SubGraphAsset sceneBed, SubGraphAsset mainLight)
        {
            var fogColor = b.ColorProp("Fog Color", new Color(0.62f, 0.7f, 0.62f, 1f), "_FogColor");
            var density = b.FloatProp("Density", 1.1f, "_Density", new Vector2(0f, 4f));
            var thickness = b.FloatProp("Fog Thickness", 1.6f, "_FogThickness");
            var maxDist = b.FloatProp("Max March Distance", 25f, "_MaxMarchDistance");
            var noiseScale = b.FloatProp("Noise Scale", 0.35f, "_NoiseScale");
            var contrast = b.FloatProp("Noise Contrast", 1.6f, "_NoiseContrast", new Vector2(0.5f, 4f));
            var wind = b.Vec4Prop("Wind", new Vector4(0.3f, 0.05f, 0.15f, 0f), "_Wind");
            var lightInfluence = b.FloatProp("Light Influence", 0.4f, "_LightInfluence", new Vector2(0f, 1f));
            var revealRadius = b.FloatProp("Clear Radius", 3f, "_RevealRadius");
            var revealSoft = b.FloatProp("Clear Softness", 3f, "_RevealSoftness");
            var revealMin = b.FloatProp("Density Near Player", 0.15f, "_RevealMin", new Vector2(0f, 1f));

            var screen = b.Node<ScreenPositionNode>(0, 0);
            var pos = b.Node<PositionNode>(0, 1);
            var bed = b.Sub(sceneBed, 1.3f, 0);

            var march = b.Func("JungleFogMarch", 3, 1,
                new[]
                {
                    ("EntryWS", V.Vector3), ("ExitWS", V.Vector3), ("ScreenUV", V.Vector2),
                    ("Density", V.Vector1), ("Thickness", V.Vector1), ("MaxDistance", V.Vector1),
                    ("NoiseScale", V.Vector1), ("NoiseContrast", V.Vector1), ("Wind", V.Vector3),
                    ("RevealRadius", V.Vector1), ("RevealSoftness", V.Vector1), ("RevealMin", V.Vector1),
                },
                new[] { ("Fog", V.Vector1) });

            var props = new (AbstractShaderProperty prop, string slot)[]
            {
                (density, "Density"), (thickness, "Thickness"), (maxDist, "MaxDistance"),
                (noiseScale, "NoiseScale"), (contrast, "NoiseContrast"), (wind, "Wind"),
                (revealRadius, "RevealRadius"), (revealSoft, "RevealSoftness"), (revealMin, "RevealMin"),
            };
            var propNodes = new AbstractMaterialNode[props.Length];
            for (int i = 0; i < props.Length; i++)
            {
                propNodes[i] = b.Prop(props[i].prop, 1.6f, 1.6f + i * 0.55f);
                b.Link(propNodes[i], march, props[i].slot);
            }

            var up = b.Vec3(0f, 1f, 0f, 3, -1.2f);
            var light = b.Sub(mainLight, 4.3f, -1.2f);
            var color = b.Prop(fogColor, 4.3f, 0.4f);
            var sc = b.Node<SplitNode>(5.3f, 0.4f);
            var rgb = b.Node<CombineNode>(6.3f, 0.2f);
            var tinted = b.Node<MultiplyNode>(7.3f, -0.6f);
            var influence = b.Prop(lightInfluence, 7.3f, 0.6f);
            var finalColor = b.Node<LerpNode>(8.5f, 0);
            var alpha = b.Node<MultiplyNode>(8.5f, 1.5f);

            b.Link(screen, "Out", bed, "ScreenUV");
            b.Link(pos, "Out", march, "EntryWS");
            b.Link(bed, "BedPositionWS", march, "ExitWS");
            b.Link(screen, "Out", march, "ScreenUV");
            b.Link(up, "Out", light, "NormalWS");
            b.Link(color, sc, "In");
            b.Link(sc, "R", rgb, "R");
            b.Link(sc, "G", rgb, "G");
            b.Link(sc, "B", rgb, "B");
            b.Link(rgb, "RGB", tinted, "A");
            b.Link(light, "Color", tinted, "B");
            b.Link(rgb, "RGB", finalColor, "A");
            b.Link(tinted, "Out", finalColor, "B");
            b.Link(influence, finalColor, "T");
            b.Link(march, "Fog", alpha, "A");
            b.Link(sc, "A", alpha, "B");

            b.ToBlock(BlockFields.SurfaceDescription.BaseColor, finalColor, "Out");
            b.ToBlock(BlockFields.SurfaceDescription.Alpha, alpha, "Out");

            var group = new AbstractMaterialNode[propNodes.Length + 1];
            propNodes.CopyTo(group, 0);
            group[propNodes.Length] = march;
            b.Group("① 판(안개 윗면) → 바닥까지 = 안개를 지나는 구간", screen, pos, bed);
            b.Group("② 그 구간을 8걸음 걸으며 안개 쌓기 (반복문 → HLSL)", group);
            b.Group("③ 안개 색 = 해 색을 살짝 받음", up, light, color, sc, rgb, tinted, influence, finalColor, alpha);
            b.Note("탑뷰 낮게 깔리는 안개 (노드 버전)",
                "평평한 판을 안개층 윗면에 두고, 판에서 바닥까지 걸어 내려가며 안개 농도를 쌓는다.\n" +
                "낮은 곳(물가)일수록 구간이 길어 짙고, 나무·언덕은 위로 솟는다.\n" +
                "노드에는 반복문이 없어서 '8걸음 쌓기'만 HLSL(JungleFogMarch).", 0, -2.8f, 520f, 170f);
        }

        private static void BuildSplashGraph(ShaderGraphBuilder b)
        {
            var softness = b.FloatProp("Edge Softness", 0.5f, "_Softness", new Vector2(0.01f, 1f));

            var uv = b.Node<UVNode>(0, 0);
            var su = b.Node<SplitNode>(1, 0);
            var xy = b.Node<CombineNode>(2, 0);
            var x2 = b.Node<MultiplyNode>(3, 0, n => ShaderGraphBuilder.Set(n, "B", 2f));
            var centered = b.Node<SubtractNode>(4, 0, n => ShaderGraphBuilder.Set(n, "B", 1f));
            var dist = b.Node<LengthNode>(5, 0);
            var soft = b.Prop(softness, 4, 1);
            var edge1 = b.Node<OneMinusNode>(5, 1);
            var circle = b.Node<SmoothstepNode>(6, 0.5f, n => ShaderGraphBuilder.Set(n, "Edge2", 1f));
            var inside = b.Node<OneMinusNode>(7, 0.5f);
            var vc = b.Node<VertexColorNode>(6, 2);
            var svc = b.Node<SplitNode>(7, 2);
            var rgb = b.Node<CombineNode>(8, 2.5f);
            var alpha = b.Node<MultiplyNode>(8.5f, 1);

            b.Link(uv, "Out", su, "In");
            b.Link(su, "R", xy, "R");
            b.Link(su, "G", xy, "G");
            b.Link(xy, "RG", x2, "A");
            b.Link(x2, "Out", centered, "A");
            b.Link(centered, "Out", dist, "In");
            b.Link(soft, edge1, "In");
            b.Link(edge1, "Out", circle, "Edge1");
            b.Link(dist, "Out", circle, "In");
            b.Link(circle, "Out", inside, "In");
            b.Link(vc, "Out", svc, "In");
            b.Link(svc, "R", rgb, "R");
            b.Link(svc, "G", rgb, "G");
            b.Link(svc, "B", rgb, "B");
            b.Link(inside, "Out", alpha, "A");
            b.Link(svc, "A", alpha, "B");

            b.ToBlock(BlockFields.SurfaceDescription.BaseColor, rgb, "RGB");
            b.ToBlock(BlockFields.SurfaceDescription.Alpha, alpha, "Out");

            b.Note("물보라 파티클 (노드 버전)",
                "텍스처 없이 UV 중심에서의 거리로 부드러운 원을 만든다. 색·투명도는 파티클 색(Vertex Color).", 0, -1.4f);
        }

        // ---------------- 도우미 ----------------

        private static CustomFunctionNode NoiseNode(ShaderGraphBuilder b, float col, float row) =>
            b.Func("JungleValueNoise", col, row,
                new[] { ("P", V.Vector2) },
                new[] { ("Noise", V.Vector1) });

        // 노드 버전 머티리얼: HLSL 머티리얼 값을 그대로 복사 (같은 프로퍼티 이름)
        private static void CreateNodeMaterials()
        {
            CreateNodeMaterial(WaterGraphPath, "M_JungleWater", 2950);
            CreateNodeMaterial(FogGraphPath, "M_JungleFog", 2990);
            CreateNodeMaterial(SplashGraphPath, "M_WaterSplash", 3000);
        }

        private static void CreateNodeMaterial(string graphPath, string hlslMaterialName, int renderQueue)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(graphPath);
            if (shader == null)
            {
                Debug.LogError($"[JungleWater] 그래프 셰이더를 불러오지 못함: {graphPath}");
                return;
            }

            string path = $"{MatDir}/{hlslMaterialName}_Node.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            var hlsl = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{hlslMaterialName}.mat");
            if (hlsl != null)
                mat.CopyMatchingPropertiesFromMaterial(hlsl);

            mat.renderQueue = renderQueue;
            EditorUtility.SetDirty(mat);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

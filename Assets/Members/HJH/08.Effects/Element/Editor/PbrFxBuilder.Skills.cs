using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEditor;
using UnityEngine;

namespace Assets.Members.HJH.EditorTools
{
    // PBR 이펙트 2부: 입체 메시 이펙트 + 스킬별 전용 이펙트 + 스킬 수치 조정
    //  - 납작한 판(빌보드)만 쓰면 위에서 내려다볼 때 종이처럼 보여서, 원기둥·반구·포물면 메시에
    //    흐르는 노이즈 셰이더(HJH/FX/ErodeMesh)를 입힌 층을 섞음 → 불기둥, 회오리, 공기 가르는 막, 폭발 껍질
    //  - 스킬마다 시전/폭발/장판/오라/돌진 이펙트를 따로 만들어 그 스킬 에셋에만 연결
    public static partial class PbrFxBuilder
    {
        private static Texture2D _noise;
        private static Material _fireVol, _flame;   // 반실사 폭발 / 날름거리는 불꽃 (HJH/FX/FireVolume)
        private static Material _flameMesh, _flameJet, _flameGround, _heatShell, _bowFire, _bowWind, _windMesh, _dustMesh, _windGround, _meteorRock;
        private static Mesh _pillar, _jet, _tube, _funnel, _dome, _bow, _disc;

        // =====================================================================
        // 재료: 노이즈 텍스처, 메시, 머티리얼
        // =====================================================================

        private static void PrepareKit()
        {
            EnsureFolder(Root + "/Meshes");
            _noise = NoiseTexture();

            EditorUtility.DisplayProgressBar("PBR 이펙트", "볼륨 폭발 시트 굽는 중 (조금 걸림)", 0.6f);
            Texture2D explosion = ExplosionFlipbook();
            Texture2D flame = FlameFlipbook();
            _fireVol = Mat("M_FX_FireVolume", "HJH/FX/FireVolume", m =>
            {
                m.SetTexture("_MainTex", explosion);
                m.SetFloat("_Intensity", 1.7f);
                m.SetFloat("_TempCurve", 1.25f);
            });
            _flame = Mat("M_FX_FlameTongue", "HJH/FX/FireVolume", m =>
            {
                m.SetTexture("_MainTex", flame);
                m.SetFloat("_Intensity", 1.6f);
                m.SetFloat("_SoftDistance", 0.3f);
            });

            _pillar = SaveMesh(Cylinder(0.5f, 0.18f), "SM_FX_Pillar");     // 아래 넓고 위로 좁아지는 불기둥
            _jet = SaveMesh(Cylinder(0.08f, 0.5f), "SM_FX_Jet");           // 화염 방사: 입에서 좁고 멀수록 넓음
            _tube = SaveMesh(Cylinder(0.5f, 0.5f), "SM_FX_Tube");
            _funnel = SaveMesh(Cylinder(0.18f, 0.5f), "SM_FX_Funnel");     // 회오리 깔때기
            _dome = SaveMesh(Dome(), "SM_FX_Dome");
            _bow = SaveMesh(BowShock(), "SM_FX_BowShock");
            _disc = SaveMesh(Disc(), "SM_FX_Disc");

            _flameMesh = Erode("M_FX_FlameMesh", m =>
            {
                m.SetVector("_Noise1", new Vector4(2f, 1f, 0f, -1.5f));
                m.SetVector("_Noise2", new Vector4(3f, 1.6f, 0.2f, -2.4f));
                m.SetFloat("_Streak", 0.55f);
                m.SetFloat("_Density", 1.7f);
                m.SetFloat("_VMask", 0.6f);
                m.SetFloat("_FadeBottom", 0.08f);
                m.SetFloat("_FadeTop", 0.45f);
                m.SetFloat("_Fresnel", 0.35f);
                m.SetFloat("_Occlusion", 0.45f);
                m.SetFloat("_SoftDistance", 0.3f);
            });
            _flameJet = Erode("M_FX_FlameJet", m =>
            {
                m.SetVector("_Noise1", new Vector4(2f, 1.2f, 0f, -3.5f));
                m.SetVector("_Noise2", new Vector4(3f, 2f, 0.3f, -5f));
                m.SetFloat("_Streak", 0.7f);
                m.SetFloat("_Density", 1.6f);
                m.SetFloat("_VMask", 0.45f);
                m.SetFloat("_FadeBottom", 0.06f);
                m.SetFloat("_FadeTop", 0.35f);
                m.SetFloat("_Fresnel", 0.4f);
                m.SetFloat("_Occlusion", 0.4f);
            });
            _flameGround = Erode("M_FX_FlameGround", m =>
            {
                m.SetVector("_Noise1", new Vector4(6f, 2f, 0.05f, -0.25f));
                m.SetVector("_Noise2", new Vector4(9f, 3f, -0.04f, -0.4f));
                m.SetFloat("_Density", 1.35f);
                m.SetFloat("_VMask", 0f);
                m.SetFloat("_FadeBottom", 0.45f);   // 가운데는 비고 테두리가 탐
                m.SetFloat("_FadeTop", 0.12f);
                m.SetFloat("_Fresnel", 0f);
                m.SetFloat("_Occlusion", 0.55f);
                m.SetFloat("_SoftDistance", 0.1f);
            });
            _heatShell = Erode("M_FX_HeatShell", m =>
            {
                m.SetVector("_Noise1", new Vector4(3f, 2f, 0f, -0.8f));
                m.SetVector("_Noise2", new Vector4(5f, 3f, 0.1f, -1.2f));
                m.SetFloat("_Density", 1.6f);
                m.SetFloat("_VMask", 0.25f);
                m.SetFloat("_FadeBottom", 0.04f);
                m.SetFloat("_FadeTop", 0.08f);
                m.SetFloat("_Fresnel", 0.75f);
                m.SetFloat("_Occlusion", 0.35f);
            });
            _bowFire = Erode("M_FX_BowShockFire", m =>
            {
                m.SetVector("_Noise1", new Vector4(3f, 1.5f, 0f, -4f));
                m.SetVector("_Noise2", new Vector4(5f, 2f, 0.2f, -6f));
                m.SetFloat("_Streak", 0.8f);
                m.SetFloat("_Density", 1.6f);
                m.SetFloat("_VMask", 0.2f);
                m.SetFloat("_FadeBottom", 0.04f);
                m.SetFloat("_FadeTop", 0.55f);
                m.SetFloat("_Fresnel", 0.8f);
                m.SetFloat("_Occlusion", 0.2f);
                m.SetFloat("_SoftDistance", 0f);
            });
            _bowWind = Erode("M_FX_BowShockWind", m =>
            {
                m.SetFloat("_Mode", 1f);
                m.SetColor("_TintColor", new Color(0.86f, 0.95f, 0.92f, 1f));
                m.SetFloat("_Lit", 0.3f);
                m.SetVector("_Noise1", new Vector4(3f, 1.5f, 0f, -4f));
                m.SetVector("_Noise2", new Vector4(6f, 2f, 0.3f, -6f));
                m.SetFloat("_Streak", 0.95f);
                m.SetFloat("_Density", 1.5f);
                m.SetFloat("_VMask", 0.2f);
                m.SetFloat("_FadeBottom", 0.04f);
                m.SetFloat("_FadeTop", 0.55f);
                m.SetFloat("_Fresnel", 0.85f);
                m.SetFloat("_Occlusion", 0.12f);
                m.SetFloat("_Opacity", 0.9f);
                m.SetFloat("_SoftDistance", 0f);
            });
            _windMesh = Erode("M_FX_WindMesh", m =>
            {
                m.SetFloat("_Mode", 1f);
                m.SetColor("_TintColor", new Color(0.8f, 0.86f, 0.78f, 1f));
                m.SetFloat("_Lit", 0.6f);
                m.SetVector("_Noise1", new Vector4(3f, 1f, 1.3f, -0.4f));   // u 로 흘러서 빙글빙글 도는 느낌
                m.SetVector("_Noise2", new Vector4(5f, 1.5f, 1.9f, -0.7f));
                m.SetFloat("_Streak", 0.85f);
                m.SetFloat("_Density", 1.5f);
                m.SetFloat("_VMask", 0f);
                m.SetFloat("_FadeBottom", 0.12f);
                m.SetFloat("_FadeTop", 0.25f);
                m.SetFloat("_Fresnel", 0.6f);
                m.SetFloat("_Occlusion", 0.25f);
                m.SetFloat("_Opacity", 0.9f);
            });
            _dustMesh = Erode("M_FX_DustMesh", m =>
            {
                m.SetFloat("_Mode", 1f);
                m.SetColor("_TintColor", new Color(0.56f, 0.49f, 0.39f, 1f));
                m.SetFloat("_Lit", 0.85f);
                m.SetVector("_Noise1", new Vector4(3f, 1f, 0.9f, -0.3f));
                m.SetVector("_Noise2", new Vector4(5f, 1.5f, 1.3f, -0.5f));
                m.SetFloat("_Streak", 0.5f);
                m.SetFloat("_Density", 1.5f);
                m.SetFloat("_VMask", 0.35f);
                m.SetFloat("_FadeBottom", 0.1f);
                m.SetFloat("_FadeTop", 0.3f);
                m.SetFloat("_Fresnel", 0.5f);
                m.SetFloat("_Occlusion", 0.75f);
            });
            _windGround = Erode("M_FX_WindGround", m =>
            {
                m.SetFloat("_Mode", 1f);
                m.SetColor("_TintColor", new Color(0.72f, 0.76f, 0.66f, 1f));
                m.SetFloat("_Lit", 0.7f);
                m.SetVector("_Noise1", new Vector4(5f, 1f, 0.7f, 0f));
                m.SetVector("_Noise2", new Vector4(8f, 2f, 1f, -0.1f));
                m.SetFloat("_Streak", 1f);
                m.SetFloat("_Density", 1.4f);
                m.SetFloat("_VMask", 0f);
                m.SetFloat("_FadeBottom", 0.3f);
                m.SetFloat("_FadeTop", 0.2f);
                m.SetFloat("_Fresnel", 0f);
                m.SetFloat("_Occlusion", 0.4f);
                m.SetFloat("_SoftDistance", 0.1f);
            });

            // 구멍·밀도·투명도 (꽉 찬 판처럼 보이지 않게, 노이즈 사이로 뒤가 비치도록)
            Tune(_flameMesh, threshold: 0.2f, density: 1.3f, opacity: 1f);
            Tune(_flameJet, threshold: 0.2f, density: 1.3f, opacity: 1f);
            Tune(_flameGround, threshold: 0.45f, density: 1.25f, opacity: 0.9f, erode: 0.15f);
            Tune(_heatShell, threshold: 0.3f, density: 1.1f, opacity: 0.75f);
            _heatShell.SetFloat("_Fresnel", 0.85f);
            _heatShell.SetFloat("_FresnelPower", 2.2f);
            Tune(_bowFire, threshold: 0.2f, density: 1.4f, opacity: 0.9f);
            Tune(_bowWind, threshold: 0.25f, density: 1.3f, opacity: 0.6f);
            Tune(_windMesh, threshold: 0.3f, density: 1.2f, opacity: 0.6f);
            Tune(_dustMesh, threshold: 0.3f, density: 1.2f, opacity: 0.7f);
            Tune(_windGround, threshold: 0.45f, density: 1.2f, opacity: 0.5f, erode: 0.2f);
            foreach (Material fire in new[] { _flameMesh, _flameJet, _flameGround, _heatShell, _bowFire })
            {
                // 하얗게 날아가지 않게 심지도 주황 기운을 남김
                fire.SetColor("_CoreColor", new Color(1.7f, 1f, 0.4f));
                fire.SetColor("_MidColor", new Color(1.1f, 0.32f, 0.05f));
                fire.SetColor("_EdgeColor", new Color(0.22f, 0.035f, 0.01f));
            }
            // 바닥 불은 불씨처럼: 어두운 그을음 위로 갈라진 틈만 빨갛게 (밝은 판처럼 보이지 않게)
            _flameGround.SetColor("_CoreColor", new Color(1f, 0.3f, 0.05f));
            _flameGround.SetColor("_MidColor", new Color(0.55f, 0.1f, 0.015f));
            _flameGround.SetColor("_EdgeColor", new Color(0.1f, 0.02f, 0.005f));
            Tune(_flameGround, threshold: 0.5f, density: 1.3f, opacity: 0.85f, erode: 0.12f);
            Tune(_flameJet, threshold: 0.25f, density: 1.3f, opacity: 0.7f);

            // 바닥에 깔리는 원판: 맵이 울퉁불퉁해서 평평한 원판이 땅에 묻혀 잘림 → 0.8m 까지는 뚫고 보이게 (캐릭터 뒤는 가려짐)
            foreach (Material ground in new[] { _flameGround, _windGround })
            {
                ground.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                ground.SetFloat("_DepthTolerance", 0.8f);
                ground.SetFloat("_SoftDistance", 0f);
            }

            ApplyToonStyle();

            _meteorRock = Mat("M_FX_MeteorRock", "Universal Render Pipeline/Lit", m =>
            {
                m.SetColor("_BaseColor", new Color(0.12f, 0.09f, 0.07f, 1f));
                m.SetFloat("_Smoothness", 0.2f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(1.6f, 0.45f, 0.08f));   // 달궈진 바위
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            });
        }

        // 젠존제 풍 카툰 스타일 (피드백: 이펙트가 생각보다 너무 사실적 → 젠존제 레퍼런스)
        //  - 색이 계단으로 끊김 (흰노랑 심지 / 원색 / 진한 테두리), 외곽이 칼같이 또렷함, 연기는 2단 셀 음영
        //  - 끄려면 ToonStyle = false → 셰이더의 _Toon 이 0 이 되어 예전(반실사) 모습
        private const bool ToonStyle = true;

        private static void ApplyToonStyle()
        {
            float on = ToonStyle ? 1f : 0f;
            var fireCore = new Color(2f, 1.8f, 1.2f);
            var fireMid = new Color(1.8f, 0.62f, 0.08f);
            var fireEdge = new Color(0.75f, 0.1f, 0.04f);

            foreach (Material m in new[] { _fireVol, _flame })
            {
                m.SetFloat("_Toon", on);
                m.SetColor("_HotColor", fireCore);
                m.SetColor("_MidColor", fireMid);
                m.SetColor("_CoolColor", fireEdge);
                m.SetFloat("_Intensity", ToonStyle ? 1f : m.GetFloat("_Intensity"));
                m.SetColor("_SmokeColor", new Color(0.4f, 0.32f, 0.36f));
                m.SetColor("_SootShadow", new Color(0.14f, 0.1f, 0.15f));
                m.SetFloat("_AlphaCut", 0.3f);
            }

            foreach (Material m in new[] { _flameMesh, _flameJet, _heatShell, _bowFire })
            {
                m.SetFloat("_Toon", on);
                m.SetColor("_CoreColor", fireCore);
                m.SetColor("_MidColor", fireMid);
                m.SetColor("_EdgeColor", fireEdge);
            }
            _flameGround.SetFloat("_Toon", on);
            _flameGround.SetColor("_CoreColor", fireMid);
            _flameGround.SetColor("_MidColor", fireEdge);
            _flameGround.SetColor("_EdgeColor", new Color(0.16f, 0.04f, 0.04f));
            if (ToonStyle)   // 카툰이면 원판이 통째로 빨간 면이 되므로: 갈라진 불씨만 남게 좁히고 옅게
            {
                _flameGround.SetColor("_MidColor", new Color(0.55f, 0.08f, 0.03f));
                _flameGround.SetFloat("_Erode", 0.5f);
                _flameGround.SetFloat("_Opacity", 0.65f);
            }

            var windTint = new Color(0.65f, 1f, 0.9f);   // 청록빛 흰 바람
            foreach (Material m in new[] { _windMesh, _bowWind, _windGround })
            {
                m.SetFloat("_Toon", on);
                m.SetColor("_TintColor", windTint);
            }
            _dustMesh.SetFloat("_Toon", on);
            _dustMesh.SetColor("_TintColor", new Color(0.82f, 0.72f, 0.55f));

            // 연기·흙먼지: 2단 셀 음영
            _smoke.SetFloat("_Toon", on);
            _smoke.SetColor("_Color", ToonStyle ? new Color(0.42f, 0.36f, 0.4f) : new Color(0.32f, 0.3f, 0.28f));
            _smoke.SetColor("_ShadowColor", new Color(0.16f, 0.12f, 0.17f));
            _smoke.SetFloat("_AlphaCut", 0.35f);
            _dust.SetFloat("_Toon", on);
            _dust.SetColor("_Color", ToonStyle ? new Color(0.85f, 0.78f, 0.62f) : new Color(0.68f, 0.62f, 0.5f));
            _dust.SetColor("_ShadowColor", new Color(0.5f, 0.42f, 0.34f));
            _dust.SetFloat("_AlphaCut", 0.35f);

            foreach (Material m in new[] { _fireVol, _flame, _flameMesh, _flameJet, _heatShell, _bowFire, _flameGround, _windMesh, _bowWind, _windGround, _dustMesh, _smoke, _dust })
                EditorUtility.SetDirty(m);
        }

        private static void Tune(Material m, float threshold, float density, float opacity, float erode = 0.25f)
        {
            m.SetFloat("_Threshold", threshold);
            m.SetFloat("_Density", density);
            m.SetFloat("_Opacity", opacity);
            m.SetFloat("_Erode", erode);
            EditorUtility.SetDirty(m);
        }

        private static Material Erode(string name, System.Action<Material> setup) =>
            Mat(name, "HJH/FX/ErodeMesh", m =>
            {
                m.SetTexture("_NoiseTex", _noise);
                setup(m);
            });

        // 이어 붙여도 이음새 없는 노이즈 (R = 덩어리, G = 잔무늬, B = 세로 줄무늬)
        private static Texture2D NoiseTexture()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float r = Contrast(PeriodicFbm(u, v, 4, 4, 5, 11), 1.8f);
                float g = Contrast(PeriodicFbm(u, v, 6, 6, 4, 23), 1.8f);
                float b = Contrast(PeriodicFbm(u, v, 16, 2, 4, 37), 2f);
                px[y * size + x] = new Color(r, g, b, 1f);
            }
            tex.SetPixels(px);
            Texture2D result = WriteTexture(tex, "T_FX_Noise", srgb: false);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(result));
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.maxTextureSize = 256;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GetAssetPath(result));
        }

        // ---- 볼륨 폭발 시트: 3D 화구를 프레임마다 광선으로 훑어 구움 (8x8, R 음영 / G 온도 / A 밀도) ----
        //  구가 부풀면 무늬도 같이 부풀어 꽃양배추처럼 피어오르고, 시간이 지나면 식어서 그을음 연기가 됨
        private static Texture2D ExplosionFlipbook()
        {
            int size = Grid * FrameSize;
            var px = new Color[size * size];
            Vector3 light = new Vector3(0.45f, 0.8f, -0.4f).normalized;   // 위·오른쪽·앞에서 오는 해
            System.Threading.Tasks.Parallel.For(0, Grid * Grid, f =>
            {
                float t = f / (float)(Grid * Grid - 1);
                int ox = (f % Grid) * FrameSize, oy = (Grid - 1 - f / Grid) * FrameSize;
                float radius = 0.28f + 0.4f * (1f - Mathf.Pow(1f - t, 2.4f));   // 프레임 밖으로 안 나가게
                var center = new Vector3(0f, -0.15f + 0.3f * t, 0f);   // 뜨거워서 위로 떠오름
                const int steps = 18;
                const float dz = 2f / steps;
                for (int y = 0; y < FrameSize; y++)
                for (int x = 0; x < FrameSize; x++)
                {
                    float u = (x + 0.5f) / FrameSize * 2f - 1f, v = (y + 0.5f) / FrameSize * 2f - 1f;
                    float transmit = 1f, shade = 0f, glow = 0f;
                    for (int s = 0; s < steps && transmit > 0.02f; s++)
                    {
                        var p = new Vector3(u, v, -1f + (s + 0.5f) * dz);   // 앞(-z)에서 뒤로
                        float d = ExplosionDensity(p, center, radius, t, 5, out float temp);
                        if (d <= 0f)
                            continue;
                        float a = 1f - Mathf.Exp(-d * 10f * dz);
                        float occluder = ExplosionDensity(p + light * 0.22f, center, radius, t, 3, out _);
                        float lit = Mathf.Exp(-occluder * 3.5f);
                        shade += transmit * a * lit;
                        glow += transmit * a * temp;
                        transmit *= 1f - a;
                    }
                    float border = 1f - Smooth(0.82f, 0.98f, Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)));   // 프레임 가장자리 네모 자국 방지
                    float alpha = (1f - transmit) * border;
                    px[(oy + y) * size + ox + x] = alpha > 0.002f ? new Color(shade / alpha, glow / alpha, 0f, alpha) : new Color(1f, 0f, 0f, 0f);
                }
            });
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.SetPixels(px);
            return WriteTexture(tex, "T_FX_ExplosionVolume_8x8", srgb: false);
        }

        private static float ExplosionDensity(Vector3 p, Vector3 center, float radius, float t, int octaves, out float temp)
        {
            Vector3 q = (p - center) / radius;
            float dist = q.magnitude;
            if (dist > 1.7f)
            {
                temp = 0f;
                return 0f;
            }
            float n = Fbm(q.x * 2.3f + 5f, q.y * 2.3f, q.z * 2.3f + t * 0.8f, octaves);
            float billow = Mathf.Abs(n * 2f - 1f);   // 둥글게 부푼 덩어리 경계
            float shape = 1f - dist;
            float d = Mathf.Clamp01((shape * 1.5f + (n - 0.5f) * 1.5f - billow * 0.25f) * 3f);
            d = d * (1f - t * 0.5f) - t * t * 0.45f;   // 끝으로 갈수록 옅어져 흩어짐
            temp = Mathf.Clamp01((shape * 2.1f + (n - 0.5f) * 1.2f - 0.15f) * Mathf.Pow(1f - t, 1.35f) * 1.45f);   // 속만 뜨겁고 겉은 금방 그을음
            return Mathf.Max(0f, d);
        }

        // ---- 불 혀 시트: 아래가 굵고 위로 갈수록 가늘게 갈라지며 날름거리는 불꽃 (8x8) ----
        private static Texture2D FlameFlipbook()
        {
            int size = Grid * FrameSize;
            var px = new Color[size * size];
            System.Threading.Tasks.Parallel.For(0, Grid * Grid, f =>
            {
                float t = f / (float)(Grid * Grid - 1);
                int ox = (f % Grid) * FrameSize, oy = (Grid - 1 - f / Grid) * FrameSize;
                float life = Smooth(0f, 0.12f, t) * (1f - Smooth(0.65f, 1f, t));
                float tall = Mathf.Lerp(0.55f, 1f, Smooth(0f, 0.3f, t));
                for (int y = 0; y < FrameSize; y++)
                for (int x = 0; x < FrameSize; x++)
                {
                    float u = (x + 0.5f) / FrameSize * 2f - 1f, v = (y + 0.5f) / FrameSize * 2f - 1f;
                    float h = Mathf.Clamp01((v + 0.92f) / (1.8f * tall));   // 0 = 밑동, 1 = 끝
                    float sway = (Fbm(u * 1.6f, v * 1.4f - t * 5f, t * 2f, 3) - 0.5f) * (0.25f + h * 0.7f);
                    float xs = u + sway;
                    float width = 0.48f * Mathf.Pow(Mathf.Clamp01(1f - h), 0.55f) + 0.02f;
                    float n = Fbm(xs * 3.2f, v * 2.4f - t * 7f, t * 3f + 7f, 5);
                    float tongue = 1f - Mathf.Abs(xs) / width;
                    float dens = Mathf.Clamp01((tongue * 1.3f + (n - 0.5f) * 1.8f - h * 0.35f) * 2.4f);
                    if (v < -0.92f)
                        dens *= Mathf.Clamp01(1f + (v + 0.92f) * 12f);
                    float temp = Mathf.Clamp01(tongue * 1.1f + (n - 0.5f) * 0.8f) * (1f - h * 0.6f) * (1f - t * 0.4f);
                    px[(oy + y) * size + ox + x] = new Color(1f, temp, 0f, dens * life);
                }
            });
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.SetPixels(px);
            return WriteTexture(tex, "T_FX_FlameTongue_8x8", srgb: false);
        }

        private static float Contrast(float value, float amount) => Mathf.Clamp01((value - 0.5f) * amount + 0.5f);

        private static float PeriodicNoise(float x, float y, int px, int py, int seed)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int x0 = Mod(ix, px), x1 = Mod(ix + 1, px), y0 = Mod(iy, py), y1 = Mod(iy + 1, py);
            float a = Mathf.Lerp(Hash(x0, y0, seed), Hash(x1, y0, seed), fx);
            float b = Mathf.Lerp(Hash(x0, y1, seed), Hash(x1, y1, seed), fx);
            return Mathf.Lerp(a, b, fy);
        }

        private static float PeriodicFbm(float u, float v, int fx, int fy, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += PeriodicNoise(u * fx, v * fy, fx, fy, seed + i * 101) * amp;
                norm += amp;
                fx *= 2;
                fy *= 2;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        private static int Mod(int a, int m) => ((a % m) + m) % m;

        // ---- 메시 (u = 둘레, v = 아래→위 / 앞→뒤 / 가운데→바깥) ----
        private const int Segments = 28;
        private const int Rings = 10;

        private static Mesh Cylinder(float rBottom, float rTop)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int j = 0; j <= Rings; j++)
            {
                float v = j / (float)Rings;
                float r = Mathf.Lerp(rBottom, rTop, v);
                for (int i = 0; i <= Segments; i++)
                {
                    float u = i / (float)Segments, a = u * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    verts.Add(dir * r + Vector3.up * v);
                    normals.Add(dir);
                    uvs.Add(new Vector2(u, v));
                }
            }
            return Grid3D(verts, normals, uvs);
        }

        private static Mesh Dome()
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int j = 0; j <= Rings; j++)
            {
                float v = j / (float)Rings, el = v * Mathf.PI * 0.5f;
                for (int i = 0; i <= Segments; i++)
                {
                    float u = i / (float)Segments, a = u * Mathf.PI * 2f;
                    var n = new Vector3(Mathf.Cos(a) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Sin(a) * Mathf.Cos(el));
                    verts.Add(n * 0.5f);
                    normals.Add(n);
                    uvs.Add(new Vector2(u, v));
                }
            }
            return Grid3D(verts, normals, uvs);
        }

        // 앞(+Z)으로 볼록한 포물면: 꼭짓점 z = 0.5, 뒤로 벌어져 z = -0.5 에서 반지름 0.5
        private static Mesh BowShock()
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int j = 0; j <= Rings; j++)
            {
                float s = j / (float)Rings, r = 0.5f * s;
                for (int i = 0; i <= Segments; i++)
                {
                    float u = i / (float)Segments, a = u * Mathf.PI * 2f;
                    float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                    verts.Add(new Vector3(r * c, r * sn, 0.5f - s * s));
                    normals.Add(new Vector3(8f * r * c, 8f * r * sn, 1f).normalized);
                    uvs.Add(new Vector2(u, s));
                }
            }
            return Grid3D(verts, normals, uvs);
        }

        private static Mesh Disc()
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int j = 0; j <= Rings; j++)
            {
                float v = j / (float)Rings, r = 0.5f * v;
                for (int i = 0; i <= Segments; i++)
                {
                    float u = i / (float)Segments, a = u * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                    normals.Add(Vector3.up);
                    uvs.Add(new Vector2(u, v));
                }
            }
            return Grid3D(verts, normals, uvs);
        }

        private static Mesh Grid3D(List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs)
        {
            var tris = new List<int>();
            int row = Segments + 1;
            for (int j = 0; j < Rings; j++)
            for (int i = 0; i < Segments; i++)
            {
                int a = j * row + i, b = a + 1, c = a + row, d = c + 1;
                tris.AddRange(new[] { a, c, b, b, c, d });
            }
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // 같은 경로에 있으면 내용만 바꿔서 기존 참조 유지
        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"{Root}/Meshes/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            mesh.name = name;
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            existing.SetVertices(mesh.vertices);
            existing.SetNormals(mesh.normals);
            existing.SetUVs(0, new List<Vector2>(mesh.uv));
            existing.SetTriangles(mesh.triangles, 0);
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        // =====================================================================
        // 스킬별 이펙트 만들기 + 연결
        // =====================================================================

        private static void BuildSkillFx(Dictionary<string, GameObject> basic)
        {
            // 불
            GameObject meteorFall = Save(MeteorFall(), "FX_Fire_MeteorFall");
            GameObject meteorBurst = Save(MeteorBurst(), "FX_Fire_MeteorBurst");
            GameObject infernoBurst = Save(InfernoBurst(), "FX_Fire_InfernoBurst");
            GameObject frenzyCast = Save(FrenzyCast(), "FX_Fire_FrenzyCast");
            GameObject frenzyAura = Save(FrenzyAura(), "FX_Fire_FrenzyAura");
            GameObject stream = Save(FlameStream(), "FX_Fire_PbrStream");
            GameObject fireDash = Save(DashShock(fire: true), "FX_Fire_DashShock");
            GameObject veilRing = Save(FireVeilRing(), "FX_Fire_VeilRing");
            GameObject fireballBurst = Save(FireballBurst(), "FX_Fire_FireballBurst");

            // 바람
            GameObject gustCone = Save(GustCone(), "FX_Wind_GustCone");
            GameObject headwind = Save(HeadwindZone(), "FX_Wind_HeadwindZone");
            GameObject windDash = Save(DashShock(fire: false), "FX_Wind_DashShock");
            GameObject tailCast = Save(TailwindCast(), "FX_Wind_TailwindCast");
            GameObject tailAura = Save(TailwindAura(), "FX_Wind_TailwindAura");
            GameObject tornado = Save(TornadoTravel(), "FX_Wind_TornadoPbr");
            GameObject cutterCast = Save(CutterCast(), "FX_Wind_CutterCast");
            GameObject veilDome = Save(WindVeilDome(), "FX_Wind_VeilDome");

            Skill("Fire/Fire_Meteor", so =>
            {
                Slot(so, "windupFx", meteorFall);
                Slot(so, "impactFx", meteorBurst);
                Set(so, "hideMagicCircle", true);
                Set(so, "shake", 0.85f);
                Set(so, "hitStop", 0.14f);
            });
            Skill("Fire/Fire_Inferno", so =>
            {
                Slot(so, "impactFx", infernoBurst);
                Set(so, "shake", 0.6f);
                Set(so, "castShake", 0.15f);
            });
            Skill("Fire/Fire_Frenzy", so =>
            {
                Slot(so, "castFx", frenzyCast);
                Slot(so, "auraFx", frenzyAura);
                Set(so, "castShake", 0.3f);
            });
            Skill("Fire/Fire_FlameThrower", so =>
            {
                Slot(so, "streamFx", stream);
                Set(so, "length", 7.5f);
                Set(so, "angle", 45f);
                Set(so, "castShake", 0.1f);
            });
            Skill("Fire/Fire_FlameDash", so =>
            {
                Slot(so, "dashFx", fireDash);
                Set(so, "shake", 0.3f);
                Set(so, "castShake", 0.15f);
            });
            Skill("Fire/Fire_FireVeil", so =>
            {
                Slot(so, "zoneFx", veilRing);
                Set(so, "hideMagicCircle", true);
                Set(so, "castShake", 0.15f);
                Set(so, "pulseShake", 0.03f);
            });
            Skill("Fire/Fire_Fireball", so =>
            {
                Slot(so, "impactFx", fireballBurst);
                Set(so, "shake", 0.32f);
                Set(so, "hitStop", 0.07f);
            });

            Skill("Wind/Wind_Gust", so =>
            {
                Slot(so, "castFx", gustCone);
                Set(so, "length", 7.5f);
                Set(so, "angle", 80f);
                Set(so, "shake", 0.4f);
                Set(so, "castShake", 0.22f);
            });
            Skill("Wind/Wind_Headwind", so =>
            {
                Slot(so, "zoneFx", headwind);
                Set(so, "hideMagicCircle", true);
                Set(so, "pushFromCaster", true);
                Set(so, "pulseShake", 0.03f);
                Set(so, "castShake", 0.15f);
                Hits(so, (0, 3f, 0f), (2, 45f, 0.8f), (5, 1.4f, 0f));   // 피해 3, 둔화 45%, 나에게서 먼 쪽으로 1.4m 밀침 (0.5초마다)
            });
            Skill("Wind/Wind_Leap", so =>
            {
                Slot(so, "dashFx", windDash);
                Set(so, "shake", 0.45f);
                Set(so, "castShake", 0.15f);
            });
            Skill("Wind/Wind_Tailwind", so =>
            {
                // 순풍: 이동 +60%, 공속 +30%, 다른 스킬 쿨 50% 빨리, 대시 2회 충전 (6초) + 시전 때 주변 적 날려버림
                Slot(so, "castFx", tailCast);
                Slot(so, "auraFx", tailAura);
                Slot(so, "impactFx", basic["WindBurst"]);
                Selfs(so, (2, 60f, 6f), (3, 30f, 6f), (5, 50f, 6f), (6, 2f, 0f));
                Hits(so, (0, 15f, 0f), (5, 3.5f, 0f));
                Set(so, "explodeDelay", 0f);
                Set(so, "explodeRadius", 3.5f);
                Set(so, "shake", 0.35f);
                Set(so, "castShake", 0.3f);
            });
            Skill("Wind/Wind_Tornado", so =>
            {
                Slot(so, "travelFx", tornado);
                Set(so, "radius", 1.8f);
                Set(so, "range", 12f);
                Set(so, "shake", 0.3f);
                Set(so, "castShake", 0.18f);
            });
            Skill("Wind/Wind_WindCutter", so =>
            {
                Slot(so, "castFx", cutterCast);
                Set(so, "shake", 0.32f);
                Set(so, "hitStop", 0.07f);
                Set(so, "castShake", 0.2f);
            });
            Skill("Wind/Wind_WindVeil", so =>
            {
                Slot(so, "barrierFx", veilDome);
                Set(so, "castShake", 0.2f);
            });
        }

        private static void Skill(string name, System.Action<SerializedObject> edit)
        {
            string path = $"{SkillFolder}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null)
            {
                Debug.LogWarning($"[ElementFx] 스킬 에셋 없음: {path}");
                return;
            }
            var so = new SerializedObject(asset);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static void Slot(SerializedObject so, string field, GameObject prefab)
        {
            SerializedProperty slot = so.FindProperty(field);
            if (slot == null)
            {
                Debug.LogWarning($"[ElementFx] {so.targetObject.name} 에 {field} 칸 없음");
                return;
            }
            slot.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            slot.FindPropertyRelative("tint").boolValue = false;
        }

        private static void Set(SerializedObject so, string field, float value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
                Debug.LogWarning($"[ElementFx] {so.targetObject.name} 에 {field} 없음");
            else
                p.floatValue = value;
        }

        private static void Set(SerializedObject so, string field, bool value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
                Debug.LogWarning($"[ElementFx] {so.targetObject.name} 에 {field} 없음");
            else
                p.boolValue = value;
        }

        // (종류 번호, 값, 시간) — HitEffectKind: 0 피해, 1 화상, 2 둔화, 3 속박, 4 기절, 5 밀치기
        private static void Hits(SerializedObject so, params (int kind, float value, float duration)[] effects) =>
            Effects(so.FindProperty("onHit"), effects);

        // SelfEffectKind: 0 회복, 1 방어, 2 이동속도, 3 공속, 4 공격력, 5 쿨감, 6 대시충전, 7 받는피해, 8 쉴드
        private static void Selfs(SerializedObject so, params (int kind, float value, float duration)[] effects) =>
            Effects(so.FindProperty("onSelf"), effects);

        private static void Effects(SerializedProperty list, (int kind, float value, float duration)[] effects)
        {
            if (list == null)
                return;
            list.arraySize = effects.Length;
            for (int i = 0; i < effects.Length; i++)
            {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("kind").enumValueIndex = effects[i].kind;
                e.FindPropertyRelative("value").floatValue = effects[i].value;
                e.FindPropertyRelative("duration").floatValue = effects[i].duration;
            }
        }

        // =====================================================================
        // 불
        // =====================================================================

        // 메테오 예고: 하늘에서 달궈진 바위가 불꼬리를 끌며 떨어지고, 바닥은 점점 뜨겁게 달아오름 (0.7초)
        private static GameObject MeteorFall()
        {
            var root = new GameObject("FX_Fire_MeteorFall");

            ParticleSystem warn = MeshFx(root.transform, "GroundHeat", _flameGround, _disc, 0.85f, new Vector3(4.6f, 1f, 4.6f));
            warn.transform.localPosition = Vector3.up * 0.04f;
            Burst(warn, 0f, 1);
            GrowUniform(warn, 0.35f, 1f);
            Alpha(warn, (0f, 0.15f), (0.8f, 0.85f), (1f, 0.75f));   // 떨어질 때가 제일 뜨거움
            ParticleSystem warnCore = MeshFx(root.transform, "GroundHeatCore", _flameGround, _disc, 0.85f, new Vector3(2.4f, 1f, 2.4f));
            warnCore.transform.localPosition = Vector3.up * 0.05f;
            Burst(warnCore, 0.25f, 1);
            Alpha(warnCore, (0f, 0f), (1f, 0.8f));

            var meteor = new GameObject("Meteor").transform;
            meteor.SetParent(root.transform, false);
            var start = new Vector3(-3f, 13f, -2.5f);
            meteor.localPosition = start;
            root.AddComponent<FxLocalMover>().Configure(meteor, start, new Vector3(0f, 0.5f, 0f), 0.7f, 1.5f);

            ParticleSystem rock = MeshFx(meteor, "Rock", _meteorRock, _rock, 0.9f, Vector3.one * 1.3f);
            Burst(rock, 0f, 1);
            Spin(rock, 4f, 7f);
            rock.GetComponent<ParticleSystemRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            ParticleSystem core = NewSystem(meteor, "Core", _fireVol);
            Main(core, (0.2f, 0.3f), (0f, 0.3f), (1.6f, 2.3f), gravity: 0f);
            Loop(core, 40f);
            var coreShape = core.shape;
            coreShape.enabled = false;
            var coreMain = core.main;
            coreMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            Random3DRotation(core, true);
            Flipbook(core);

            ParticleSystem shock = MeshFx(meteor, "AirSplit", _bowFire, _bow, 0.2f, new Vector3(2f, 2f, 2.6f));
            shock.transform.localPosition = Vector3.forward * 0.6f;
            Loop(shock, 18f);
            Alpha(shock, (0f, 0.6f), (0.3f, 1f), (1f, 0f));

            ParticleSystem trail = NewSystem(meteor, "FireTrail", _fireVol);
            Main(trail, (0.35f, 0.55f), (0f, 0.5f), (1.2f, 1.9f), gravity: -0.2f);
            Trail(trail, 5f);
            Random3DRotation(trail, true);
            Grow(trail, 0.8f, 1.6f);
            Flipbook(trail);

            ParticleSystem smoke = NewSystem(meteor, "SmokeTrail", _smoke);
            Main(smoke, (1f, 1.6f), (0.2f, 0.6f), (1.4f, 2.4f), gravity: -0.1f);
            Trail(smoke, 2.5f);
            Random3DRotation(smoke, true);
            Grow(smoke, 0.6f, 1.8f);
            Fade(smoke, 0.1f, 0.3f);
            Flipbook(smoke);
            smoke.GetComponent<ParticleSystemRenderer>().sortingFudge = 20f;

            if (_spark != null)
            {
                ParticleSystem embers = NewSystem(meteor, "EmberTrail", _spark);
                Main(embers, (0.4f, 0.9f), (1f, 3f), (0.04f, 0.1f), gravity: 0.3f);
                var main = embers.main;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.1f), new Color(1f, 0.8f, 0.4f));
                Trail(embers, 8f);
                Fade(embers, 0f, 0.5f);
            }
            return root;
        }

        // 메테오 착탄: 눈부신 화구 → 열 껍질이 부풀고 → 버섯 불기둥 → 바위 파편·흙먼지 고리 → 불타는 크레이터 + 검은 연기
        private static GameObject MeteorBurst()
        {
            var root = new GameObject("FX_Fire_MeteorBurst");
            Fireballs(root.transform, count: 9, size: (2.4f, 3.8f), life: (0.55f, 0.9f), speed: (1.5f, 4f), radius: 0.6f);
            Shell(root.transform, _heatShell, 6f, 0.5f);
            Pillar(root.transform, "Mushroom", _flameMesh, new Vector3(2.6f, 6.5f, 2.6f), 0.85f, 0.02f);
            Pillar(root.transform, "MushroomCore", _flameMesh, new Vector3(1.3f, 5f, 1.3f), 0.6f, 0f);
            GroundDisc(root.transform, "Crater", _flameGround, 7f, 1.8f, 0.4f);
            RingFireballs(root.transform, 12, 1.4f, (5f, 8f), (1f, 1.6f));
            Debris(root.transform, count: 30, speed: (7f, 13f));
            DustRing(root.transform, count: 26, speed: (6f, 10f), size: (2.2f, 3.6f));
            Smoke(root.transform, _smoke, count: 14, size: (3f, 5f), life: (2.4f, 3.4f), color: new Color(1f, 1f, 1f, 0.95f), delay: 0.1f);
            Embers(root.transform, 60, (4f, 11f));
            Sparks(root.transform, 30, (6f, 10f));
            return root;
        }

        // 폭염: 내 주변으로 불바다가 퍼지고 + 내 머리 위로 짧은 불기둥이 솟음
        private static GameObject InfernoBurst()
        {
            var root = new GameObject("FX_Fire_InfernoBurst");
            Pillar(root.transform, "Pillar", _flameMesh, new Vector3(0.9f, 2.8f, 0.9f), 0.7f, 0f);
            Pillar(root.transform, "PillarCore", _flameMesh, new Vector3(0.45f, 3.2f, 0.45f), 0.55f, 0.03f);
            RisingFlames(root.transform, 18, 0.35f, (5f, 8f), (0.7f, 1.2f));
            Shell(root.transform, _heatShell, 4.4f, 0.45f);
            GroundDisc(root.transform, "FireSea", _flameGround, 4.6f, 1.1f, 0.2f);
            RingFireballs(root.transform, 16, 0.6f, (5f, 7f), (0.9f, 1.4f));
            DustRing(root.transform, count: 14, speed: (4f, 6.5f), size: (1.4f, 2.2f));
            Smoke(root.transform, _smoke, count: 6, size: (1.8f, 3f), life: (1.4f, 2.2f), color: new Color(1f, 1f, 1f, 0.8f), delay: 0.1f);
            Embers(root.transform, 34, (3f, 8f));
            return root;
        }

        // 폭주 시전: 몸에서 불기둥이 터져 오름
        private static GameObject FrenzyCast()
        {
            var root = new GameObject("FX_Fire_FrenzyCast");
            var feet = new GameObject("Feet").transform;
            feet.SetParent(root.transform, false);
            feet.localPosition = new Vector3(0f, -0.7f, -0.35f);   // 손 위치에서 나오므로 발밑으로
            Pillar(feet, "Pillar", _flameMesh, new Vector3(1.1f, 3.2f, 1.1f), 0.75f, 0f);
            Pillar(feet, "PillarCore", _flameMesh, new Vector3(0.55f, 3.6f, 0.55f), 0.6f, 0.04f);
            Shell(feet, _heatShell, 3f, 0.4f);
            GroundDisc(feet, "Ring", _flameGround, 3f, 0.9f, 0.3f);
            RingFireballs(feet, 10, 0.3f, (3f, 5f), (0.6f, 1f));
            RisingFlames(feet, 14, 0.3f, (4f, 7f), (0.6f, 1f));
            Embers(feet, 30, (3f, 7f));
            return root;
        }

        // 폭주 오라: 몸을 휘감는 불길 (5초). 원점 = 몸 가운데
        private static GameObject FrenzyAura()
        {
            var root = new GameObject("FX_Fire_FrenzyAura");
            var feet = new GameObject("Feet").transform;
            feet.SetParent(root.transform, false);
            feet.localPosition = Vector3.down * 1f;

            ParticleSystem tongues = MeshFx(feet, "FlameTongues", _flameMesh, _pillar, 0.6f, Vector3.one);
            Loop(tongues, 12f);
            SizeRange3D(tongues, new Vector3(0.5f, 1.5f, 0.5f), new Vector3(0.8f, 2.4f, 0.8f));
            var tShape = tongues.shape;
            tShape.enabled = true;
            tShape.shapeType = ParticleSystemShapeType.Circle;
            tShape.radius = 0.4f;
            tShape.rotation = new Vector3(90f, 0f, 0f);
            YRotation(tongues);
            Grow3D(tongues, new Vector3(0.7f, 0.3f, 0.7f), Vector3.one);
            Alpha(tongues, (0f, 0f), (0.15f, 1f), (0.5f, 0.8f), (1f, 0f));

            ParticleSystem flames = NewSystem(feet, "Flames", _flame);
            Main(flames, (0.45f, 0.7f), (0.3f, 0.8f), (0.9f, 1.5f), gravity: -0.6f);
            Loop(flames, 34f);
            Shape(flames, ParticleSystemShapeType.Circle, 0.45f);
            Upright(flames);
            Grow(flames, 0.7f, 1.2f);
            Flipbook(flames);

            ParticleSystem ground = MeshFx(feet, "GroundRing", _flameGround, _disc, 0.9f, new Vector3(2.6f, 1f, 2.6f));
            ground.transform.localPosition = Vector3.up * 0.04f;
            Loop(ground, 3f);
            YRotation(ground);
            Alpha(ground, (0f, 0f), (0.3f, 0.9f), (1f, 0f));

            if (_spark != null)
            {
                ParticleSystem embers = NewSystem(feet, "Embers", _spark);
                Main(embers, (0.7f, 1.3f), (0.5f, 1.5f), (0.04f, 0.09f), gravity: -0.4f);
                var main = embers.main;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.1f), new Color(1f, 0.8f, 0.4f));
                Loop(embers, 28f);
                Shape(embers, ParticleSystemShapeType.Circle, 0.6f);
                Fade(embers, 0f, 0.6f);
            }
            return root;
        }

        // 화염 방사 한 줄기 (0.15초마다 손에서): 7.5m 까지 뻗는 불 줄기 + 불덩이 + 끝에 연기
        private static GameObject FlameStream()
        {
            var root = new GameObject("FX_Fire_PbrStream");

            ParticleSystem jet = MeshFx(root.transform, "Jet", _flameJet, _jet, 0.32f, new Vector3(2.2f, 7.5f, 2.2f));
            Burst(jet, 0f, 1);
            var jMain = jet.main;
            jMain.startRotation3D = true;
            jMain.startRotationX = Mathf.PI * 0.5f;   // 위(+Y)로 선 메시를 앞(+Z)으로 눕힘
            jMain.startRotationY = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);   // 조준 방향 기준 좌우로만 살짝 (±7도)
            jMain.startRotationZ = 0f;   // 축 회전을 섞으면 줄기가 위·아래·옆으로 꺾여 나감 (버그였음)
            Grow3D(jet, new Vector3(0.5f, 0.35f, 0.5f), Vector3.one);
            Alpha(jet, (0f, 0.4f), (0.25f, 1f), (1f, 0f));

            ParticleSystem flames = NewSystem(root.transform, "Flames", _fireVol);
            Main(flames, (0.42f, 0.55f), (13f, 17f), (0.9f, 1.4f), gravity: -0.25f);
            Burst(flames, 0f, 14);
            ForwardCone(flames, 12f, 0.12f);   // 조준 방향으로 모이게 (안에서만 퍼짐)
            Drag(flames, 1.2f);
            Random3DRotation(flames, true);
            Grow(flames, 0.35f, 2.2f);
            Flipbook(flames);

            ParticleSystem smoke = NewSystem(root.transform, "Smoke", _smoke);
            Main(smoke, (0.8f, 1.2f), (8f, 11f), (1.2f, 2f), gravity: -0.15f);
            Burst(smoke, 0.05f, 3);
            ForwardCone(smoke, 18f, 0.2f);
            Drag(smoke, 2f);
            var sMain = smoke.main;
            sMain.startColor = new Color(1f, 1f, 1f, 0.55f);
            Random3DRotation(smoke, true);
            Grow(smoke, 0.4f, 2f);
            Fade(smoke, 0.3f, 0.5f);
            Flipbook(smoke);
            smoke.GetComponent<ParticleSystemRenderer>().sortingFudge = 20f;

            if (_spark != null)
            {
                ParticleSystem embers = NewSystem(root.transform, "Embers", _spark);
                Main(embers, (0.4f, 0.7f), (10f, 16f), (0.04f, 0.09f), gravity: -0.3f);
                var main = embers.main;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.1f), new Color(1f, 0.8f, 0.4f));
                Burst(embers, 0f, 6);
                ForwardCone(embers, 22f, 0.1f);
                Fade(embers, 0f, 0.6f);
            }
            return root;
        }

        // 돌진 중 몸 앞: 공기가 갈라지는 막 + 뒤로 흩날리는 불꽃/바람줄기. 원점 = 발, 앞 = +Z
        private static GameObject DashShock(bool fire)
        {
            var root = new GameObject(fire ? "FX_Fire_DashShock" : "FX_Wind_DashShock");

            ParticleSystem shock = MeshFx(root.transform, "AirSplit", fire ? _bowFire : _bowWind, _bow, 0.16f, new Vector3(2.2f, 2.4f, 2.4f));
            shock.transform.localPosition = new Vector3(0f, 1f, 0.7f);
            Loop(shock, 22f);
            Alpha(shock, (0f, 0.5f), (0.3f, 1f), (1f, 0f));

            ParticleSystem outer = MeshFx(root.transform, "AirSplitOuter", fire ? _bowFire : _bowWind, _bow, 0.22f, new Vector3(3f, 3.2f, 3.6f));
            outer.transform.localPosition = new Vector3(0f, 1f, 0.2f);
            Loop(outer, 10f);
            Alpha(outer, (0f, 0f), (0.3f, 0.7f), (1f, 0f));

            if (fire)
            {
                ParticleSystem flames = NewSystem(root.transform, "FlameWake", _fireVol);
                Main(flames, (0.3f, 0.45f), (0.5f, 1.5f), (0.8f, 1.4f), gravity: -0.4f);
                Loop(flames, 70f);
                Shape(flames, ParticleSystemShapeType.Sphere, 0.5f);
                flames.transform.localPosition = Vector3.up * 1f;
                Random3DRotation(flames, true);
                Grow(flames, 0.8f, 1.5f);
                Flipbook(flames);
            }
            else
            {
                ParticleSystem dust = NewSystem(root.transform, "DustWake", _dust);
                Main(dust, (0.5f, 0.8f), (0.5f, 1.5f), (0.8f, 1.4f), gravity: -0.1f);
                Loop(dust, 30f);
                Shape(dust, ParticleSystemShapeType.Sphere, 0.4f);
                dust.transform.localPosition = Vector3.up * 0.4f;
                var main = dust.main;
                main.startColor = new Color(1f, 1f, 1f, 0.7f);
                Random3DRotation(dust, true);
                Grow(dust, 0.6f, 1.6f);
                Fade(dust, 0.1f, 0.3f);
                Flipbook(dust);

                if (_leaf != null)
                {
                    ParticleSystem leaves = NewSystem(root.transform, "LeafWake", _leaf);
                    Main(leaves, (0.6f, 1f), (1.5f, 3f), (0.2f, 0.4f), gravity: 0.4f);
                    var lMain = leaves.main;
                    lMain.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.32f, 0.12f), new Color(0.36f, 0.28f, 0.14f));
                    Loop(leaves, 20f);
                    Shape(leaves, ParticleSystemShapeType.Sphere, 0.6f);
                    leaves.transform.localPosition = Vector3.up * 1f;
                    Fade(leaves, 0f, 0.7f);
                }
            }

            if (_spark != null)
            {
                ParticleSystem streaks = NewSystem(root.transform, "Streaks", _spark);
                Main(streaks, (0.12f, 0.2f), (2f, 4f), (0.05f, 0.09f), gravity: 0f);
                var main = streaks.main;
                main.startColor = fire ? new Color(1f, 0.6f, 0.25f, 0.9f) : new Color(0.8f, 0.95f, 0.9f, 0.7f);
                Loop(streaks, 60f);
                var shape = streaks.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.9f;
                shape.rotation = Vector3.zero;   // 진행 방향을 둘러싼 원
                shape.position = new Vector3(0f, 1f, 0.6f);
                var r = streaks.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.lengthScale = 6f;
                r.velocityScale = 0.05f;
                Velocity(streaks, true, Vector2.zero, Vector2.zero, new Vector2(-14f, -9f));   // 뒤로 휙
                Fade(streaks, 0f, 0.4f);
            }
            return root;
        }

        // 불의 장막: 내 둘레를 도는 불기둥 고리 (장판 유지 동안). 반지름 2m 기준
        private static GameObject FireVeilRing()
        {
            var root = new GameObject("FX_Fire_VeilRing");

            ParticleSystem ground = MeshFx(root.transform, "GroundFire", _flameGround, _disc, 1.1f, new Vector3(4.6f, 1f, 4.6f));
            ground.transform.localPosition = Vector3.up * 0.04f;
            Loop(ground, 2.5f);
            YRotation(ground);
            Alpha(ground, (0f, 0f), (0.3f, 0.95f), (1f, 0f));

            ParticleSystem pillars = MeshFx(root.transform, "FirePillars", _flameMesh, _pillar, 0.8f, Vector3.one);
            Loop(pillars, 16f);
            SizeRange3D(pillars, new Vector3(0.6f, 1.8f, 0.6f), new Vector3(0.95f, 3.2f, 0.95f));
            var pShape = pillars.shape;
            pShape.enabled = true;
            pShape.shapeType = ParticleSystemShapeType.Circle;
            pShape.radius = 2f;
            pShape.radiusThickness = 0f;
            pShape.rotation = new Vector3(90f, 0f, 0f);
            YRotation(pillars);
            Grow3D(pillars, new Vector3(0.7f, 0.2f, 0.7f), Vector3.one);
            Alpha(pillars, (0f, 0f), (0.15f, 1f), (0.55f, 0.8f), (1f, 0f));

            ParticleSystem flames = NewSystem(root.transform, "RingFlames", _flame);
            Main(flames, (0.5f, 0.8f), (0.2f, 0.6f), (1.1f, 1.8f), gravity: -0.7f);
            Loop(flames, 45f);
            var fShape = flames.shape;
            fShape.shapeType = ParticleSystemShapeType.Circle;
            fShape.radius = 2f;
            fShape.radiusThickness = 0f;
            fShape.rotation = new Vector3(90f, 0f, 0f);
            var fMain = flames.main;
            fMain.simulationSpace = ParticleSystemSimulationSpace.Local;   // 나를 따라다님
            Upright(flames);
            Grow(flames, 0.7f, 1.3f);
            Flipbook(flames);

            ParticleSystem smoke = NewSystem(root.transform, "Smoke", _smoke);
            Main(smoke, (1.2f, 1.8f), (0.5f, 1f), (1.2f, 2f), gravity: -0.15f);
            Loop(smoke, 5f);
            var sShape = smoke.shape;
            sShape.shapeType = ParticleSystemShapeType.Circle;
            sShape.radius = 2f;
            sShape.radiusThickness = 0f;
            sShape.rotation = new Vector3(90f, 0f, 0f);
            smoke.transform.localPosition = Vector3.up * 1.4f;
            var sMain = smoke.main;
            sMain.startColor = new Color(1f, 1f, 1f, 0.5f);
            Random3DRotation(smoke, true);
            Grow(smoke, 0.6f, 1.6f);
            Fade(smoke, 0.15f, 0.4f);
            Flipbook(smoke);
            smoke.GetComponent<ParticleSystemRenderer>().sortingFudge = 20f;

            if (_spark != null)
            {
                ParticleSystem embers = NewSystem(root.transform, "Embers", _spark);
                Main(embers, (0.8f, 1.4f), (0.5f, 1.5f), (0.04f, 0.09f), gravity: -0.4f);
                var main = embers.main;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.1f), new Color(1f, 0.8f, 0.4f));
                Loop(embers, 30f);
                var eShape = embers.shape;
                eShape.shapeType = ParticleSystemShapeType.Circle;
                eShape.radius = 2f;
                eShape.rotation = new Vector3(90f, 0f, 0f);
                Fade(embers, 0f, 0.6f);
            }
            return root;
        }

        // 화염구 착탄: 일반 폭발보다 화려하게 (불꽃 튀는 파편선 + 열 껍질 + 바닥 불)
        private static GameObject FireballBurst()
        {
            var root = new GameObject("FX_Fire_FireballBurst");
            Fireballs(root.transform, count: 7, size: (1.6f, 2.6f), life: (0.5f, 0.8f), speed: (1f, 3f), radius: 0.4f);
            Shell(root.transform, _heatShell, 3.6f, 0.4f);
            Pillar(root.transform, "Flare", _flameMesh, new Vector3(1f, 2.4f, 1f), 0.45f, 0f);
            GroundDisc(root.transform, "Scorch", _flameGround, 3.4f, 1f, 0.3f);
            Sparks(root.transform, 36, (5f, 9f));
            Debris(root.transform, count: 10, speed: (4f, 8f));
            Smoke(root.transform, _smoke, count: 6, size: (1.6f, 2.8f), life: (1.4f, 2f), color: new Color(1f, 1f, 1f, 0.8f), delay: 0.06f);
            Embers(root.transform, 34, (3f, 8f));
            return root;
        }

        // =====================================================================
        // 바람
        // =====================================================================

        // 돌풍: 앞으로 쏟아지는 바람 (손에서 1.6배로 재생 → 4.7m 로 만들면 7.5m)
        private static GameObject GustCone()
        {
            var root = new GameObject("FX_Wind_GustCone");

            ParticleSystem swoosh = MeshFx(root.transform, "Swoosh", _windMesh, _funnel, 0.35f, new Vector3(5.2f, 4.7f, 5.2f));
            Burst(swoosh, 0f, 2);
            var sMain = swoosh.main;
            sMain.startRotation3D = true;
            sMain.startRotationX = Mathf.PI * 0.5f;
            sMain.startRotationY = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            sMain.startRotationZ = 0f;
            Grow3D(swoosh, new Vector3(0.3f, 0.25f, 0.3f), Vector3.one);
            Alpha(swoosh, (0f, 0.6f), (0.2f, 1f), (1f, 0f));

            ParticleSystem dust = NewSystem(root.transform, "Dust", _dust);
            Main(dust, (0.45f, 0.65f), (8f, 12f), (0.8f, 1.4f), gravity: 0f);
            Burst(dust, 0f, 18);
            ForwardCone(dust, 36f, 0.3f);
            Drag(dust, 2f);
            var dMain = dust.main;
            dMain.startColor = new Color(1f, 1f, 1f, 0.8f);
            Random3DRotation(dust, true);
            Grow(dust, 0.5f, 1.8f);
            Fade(dust, 0.05f, 0.35f);
            Flipbook(dust);

            if (_spark != null)
            {
                ParticleSystem streaks = NewSystem(root.transform, "Streaks", _spark);
                Main(streaks, (0.18f, 0.3f), (14f, 20f), (0.05f, 0.09f), gravity: 0f);
                var main = streaks.main;
                main.startColor = new Color(0.8f, 0.95f, 0.9f, 0.7f);
                Burst(streaks, 0f, 34);
                ForwardCone(streaks, 38f, 0.3f);
                var r = streaks.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.lengthScale = 6f;
                r.velocityScale = 0.04f;
                Fade(streaks, 0f, 0.4f);
            }

            if (_leaf != null)
            {
                ParticleSystem leaves = NewSystem(root.transform, "Leaves", _leaf);
                Main(leaves, (0.6f, 1f), (6f, 10f), (0.2f, 0.4f), gravity: 0.3f);
                var main = leaves.main;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.32f, 0.12f), new Color(0.36f, 0.28f, 0.14f));
                Burst(leaves, 0f, 18);
                ForwardCone(leaves, 34f, 0.3f);
                Drag(leaves, 1.5f);
                var rot = leaves.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
                Fade(leaves, 0f, 0.7f);
            }
            return root;
        }

        // 역풍 장판: 나에게서 먼 쪽(+Z)으로 계속 부는 맞바람. 반지름 2m 기준
        private static GameObject HeadwindZone()
        {
            var root = new GameObject("FX_Wind_HeadwindZone");

            ParticleSystem ground = MeshFx(root.transform, "GroundGust", _windGround, _disc, 0.9f, new Vector3(4.4f, 1f, 4.4f));
            ground.transform.localPosition = Vector3.up * 0.05f;
            Loop(ground, 3f);
            YRotation(ground);
            Alpha(ground, (0f, 0f), (0.3f, 0.8f), (1f, 0f));

            ParticleSystem sheets = MeshFx(root.transform, "WindSheets", _bowWind, _bow, 0.7f, new Vector3(3.6f, 1.8f, 1.6f));
            sheets.transform.localPosition = new Vector3(0f, 0.9f, -1.6f);
            Loop(sheets, 5f);
            Velocity(sheets, true, Vector2.zero, Vector2.zero, new Vector2(4f, 5.5f));
            Alpha(sheets, (0f, 0f), (0.25f, 0.8f), (1f, 0f));

            ParticleSystem dust = NewSystem(root.transform, "Dust", _dust);
            Main(dust, (0.7f, 1f), (0f, 0f), (1f, 1.6f), gravity: 0f);
            Loop(dust, 14f);
            Box(dust, new Vector3(4f, 0.5f, 1f), new Vector3(0f, 0.6f, -1.5f));
            Velocity(dust, true, new Vector2(-0.5f, 0.5f), new Vector2(0f, 0.4f), new Vector2(4.5f, 6.5f));
            var dMain = dust.main;
            dMain.startColor = new Color(1f, 1f, 1f, 0.65f);
            Random3DRotation(dust, true);
            Grow(dust, 0.6f, 1.5f);
            Fade(dust, 0.15f, 0.4f);
            Flipbook(dust);

            if (_spark != null)
            {
                ParticleSystem streaks = NewSystem(root.transform, "Streaks", _spark);
                Main(streaks, (0.3f, 0.45f), (0f, 0f), (0.05f, 0.08f), gravity: 0f);
                var main = streaks.main;
                main.startColor = new Color(0.8f, 0.95f, 0.9f, 0.6f);
                Loop(streaks, 45f);
                Box(streaks, new Vector3(4f, 1.6f, 1f), new Vector3(0f, 1f, -1.8f));
                Velocity(streaks, true, Vector2.zero, Vector2.zero, new Vector2(11f, 15f));
                var r = streaks.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.lengthScale = 7f;
                r.velocityScale = 0.04f;
                Fade(streaks, 0f, 0.5f);
            }

            if (_leaf != null)
            {
                ParticleSystem leaves = NewSystem(root.transform, "Leaves", _leaf);
                Main(leaves, (0.8f, 1.2f), (0f, 0f), (0.2f, 0.4f), gravity: 0f);
                var main = leaves.main;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.32f, 0.12f), new Color(0.36f, 0.28f, 0.14f));
                Loop(leaves, 14f);
                Box(leaves, new Vector3(4f, 1.2f, 1f), new Vector3(0f, 0.8f, -1.6f));
                Velocity(leaves, true, new Vector2(-1f, 1f), new Vector2(-0.3f, 1f), new Vector2(5f, 8f));
                var rot = leaves.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
                Fade(leaves, 0f, 0.7f);
            }
            return root;
        }

        // 순풍 시전: 몸에서 바람이 폭발하듯 퍼짐 (손 위치에서 1.6배)
        private static GameObject TailwindCast()
        {
            var root = new GameObject("FX_Wind_TailwindCast");
            var feet = new GameObject("Feet").transform;
            feet.SetParent(root.transform, false);
            feet.localPosition = new Vector3(0f, -0.7f, -0.35f);

            Shell(feet, _windMesh, 4.5f, 0.5f);
            ParticleSystem column = MeshFx(feet, "Column", _windMesh, _funnel, 0.6f, new Vector3(1.6f, 3f, 1.6f));
            Burst(column, 0f, 2);
            YRotation(column);
            Spin(column, 6f, 9f);
            Grow3D(column, new Vector3(0.5f, 0.3f, 0.5f), Vector3.one);
            Alpha(column, (0f, 0.5f), (0.2f, 1f), (1f, 0f));
            GroundDisc(feet, "Swirl", _windGround, 4f, 0.7f, 0.3f);
            DustRing(feet, count: 18, speed: (4f, 7f), size: (1.2f, 2f));
            Leaves(feet, count: 24, radius: 0.8f);
            Gusts(feet, count: 30, speed: (12f, 18f));
            return root;
        }

        // 순풍 오라: 몸을 감도는 바람 고리 + 발밑 먼지 (6초). 원점 = 몸 가운데
        private static GameObject TailwindAura()
        {
            var root = new GameObject("FX_Wind_TailwindAura");
            var feet = new GameObject("Feet").transform;
            feet.SetParent(root.transform, false);
            feet.localPosition = Vector3.down * 1f;

            ParticleSystem coil = MeshFx(feet, "WindCoil", _windMesh, _funnel, 0.7f, new Vector3(1.5f, 2.3f, 1.5f));
            Loop(coil, 4f);
            YRotation(coil);
            Spin(coil, 7f, 10f);
            Alpha(coil, (0f, 0f), (0.3f, 0.8f), (1f, 0f));

            if (_spark != null)
            {
                ParticleSystem streaks = NewSystem(feet, "OrbitStreaks", _spark);
                Main(streaks, (0.35f, 0.5f), (0f, 0f), (0.04f, 0.07f), gravity: 0f);
                var main = streaks.main;
                main.startColor = new Color(0.8f, 0.95f, 0.9f, 0.7f);
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                Loop(streaks, 30f);
                Shape(streaks, ParticleSystemShapeType.Circle, 0.8f);
                Velocity(streaks, true, Vector2.zero, new Vector2(1f, 2.5f), Vector2.zero, new Vector2(9f, 12f));
                var r = streaks.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.lengthScale = 4f;
                r.velocityScale = 0.06f;
                Fade(streaks, 0f, 0.5f);
            }

            if (_leaf != null)
            {
                ParticleSystem leaves = NewSystem(feet, "OrbitLeaves", _leaf);
                Main(leaves, (0.8f, 1.2f), (0f, 0f), (0.2f, 0.35f), gravity: 0f);
                var main = leaves.main;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.32f, 0.12f), new Color(0.36f, 0.28f, 0.14f));
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                Loop(leaves, 8f);
                Shape(leaves, ParticleSystemShapeType.Circle, 0.9f);
                Velocity(leaves, true, Vector2.zero, new Vector2(0.8f, 1.8f), Vector2.zero, new Vector2(6f, 9f));
                Fade(leaves, 0f, 0.7f);
            }

            ParticleSystem dust = NewSystem(feet, "FootDust", _dust);
            Main(dust, (0.6f, 0.9f), (0.3f, 0.8f), (0.6f, 1f), gravity: -0.1f);
            Loop(dust, 10f);
            Shape(dust, ParticleSystemShapeType.Circle, 0.4f);
            dust.transform.localPosition = Vector3.up * 0.3f;
            var dMain = dust.main;
            dMain.startColor = new Color(1f, 1f, 1f, 0.6f);
            Random3DRotation(dust, true);
            Grow(dust, 0.6f, 1.5f);
            Fade(dust, 0.1f, 0.3f);
            Flipbook(dust);
            return root;
        }

        // 회오리 (날아가는 동안 붙어 있음, 반지름 1.8m): 거대한 깔때기 + 먼지 + 빨려 올라가는 파편·나뭇잎
        private static GameObject TornadoTravel()
        {
            var root = new GameObject("FX_Wind_TornadoPbr");

            ParticleSystem funnel = MeshFx(root.transform, "Funnel", _windMesh, _funnel, 0.55f, new Vector3(3.4f, 5.5f, 3.4f));
            Loop(funnel, 7f);
            YRotation(funnel);
            Spin(funnel, 8f, 12f);
            Alpha(funnel, (0f, 0f), (0.25f, 0.9f), (1f, 0f));

            ParticleSystem inner = MeshFx(root.transform, "DustFunnel", _dustMesh, _funnel, 0.5f, new Vector3(2.2f, 4.2f, 2.2f));
            Loop(inner, 6f);
            YRotation(inner);
            Spin(inner, 10f, 14f);
            Alpha(inner, (0f, 0f), (0.25f, 0.9f), (1f, 0f));

            ParticleSystem ground = MeshFx(root.transform, "GroundSwirl", _windGround, _disc, 0.5f, new Vector3(4.4f, 1f, 4.4f));
            ground.transform.localPosition = Vector3.up * 0.05f;
            Loop(ground, 4f);
            YRotation(ground);
            Alpha(ground, (0f, 0f), (0.3f, 0.8f), (1f, 0f));

            ParticleSystem dust = NewSystem(root.transform, "BaseDust", _dust);
            Main(dust, (0.7f, 1.1f), (0f, 0f), (1.2f, 2f), gravity: 0f);
            Loop(dust, 26f);
            Shape(dust, ParticleSystemShapeType.Circle, 1.3f);
            dust.transform.localPosition = Vector3.up * 0.4f;
            Velocity(dust, true, Vector2.zero, new Vector2(0.5f, 2f), Vector2.zero, new Vector2(5f, 7f), new Vector2(0.5f, 1.5f));
            var dMain = dust.main;
            dMain.startColor = new Color(1f, 1f, 1f, 0.8f);
            Random3DRotation(dust, true);
            Grow(dust, 0.6f, 1.6f);
            Fade(dust, 0.1f, 0.35f);
            Flipbook(dust);

            ParticleSystem debris = MeshFx(root.transform, "Debris", _debris, _rock, 1.2f, Vector3.one * 0.18f);
            Loop(debris, 10f);
            SizeRange3D(debris, Vector3.one * 0.1f, Vector3.one * 0.26f);
            Shape(debris, ParticleSystemShapeType.Circle, 1f);
            Velocity(debris, true, Vector2.zero, new Vector2(2f, 4f), Vector2.zero, new Vector2(6f, 9f));
            Random3DRotation(debris, false);
            Spin(debris, -8f, 8f);
            Alpha(debris, (0f, 1f), (0.8f, 1f), (1f, 0f));
            debris.GetComponent<ParticleSystemRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            if (_leaf != null)
            {
                ParticleSystem leaves = NewSystem(root.transform, "Leaves", _leaf);
                Main(leaves, (1f, 1.5f), (0f, 0f), (0.22f, 0.42f), gravity: 0f);
                var main = leaves.main;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.32f, 0.12f), new Color(0.36f, 0.28f, 0.14f));
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                Loop(leaves, 22f);
                Shape(leaves, ParticleSystemShapeType.Circle, 1.2f);
                Velocity(leaves, true, Vector2.zero, new Vector2(2f, 4f), Vector2.zero, new Vector2(7f, 10f), new Vector2(0.2f, 0.6f));
                Fade(leaves, 0f, 0.7f);
            }

            if (_spark != null)
            {
                ParticleSystem streaks = NewSystem(root.transform, "Streaks", _spark);
                Main(streaks, (0.4f, 0.6f), (0f, 0f), (0.05f, 0.08f), gravity: 0f);
                var main = streaks.main;
                main.startColor = new Color(0.8f, 0.95f, 0.9f, 0.6f);
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                Loop(streaks, 30f);
                Shape(streaks, ParticleSystemShapeType.Circle, 1.4f);
                Velocity(streaks, true, Vector2.zero, new Vector2(2f, 5f), Vector2.zero, new Vector2(10f, 14f));
                var r = streaks.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.lengthScale = 4f;
                r.velocityScale = 0.05f;
                Fade(streaks, 0f, 0.5f);
            }
            return root;
        }

        // 윈드 커터 시전: 앞으로 베어 나가는 바람 막 + 줄기 (손에서 1.6배)
        private static GameObject CutterCast()
        {
            var root = new GameObject("FX_Wind_CutterCast");
            ParticleSystem slash = MeshFx(root.transform, "Slash", _bowWind, _bow, 0.25f, new Vector3(3.2f, 0.7f, 1.6f));
            Burst(slash, 0f, 1);
            Velocity(slash, true, Vector2.zero, Vector2.zero, new Vector2(6f, 6f));
            Grow3D(slash, new Vector3(0.4f, 0.6f, 0.6f), Vector3.one);
            Alpha(slash, (0f, 0.8f), (0.2f, 1f), (1f, 0f));
            Gusts(root.transform, count: 14, speed: (12f, 16f));
            Leaves(root.transform, count: 8, radius: 0.3f);
            return root;
        }

        // 바람 장막: 나를 감싸는 바람 돔 + 도는 바람 벽 + 나뭇잎·먼지 소용돌이. 반지름 2m 기준
        private static GameObject WindVeilDome()
        {
            var root = new GameObject("FX_Wind_VeilDome");

            ParticleSystem dome = MeshFx(root.transform, "Dome", _windMesh, _dome, 0.8f, Vector3.one * 4.2f);
            Loop(dome, 4f);
            YRotation(dome);
            Spin(dome, 2f, 3f);
            Alpha(dome, (0f, 0f), (0.3f, 0.75f), (1f, 0f));

            ParticleSystem wall = MeshFx(root.transform, "Wall", _windMesh, _tube, 0.7f, new Vector3(4.2f, 2.4f, 4.2f));
            Loop(wall, 5f);
            YRotation(wall);
            Spin(wall, 4f, 6f);
            Alpha(wall, (0f, 0f), (0.3f, 0.9f), (1f, 0f));

            ParticleSystem ground = MeshFx(root.transform, "GroundSwirl", _windGround, _disc, 0.8f, new Vector3(4.8f, 1f, 4.8f));
            ground.transform.localPosition = Vector3.up * 0.05f;
            Loop(ground, 3f);
            YRotation(ground);
            Alpha(ground, (0f, 0f), (0.3f, 0.8f), (1f, 0f));

            ParticleSystem dust = NewSystem(root.transform, "Dust", _dust);
            Main(dust, (0.7f, 1f), (0f, 0f), (0.9f, 1.5f), gravity: 0f);
            Loop(dust, 18f);
            var dShape = dust.shape;
            dShape.shapeType = ParticleSystemShapeType.Circle;
            dShape.radius = 2f;
            dShape.radiusThickness = 0f;
            dShape.rotation = new Vector3(90f, 0f, 0f);
            dust.transform.localPosition = Vector3.up * 0.4f;
            Velocity(dust, true, Vector2.zero, new Vector2(0.3f, 1f), Vector2.zero, new Vector2(3f, 4f));
            var dMain = dust.main;
            dMain.startColor = new Color(1f, 1f, 1f, 0.6f);
            Random3DRotation(dust, true);
            Grow(dust, 0.6f, 1.4f);
            Fade(dust, 0.1f, 0.35f);
            Flipbook(dust);

            if (_leaf != null)
            {
                ParticleSystem leaves = NewSystem(root.transform, "Leaves", _leaf);
                Main(leaves, (1f, 1.4f), (0f, 0f), (0.22f, 0.4f), gravity: 0f);
                var main = leaves.main;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.32f, 0.12f), new Color(0.36f, 0.28f, 0.14f));
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                Loop(leaves, 24f);
                var lShape = leaves.shape;
                lShape.shapeType = ParticleSystemShapeType.Circle;
                lShape.radius = 2f;
                lShape.radiusThickness = 0.1f;
                lShape.rotation = new Vector3(90f, 0f, 0f);
                Velocity(leaves, true, Vector2.zero, new Vector2(0.3f, 1.4f), Vector2.zero, new Vector2(3f, 4.5f));
                Fade(leaves, 0f, 0.7f);
            }

            if (_spark != null)
            {
                ParticleSystem streaks = NewSystem(root.transform, "Streaks", _spark);
                Main(streaks, (0.4f, 0.6f), (0f, 0f), (0.05f, 0.08f), gravity: 0f);
                var main = streaks.main;
                main.startColor = new Color(0.8f, 0.95f, 0.9f, 0.6f);
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                Loop(streaks, 40f);
                var sShape = streaks.shape;
                sShape.shapeType = ParticleSystemShapeType.Circle;
                sShape.radius = 2f;
                sShape.radiusThickness = 0f;
                sShape.rotation = new Vector3(90f, 0f, 0f);
                streaks.transform.localPosition = Vector3.up * 1f;
                Velocity(streaks, true, Vector2.zero, new Vector2(-0.5f, 0.5f), Vector2.zero, new Vector2(4f, 5f));
                var r = streaks.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.lengthScale = 5f;
                r.velocityScale = 0.06f;
                Fade(streaks, 0f, 0.5f);
            }
            return root;
        }

        // =====================================================================
        // 공용 층
        // =====================================================================

        // 반구 껍질이 확 부풀며 갉아먹히듯 사라짐 (폭발의 부피감)
        private static void Shell(Transform parent, Material mat, float size, float life, float delay = 0f)
        {
            ParticleSystem ps = MeshFx(parent, "Shell", mat, _dome, life, Vector3.one * size);
            Burst(ps, delay, 1);
            YRotation(ps);
            GrowUniform(ps, 0.2f, 1f, fast: true);
            Alpha(ps, (0f, 1f), (0.25f, 0.9f), (1f, 0f));
            ps.GetComponent<ParticleSystemRenderer>().sortingFudge = -10f;
        }

        // 바닥에 깔리는 불/바람 원판
        private static void GroundDisc(Transform parent, string name, Material mat, float size, float life, float growFrom)
        {
            ParticleSystem ps = MeshFx(parent, name, mat, _disc, life, new Vector3(size, 1f, size));
            ps.transform.localPosition = Vector3.up * 0.05f;
            Burst(ps, 0f, 1);
            YRotation(ps);
            GrowUniform(ps, growFrom, 1f, fast: true);
            Alpha(ps, (0f, 1f), (0.35f, 0.9f), (1f, 0f));
        }

        // 위로 솟는 불기둥 (원기둥 메시)
        private static void Pillar(Transform parent, string name, Material mat, Vector3 size, float life, float delay)
        {
            ParticleSystem ps = MeshFx(parent, name, mat, _pillar, life, size);
            Burst(ps, delay, 1);
            YRotation(ps);
            Grow3D(ps, new Vector3(0.6f, 0.15f, 0.6f), Vector3.one);
            Alpha(ps, (0f, 1f), (0.3f, 0.95f), (1f, 0f));
        }

        // 고리 모양으로 바깥으로 퍼지는 불덩이
        private static void RingFireballs(Transform parent, int count, float radius, (float, float) speed, (float, float) size)
        {
            ParticleSystem ps = NewSystem(parent, "RingFire", _fireVol);
            Main(ps, (0.45f, 0.7f), speed, size, gravity: -0.3f);
            Burst(ps, 0.03f, count);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = 0f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            shape.position = Vector3.up * 0.5f;
            Drag(ps, 3f);
            Random3DRotation(ps, true);
            Grow(ps, 0.6f, 1.5f);
            Flipbook(ps);
        }

        // 위로 치솟는 불꽃 (불기둥 속을 채움)
        private static void RisingFlames(Transform parent, int count, float radius, (float, float) speed, (float, float) size)
        {
            ParticleSystem ps = NewSystem(parent, "RisingFlames", _flame);
            Main(ps, (0.4f, 0.6f), speed, size, gravity: 0f);
            Burst(ps, 0f, count);
            Shape(ps, ParticleSystemShapeType.Cone, radius, angle: 8f);
            Drag(ps, 1.5f);
            Upright(ps);
            Grow(ps, 0.6f, 1.4f);
            Flipbook(ps);
        }

        // 길게 늘어난 불티 (튀는 파편선)
        private static void Sparks(Transform parent, int count, (float, float) speed)
        {
            if (_spark == null)
                return;
            ParticleSystem ps = NewSystem(parent, "Sparks", _spark);
            Main(ps, (0.12f, 0.22f), speed, (0.05f, 0.1f), gravity: 2f);   // 짧게: 맞은 자리 주변 1~2m 만 (멀리 날아가 화면을 가로지르지 않게)
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.85f, 0.5f));
            Burst(ps, 0f, count);
            Shape(ps, ParticleSystemShapeType.Cone, 0.3f, angle: 65f);   // 위쪽으로 퍼지는 원뿔 (Shape 가 위로 세움)
            Drag(ps, 4f);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 2f;
            r.velocityScale = 0.06f;
            Fade(ps, 0f, 0.5f);
        }

        // =====================================================================
        // 도우미
        // =====================================================================

        // 메시 파티클 하나 (원기둥·반구 등). 기본은 시스템 원점에서 로컬 공간으로
        private static ParticleSystem MeshFx(Transform parent, string name, Material mat, Mesh mesh, float life, Vector3 size)
        {
            ParticleSystem ps = NewSystem(parent, name, mat);
            Main(ps, (life, life), (0f, 0f), (1f, 1f), gravity: 0f);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSize3D = true;
            main.startSizeX = size.x;
            main.startSizeY = size.y;
            main.startSizeZ = size.z;
            var shape = ps.shape;
            shape.enabled = false;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = mesh;
            r.alignment = ParticleSystemRenderSpace.Local;
            r.enableGPUInstancing = false;   // 이 셰이더는 파티클 인스턴싱을 지원하지 않음
            return ps;
        }

        private static void Loop(ParticleSystem ps, float rate)
        {
            var main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.maxParticles = 200;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            emission.SetBursts(new ParticleSystem.Burst[0]);
        }

        // 움직이는 동안만 거리당 방출 (꼬리)
        private static void Trail(ParticleSystem ps, float perMeter)
        {
            var main = ps.main;
            main.loop = true;
            main.maxParticles = 300;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = perMeter;
            Shape(ps, ParticleSystemShapeType.Sphere, 0.3f);
        }

        // 불꽃은 세워서 (거의 안 기울고 안 돔)
        private static void Upright(ParticleSystem ps)
        {
            var main = ps.main;
            main.startRotation3D = false;
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            var rot = ps.rotationOverLifetime;
            rot.enabled = false;
        }

        private static void YRotation(ParticleSystem ps)
        {
            var main = ps.main;
            main.startRotation3D = true;
            main.startRotationX = 0f;
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = 0f;
        }

        // Y 축으로 빙글빙글 (rad/s)
        private static void Spin(ParticleSystem ps, float min, float max)
        {
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            rot.y = new ParticleSystem.MinMaxCurve(min, max);
            rot.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        }

        private static void SizeRange3D(ParticleSystem ps, Vector3 min, Vector3 max)
        {
            var main = ps.main;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(min.x, max.x);
            main.startSizeY = new ParticleSystem.MinMaxCurve(min.y, max.y);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(min.z, max.z);
        }

        private static void GrowUniform(ParticleSystem ps, float from, float to, bool fast = false)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = false;
            size.size = new ParticleSystem.MinMaxCurve(1f, fast ? FastCurve(from, to) : AnimationCurve.EaseInOut(0f, from, 1f, to));
        }

        private static void Grow3D(ParticleSystem ps, Vector3 from, Vector3 to)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = true;
            size.x = new ParticleSystem.MinMaxCurve(1f, FastCurve(from.x, to.x));
            size.y = new ParticleSystem.MinMaxCurve(1f, FastCurve(from.y, to.y));
            size.z = new ParticleSystem.MinMaxCurve(1f, FastCurve(from.z, to.z));
        }

        // 처음에 확 커지고 천천히 마무리 (폭발 느낌)
        private static AnimationCurve FastCurve(float from, float to) =>
            new(new Keyframe(0f, from, 0f, (to - from) * 4f), new Keyframe(0.3f, from + (to - from) * 0.8f), new Keyframe(1f, to, 0f, 0f));

        // 알파 키 (시간, 알파) — 메시 층은 알파가 줄면 노이즈가 갉아먹히듯 사라짐
        private static void Alpha(ParticleSystem ps, params (float time, float alpha)[] keys)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var alphas = new GradientAlphaKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
                alphas[i] = new GradientAlphaKey(keys[i].alpha, keys[i].time);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alphas);
            color.color = g;
        }

        // 앞(+Z)으로 퍼지는 원뿔
        private static void ForwardCone(ParticleSystem ps, float angle, float radius)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
            shape.rotation = Vector3.zero;
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
        }

        private static void Box(ParticleSystem ps, Vector3 size, Vector3 position)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            shape.position = position;
            shape.rotation = Vector3.zero;
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;   // 장판 방향(+Z)을 따름
        }

        // 확 퍼졌다가 공기 저항으로 느려짐
        private static void Drag(ParticleSystem ps, float drag)
        {
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.drag = drag;
            limit.limit = 100f;
            limit.multiplyDragByParticleSize = false;   // 기본값(크기 비례)이면 큰 불덩이가 제자리에 멈춤
            limit.multiplyDragByParticleVelocity = false;
        }

        // 속도 (x·y·z 와 궤도·방사는 같은 모드로 맞춰야 함)
        private static void Velocity(ParticleSystem ps, bool local, Vector2 x, Vector2 y, Vector2 z, Vector2 orbitalY = default, Vector2 radial = default)
        {
            var v = ps.velocityOverLifetime;
            v.enabled = true;
            v.space = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
            v.x = new ParticleSystem.MinMaxCurve(x.x, x.y);
            v.y = new ParticleSystem.MinMaxCurve(y.x, y.y);
            v.z = new ParticleSystem.MinMaxCurve(z.x, z.y);
            v.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            v.orbitalY = new ParticleSystem.MinMaxCurve(orbitalY.x, orbitalY.y);
            v.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
            v.radial = new ParticleSystem.MinMaxCurve(radial.x, radial.y);
        }
    }
}

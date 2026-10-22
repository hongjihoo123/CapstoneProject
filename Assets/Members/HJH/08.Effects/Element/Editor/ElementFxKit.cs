using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Assets.Members.HJH.Effects.Element.EditorTools
{
    // 원소 이펙트 세트 (불/물/바람/땅/번개 × 맞음·터짐·시전 + 스킬별 특수 이펙트 + CC 표시)를 코드로 한 번에 만들고,
    // 원소 스킬 34개의 이펙트 칸에 꽂음. 결과물은 08.Effects/Element/Kit 아래 평범한 프리팹 → 그다음부턴 손으로 고쳐도 됨.
    //  ⚠️ 다시 돌리면 Kit 프리팹을 덮어씀 (손으로 고친 게 날아감). 스킬 에셋은 아래 표에 있는 칸만 바꿈.
    //
    // 크기 기준: 터짐/장판 = 반지름 2m, 벽 = 폭 5m (스킬이 범위에 맞춰 키워서 씀). 맞음 = 몸 높이에서 1m 정도.
    // 색은 프리팹에 직접 칠함 (스킬 칸 tint 끔).
    public static class ElementFxKit
    {
        private const string Root = "Assets/Members/HJH/08.Effects/Element/Kit";
        private const string Learn = "Assets/Members/HJH/08.Effects/Learn/Materials/";
        private const string SkillFolder = "Assets/Members/HJH/05.Data/Skill/Element/";
        private const string FinisherFolder = "Assets/Members/HJH/05.Data/Skill/Finisher/";

        // ---------- 원소 색 ----------
        private readonly struct Pal
        {
            public readonly string Name;
            public readonly Color Hot, Main, Deep, Dust;
            public Pal(string name, Color hot, Color main, Color deep, Color dust)
            {
                Name = name; Hot = hot; Main = main; Deep = deep; Dust = dust;
            }
        }

        private static readonly Pal Fire = new("Fire", C(1f, 0.93f, 0.7f), C(1f, 0.45f, 0.08f), C(0.75f, 0.1f, 0.02f), C(0.16f, 0.11f, 0.09f));
        private static readonly Pal Water = new("Water", C(0.85f, 0.97f, 1f), C(0.25f, 0.68f, 1f), C(0.05f, 0.28f, 0.75f), C(0.7f, 0.85f, 0.95f));
        private static readonly Pal Wind = new("Wind", C(0.92f, 1f, 0.96f), C(0.5f, 1f, 0.78f), C(0.12f, 0.6f, 0.45f), C(0.78f, 0.8f, 0.68f));
        private static readonly Pal Earth = new("Earth", C(1f, 0.85f, 0.55f), C(0.88f, 0.56f, 0.24f), C(0.42f, 0.26f, 0.12f), C(0.5f, 0.4f, 0.3f));
        private static readonly Pal Electric = new("Electric", C(1f, 0.97f, 0.72f), C(1f, 0.88f, 0.3f), C(0.95f, 0.55f, 0.05f), C(0.6f, 0.6f, 0.7f));

        // ---------- 머티리얼 ----------
        private static Material _glow, _spark, _ring, _smoke, _star, _flame, _tongue, _bolt, _drop, _rock, _crack, _swirl, _scorch;

        [MenuItem("HJH/Element FX/Build Kit (원소 이펙트 세트)")]
        public static void BuildAll()
        {
            EnsureFolders();
            LoadMaterials();

            var built = new Dictionary<string, GameObject>();
            foreach (Pal p in new[] { Fire, Water, Wind, Earth, Electric })
            {
                built[$"{p.Name}_Hit"] = Save(Hit(p), p.Name, "Hit");
                built[$"{p.Name}_Impact"] = Save(Impact(p), p.Name, "Impact");
                built[$"{p.Name}_Cast"] = Save(Cast(p), p.Name, "Cast");
                built[$"{p.Name}_Windup"] = Save(Windup(p), p.Name, "Windup");
            }

            built["Fire_Stream"] = Save(FireStream(), "Fire", "Stream");
            built["Fire_Ring"] = Save(FireRing(), "Fire", "Ring");
            built["Fire_Aura"] = Save(FireAura(), "Fire", "Aura");
            built["Water_Wave"] = Save(WaterWave(), "Water", "Wave");
            built["Water_Whirlpool"] = Save(Whirlpool(), "Water", "Whirlpool");
            built["Water_Wall"] = Save(WaterWall(), "Water", "Wall");
            built["Wind_Tornado"] = Save(Tornado(), "Wind", "Tornado");
            built["Wind_Headwind"] = Save(Headwind(), "Wind", "Headwind");
            built["Wind_Aura"] = Save(WindAura(), "Wind", "Aura");
            built["Wind_Dome"] = Save(WindDome(), "Wind", "Dome");
            built["Earth_Boulder"] = Save(Boulder(), "Earth", "Boulder");
            built["Earth_Wall"] = Save(RockWall(), "Earth", "Wall");
            built["Earth_Quake"] = Save(Quake(), "Earth", "Quake");
            built["Earth_Aura"] = Save(EarthAura(), "Earth", "Aura");
            built["Electric_Spear"] = Save(Spear(), "Electric", "Spear");
            built["Electric_Shell"] = Save(Shell(), "Electric", "Shell");
            built["Electric_Overcharge"] = Save(Overcharge(), "Electric", "Overcharge");
            built["Electric_Net"] = Save(Net(), "Electric", "Net");
            built["Status_Stun"] = Save(StatusStun(), "Status", "Stun");
            built["Status_Slow"] = Save(StatusSlow(), "Status", "Slow");
            built["Status_Root"] = Save(StatusRoot(), "Status", "Root");
            built["Status_Burn"] = Save(StatusBurn(), "Status", "Burn");

            Assign(built);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ElementFxKit] built {built.Count} prefabs into {Root}");
        }

        // =====================================================================================
        //  공통: 맞음 / 터짐 / 시전
        // =====================================================================================

        // 몸에 맞았을 때: 번쩍 + 튀는 불똥 + 원소 조각. 몸 높이 기준(원점 = 맞은 자리)
        private static GameObject Hit(Pal p)
        {
            Transform root = NewRoot($"FX_{p.Name}_Hit");

            ParticleSystem flash = Sys(root, "Flash", _glow);
            Life(flash, 0.12f); Size(flash, 1.5f, 1.8f); Col(flash, p.Hot);
            Burst(flash, 1);
            Fade(flash, (0f, p.Hot, 1f), (1f, p.Main, 0f));
            SizeCurve(flash, 0.6f, 1.2f);

            ParticleSystem core = Sys(root, "Core", _glow);
            Life(core, 0.08f); Size(core, 0.7f); Col(core, Color.white); Burst(core, 1);
            Fade(core, (0f, Color.white, 1f), (1f, p.Hot, 0f));

            ParticleSystem sparks = Sys(root, "Sparks", _spark, world: true);
            Life(sparks, 0.15f, 0.32f); Size(sparks, 0.07f, 0.13f); Speed(sparks, 5f, 11f); Col(sparks, p.Hot);
            Sphere(sparks, 0.1f); Burst(sparks, 14);
            Fade(sparks, (0f, p.Hot, 1f), (0.5f, p.Main, 1f), (1f, p.Deep, 0f));
            Stretch(sparks, 0.05f, 1.6f); Drag(sparks, 4f);

            ParticleSystem ring = Sys(root, "Ring", _ring);
            Life(ring, 0.2f); Size(ring, 1.4f); Col(ring, p.Main); Burst(ring, 1);
            SizeCurve(ring, 0.3f, 1.4f);
            Fade(ring, (0f, p.Hot, 0.9f), (1f, p.Main, 0f));

            switch (p.Name)
            {
                case "Fire":
                    ParticleSystem flames = Sys(root, "Flames", _tongue, world: true);
                    Life(flames, 0.25f, 0.4f); Size(flames, 0.5f, 0.8f); Speed(flames, 1f, 2.5f);
                    Sphere(flames, 0.2f); Burst(flames, 6); Rise(flames, 1.5f);
                    Fade(flames, (0f, p.Hot, 1f), (0.4f, p.Main, 0.9f), (1f, p.Deep, 0f));
                    SizeCurve(flames, 0.6f, 1.2f);
                    break;
                case "Water":
                    ParticleSystem drops = Sys(root, "Drops", _drop, world: true);
                    Life(drops, 0.3f, 0.5f); Size(drops, 0.1f, 0.18f); Speed(drops, 3f, 6f);
                    Sphere(drops, 0.15f); Burst(drops, 14); Gravity(drops, 1.6f);
                    Fade(drops, (0f, p.Hot, 1f), (1f, p.Main, 0f));
                    Stretch(drops, 0.06f, 1.2f);
                    break;
                case "Wind":
                    ParticleSystem arcs = Sys(root, "Arcs", _swirl);
                    Life(arcs, 0.25f, 0.32f); Size(arcs, 1.1f, 1.5f); RandomSpin(arcs); Spin(arcs, 900f);
                    Burst(arcs, 2);
                    Fade(arcs, (0f, p.Hot, 1f), (1f, p.Main, 0f));
                    SizeCurve(arcs, 0.6f, 1.2f);
                    break;
                case "Earth":
                    ParticleSystem chips = Sys(root, "Chips", _rock, world: true);
                    Life(chips, 0.4f, 0.6f); Size(chips, 0.2f, 0.38f); Speed(chips, 3f, 6f); RandomSpin(chips); Spin(chips, 600f);
                    Sphere(chips, 0.15f); Burst(chips, 8); Gravity(chips, 2.2f);
                    Fade(chips, (0f, Color.white, 1f), (0.8f, Color.white, 1f), (1f, Color.white, 0f));
                    ParticleSystem dust = Sys(root, "Dust", _smoke, world: true);
                    Life(dust, 0.5f, 0.8f); Size(dust, 0.6f, 0.9f); Speed(dust, 0.5f, 1.5f); RandomSpin(dust);
                    Sphere(dust, 0.2f); Burst(dust, 4);
                    Fade(dust, (0f, p.Dust, 0f), (0.2f, p.Dust, 0.6f), (1f, p.Dust, 0f));
                    SizeCurve(dust, 0.6f, 1.6f);
                    break;
                case "Electric":
                    ParticleSystem bolts = Sys(root, "Bolts", _bolt);
                    Life(bolts, 0.06f, 0.1f); Size(bolts, 0.9f, 1.4f); RandomSpin(bolts); Col(bolts, p.Hot);
                    BurstRepeat(bolts, 2, 3, 0.04f);
                    Fade(bolts, (0f, Color.white, 1f), (1f, p.Main, 0.6f));
                    break;
            }

            return root.gameObject;
        }

        // 바닥에서 크게 터짐 (반지름 2m 기준). 원점 = 바닥
        private static GameObject Impact(Pal p)
        {
            Transform root = NewRoot($"FX_{p.Name}_Impact");

            ParticleSystem flash = Sys(root, "Flash", _glow);
            flash.transform.localPosition = Vector3.up * 0.6f;
            Life(flash, 0.16f); Size(flash, 4.2f); Col(flash, p.Hot); Burst(flash, 1);
            Fade(flash, (0f, p.Hot, 1f), (1f, p.Main, 0f));
            SizeCurve(flash, 0.5f, 1.1f);

            ParticleSystem wave = Sys(root, "GroundRing", _ring);
            Flat(wave); wave.transform.localPosition = Vector3.up * 0.05f;
            Life(wave, 0.35f); Size(wave, 4.6f); Burst(wave, 1);
            SizeCurve(wave, 0.15f, 1f, easeOut: true);
            Fade(wave, (0f, p.Hot, 1f), (0.5f, p.Main, 0.8f), (1f, p.Main, 0f));

            ParticleSystem decal = Sys(root, "Decal", p.Name == "Earth" ? _crack : _scorch);
            Flat(decal); decal.transform.localPosition = Vector3.up * 0.03f;
            Life(decal, 1.6f); Size(decal, 3.8f, 4.2f); RandomSpin(decal); Burst(decal, 1);
            Color mark = p.Name switch { "Water" => C(0.4f, 0.7f, 1f), "Wind" => C(0.6f, 0.75f, 0.6f), "Electric" => C(0.35f, 0.33f, 0.25f), "Earth" => C(0.25f, 0.17f, 0.1f), _ => C(0.1f, 0.06f, 0.04f) };
            Fade(decal, (0f, mark, 0.8f), (0.6f, mark, 0.6f), (1f, mark, 0f));

            switch (p.Name)
            {
                case "Fire":
                {
                    ParticleSystem fireball = Sys(root, "Fireball", _flame, world: true);
                    Life(fireball, 0.35f, 0.65f); Size(fireball, 1.2f, 2.1f); Speed(fireball, 1.5f, 4.5f); RandomSpin(fireball); Spin(fireball, 90f);
                    Hemi(fireball, 0.7f); Burst(fireball, 22); Rise(fireball, 2f); Drag(fireball, 3f);
                    Fade(fireball, (0f, p.Hot, 1f), (0.25f, p.Main, 1f), (0.7f, p.Deep, 0.7f), (1f, p.Deep, 0f));
                    SizeCurve(fireball, 0.5f, 1.3f);

                    ParticleSystem smoke = Sys(root, "Smoke", _smoke, world: true);
                    Life(smoke, 1f, 1.6f); Size(smoke, 1.4f, 2.2f); Speed(smoke, 0.8f, 2f); RandomSpin(smoke);
                    Hemi(smoke, 0.8f); Burst(smoke, 10, 0.08f); Rise(smoke, 1.2f); Drag(smoke, 2f);
                    Fade(smoke, (0f, p.Dust, 0f), (0.15f, p.Dust, 0.75f), (1f, p.Dust, 0f));
                    SizeCurve(smoke, 0.6f, 1.6f);
                    Behind(smoke);

                    Embers(root, p, 30, 5f, 10f);
                    break;
                }
                case "Water":
                {
                    ParticleSystem column = Sys(root, "Splash", _drop, world: true);
                    Life(column, 0.5f, 0.8f); Size(column, 0.14f, 0.28f); Speed(column, 5f, 9f);
                    Cone(column, 25f, 0.6f, up: true); Burst(column, 40); Gravity(column, 2.4f);
                    Fade(column, (0f, p.Hot, 1f), (0.6f, p.Main, 0.9f), (1f, p.Main, 0f));
                    Stretch(column, 0.05f, 1.3f);

                    ParticleSystem crown = Sys(root, "Crown", _drop, world: true);
                    Life(crown, 0.4f, 0.6f); Size(crown, 0.12f, 0.2f); Speed(crown, 4f, 7f);
                    Ring(crown, 1f, 0f); Burst(crown, 36); Gravity(crown, 2f); UpKick(crown, 3f);
                    Fade(crown, (0f, p.Hot, 1f), (1f, p.Main, 0f));
                    Stretch(crown, 0.05f, 1.2f);

                    ParticleSystem mist = Sys(root, "Mist", _smoke, world: true);
                    Life(mist, 0.5f, 0.8f); Size(mist, 1.2f, 1.8f); Speed(mist, 2.5f, 4f); RandomSpin(mist);
                    Ring(mist, 0.6f, 0f); Burst(mist, 7); Drag(mist, 4f);
                    Fade(mist, (0f, p.Dust, 0f), (0.15f, p.Dust, 0.3f), (1f, p.Dust, 0f));
                    SizeCurve(mist, 0.7f, 1.4f);

                    ParticleSystem foam = Sys(root, "Foam", _ring);
                    Flat(foam); foam.transform.localPosition = Vector3.up * 0.08f;
                    Life(foam, 0.6f); Size(foam, 4f); Burst(foam, 1, 0.05f); Col(foam, Color.white);
                    SizeCurve(foam, 0.3f, 1.05f, easeOut: true);
                    Fade(foam, (0f, Color.white, 0.9f), (1f, p.Main, 0f));
                    break;
                }
                case "Wind":
                {
                    ParticleSystem swirl = Sys(root, "Swirl", _swirl);
                    Flat(swirl); swirl.transform.localPosition = Vector3.up * 0.2f;
                    Life(swirl, 0.45f, 0.55f); Size(swirl, 3.4f, 4.4f); RandomSpin(swirl); Spin(swirl, -540f); Burst(swirl, 3);
                    Fade(swirl, (0f, p.Hot, 1f), (0.5f, p.Main, 0.8f), (1f, p.Main, 0f));
                    SizeCurve(swirl, 0.5f, 1.1f, easeOut: true);

                    ParticleSystem streaks = Sys(root, "Streaks", _spark);
                    streaks.transform.localPosition = Vector3.up * 0.5f;
                    Life(streaks, 0.3f, 0.45f); Size(streaks, 0.08f, 0.14f); Col(streaks, p.Hot);
                    Ring(streaks, 1.2f, 0.4f); Burst(streaks, 30); Orbit(streaks, -7f, 3f); UpKick(streaks, 1.5f);
                    Fade(streaks, (0f, p.Hot, 1f), (1f, p.Main, 0f));
                    Stretch(streaks, 0.12f, 1f);

                    ParticleSystem dust = Sys(root, "Dust", _smoke, world: true);
                    Life(dust, 0.5f, 0.8f); Size(dust, 0.9f, 1.3f); Speed(dust, 4f, 6f); RandomSpin(dust);
                    Ring(dust, 0.8f, 0f); Burst(dust, 9); Drag(dust, 4f);
                    Fade(dust, (0f, p.Dust, 0f), (0.15f, p.Dust, 0.3f), (1f, p.Dust, 0f));
                    SizeCurve(dust, 0.6f, 1.5f);
                    break;
                }
                case "Earth":
                {
                    ParticleSystem rocks = Sys(root, "Rocks", _rock, world: true);
                    Life(rocks, 0.7f, 1f); Size(rocks, 0.25f, 0.55f); Speed(rocks, 5f, 9f); RandomSpin(rocks); Spin(rocks, 400f);
                    Cone(rocks, 35f, 0.9f, up: true); Burst(rocks, 20); Gravity(rocks, 2.8f);
                    Fade(rocks, (0f, Color.white, 1f), (0.85f, Color.white, 1f), (1f, Color.white, 0f));

                    ParticleSystem spikes = Sys(root, "Spikes", _rock);
                    Life(spikes, 0.6f); Size3(spikes, new Vector3(0.6f, 1.4f, 1f), new Vector3(0.9f, 2f, 1f));
                    Ring(spikes, 1.3f, 0.5f); Burst(spikes, 8);
                    spikes.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;
                    SizeCurveKeys(spikes, (0f, 0.1f), (0.15f, 1f), (0.7f, 1f), (1f, 0f));
                    Fade(spikes, (0f, Color.white, 1f), (1f, Color.white, 1f));
                    Pivot(spikes, 0.5f);

                    ParticleSystem dust = Sys(root, "Dust", _smoke, world: true);
                    Life(dust, 1f, 1.6f); Size(dust, 1.6f, 2.6f); Speed(dust, 2f, 4.5f); RandomSpin(dust);
                    Ring(dust, 0.8f, 0f); Burst(dust, 16); Drag(dust, 3f); Rise(dust, 0.4f);
                    Fade(dust, (0f, p.Dust, 0f), (0.1f, p.Dust, 0.8f), (1f, p.Dust, 0f));
                    SizeCurve(dust, 0.6f, 1.5f);
                    Behind(dust);
                    break;
                }
                case "Electric":
                {
                    ParticleSystem bolts = Sys(root, "Bolts", _bolt);
                    bolts.transform.localPosition = Vector3.up * 1.2f;
                    Life(bolts, 0.07f, 0.12f); Size(bolts, 2.2f, 3.6f); RandomSpin(bolts); Col(bolts, p.Hot);
                    Sphere(bolts, 0.6f); BurstRepeat(bolts, 3, 4, 0.05f);
                    Fade(bolts, (0f, Color.white, 1f), (1f, p.Main, 0.7f));

                    ParticleSystem pillar = Sys(root, "Pillar", _bolt);
                    pillar.transform.localPosition = Vector3.up * 3f;
                    Life(pillar, 0.12f); Size3(pillar, new Vector3(1.4f, 7f, 1f), new Vector3(1.8f, 7f, 1f)); Burst(pillar, 1);
                    pillar.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;
                    Fade(pillar, (0f, Color.white, 1f), (1f, p.Main, 0f));

                    ParticleSystem sparks = Sys(root, "Sparks", _spark, world: true);
                    Life(sparks, 0.25f, 0.5f); Size(sparks, 0.08f, 0.14f); Speed(sparks, 6f, 13f); Col(sparks, p.Hot);
                    Hemi(sparks, 0.4f); Burst(sparks, 40); Gravity(sparks, 1.2f); Drag(sparks, 2f);
                    Fade(sparks, (0f, Color.white, 1f), (0.5f, p.Main, 1f), (1f, p.Deep, 0f));
                    Stretch(sparks, 0.05f, 1.5f);

                    ParticleSystem glow = Sys(root, "Afterglow", _glow);
                    glow.transform.localPosition = Vector3.up * 0.3f;
                    Life(glow, 0.3f); Size(glow, 3f); Burst(glow, 1, 0.05f);
                    Fade(glow, (0f, p.Main, 0.7f), (1f, p.Main, 0f));
                    break;
                }
            }

            return root.gameObject;
        }

        // 손에서 쏠 때 (앞 = +Z). 작게
        private static GameObject Cast(Pal p)
        {
            Transform root = NewRoot($"FX_{p.Name}_Cast");

            ParticleSystem flash = Sys(root, "Flash", _glow);
            Life(flash, 0.12f); Size(flash, 1.3f); Col(flash, p.Hot); Burst(flash, 1);
            Fade(flash, (0f, p.Hot, 1f), (1f, p.Main, 0f));

            ParticleSystem jet = Sys(root, "Jet", _spark, world: true);
            Life(jet, 0.12f, 0.22f); Size(jet, 0.08f, 0.14f); Speed(jet, 6f, 12f); Col(jet, p.Hot);
            Cone(jet, 22f, 0.1f); Burst(jet, 12); Drag(jet, 5f);
            Fade(jet, (0f, p.Hot, 1f), (1f, p.Main, 0f));
            Stretch(jet, 0.06f, 1.5f);

            ParticleSystem ring = Sys(root, "Ring", _ring);
            ring.transform.localRotation = Quaternion.identity;
            Life(ring, 0.18f); Size(ring, 1.1f); Burst(ring, 1);
            ring.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.Local; // 앞을 보는 고리
            SizeCurve(ring, 0.3f, 1.2f);
            Fade(ring, (0f, p.Hot, 0.9f), (1f, p.Main, 0f));

            Material body = p.Name switch { "Fire" => _flame, "Water" => _drop, "Wind" => _swirl, "Earth" => _rock, _ => _bolt };
            ParticleSystem bits = Sys(root, "Bits", body, world: true);
            Life(bits, 0.2f, 0.3f); Size(bits, p.Name == "Wind" ? 0.8f : 0.25f, p.Name == "Wind" ? 1f : 0.45f); Speed(bits, 2f, 4f); RandomSpin(bits);
            Cone(bits, 30f, 0.15f); Burst(bits, p.Name == "Wind" ? 2 : 6);
            Fade(bits, (0f, p.Name == "Earth" ? Color.white : p.Hot, 1f), (1f, p.Name == "Earth" ? Color.white : p.Main, 0f));
            if (p.Name == "Earth")
                Gravity(bits, 1.5f);

            return root.gameObject;
        }

        // 떨어지기 전 바닥 예고 (반지름 2m, 계속): 바깥에서 가운데로 모여드는 원소 + 가운데가 점점 밝아짐
        private static GameObject Windup(Pal p)
        {
            Transform root = NewRoot($"FX_{p.Name}_Windup");

            Material bit = p.Name switch { "Fire" => _tongue, "Water" => _drop, "Wind" => _spark, "Earth" => _rock, _ => _spark };
            ParticleSystem gather = Sys(root, "Gather", bit);
            gather.transform.localPosition = Vector3.up * 0.15f;
            Loop(gather, 45f); Life(gather, 0.45f, 0.6f); Size(gather, p.Name == "Earth" ? 0.18f : 0.14f, p.Name == "Earth" ? 0.3f : 0.24f); Col(gather, p.Hot);
            Ring(gather, 2f, 0f); Orbit(gather, 2.5f, -3.6f);
            Fade(gather, (0f, p.Main, 0f), (0.3f, p.Hot, 1f), (1f, p.Hot, 0.6f));
            if (p.Name is "Wind" or "Electric" or "Water")
                Stretch(gather, 0.15f, 1f);

            ParticleSystem core = Sys(root, "Core", _glow);
            core.transform.localPosition = Vector3.up * 0.3f;
            Loop(core, 8f); Life(core, 0.35f); Size(core, 1.6f, 2.2f);
            Fade(core, (0f, p.Main, 0f), (0.5f, p.Main, 0.4f), (1f, p.Deep, 0f));

            ParticleSystem floor = Sys(root, "Floor", _ring);
            Flat(floor); floor.transform.localPosition = Vector3.up * 0.05f;
            Loop(floor, 3f); Life(floor, 0.6f); Size(floor, 4.2f);
            SizeCurve(floor, 1f, 0.2f);
            Fade(floor, (0f, p.Main, 0f), (0.3f, p.Main, 0.9f), (1f, p.Main, 0f));

            if (p.Name == "Electric")
            {
                ParticleSystem arcs = Sys(root, "Arcs", _bolt);
                arcs.transform.localPosition = Vector3.up * 0.6f;
                Loop(arcs, 12f); Life(arcs, 0.06f); Size(arcs, 1f, 1.6f); RandomSpin(arcs); Col(arcs, p.Hot);
                Sphere(arcs, 1.2f);
                Fade(arcs, (0f, Color.white, 1f), (1f, p.Main, 0.5f));
            }
            else if (p.Name == "Fire")
            {
                ParticleSystem embers = Sys(root, "Embers", _spark);
                Loop(embers, 25f); Life(embers, 0.5f, 0.8f); Size(embers, 0.05f, 0.09f); Col(embers, p.Hot);
                Circle(embers, 1.6f); Rise(embers, 2.5f, local: true);
                Fade(embers, (0f, p.Hot, 1f), (1f, p.Main, 0f));
            }
            else if (p.Name == "Earth")
            {
                ParticleSystem dust = Sys(root, "Dust", _smoke);
                Loop(dust, 10f); Life(dust, 0.6f); Size(dust, 0.8f, 1.2f); RandomSpin(dust);
                Circle(dust, 1.8f); Rise(dust, 0.6f, local: true);
                Fade(dust, (0f, p.Dust, 0f), (0.3f, p.Dust, 0.45f), (1f, p.Dust, 0f));
            }
            return root.gameObject;
        }

        // 전기 그물 (날아가는 거): 펼쳐진 번개 그물이 돌면서 날아감
        private static GameObject Net()
        {
            Pal p = Electric;
            Transform root = NewRoot("FX_Electric_Net");

            ParticleSystem web = Sys(root, "Web", _swirl);
            Loop(web, 20f); Life(web, 0.12f); Size(web, 1.6f, 1.9f); RandomSpin(web); Col(web, p.Main);
            web.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.Local; // 앞을 보는 면
            Fade(web, (0f, p.Hot, 0.9f), (1f, p.Main, 0.4f));

            ParticleSystem arcs = Sys(root, "Arcs", _bolt);
            Loop(arcs, 40f); Life(arcs, 0.05f); Size(arcs, 0.9f, 1.3f); RandomSpin(arcs); Col(arcs, p.Hot);
            Ring(arcs, 0.7f, 0f); arcs.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.Local;
            ParticleSystem.ShapeModule shape = arcs.shape;
            shape.rotation = Vector3.zero; // 고리가 앞을 봄
            Fade(arcs, (0f, Color.white, 1f), (1f, p.Main, 0.5f));

            ParticleSystem core = Sys(root, "Core", _glow);
            Loop(core, 20f); Life(core, 0.08f); Size(core, 0.7f); Col(core, p.Hot);
            Fade(core, (0f, Color.white, 1f), (1f, p.Main, 0.5f));

            ParticleSystem sparks = Sys(root, "Sparks", _spark, world: true);
            Life(sparks, 0.15f, 0.25f); Size(sparks, 0.05f, 0.08f); Speed(sparks, 1f, 3f); Col(sparks, p.Hot);
            Sphere(sparks, 0.6f); RateOverDistance(sparks, 5f); Loop(sparks, 0f, keepRate: true);
            Fade(sparks, (0f, Color.white, 1f), (1f, p.Main, 0f));
            Stretch(sparks, 0.05f, 1f);
            return root.gameObject;
        }

        // =====================================================================================
        //  불
        // =====================================================================================

        // 화염 방사 한 번 뿜기 (0.15초마다 하나씩, 앞 = +Z, 길이 5m 각도 40°)
        private static GameObject FireStream()
        {
            Pal p = Fire;
            Transform root = NewRoot("FX_Fire_Stream");

            ParticleSystem flames = Sys(root, "Flames", _flame, world: true);
            Life(flames, 0.22f, 0.3f); Size(flames, 0.5f, 0.8f); Speed(flames, 16f, 22f); RandomSpin(flames); Spin(flames, 200f);
            Cone(flames, 14f, 0.15f); BurstRepeat(flames, 5, 3, 0.04f); Drag(flames, 1.5f);
            Fade(flames, (0f, Color.white, 1f), (0.2f, p.Hot, 1f), (0.55f, p.Main, 0.95f), (1f, p.Deep, 0f));
            SizeCurve(flames, 0.4f, 3.2f);

            ParticleSystem core = Sys(root, "Core", _glow, world: true);
            Life(core, 0.15f, 0.2f); Size(core, 0.4f, 0.6f); Speed(core, 14f, 18f);
            Cone(core, 6f, 0.05f); BurstRepeat(core, 3, 3, 0.04f);
            Fade(core, (0f, Color.white, 1f), (1f, p.Hot, 0f));
            SizeCurve(core, 1f, 2f);

            ParticleSystem smoke = Sys(root, "Smoke", _smoke, world: true);
            Life(smoke, 0.5f, 0.8f); Size(smoke, 0.8f, 1.2f); Speed(smoke, 10f, 13f); RandomSpin(smoke);
            Cone(smoke, 16f, 0.15f); Burst(smoke, 2, 0.05f); Drag(smoke, 4f); Rise(smoke, 1f);
            Fade(smoke, (0f, p.Dust, 0f), (0.4f, p.Dust, 0.5f), (1f, p.Dust, 0f));
            SizeCurve(smoke, 0.6f, 2.2f);
            Behind(smoke);

            ParticleSystem embers = Sys(root, "Embers", _spark, world: true);
            Life(embers, 0.3f, 0.5f); Size(embers, 0.05f, 0.09f); Speed(embers, 10f, 16f); Col(embers, p.Hot);
            Cone(embers, 18f, 0.1f); Burst(embers, 4); Drag(embers, 3f); Rise(embers, 1f);
            Fade(embers, (0f, p.Hot, 1f), (1f, p.Main, 0f));
            Stretch(embers, 0.04f, 1f);
            return root.gameObject;
        }

        // 불의 장막: 내 주변 반지름 2m 불 고리 (계속)
        private static GameObject FireRing()
        {
            Pal p = Fire;
            Transform root = NewRoot("FX_Fire_Ring");

            ParticleSystem flames = Sys(root, "Flames", _tongue);
            Loop(flames, 150f); Life(flames, 0.4f, 0.65f); Size(flames, 0.8f, 1.25f);
            Ring(flames, 2f, 0.05f); Rise(flames, 2f, local: true); Orbit(flames, 0.6f, 0f);
            Fade(flames, (0f, p.Main, 0f), (0.12f, p.Hot, 1f), (0.35f, p.Main, 1f), (1f, p.Deep, 0f));
            SizeCurveKeys(flames, (0f, 0.3f), (0.25f, 1f), (1f, 0.35f));

            ParticleSystem glow = Sys(root, "Floor", _ring);
            Flat(glow); glow.transform.localPosition = Vector3.up * 0.05f;
            Loop(glow, 4f); Life(glow, 0.5f); Size(glow, 4.4f);
            Fade(glow, (0f, p.Main, 0f), (0.5f, p.Main, 0.6f), (1f, p.Main, 0f));

            ParticleSystem embers = Sys(root, "Embers", _spark);
            Loop(embers, 20f); Life(embers, 0.6f, 1f); Size(embers, 0.05f, 0.09f); Col(embers, p.Hot);
            Ring(embers, 2f, 0.1f); Rise(embers, 2.5f, local: true); Noise(embers, 0.6f, 2f);
            Fade(embers, (0f, p.Hot, 1f), (1f, p.Main, 0f));
            return root.gameObject;
        }

        // 폭주: 몸에 붙는 불 오라 (계속). 원점 = 몸 가운데(바닥 + 1m)
        private static GameObject FireAura()
        {
            Pal p = Fire;
            Transform root = NewRoot("FX_Fire_Aura");

            ParticleSystem flames = Sys(root, "Flames", _tongue, world: true);
            flames.transform.localPosition = Vector3.down * 0.9f;
            Loop(flames, 50f); Life(flames, 0.4f, 0.6f); Size(flames, 0.5f, 0.8f);
            Ring(flames, 0.55f, 0.3f); Rise(flames, 3.2f);
            Fade(flames, (0f, p.Main, 0f), (0.12f, p.Hot, 1f), (0.35f, p.Main, 1f), (1f, p.Deep, 0f));
            SizeCurve(flames, 1f, 0.3f);

            ParticleSystem glow = Sys(root, "Glow", _glow);
            Loop(glow, 6f); Life(glow, 0.4f); Size(glow, 2.4f); Col(glow, p.Main);
            Fade(glow, (0f, p.Main, 0f), (0.5f, p.Main, 0.5f), (1f, p.Main, 0f));

            ParticleSystem embers = Sys(root, "Embers", _spark, world: true);
            Loop(embers, 18f); Life(embers, 0.5f, 0.9f); Size(embers, 0.05f, 0.08f); Col(embers, p.Hot);
            Sphere(embers, 0.6f); Rise(embers, 2f); Noise(embers, 0.8f, 2f);
            Fade(embers, (0f, p.Hot, 1f), (1f, p.Main, 0f));
            return root.gameObject;
        }

        // =====================================================================================
        //  물
        // =====================================================================================

        // 파도 (날아가는 거, 앞 = +Z, 폭 3m)
        private static GameObject WaterWave()
        {
            Pal p = Water;
            Transform root = NewRoot("FX_Water_Wave");

            ParticleSystem crest = Sys(root, "Crest", _drop, world: true);
            Loop(crest, 160f); Life(crest, 0.35f, 0.55f); Size(crest, 0.12f, 0.24f);
            Box(crest, new Vector3(3.2f, 0.2f, 0.3f)); Gravity(crest, 1.6f); Rise(crest, 3.5f);
            Fade(crest, (0f, Color.white, 1f), (0.5f, p.Hot, 0.9f), (1f, p.Main, 0f));
            Stretch(crest, 0.05f, 1.2f);

            ParticleSystem body = Sys(root, "Body", _smoke, world: true);
            Loop(body, 60f); Life(body, 0.3f, 0.45f); Size(body, 0.9f, 1.4f); RandomSpin(body);
            Box(body, new Vector3(3f, 0.6f, 0.4f)); body.transform.localPosition = Vector3.up * 0.5f;
            Fade(body, (0f, p.Main, 0f), (0.25f, p.Main, 0.55f), (1f, p.Deep, 0f));
            SizeCurve(body, 0.8f, 1.3f);

            ParticleSystem foam = Sys(root, "Foam", _glow, world: true);
            Loop(foam, 50f); Life(foam, 0.25f, 0.35f); Size(foam, 0.6f, 0.9f);
            Box(foam, new Vector3(3f, 0.1f, 0.2f)); foam.transform.localPosition = Vector3.up * 0.9f;
            Fade(foam, (0f, p.Hot, 0.8f), (1f, p.Main, 0f));
            return root.gameObject;
        }

        // 소용돌이 바닥 (반지름 2m, 계속)
        private static GameObject Whirlpool()
        {
            Pal p = Water;
            Transform root = NewRoot("FX_Water_Whirlpool");

            ParticleSystem swirl = Sys(root, "Swirl", _swirl);
            Flat(swirl); swirl.transform.localPosition = Vector3.up * 0.06f;
            Loop(swirl, 5f); Life(swirl, 0.9f, 1.1f); Size(swirl, 3.6f, 4.2f); RandomSpin(swirl); Spin(swirl, -260f);
            Fade(swirl, (0f, p.Main, 0f), (0.3f, p.Main, 0.8f), (1f, p.Deep, 0f));
            SizeCurve(swirl, 1.1f, 0.4f);

            ParticleSystem inner = Sys(root, "Inner", _swirl);
            Flat(inner); inner.transform.localPosition = Vector3.up * 0.08f;
            Loop(inner, 4f); Life(inner, 0.7f); Size(inner, 2f); RandomSpin(inner); Spin(inner, -420f);
            Fade(inner, (0f, p.Hot, 0f), (0.4f, p.Hot, 0.8f), (1f, p.Main, 0f));
            SizeCurve(inner, 1f, 0.3f);

            ParticleSystem drops = Sys(root, "Drops", _drop);
            drops.transform.localPosition = Vector3.up * 0.15f;
            Loop(drops, 50f); Life(drops, 0.6f, 0.9f); Size(drops, 0.08f, 0.14f); Col(drops, p.Hot);
            Ring(drops, 2f, 0.3f); Orbit(drops, -4f, -1.8f);
            Fade(drops, (0f, p.Hot, 0f), (0.3f, p.Hot, 1f), (1f, p.Main, 0f));
            Stretch(drops, 0.1f, 1f);

            ParticleSystem edge = Sys(root, "Edge", _ring);
            Flat(edge); edge.transform.localPosition = Vector3.up * 0.04f;
            Loop(edge, 2f); Life(edge, 1f); Size(edge, 4.2f);
            Fade(edge, (0f, p.Main, 0f), (0.5f, p.Main, 0.5f), (1f, p.Main, 0f));
            return root.gameObject;
        }

        // 물 장막 (벽, 폭 5m, 앞 = +Z 쪽을 막음, 계속)
        private static GameObject WaterWall()
        {
            Pal p = Water;
            Transform root = NewRoot("FX_Water_Wall");

            ParticleSystem sheet = Sys(root, "Sheet", _smoke);
            Loop(sheet, 70f); Life(sheet, 0.5f, 0.7f); Size(sheet, 1f, 1.5f); RandomSpin(sheet);
            Box(sheet, new Vector3(5f, 0f, 0.3f)); Rise(sheet, 4f, local: true);
            Fade(sheet, (0f, p.Main, 0f), (0.2f, p.Main, 0.6f), (0.8f, p.Hot, 0.4f), (1f, p.Hot, 0f));
            SizeCurve(sheet, 0.7f, 1.1f);

            ParticleSystem jets = Sys(root, "Jets", _drop);
            Loop(jets, 160f); Life(jets, 0.5f, 0.8f); Size(jets, 0.1f, 0.2f); Col(jets, p.Hot);
            Box(jets, new Vector3(5f, 0f, 0.2f)); Rise(jets, 5.5f, local: true); Gravity(jets, 0.9f);
            Fade(jets, (0f, Color.white, 1f), (0.7f, p.Hot, 0.9f), (1f, p.Main, 0f));
            Stretch(jets, 0.06f, 1.4f);

            ParticleSystem foam = Sys(root, "Foam", _glow);
            foam.transform.localPosition = Vector3.up * 0.15f;
            Loop(foam, 30f); Life(foam, 0.4f); Size(foam, 0.8f, 1.2f);
            Box(foam, new Vector3(5f, 0f, 0.3f));
            Fade(foam, (0f, p.Hot, 0f), (0.3f, p.Hot, 0.8f), (1f, p.Main, 0f));
            return root.gameObject;
        }

        // =====================================================================================
        //  바람
        // =====================================================================================

        // 회오리 (날아가는 거, 원점 = 바닥, 높이 3m)
        private static GameObject Tornado()
        {
            Pal p = Wind;
            Transform root = NewRoot("FX_Wind_Tornado");
            root.transform.position = Vector3.zero;

            ParticleSystem funnel = Sys(root, "Funnel", _spark);
            funnel.transform.localPosition = Vector3.down * 0.9f; // 투사체는 몸 높이(1m)로 날아감 → 바닥부터
            Loop(funnel, 120f); Life(funnel, 0.6f, 0.8f); Size(funnel, 0.1f, 0.16f); Col(funnel, p.Hot);
            Ring(funnel, 0.35f, 0.2f); Rise(funnel, 4f, local: true); Orbit(funnel, 9f, 1.2f);
            Fade(funnel, (0f, p.Hot, 0f), (0.2f, p.Hot, 1f), (1f, p.Main, 0f));
            Stretch(funnel, 0.08f, 1f);

            ParticleSystem bands = Sys(root, "Bands", _swirl);
            bands.transform.localPosition = Vector3.down * 0.9f;
            Flat(bands);
            Loop(bands, 14f); Life(bands, 0.6f); Size(bands, 1f, 1.3f); RandomSpin(bands); Spin(bands, 720f);
            Rise(bands, 4f, local: true);
            Fade(bands, (0f, p.Main, 0f), (0.25f, p.Hot, 0.9f), (1f, p.Main, 0f));
            SizeCurve(bands, 0.6f, 2.4f);

            ParticleSystem dust = Sys(root, "Dust", _smoke, world: true);
            dust.transform.localPosition = Vector3.down * 0.9f;
            Loop(dust, 18f); Life(dust, 0.6f, 0.9f); Size(dust, 0.8f, 1.3f); RandomSpin(dust); Speed(dust, 1f, 2f);
            Ring(dust, 0.4f, 0f);
            Fade(dust, (0f, p.Dust, 0f), (0.2f, p.Dust, 0.55f), (1f, p.Dust, 0f));
            SizeCurve(dust, 0.6f, 1.6f);
            return root.gameObject;
        }

        // 역풍 지대 (반지름 2m, 바람이 앞(+Z)에서 나 쪽(-Z)으로 붐, 계속)
        private static GameObject Headwind()
        {
            Pal p = Wind;
            Transform root = NewRoot("FX_Wind_Headwind");

            ParticleSystem streaks = Sys(root, "Streaks", _spark);
            streaks.transform.localPosition = new Vector3(0f, 0.6f, 2f);
            Loop(streaks, 45f); Life(streaks, 0.4f, 0.55f); Size(streaks, 0.07f, 0.12f); Col(streaks, p.Hot);
            Box(streaks, new Vector3(4f, 1.2f, 0.2f)); Push(streaks, new Vector3(0f, 0f, -9f));
            Fade(streaks, (0f, p.Hot, 0f), (0.2f, p.Hot, 1f), (1f, p.Main, 0f));
            Stretch(streaks, 0.1f, 2f);

            ParticleSystem gusts = Sys(root, "Gusts", _swirl);
            gusts.transform.localPosition = new Vector3(0f, 0.3f, 1.5f);
            Flat(gusts);
            Loop(gusts, 4f); Life(gusts, 0.5f); Size(gusts, 1.6f, 2.2f); RandomSpin(gusts); Spin(gusts, 300f);
            Box(gusts, new Vector3(3f, 0f, 1f)); Push(gusts, new Vector3(0f, 0f, -4f));
            Fade(gusts, (0f, p.Main, 0f), (0.4f, p.Main, 0.6f), (1f, p.Main, 0f));

            ParticleSystem dust = Sys(root, "Dust", _smoke);
            dust.transform.localPosition = new Vector3(0f, 0.2f, 2f);
            Loop(dust, 10f); Life(dust, 0.6f, 0.8f); Size(dust, 0.8f, 1.2f); RandomSpin(dust);
            Box(dust, new Vector3(4f, 0.2f, 0.2f)); Push(dust, new Vector3(0f, 0f, -6f));
            Fade(dust, (0f, p.Dust, 0f), (0.3f, p.Dust, 0.45f), (1f, p.Dust, 0f));
            SizeCurve(dust, 0.6f, 1.5f);

            ParticleSystem edge = Sys(root, "Edge", _ring);
            Flat(edge); edge.transform.localPosition = Vector3.up * 0.04f;
            Loop(edge, 2f); Life(edge, 1f); Size(edge, 4.2f);
            Fade(edge, (0f, p.Main, 0f), (0.5f, p.Main, 0.5f), (1f, p.Main, 0f));
            return root.gameObject;
        }

        // 순풍: 몸 주변 바람 (계속, 원점 = 몸 가운데)
        private static GameObject WindAura()
        {
            Pal p = Wind;
            Transform root = NewRoot("FX_Wind_Aura");

            ParticleSystem streaks = Sys(root, "Streaks", _spark);
            Loop(streaks, 35f); Life(streaks, 0.35f, 0.5f); Size(streaks, 0.06f, 0.1f); Col(streaks, p.Hot);
            Ring(streaks, 0.7f, 0.2f); Orbit(streaks, 7f, 0f); Rise(streaks, 1.2f, local: true);
            streaks.transform.localPosition = Vector3.down * 0.7f;
            Fade(streaks, (0f, p.Hot, 0f), (0.2f, p.Hot, 1f), (1f, p.Main, 0f));
            Stretch(streaks, 0.12f, 1f);

            ParticleSystem bands = Sys(root, "Bands", _swirl);
            Flat(bands); bands.transform.localPosition = Vector3.down * 0.8f;
            Loop(bands, 3f); Life(bands, 0.6f); Size(bands, 1.8f); RandomSpin(bands); Spin(bands, 500f);
            Fade(bands, (0f, p.Main, 0f), (0.4f, p.Hot, 0.7f), (1f, p.Main, 0f));
            return root.gameObject;
        }

        // 바람 장막 (반지름 2m 둥근 돔, 계속, 원점 = 바닥)
        private static GameObject WindDome()
        {
            Pal p = Wind;
            Transform root = NewRoot("FX_Wind_Dome");

            for (int i = 0; i < 3; i++)
            {
                ParticleSystem layer = Sys(root, $"Layer{i}", _spark);
                layer.transform.localPosition = Vector3.up * (0.4f + i * 0.6f);
                float r = 2f - i * 0.35f;
                Loop(layer, 50f); Life(layer, 0.35f, 0.5f); Size(layer, 0.07f, 0.11f); Col(layer, p.Hot);
                Ring(layer, r, 0f); Orbit(layer, i % 2 == 0 ? 7f : -7f, 0f);
                Fade(layer, (0f, p.Hot, 0f), (0.2f, p.Hot, 1f), (1f, p.Main, 0f));
                Stretch(layer, 0.18f, 1f);
            }

            ParticleSystem shell = Sys(root, "Shell", _swirl);
            Flat(shell); shell.transform.localPosition = Vector3.up * 0.05f;
            Loop(shell, 3f); Life(shell, 0.8f); Size(shell, 4.2f); RandomSpin(shell); Spin(shell, 360f);
            Fade(shell, (0f, p.Main, 0f), (0.5f, p.Main, 0.6f), (1f, p.Main, 0f));

            ParticleSystem leaves = Sys(root, "Motes", _glow);
            Loop(leaves, 12f); Life(leaves, 0.8f, 1.2f); Size(leaves, 0.15f, 0.25f); Col(leaves, p.Hot);
            Ring(leaves, 1.9f, 0.1f); Orbit(leaves, 3f, 0f); Rise(leaves, 1.4f, local: true);
            Fade(leaves, (0f, p.Hot, 0f), (0.3f, p.Hot, 0.9f), (1f, p.Main, 0f));
            return root.gameObject;
        }

        // =====================================================================================
        //  땅
        // =====================================================================================

        // 바위 던지기 (날아가는 거)
        private static GameObject Boulder()
        {
            Pal p = Earth;
            Transform root = NewRoot("FX_Earth_Boulder");

            ParticleSystem rock = Sys(root, "Rock", _rock);
            // 하나씩 짧게 계속 갈아끼움 (끄면 바로 사라지게). 각도가 매번 달라서 굴러가는 것처럼 보임
            Loop(rock, 14f); Life(rock, 0.09f); Size(rock, 1.4f); RandomSpin(rock);
            Fade(rock, (0f, Color.white, 1f), (1f, Color.white, 1f));

            ParticleSystem dust = Sys(root, "Trail", _smoke, world: true);
            Life(dust, 0.4f, 0.6f); Size(dust, 0.6f, 0.9f); RandomSpin(dust); Sphere(dust, 0.3f);
            RateOverDistance(dust, 4f); Loop(dust, 0f, keepRate: true);
            Fade(dust, (0f, p.Dust, 0f), (0.2f, p.Dust, 0.6f), (1f, p.Dust, 0f));
            SizeCurve(dust, 0.6f, 1.6f);
            Behind(dust);

            ParticleSystem chips = Sys(root, "Chips", _rock, world: true);
            Life(chips, 0.3f, 0.5f); Size(chips, 0.1f, 0.18f); Speed(chips, 1f, 2.5f); RandomSpin(chips);
            Sphere(chips, 0.5f); RateOverDistance(chips, 3f); Loop(chips, 0f, keepRate: true); Gravity(chips, 2f);
            Fade(chips, (0f, Color.white, 1f), (1f, Color.white, 0f));
            return root.gameObject;
        }

        // 바위 벽 (폭 5m, 계속). 같은 씨앗으로 매번 같은 자리에 바위가 섬
        private static GameObject RockWall()
        {
            Pal p = Earth;
            Transform root = NewRoot("FX_Earth_Wall");

            ParticleSystem rocks = Sys(root, "Rocks", _rock);
            Loop(rocks, 0f); BurstRepeat(rocks, 14, 1, 0f); Life(rocks, 1.05f);
            Size3(rocks, new Vector3(1.2f, 2.8f, 1f), new Vector3(1.6f, 3.6f, 1f));
            Box(rocks, new Vector3(5f, 0f, 0.4f));
            rocks.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;
            Pivot(rocks, 0.45f);
            Fade(rocks, (0f, Color.white, 1f), (1f, Color.white, 1f));
            Seed(rocks, 1234);
            ParticleSystem.MainModule main = rocks.main;
            main.duration = 1f;

            ParticleSystem back = Sys(root, "BackRocks", _rock);
            back.transform.localPosition = new Vector3(0f, 0f, -0.3f);
            Loop(back, 0f); BurstRepeat(back, 12, 1, 0f); Life(back, 1.05f);
            Size3(back, new Vector3(1.1f, 2.2f, 1f), new Vector3(1.4f, 3f, 1f));
            Box(back, new Vector3(5f, 0f, 0.3f));
            back.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;
            Pivot(back, 0.45f);
            Fade(back, (0f, C(0.75f, 0.75f, 0.75f), 1f), (1f, C(0.75f, 0.75f, 0.75f), 1f));
            Seed(back, 77);
            main = back.main;
            main.duration = 1f;

            ParticleSystem dust = Sys(root, "Dust", _smoke);
            Loop(dust, 8f); Life(dust, 0.8f, 1.2f); Size(dust, 0.9f, 1.4f); RandomSpin(dust);
            Box(dust, new Vector3(5.2f, 0f, 0.8f)); Rise(dust, 0.3f, local: true);
            Fade(dust, (0f, p.Dust, 0f), (0.3f, p.Dust, 0.35f), (1f, p.Dust, 0f));
            return root.gameObject;
        }

        // 지진 (반지름 2m, 계속)
        private static GameObject Quake()
        {
            Pal p = Earth;
            Transform root = NewRoot("FX_Earth_Quake");

            ParticleSystem cracks = Sys(root, "Cracks", _crack);
            Flat(cracks); cracks.transform.localPosition = Vector3.up * 0.04f;
            Loop(cracks, 4f); Life(cracks, 1.2f); Size(cracks, 2f, 3f); RandomSpin(cracks);
            Circle(cracks, 1.6f);
            Fade(cracks, (0f, C(0.18f, 0.12f, 0.07f), 0f), (0.15f, C(0.18f, 0.12f, 0.07f), 1f), (1f, C(0.18f, 0.12f, 0.07f), 0f));

            ParticleSystem hops = Sys(root, "Pebbles", _rock);
            Loop(hops, 45f); Life(hops, 0.35f, 0.5f); Size(hops, 0.1f, 0.22f); RandomSpin(hops); Spin(hops, 300f);
            Circle(hops, 2f); Rise(hops, 3f, local: true); Gravity(hops, 2f);
            Fade(hops, (0f, Color.white, 1f), (1f, Color.white, 1f));

            ParticleSystem dust = Sys(root, "Dust", _smoke);
            Loop(dust, 22f); Life(dust, 0.6f, 0.9f); Size(dust, 1.1f, 1.7f); RandomSpin(dust);
            Circle(dust, 2f); Rise(dust, 1.2f, local: true);
            Fade(dust, (0f, p.Dust, 0f), (0.3f, p.Dust, 0.55f), (1f, p.Dust, 0f));
            SizeCurve(dust, 0.6f, 1.4f);

            ParticleSystem edge = Sys(root, "Edge", _ring);
            Flat(edge); edge.transform.localPosition = Vector3.up * 0.04f;
            Loop(edge, 2f); Life(edge, 0.5f); Size(edge, 4.2f);
            Fade(edge, (0f, p.Main, 0f), (0.5f, p.Main, 0.5f), (1f, p.Main, 0f));
            return root.gameObject;
        }

        // 바위 방패 오라 (몸 주변을 도는 돌, 계속)
        private static GameObject EarthAura()
        {
            Pal p = Earth;
            Transform root = NewRoot("FX_Earth_Aura");

            ParticleSystem stones = Sys(root, "Stones", _rock);
            Loop(stones, 0f); BurstRepeat(stones, 6, 1, 0f); Life(stones, 1.05f); Size(stones, 0.3f, 0.45f); RandomSpin(stones); Spin(stones, 200f);
            Ring(stones, 1f, 0f); Orbit(stones, 3f, 0f); Seed(stones, 5);
            Fade(stones, (0f, Color.white, 1f), (1f, Color.white, 1f));
            ParticleSystem.MainModule main = stones.main;
            main.duration = 1f;

            ParticleSystem glow = Sys(root, "Glow", _glow);
            Loop(glow, 4f); Life(glow, 0.5f); Size(glow, 2.4f);
            Fade(glow, (0f, p.Main, 0f), (0.5f, p.Main, 0.35f), (1f, p.Main, 0f));

            ParticleSystem dust = Sys(root, "Dust", _smoke);
            dust.transform.localPosition = Vector3.down * 0.9f;
            Loop(dust, 6f); Life(dust, 0.8f); Size(dust, 0.6f, 0.9f); RandomSpin(dust);
            Ring(dust, 0.8f, 0.2f); Rise(dust, 0.5f, local: true);
            Fade(dust, (0f, p.Dust, 0f), (0.3f, p.Dust, 0.35f), (1f, p.Dust, 0f));
            return root.gameObject;
        }

        // =====================================================================================
        //  번개
        // =====================================================================================

        // 번개 창 (날아가는 거, 앞 = +Z)
        private static GameObject Spear()
        {
            Pal p = Electric;
            Transform root = NewRoot("FX_Electric_Spear");

            ParticleSystem head = Sys(root, "Head", _glow);
            Loop(head, 30f); Life(head, 0.06f); Size(head, 0.7f, 0.9f); Col(head, Color.white);
            Fade(head, (0f, Color.white, 1f), (1f, p.Hot, 0.6f));

            ParticleSystem shaft = Sys(root, "Shaft", _glow, world: true);
            Life(shaft, 0.12f); Size(shaft, 0.35f, 0.45f); RateOverDistance(shaft, 14f); Loop(shaft, 0f, keepRate: true);
            Fade(shaft, (0f, Color.white, 1f), (0.4f, p.Hot, 0.9f), (1f, p.Main, 0f));
            SizeCurve(shaft, 1f, 0.3f);

            ParticleSystem arcs = Sys(root, "Arcs", _bolt);
            Loop(arcs, 30f); Life(arcs, 0.05f); Size(arcs, 0.8f, 1.2f); RandomSpin(arcs); Col(arcs, p.Hot);
            Sphere(arcs, 0.2f);
            Fade(arcs, (0f, Color.white, 1f), (1f, p.Main, 0.5f));

            ParticleSystem sparks = Sys(root, "Sparks", _spark, world: true);
            Life(sparks, 0.15f, 0.25f); Size(sparks, 0.05f, 0.09f); Speed(sparks, 2f, 5f); Col(sparks, p.Hot);
            Sphere(sparks, 0.15f); RateOverDistance(sparks, 4f); Loop(sparks, 0f, keepRate: true);
            Fade(sparks, (0f, Color.white, 1f), (1f, p.Main, 0f));
            Stretch(sparks, 0.05f, 1f);
            return root.gameObject;
        }

        // 전기 장막 (몸 주변 번개, 계속, 원점 = 몸 가운데)
        private static GameObject Shell()
        {
            Pal p = Electric;
            Transform root = NewRoot("FX_Electric_Shell");

            ParticleSystem arcs = Sys(root, "Arcs", _bolt);
            Loop(arcs, 22f); Life(arcs, 0.06f, 0.1f); Size(arcs, 0.8f, 1.3f); RandomSpin(arcs); Col(arcs, p.Hot);
            Sphere(arcs, 0.9f, shell: true);
            Fade(arcs, (0f, Color.white, 1f), (1f, p.Main, 0.6f));

            ParticleSystem bubble = Sys(root, "Bubble", _ring);
            Loop(bubble, 3f); Life(bubble, 0.5f); Size(bubble, 2.4f, 2.6f);
            Fade(bubble, (0f, p.Main, 0f), (0.5f, p.Main, 0.45f), (1f, p.Main, 0f));

            ParticleSystem motes = Sys(root, "Motes", _spark);
            Loop(motes, 20f); Life(motes, 0.3f, 0.5f); Size(motes, 0.05f, 0.08f); Col(motes, p.Hot);
            Sphere(motes, 1f, shell: true); Orbit(motes, 5f, 0f);
            Fade(motes, (0f, Color.white, 1f), (1f, p.Main, 0f));
            return root.gameObject;
        }

        // 과충전 (몸에서 위로 솟는 전기, 계속)
        private static GameObject Overcharge()
        {
            Pal p = Electric;
            Transform root = NewRoot("FX_Electric_Overcharge");

            ParticleSystem rise = Sys(root, "Rise", _spark, world: true);
            rise.transform.localPosition = Vector3.down * 0.9f;
            Loop(rise, 60f); Life(rise, 0.4f, 0.6f); Size(rise, 0.08f, 0.13f); Col(rise, p.Main);
            Ring(rise, 0.6f, 0.4f); Rise(rise, 4f);
            Fade(rise, (0f, Color.white, 1f), (0.5f, p.Hot, 1f), (1f, p.Main, 0f));
            Stretch(rise, 0.08f, 1.5f);

            ParticleSystem arcs = Sys(root, "Arcs", _bolt);
            Loop(arcs, 18f); Life(arcs, 0.07f); Size(arcs, 1f, 1.5f); RandomSpin(arcs); Col(arcs, p.Main);
            Sphere(arcs, 0.7f);
            Fade(arcs, (0f, Color.white, 1f), (1f, p.Main, 0.5f));

            ParticleSystem ring = Sys(root, "FootRing", _ring);
            Flat(ring); ring.transform.localPosition = Vector3.down * 0.95f;
            Loop(ring, 3f); Life(ring, 0.5f); Size(ring, 1.8f);
            SizeCurve(ring, 0.6f, 1.1f);
            Fade(ring, (0f, p.Hot, 0f), (0.3f, p.Hot, 0.8f), (1f, p.Main, 0f));
            return root.gameObject;
        }

        // =====================================================================================
        //  CC 표시 (머리 위, 계속)
        // =====================================================================================

        private static GameObject StatusStun()
        {
            Transform root = NewRoot("FX_Status_Stun");
            ParticleSystem stars = Sys(root, "Stars", _star);
            Loop(stars, 0f); BurstRepeat(stars, 3, 1, 0f); Life(stars, 1.02f); Size(stars, 0.32f); Col(stars, C(1f, 0.92f, 0.4f));
            Ring(stars, 0.45f, 0f); Orbit(stars, 5f, 0f); Seed(stars, 3);
            Fade(stars, (0f, C(1f, 0.95f, 0.5f), 1f), (1f, C(1f, 0.95f, 0.5f), 1f));
            ParticleSystem.MainModule main = stars.main;
            main.duration = 1f;
            return root.gameObject;
        }

        private static GameObject StatusSlow()
        {
            Color blue = C(0.45f, 0.75f, 1f);
            Transform root = NewRoot("FX_Status_Slow");
            ParticleSystem drops = Sys(root, "Drops", _drop, world: true);
            Loop(drops, 7f); Life(drops, 0.6f, 0.8f); Size(drops, 0.12f, 0.18f); Col(drops, blue);
            Ring(drops, 0.35f, 0.3f); Gravity(drops, 0.4f);
            Fade(drops, (0f, blue, 0f), (0.2f, blue, 1f), (1f, blue, 0f));
            Stretch(drops, 0.1f, 1.3f);

            ParticleSystem band = Sys(root, "Band", _ring);
            Flat(band);
            Loop(band, 2f); Life(band, 0.6f); Size(band, 0.9f);
            Fade(band, (0f, blue, 0f), (0.5f, blue, 0.7f), (1f, blue, 0f));
            return root.gameObject;
        }

        private static GameObject StatusRoot()
        {
            Color c = C(0.55f, 1f, 0.45f);
            Transform root = NewRoot("FX_Status_Root");
            ParticleSystem chains = Sys(root, "Ring", _ring);
            Flat(chains);
            Loop(chains, 3f); Life(chains, 0.5f); Size(chains, 1f);
            Fade(chains, (0f, c, 0f), (0.5f, c, 0.9f), (1f, c, 0f));

            ParticleSystem links = Sys(root, "Links", _spark);
            Loop(links, 0f); BurstRepeat(links, 8, 1, 0f); Life(links, 1.02f); Size(links, 0.12f); Col(links, c);
            Ring(links, 0.5f, 0f); Orbit(links, -2f, 0f); Seed(links, 9);
            Fade(links, (0f, c, 1f), (1f, c, 1f));
            ParticleSystem.MainModule main = links.main;
            main.duration = 1f;
            return root.gameObject;
        }

        private static GameObject StatusBurn()
        {
            Pal p = Fire;
            Transform root = NewRoot("FX_Status_Burn");
            ParticleSystem flames = Sys(root, "Flames", _tongue, world: true);
            flames.transform.localPosition = Vector3.down * 1f;
            Loop(flames, 18f); Life(flames, 0.35f, 0.5f); Size(flames, 0.3f, 0.5f);
            Sphere(flames, 0.4f); Rise(flames, 2f);
            Fade(flames, (0f, p.Main, 0f), (0.15f, p.Hot, 1f), (0.4f, p.Main, 1f), (1f, p.Deep, 0f));
            return root.gameObject;
        }

        // =====================================================================================
        //  스킬에 꽂기
        // =====================================================================================

        private static void Assign(Dictionary<string, GameObject> fx)
        {
            // 스킬 → (칸, 키). 키가 null 이면 그 칸 비움. 표에 없는 칸은 안 건드림.
            var table = new Dictionary<string, (string slot, string key)[]>
            {
                // 불
                ["Fire_Fireball"] = Std("Fire"),
                ["Fire_FlameDash"] = Std("Fire"),
                ["Fire_Meteor"] = Std("Fire", ("windupFx", "Fire_Windup")),
                ["Fire_FlameThrower"] = Std("Fire", ("streamFx", "Fire_Stream"), ("statusFx", "Status_Burn"), ("impactFx", null)),
                ["Fire_Inferno"] = Std("Fire"),
                ["Fire_FireVeil"] = Std("Fire", ("zoneFx", "Fire_Ring"), ("statusFx", "Status_Burn"), ("impactFx", null)),
                ["Fire_Frenzy"] = Std("Fire", ("auraFx", "Fire_Aura"), ("impactFx", null)),
                // 물
                ["Water_WaterCannon"] = Std("Water", ("statusFx", "Status_Slow")),
                ["Water_BubblePrison"] = Std("Water", ("statusFx", "Status_Root")),
                ["Water_HealingRain"] = Std("Water"),
                ["Water_Wave"] = Std("Water", ("travelFx", "Water_Wave"), ("impactFx", null)),
                ["Water_Whirlpool"] = Std("Water", ("zoneFx", "Water_Whirlpool"), ("statusFx", "Status_Slow"), ("impactFx", null)),
                ["Water_WaterVeil"] = Std("Water", ("barrierFx", "Water_Wall"), ("blockFx", "Water_Hit")),
                // 바람
                ["Wind_WindCutter"] = Std("Wind"),
                ["Wind_Gust"] = Std("Wind"),
                ["Wind_Leap"] = Std("Wind"),
                ["Wind_Tornado"] = Std("Wind", ("travelFx", "Wind_Tornado"), ("impactFx", null)),
                ["Wind_Headwind"] = Std("Wind", ("zoneFx", "Wind_Headwind"), ("statusFx", "Status_Slow"), ("impactFx", null)),
                ["Wind_Tailwind"] = Std("Wind", ("auraFx", "Wind_Aura"), ("impactFx", null)),
                ["Wind_WindVeil"] = Std("Wind", ("barrierFx", "Wind_Dome"), ("blockFx", "Wind_Hit"), ("impactFx", null)),
                // 땅
                ["Earth_EarthSplitter"] = Std("Earth", ("statusFx", "Status_Stun")),
                ["Earth_GraspOfEarth"] = Std("Earth", ("statusFx", "Status_Root"), ("windupFx", "Earth_Windup")),
                ["Earth_StoneShield"] = Std("Earth", ("auraFx", "Earth_Aura")),
                ["Earth_EarthSmash"] = Std("Earth", ("statusFx", "Status_Stun")),
                ["Earth_Boulder"] = Std("Earth", ("travelFx", "Earth_Boulder"), ("impactFx", null)),
                ["Earth_RockWall"] = Std("Earth", ("barrierFx", "Earth_Wall"), ("blockFx", "Earth_Hit")),
                ["Earth_Earthquake"] = Std("Earth", ("zoneFx", "Earth_Quake"), ("statusFx", "Status_Slow"), ("impactFx", null)),
                // 번개
                ["Electric_Thunderbolt"] = Std("Electric", ("statusFx", "Status_Stun"), ("windupFx", "Electric_Windup")),
                ["Electric_ElectricNet"] = Std("Electric", ("statusFx", "Status_Root"), ("travelFx", "Electric_Net")),
                ["Electric_Flash"] = Std("Electric", ("statusFx", "Status_Stun")),
                ["Electric_ChainLightning"] = Std("Electric", ("impactFx", null)),
                ["Electric_LightningSpear"] = Std("Electric", ("travelFx", "Electric_Spear"), ("impactFx", null)),
                ["Electric_StaticShell"] = Std("Electric", ("auraFx", "Electric_Shell"), ("statusFx", "Status_Slow"), ("impactFx", null)),
                ["Electric_Overcharge"] = Std("Electric", ("auraFx", "Electric_Overcharge"), ("impactFx", null)),
                // 피니시 스킬 (05.Data/Skill/Finisher)
                ["Finisher_PlasmaBurst"] = Std("Fire", ("castFx", "Electric_Cast"), ("windupFx", null)),
                ["Finisher_Tsunami"] = Std("Water", ("statusFx", "Status_Slow"), ("windupFx", null)),
                ["Finisher_MeteorFall"] = Std("Fire", ("windupFx", "Fire_Windup"), ("statusFx", "Status_Stun")),
                ["Finisher_Storm"] = Std("Electric", ("statusFx", "Status_Stun"), ("zoneFx", null), ("selfFx", null)),
                ["Finisher_Blessing"] = Std("Water", ("auraFx", "Earth_Aura"), ("impactFx", null)),
            };

            foreach (var entry in table)
            {
                var skill = AssetDatabase.LoadAssetAtPath<ScriptableObject>(SkillFolder + entry.Key + ".asset")
                            ?? AssetDatabase.LoadAssetAtPath<ScriptableObject>(FinisherFolder + entry.Key + ".asset");
                if (skill == null)
                {
                    Debug.LogWarning($"[ElementFxKit] no skill {entry.Key}");
                    continue;
                }

                var so = new SerializedObject(skill);
                foreach ((string slot, string key) in entry.Value)
                {
                    SerializedProperty prefab = so.FindProperty(slot + ".prefab");
                    if (prefab == null)
                        continue; // 그 모양엔 없는 칸
                    prefab.objectReferenceValue = key != null && fx.TryGetValue(key, out GameObject go) ? go : null;
                    so.FindProperty(slot + ".tint").boolValue = false;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(skill);
            }
        }

        // 기본: 시전·터짐·맞음은 원소 공통. extra 가 같은 칸이면 extra 가 이김
        private static (string, string)[] Std(string element, params (string slot, string key)[] extra)
        {
            var list = new List<(string, string)> { ("castFx", $"{element}_Cast"), ("impactFx", $"{element}_Impact"), ("hitFx", $"{element}_Hit") };
            foreach ((string slot, string key) in extra)
            {
                list.RemoveAll(e => e.Item1 == slot);
                list.Add((slot, key));
            }
            return list.ToArray();
        }

        // =====================================================================================
        //  텍스처 / 머티리얼
        // =====================================================================================

        private static void LoadMaterials()
        {
            _glow = Mat("M_FX_Glow");
            _spark = Mat("M_FX_Spark");
            _ring = Mat("M_FX_Ring");
            _smoke = Mat("M_FX_Smoke");
            _star = Mat("M_FX_Star");

            _flame = Made("Flame", additive: true, FlameTexture);
            _tongue = Made("Tongue", additive: true, TongueTexture);
            _bolt = Made("Bolt", additive: true, BoltTexture);
            _drop = Made("Drop", additive: true, DropTexture);
            _swirl = Made("Swirl", additive: true, SwirlTexture);
            _rock = Made("Rock", additive: false, RockTexture);
            _crack = Made("Crack", additive: false, CrackTexture);
            _scorch = Made("Scorch", additive: false, ScorchTexture);
        }

        private static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(Learn + name + ".mat");

        // 텍스처를 그려서 저장 → 같은 셰이더(가산 = Glow, 반투명 = Smoke) 복사본에 끼움
        private static Material Made(string name, bool additive, Func<int, Texture2D> paint)
        {
            string texturePath = $"{Root}/Textures/T_Kit_{name}.png";
            Texture2D texture = paint(256);
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();

            string materialPath = $"{Root}/Materials/M_Kit_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Mat(additive ? "M_FX_Glow" : "M_FX_Smoke"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D Paint(int size, Func<float, float, Color> pixel)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / size);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static float Fbm(float x, float y, int octaves = 4)
        {
            float sum = 0f, amp = 0.5f, freq = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Mathf.PerlinNoise(x * freq + 13.7f * i, y * freq + 7.3f * i);
                amp *= 0.5f;
                freq *= 2f;
            }
            return sum;
        }

        // 불꽃 덩어리: 가장자리가 노이즈로 흩어지는 흐릿한 덩어리 (폭발, 화염 방사)
        private static Texture2D FlameTexture(int size) => Paint(size, (u, v) =>
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
            float n = Fbm(u * 4f, v * 4f, 5);
            float n2 = Fbm(u * 9f + 3f, v * 9f, 3);
            float a = Mathf.Clamp01((1f - d) * 1.3f + (n - 0.5f) * 1.6f - 0.15f);
            a = Mathf.SmoothStep(0f, 1f, a) * (0.55f + 0.45f * n2);
            float core = Mathf.Clamp01(1f - d * 2.2f);
            a = Mathf.Clamp01(a * 0.8f + core * core * 0.35f);
            return new Color(a, a, a, a);
        });

        // 불꽃 혀: 아래가 넓고 밝고 위로 갈수록 가늘게 흔들리며 사라짐 (똑바로 세워 씀, 회전 X)
        private static Texture2D TongueTexture(int size) => Paint(size, (u, v) =>
        {
            float sway = (Fbm(v * 3f, 1.7f, 3) - 0.5f) * 0.25f * v;
            float x = u - 0.5f - sway;
            float width = 0.36f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(v * 1.05f), 0.55f)), 1.2f) * (1f - v * 0.6f);
            float inside = width <= 0.001f ? 0f : 1f - Mathf.Abs(x) / width;
            float n = Fbm(u * 6f, v * 4f - 0.3f, 4);
            float a = Mathf.Clamp01(inside * 1.4f + (n - 0.5f) * 0.9f);
            a = Mathf.SmoothStep(0f, 1f, a);
            a *= Mathf.SmoothStep(0f, 1f, v / 0.12f);
            float core = Mathf.Clamp01(inside * 1.6f - 0.6f) * Mathf.Clamp01(1f - v * 1.4f);
            a = Mathf.Clamp01(a * 0.75f + core * 0.5f);
            return new Color(a, a, a, a);
        });

        // 지그재그 번개 (세로). 가지 하나
        private static Texture2D BoltTexture(int size)
        {
            var rng = new System.Random(42);
            var main = Zigzag(rng, new Vector2(0.5f, 0.02f), new Vector2(0.5f, 0.98f), 6, 0.18f);
            var branch = Zigzag(rng, main[main.Count / 2], new Vector2(0.85f, 0.75f), 4, 0.08f);
            var branch2 = Zigzag(rng, main[main.Count / 3], new Vector2(0.18f, 0.2f), 4, 0.06f);
            return Paint(size, (u, v) =>
            {
                var p = new Vector2(u, v);
                float d = Mathf.Min(Dist(p, main), Mathf.Min(Dist(p, branch), Dist(p, branch2)) * 1.6f);
                float core = Mathf.Clamp01(1f - d / 0.012f);
                float glow = Mathf.Clamp01(1f - d / 0.07f);
                float a = Mathf.Clamp01(core + glow * glow * 0.6f);
                a *= Mathf.SmoothStep(0f, 1f, Mathf.Min(v, 1f - v) / 0.08f);
                return new Color(a, a, a, a);
            });
        }

        private static List<Vector2> Zigzag(System.Random rng, Vector2 a, Vector2 b, int depth, float jitter)
        {
            var points = new List<Vector2> { a, b };
            for (int level = 0; level < depth; level++)
            {
                var next = new List<Vector2>();
                for (int i = 0; i < points.Count - 1; i++)
                {
                    Vector2 mid = (points[i] + points[i + 1]) * 0.5f;
                    Vector2 dir = (points[i + 1] - points[i]);
                    Vector2 normal = new Vector2(-dir.y, dir.x).normalized;
                    mid += normal * ((float)rng.NextDouble() - 0.5f) * jitter * dir.magnitude * 2f;
                    next.Add(points[i]);
                    next.Add(mid);
                }
                next.Add(points[^1]);
                points = next;
            }
            return points;
        }

        private static float Dist(Vector2 p, List<Vector2> line)
        {
            float best = float.MaxValue;
            for (int i = 0; i < line.Count - 1; i++)
            {
                Vector2 a = line[i], ab = line[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        // 물방울: 동그란 밝은 점 + 하이라이트
        private static Texture2D DropTexture(int size) => Paint(size, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float a = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.55f, 1f, d));
            float rim = Mathf.Clamp01(1f - Mathf.Abs(d - 0.75f) / 0.2f) * 0.4f;
            float hi = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.38f, 0.62f)) / 0.12f);
            a = Mathf.Clamp01(a * 0.55f + rim + hi);
            return new Color(a, a, a, a);
        });

        // 소용돌이 휘어진 띠 (위에서 보면 바람/물 감기는 모양). 3갈래
        private static Texture2D SwirlTexture(int size) => Paint(size, (u, v) =>
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
            float angle = Mathf.Atan2(dy, dx);
            float arm = Mathf.Repeat((angle + r * 3.2f) / (Mathf.PI * 2f) * 3f, 1f);
            float band = Mathf.Clamp01(1f - Mathf.Abs(arm - 0.5f) / 0.18f);
            band = Mathf.SmoothStep(0f, 1f, band);
            float radial = Mathf.Clamp01(Mathf.InverseLerp(0.15f, 0.5f, r)) * Mathf.Clamp01(Mathf.InverseLerp(1f, 0.7f, r));
            float tail = Mathf.Lerp(0.3f, 1f, arm);
            float a = Mathf.Clamp01(band * radial * tail * 1.2f);
            return new Color(a, a, a, a);
        });

        // 바위 조각: 각진 다각형 + 위쪽 밝은 면 (반투명 셰이더, 색 들어감)
        private static Texture2D RockTexture(int size)
        {
            var rng = new System.Random(7);
            const int corners = 7;
            var radii = new float[corners];
            for (int i = 0; i < corners; i++)
                radii[i] = 0.32f + (float)rng.NextDouble() * 0.14f;

            Color lit = C(0.62f, 0.48f, 0.34f), mid = C(0.45f, 0.33f, 0.22f), dark = C(0.25f, 0.18f, 0.12f);
            return Paint(size, (u, v) =>
            {
                float dx = u - 0.5f, dy = v - 0.5f;
                float angle = Mathf.Repeat(Mathf.Atan2(dy, dx) / (Mathf.PI * 2f), 1f) * corners;
                int i = Mathf.FloorToInt(angle) % corners;
                float t = angle - Mathf.Floor(angle);
                float edge = Mathf.Lerp(radii[i], radii[(i + 1) % corners], t);
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(edge - 0.01f, edge + 0.005f, r));
                float facet = Mathf.Repeat(i * 0.37f, 1f);
                float light = Mathf.Clamp01(0.5f + dy * 1.4f - dx * 0.6f) * 0.7f + facet * 0.3f;
                float n = Fbm(u * 9f, v * 9f, 3);
                Color c = light > 0.55f ? Color.Lerp(mid, lit, (light - 0.55f) / 0.45f) : Color.Lerp(dark, mid, light / 0.55f);
                c *= 0.8f + n * 0.4f;
                float rim = Mathf.Clamp01(1f - (edge - r) / 0.03f);
                c = Color.Lerp(c, dark, rim * 0.6f);
                c.a = a;
                return c;
            });
        }

        // 바닥 갈라짐: 가운데에서 뻗는 금 (반투명, 어두움)
        private static Texture2D CrackTexture(int size)
        {
            var rng = new System.Random(11);
            var lines = new List<List<Vector2>>();
            for (int i = 0; i < 7; i++)
            {
                float angle = i / 7f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.5f;
                Vector2 end = new Vector2(0.5f, 0.5f) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (0.32f + (float)rng.NextDouble() * 0.14f);
                List<Vector2> line = Zigzag(rng, new Vector2(0.5f, 0.5f), end, 4, 0.12f);
                lines.Add(line);
                Vector2 from = line[line.Count / 2];
                lines.Add(Zigzag(rng, from, from + new Vector2(Mathf.Cos(angle + 0.8f), Mathf.Sin(angle + 0.8f)) * 0.12f, 3, 0.1f));
            }

            return Paint(size, (u, v) =>
            {
                var p = new Vector2(u, v);
                float d = float.MaxValue;
                foreach (List<Vector2> line in lines)
                    d = Mathf.Min(d, Dist(p, line));
                float r = Vector2.Distance(p, new Vector2(0.5f, 0.5f)) * 2f;
                float width = Mathf.Lerp(0.014f, 0.003f, r);
                float a = Mathf.Clamp01(1f - d / width) + Mathf.Clamp01(1f - d / (width * 4f)) * 0.25f;
                a += Mathf.Clamp01(1f - r / 0.25f) * 0.5f;
                a *= Mathf.Clamp01(1f - r);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            });
        }

        // 그을음: 부드러운 원 + 노이즈 (반투명)
        private static Texture2D ScorchTexture(int size) => Paint(size, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float n = Fbm(u * 6f, v * 6f);
            float a = Mathf.Clamp01((1f - d) * 1.5f + (n - 0.5f) * 0.9f);
            a = Mathf.SmoothStep(0f, 1f, a) * 0.85f;
            return new Color(1f, 1f, 1f, a);
        });

        // =====================================================================================
        //  파티클 도우미
        // =====================================================================================

        private static Transform NewRoot(string name) => new GameObject(name).transform;

        private static ParticleSystem Sys(Transform parent, string name, Material material, bool world = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = true;
            main.startSpeed = 0f;
            main.startLifetime = 0.5f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.maxParticles = 400;
            main.stopAction = ParticleSystemStopAction.None;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 5f;
            return system;
        }

        private static void Life(ParticleSystem s, float a, float? b = null)
        {
            ParticleSystem.MainModule m = s.main;
            m.startLifetime = b.HasValue ? new ParticleSystem.MinMaxCurve(a, b.Value) : new ParticleSystem.MinMaxCurve(a);
        }

        private static void Size(ParticleSystem s, float a, float? b = null)
        {
            ParticleSystem.MainModule m = s.main;
            m.startSize = b.HasValue ? new ParticleSystem.MinMaxCurve(a, b.Value) : new ParticleSystem.MinMaxCurve(a);
        }

        private static void Size3(ParticleSystem s, Vector3 a, Vector3 b)
        {
            ParticleSystem.MainModule m = s.main;
            m.startSize3D = true;
            m.startSizeX = new ParticleSystem.MinMaxCurve(a.x, b.x);
            m.startSizeY = new ParticleSystem.MinMaxCurve(a.y, b.y);
            m.startSizeZ = new ParticleSystem.MinMaxCurve(a.z, b.z);
        }

        private static void Speed(ParticleSystem s, float a, float b)
        {
            ParticleSystem.MainModule m = s.main;
            m.startSpeed = new ParticleSystem.MinMaxCurve(a, b);
        }

        private static void Col(ParticleSystem s, Color c)
        {
            ParticleSystem.MainModule m = s.main;
            m.startColor = c;
        }

        private static void Gravity(ParticleSystem s, float g)
        {
            ParticleSystem.MainModule m = s.main;
            m.gravityModifier = g;
        }

        private static void RandomSpin(ParticleSystem s)
        {
            ParticleSystem.MainModule m = s.main;
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        }

        // 도/초. 반은 반대로 돎
        private static void Spin(ParticleSystem s, float degreesPerSecond)
        {
            ParticleSystem.RotationOverLifetimeModule r = s.rotationOverLifetime;
            r.enabled = true;
            float rad = degreesPerSecond * Mathf.Deg2Rad;
            r.z = new ParticleSystem.MinMaxCurve(rad * 0.7f, rad * 1.3f);
        }

        private static void Burst(ParticleSystem s, int count, float time = 0f)
        {
            ParticleSystem.EmissionModule e = s.emission;
            var bursts = new ParticleSystem.Burst[e.burstCount + 1];
            e.GetBursts(bursts);
            bursts[^1] = new ParticleSystem.Burst(time, (short)count);
            e.SetBursts(bursts);
        }

        private static void BurstRepeat(ParticleSystem s, int count, int cycles, float interval)
        {
            ParticleSystem.EmissionModule e = s.emission;
            e.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count, (short)count, cycles, Mathf.Max(0.01f, interval)) });
        }

        // 계속 뿜는 거 (Hold 용). rate 0 이면 버스트만 반복
        private static void Loop(ParticleSystem s, float perSecond, bool keepRate = false)
        {
            ParticleSystem.MainModule m = s.main;
            m.loop = true;
            m.prewarm = false;
            if (!keepRate)
            {
                ParticleSystem.EmissionModule e = s.emission;
                e.rateOverTime = perSecond;
            }
        }

        private static void RateOverDistance(ParticleSystem s, float perMeter)
        {
            ParticleSystem.EmissionModule e = s.emission;
            e.rateOverTime = 0f;
            e.rateOverDistance = perMeter;
        }

        private static void Seed(ParticleSystem s, uint seed)
        {
            s.useAutoRandomSeed = false;
            s.randomSeed = seed;
        }

        private static ParticleSystem.ShapeModule Shape(ParticleSystem s, ParticleSystemShapeType type)
        {
            ParticleSystem.ShapeModule shape = s.shape;
            shape.enabled = true;
            shape.shapeType = type;
            return shape;
        }

        private static void Sphere(ParticleSystem s, float radius, bool shell = false)
        {
            ParticleSystem.ShapeModule shape = Shape(s, ParticleSystemShapeType.Sphere);
            shape.radius = radius;
            shape.radiusThickness = shell ? 0f : 1f;
        }

        private static void Hemi(ParticleSystem s, float radius)
        {
            ParticleSystem.ShapeModule shape = Shape(s, ParticleSystemShapeType.Hemisphere);
            shape.radius = radius;
            shape.rotation = new Vector3(-90f, 0f, 0f); // 반구가 위(+Y)를 봄
        }

        // 원뿔. up 이면 위로, 아니면 앞(+Z)으로
        private static void Cone(ParticleSystem s, float angle, float radius, bool up = false, bool keepBox = false)
        {
            if (keepBox)
                return; // 박스 모양 유지 (속도는 startSpeed 방향 = 박스 기본 +Z → Rise 로 대신)
            ParticleSystem.ShapeModule shape = Shape(s, ParticleSystemShapeType.Cone);
            shape.angle = angle;
            shape.radius = Mathf.Max(0.001f, radius);
            shape.rotation = up ? new Vector3(-90f, 0f, 0f) : Vector3.zero;
        }

        private static void Box(ParticleSystem s, Vector3 scale)
        {
            ParticleSystem.ShapeModule shape = Shape(s, ParticleSystemShapeType.Box);
            shape.scale = scale;
        }

        // 바닥에 누운 원판 (안쪽까지)
        private static void Circle(ParticleSystem s, float radius)
        {
            ParticleSystem.ShapeModule shape = Shape(s, ParticleSystemShapeType.Circle);
            shape.radius = radius;
            shape.radiusThickness = 1f;
            shape.rotation = new Vector3(90f, 0f, 0f);
        }

        // 바닥에 누운 고리 (thickness 0 = 테두리만)
        private static void Ring(ParticleSystem s, float radius, float thickness)
        {
            ParticleSystem.ShapeModule shape = Shape(s, ParticleSystemShapeType.Circle);
            shape.radius = radius;
            shape.radiusThickness = thickness;
            shape.rotation = new Vector3(90f, 0f, 0f);
        }

        private static void Flat(ParticleSystem s) =>
            s.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;

        private static void Pivot(ParticleSystem s, float up) =>
            s.GetComponent<ParticleSystemRenderer>().pivot = new Vector3(0f, up, 0f);

        private static void Stretch(ParticleSystem s, float velocityScale, float lengthScale)
        {
            var r = s.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = velocityScale;
            r.lengthScale = lengthScale;
        }

        // 반투명(연기)은 가산 이펙트보다 뒤에 그려지게
        private static void Behind(ParticleSystem s) => s.GetComponent<ParticleSystemRenderer>().sortingFudge = 40f;

        private static ParticleSystem.VelocityOverLifetimeModule Velocity(ParticleSystem s, bool local)
        {
            ParticleSystem.VelocityOverLifetimeModule v = s.velocityOverLifetime;
            v.enabled = true;
            v.space = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
            return v;
        }

        // 위로 일정 속도 (local = 이펙트 기준 위)
        private static void Rise(ParticleSystem s, float up, bool local = false)
        {
            ParticleSystem.VelocityOverLifetimeModule v = Velocity(s, local);
            v.x = 0f; v.y = up; v.z = 0f;
        }

        private static void Push(ParticleSystem s, Vector3 velocity)
        {
            ParticleSystem.VelocityOverLifetimeModule v = Velocity(s, true);
            v.x = velocity.x; v.y = velocity.y; v.z = velocity.z;
        }

        // 처음에 위로 툭 (왕관 물보라)
        private static void UpKick(ParticleSystem s, float up)
        {
            ParticleSystem.VelocityOverLifetimeModule v = Velocity(s, false);
            // x,y,z 는 같은 모드여야 함 → 셋 다 커브
            AnimationCurve zero = AnimationCurve.Constant(0f, 1f, 0f);
            v.x = new ParticleSystem.MinMaxCurve(0f, zero);
            v.z = new ParticleSystem.MinMaxCurve(0f, zero);
            v.y = new ParticleSystem.MinMaxCurve(up, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
        }

        // 세로축으로 돎 (rad/s) + 바깥으로 퍼짐
        private static void Orbit(ParticleSystem s, float orbitalY, float radial)
        {
            ParticleSystem.VelocityOverLifetimeModule v = s.velocityOverLifetime;
            v.enabled = true;
            v.space = ParticleSystemSimulationSpace.Local;
            v.orbitalX = 0f;
            v.orbitalY = orbitalY;
            v.orbitalZ = 0f;
            v.radial = radial;
        }

        private static void Drag(ParticleSystem s, float drag)
        {
            ParticleSystem.LimitVelocityOverLifetimeModule l = s.limitVelocityOverLifetime;
            l.enabled = true;
            l.limit = 1000f;
            l.drag = drag;
            l.multiplyDragByParticleSize = false;
            l.multiplyDragByParticleVelocity = false;
        }

        private static void Noise(ParticleSystem s, float strength, float frequency)
        {
            ParticleSystem.NoiseModule n = s.noise;
            n.enabled = true;
            n.strength = strength;
            n.frequency = frequency;
            n.scrollSpeed = 1f;
        }

        private static void Fade(ParticleSystem s, params (float t, Color c, float a)[] keys)
        {
            ParticleSystem.ColorOverLifetimeModule col = s.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                keys.Select(k => new GradientColorKey(k.c, k.t)).ToArray(),
                keys.Select(k => new GradientAlphaKey(k.a, k.t)).ToArray());
            col.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void SizeCurve(ParticleSystem s, float from, float to, bool easeOut = false)
        {
            AnimationCurve curve = easeOut
                ? new AnimationCurve(new Keyframe(0f, from, 0f, (to - from) * 2.5f), new Keyframe(1f, to, 0f, 0f))
                : AnimationCurve.Linear(0f, from, 1f, to);
            SetSize(s, curve);
        }

        private static void SizeCurveKeys(ParticleSystem s, params (float t, float v)[] keys) =>
            SetSize(s, new AnimationCurve(keys.Select(k => new Keyframe(k.t, k.v)).ToArray()));

        private static void SetSize(ParticleSystem s, AnimationCurve curve)
        {
            ParticleSystem.SizeOverLifetimeModule size = s.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void Embers(Transform root, Pal p, int count, float min, float max)
        {
            ParticleSystem embers = Sys(root, "Embers", _spark, world: true);
            Life(embers, 0.4f, 0.8f); Size(embers, 0.06f, 0.11f); Speed(embers, min, max); Col(embers, p.Hot);
            Hemi(embers, 0.5f); Burst(embers, count); Gravity(embers, 0.8f); Drag(embers, 1.5f);
            Fade(embers, (0f, Color.white, 1f), (0.4f, p.Hot, 1f), (1f, p.Main, 0f));
            Stretch(embers, 0.05f, 1.3f);
        }

        private static Color C(float r, float g, float b, float a = 1f) => new(r, g, b, a);

        // =====================================================================================
        //  저장
        // =====================================================================================

        private static void EnsureFolders()
        {
            foreach (string sub in new[] { "", "/Textures", "/Materials", "/Fire", "/Water", "/Wind", "/Earth", "/Electric", "/Status" })
            {
                string path = Root + sub;
                if (AssetDatabase.IsValidFolder(path))
                    continue;
                string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            }
        }

        private static GameObject Save(GameObject root, string folder, string name)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/{folder}/FX_{folder}_{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}

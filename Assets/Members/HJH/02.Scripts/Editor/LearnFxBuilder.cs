using System;
using System.IO;
using TMPro;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Assets.Members.HJH._02.Scripts.EditorTools
{
    // HJH > Build Learn FX: builds the authored effect set from the Shader Graphs in Effects/Learn/Shaders.
    //   Textures  : painted here (glow, spark, star, ring, smoke, magic circle, energy streak, line mask)
    //   Materials : one per look, all on the four learning Shader Graphs
    //   Prefabs   : normal Particle System / Trail Renderer / Mesh objects - open and tweak them in the Inspector
    //   Library   : Resources/HJH_FxLibrary.asset, the one place the game looks up every effect
    // Existing textures/materials/prefabs are left alone, so your own edits survive a rebuild.
    // Delete an asset to have it regenerated.
    public static class LearnFxBuilder
    {
        private const string Root = "Assets/Members/HJH/Effects/Learn";
        private const string ShaderRoot = Root + "/Shaders";
        private const string TextureRoot = Root + "/Textures";
        private const string MaterialRoot = Root + "/Materials";
        private const string PrefabRoot = Root + "/Prefabs";
        private const string LibraryPath = Root + "/Resources/" + FxLibrary.ResourcePath + ".asset";

        [MenuItem("HJH/Build Learn FX (Shader Graph + Particle System)")]
        public static void Build()
        {
            foreach (string folder in new[] { TextureRoot, MaterialRoot, PrefabRoot, Root + "/Resources" })
                Directory.CreateDirectory(folder);

            Shader additive = LoadShader("SG_FX_Particle_Additive");
            Shader alpha = LoadShader("SG_FX_Particle_Alpha");
            Shader scroll = LoadShader("SG_FX_Scroll");
            Shader circle = LoadShader("SG_FX_MagicCircle");
            if (additive == null || alpha == null || scroll == null || circle == null)
            {
                EditorUtility.DisplayDialog("Learn FX", "Effects/Learn/Shaders 의 셰이더 그래프가 아직 임포트되지 않았습니다.\n" +
                                                         "Console 에 셰이더 에러가 없는지 확인 후 다시 실행하세요.", "OK");
                return;
            }

            Textures.EnsureAll();

            Material glow = Mat("M_FX_Glow", additive, "T_Glow", 3f);
            Material spark = Mat("M_FX_Spark", additive, "T_Spark", 4f);
            Material star = Mat("M_FX_Star", additive, "T_Star", 3f);
            Material ring = Mat("M_FX_Ring", additive, "T_Ring", 3f);
            Material slash = Mat("M_FX_Slash", additive, "T_Slash", 3.5f);
            Material smoke = Mat("M_FX_Smoke", alpha, "T_Smoke", 1f);
            Material line = Mat("M_FX_Line", scroll, "T_EnergyStreak", 3f, m =>
            {
                m.SetTexture("_MaskTex", Textures.Load("T_LineMask"));
                m.SetVector("_ScrollSpeed", new Vector4(-3f, 0f));
                m.SetVector("_Tiling", new Vector4(2f, 1f));
            });
            Material trail = Mat("M_FX_Trail", scroll, "T_EnergyStreak", 2.5f, m =>
            {
                m.SetTexture("_MaskTex", Textures.Load("T_LineMask"));
                m.SetVector("_ScrollSpeed", new Vector4(-2f, 0f));
                m.SetVector("_Tiling", new Vector4(1f, 1f));
            });
            Material magic = Mat("M_FX_MagicCircle", circle, "T_MagicCircle", 3f, m => m.SetFloat("_RotateSpeed", 1f));

            var library = AssetDatabase.LoadAssetAtPath<FxLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<FxLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            var m = new Mats { Slash = slash, Glow = glow, Spark = spark, Star = star, Ring = ring, Smoke = smoke, Line = line, Trail = trail, Magic = magic };

            // Combat
            library.hit = Prefab("FX_Hit", go => Prefabs.Hit(go, m));
            library.muzzle = Prefab("FX_Muzzle", go => Prefabs.Muzzle(go, m));
            library.explosion = Prefab("FX_Explosion", go => Prefabs.Explosion(go, m));
            library.shockwave = Prefab("FX_Shockwave", go => Prefabs.Shockwave(go, m));
            library.groundSlam = Prefab("FX_GroundSlam", go => Prefabs.GroundSlam(go, m));
            library.damageNumber = Prefab("FX_DamageNumber", Prefabs.DamageNumber, typeof(FxDamageNumber));
            library.characterSwap = Prefab("FX_CharacterSwap", go => Prefabs.CharacterSwap(go, m));

            // Weapons and projectiles
            library.tracer = Prefab("FX_Tracer", go => Prefabs.Tracer(go, m), typeof(FxLine));
            Prefab("FX_Swipe", go => Prefabs.Swipe(go, m), typeof(FxSwipe)); // plain ribbon version, kept for reference
            library.swipe = Prefab("FX_ChainSlash", go => Prefabs.ChainSlash(go, m), typeof(FxSlash), outdatedPart: typeof(FxSwipe));
            library.slash = library.swipe;
            library.orb = Prefab("FX_Orb", go => Prefabs.Orb(go, m));
            library.projectileImpact = Prefab("FX_ProjectileImpact", go => Prefabs.ProjectileImpact(go, m));

            // Movement
            library.dashTrail = Prefab("FX_DashTrail", go => Prefabs.DashTrail(go, m), typeof(FxTrailToggle));
            library.teleportStreak = Prefab("FX_TeleportStreak", go => Prefabs.TeleportStreak(go, m), typeof(FxLine));
            library.blinkOut = Prefab("FX_BlinkOut", go => Prefabs.BlinkOut(go, m));
            library.blinkIn = Prefab("FX_BlinkIn", go => Prefabs.BlinkIn(go, m));

            // Skills
            library.charge = Prefab("FX_Charge", go => Prefabs.Charge(go, m), typeof(FxScaleByValue));
            library.beam = Prefab("FX_Beam", go => Prefabs.Beam(go, m), typeof(FxLine));
            library.energyCannon = Prefab("FX_EnergyCannon", go => Prefabs.EnergyCannon(go, m), typeof(FxLine));
            library.cannonMuzzle = Prefab("FX_CannonMuzzle", go => Prefabs.CannonMuzzle(go, m));
            library.magicCircle = Prefab("FX_MagicCircle", go => Prefabs.MagicCircle(go, m), typeof(FxMagicCircleDriver));
            library.pull = Prefab("FX_Pull", go => Prefabs.Pull(go, m, loop: true), typeof(FxReach));
            library.pullBurst = Prefab("FX_PullBurst", go => Prefabs.Pull(go, m, loop: false), typeof(FxReach));
            library.runeCast = Prefab("FX_RuneCast", go => Prefabs.RuneCast(go, m));
            library.runeGain = Prefab("FX_RuneGain", go => Prefabs.RuneGain(go, m));

            // Passives
            library.lifesteal = Prefab("FX_Lifesteal", go => Prefabs.Lifesteal(go, m), typeof(FxReach));
            library.frenzyStack = Prefab("FX_FrenzyStack", go => Prefabs.FrenzyStack(go, m));
            library.frenzyAura = Prefab("FX_FrenzyAura", go => Prefabs.FrenzyAura(go, m), typeof(FxEmissionByValue));

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            Selection.activeObject = library;
            Debug.Log("[LearnFX] 완료: Effects/Learn 에 텍스처/머티리얼/프리팹 27개, Resources/HJH_FxLibrary 생성.");
        }

        private struct Mats
        {
            public Material Slash, Glow, Spark, Star, Ring, Smoke, Line, Trail, Magic;
        }

        private static Shader LoadShader(string name) => AssetDatabase.LoadAssetAtPath<Shader>($"{ShaderRoot}/{name}.shadergraph");

        private static Material Mat(string name, Shader shader, string texture, float intensity, Action<Material> extra = null)
        {
            string path = $"{MaterialRoot}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(shader);
            material.SetTexture("_MainTex", Textures.Load(texture));
            material.SetColor("_Tint", Color.white);
            material.SetFloat("_Intensity", intensity);
            extra?.Invoke(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // requiredDriver: an existing prefab without it is from an older build and gets rebuilt.
        // outdatedPart: an existing prefab that still has it is from an older build and gets rebuilt too.
        private static GameObject Prefab(string name, Action<GameObject> build, Type requiredDriver = null, Type outdatedPart = null)
        {
            string path = $"{PrefabRoot}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            bool upToDate = existing != null
                            && (requiredDriver == null || existing.GetComponentInChildren(requiredDriver, true) != null)
                            && (outdatedPart == null || existing.GetComponentInChildren(outdatedPart, true) == null);
            if (upToDate)
                return existing;

            var root = new GameObject(name);
            try
            {
                build(root);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // =====================================================================================
        // Effect prefabs. Each child = one layer of the effect; open the prefab to see which modules
        // are on. Particles use Scaling Mode = Hierarchy so the root scale sizes the whole effect.
        // Components named Fx* (FxLine, FxSwipe, FxReach...) receive runtime values from the game.
        // =====================================================================================
        private static class Prefabs
        {
            // ------------------------------------------------------------- combat

            public static void Hit(GameObject root, Mats m)
            {
                Flash(root, "Flash", m.Glow, 1.3f, 0.12f);
                Flash(root, "Flare", m.Star, 1.7f, 0.08f, randomRotation: true);

                ParticleSystem sparks = Layer(root, "Sparks", m.Spark, ParticleSystemRenderMode.Stretch);
                Burst(sparks, 8, 12);
                Life(sparks, 0.15f, 0.3f);
                Speed(sparks, 8f, 16f);
                Size(sparks, 0.08f, 0.15f);
                Cone(sparks, 35f);
                Drag(sparks, 5f);
                Stretch(sparks, 0.06f);
                ShrinkAndFade(sparks, 0.3f);
            }

            public static void Muzzle(GameObject root, Mats m)
            {
                Flash(root, "Flash", m.Glow, 0.9f, 0.06f);
                Flash(root, "Flare", m.Star, 1.1f, 0.05f, randomRotation: true);

                ParticleSystem sparks = Layer(root, "Sparks", m.Spark, ParticleSystemRenderMode.Stretch);
                Burst(sparks, 4, 6);
                Life(sparks, 0.06f, 0.12f);
                Speed(sparks, 6f, 12f);
                Size(sparks, 0.05f, 0.09f);
                Cone(sparks, 20f);
                Stretch(sparks, 0.05f);
                ShrinkAndFade(sparks, 0.2f);
            }

            public static void Explosion(GameObject root, Mats m)
            {
                Flash(root, "Flash", m.Glow, 3.5f, 0.18f, height: 0.6f);
                Flash(root, "Core", m.Star, 3f, 0.12f, randomRotation: true, height: 0.6f);
                SparkBurst(root, "Sparks", m.Spark, 20, 28, 6f, 13f, 0.2f, 0.45f, 0.6f);

                ParticleSystem embers = Layer(root, "Embers", m.Glow, ParticleSystemRenderMode.Billboard, 0.5f);
                Burst(embers, 8, 12);
                Life(embers, 0.5f, 1f);
                Speed(embers, 1.5f, 4f);
                Size(embers, 0.12f, 0.22f);
                Sphere(embers, 0.3f);
                ParticleSystem.MainModule emberMain = embers.main;
                emberMain.gravityModifier = -0.15f;
                Drag(embers, 2f);
                ShrinkAndFade(embers, 0f);

                GroundRing(root, "Ring", m.Ring, 0.35f);
                Dust(root, "Smoke", m.Smoke, 6, 8, 0.4f, 1.2f, 2f);
            }

            public static void Shockwave(GameObject root, Mats m)
            {
                GroundRing(root, "Ring", m.Ring, 0.3f);
                Flash(root, "Flash", m.Glow, 1.2f, 0.1f, height: 0.3f);
            }

            public static void GroundSlam(GameObject root, Mats m)
            {
                GroundRing(root, "Ring", m.Ring, 0.3f);
                Flash(root, "Flash", m.Glow, 1.5f, 0.12f, height: 0.3f);
                SparkBurst(root, "Debris", m.Spark, 10, 14, 5f, 10f, 0.2f, 0.35f, 0.3f);
                Dust(root, "Dust", m.Smoke, 8, 10, 0.5f, 0.8f, 1.4f);
            }

            // Text, not particles: TextMeshPro + FxDamageNumber animates it.
            public static void DamageNumber(GameObject root)
            {
                var text = root.AddComponent<TextMeshPro>();
                if (TMP_Settings.defaultFontAsset != null)
                    text.font = TMP_Settings.defaultFontAsset;
                text.fontSize = 6f;
                text.alignment = TextAlignmentOptions.Center;
                text.fontStyle = FontStyles.Bold;
                text.outlineWidth = 0.25f;
                text.outlineColor = new Color32(0, 0, 0, 220);
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.rectTransform.sizeDelta = new Vector2(6f, 2f);
                text.sortingOrder = 100;
                text.text = "123";

                Set(root.AddComponent<FxDamageNumber>(), "text", text);
            }

            public static void CharacterSwap(GameObject root, Mats m)
            {
                GroundRing(root, "Ring", m.Ring, 0.4f, 2.5f);
                Flash(root, "Flash", m.Glow, 3f, 0.25f, height: 1f);

                ParticleSystem rise = Layer(root, "Rise", m.Glow, ParticleSystemRenderMode.Billboard, 0.2f);
                Burst(rise, 16, 20);
                Life(rise, 0.4f, 0.8f);
                Size(rise, 0.1f, 0.2f);
                Circle(rise, 1f);
                Velocity(rise, 2f, 4f);
                ShrinkAndFade(rise, 0f);
            }

            // ------------------------------------------------------------- weapons and projectiles

            public static void Tracer(GameObject root, Mats m)
            {
                LineRenderer body = Line(root, "Line", m.Line);
                var line = root.AddComponent<FxLine>();
                SetArray(line, "lines", body);
            }

            public static void Swipe(GameObject root, Mats m)
            {
                // Smooth blade texture (bright center, soft edges, fading toward the tail) - no scrolling, it reads as one swing.
                LineRenderer ribbon = Line(root, "Ribbon", m.Slash);
                ribbon.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.7f, 0.8f), new Keyframe(1f, 1f));

                var tip = new GameObject("Tip");
                tip.transform.SetParent(root.transform, false);
                ParticleSystem glow = Layer(tip, "TipGlow", m.Glow, ParticleSystemRenderMode.Billboard);
                Loop(glow, 60f);
                Life(glow, 0.06f, 0.06f);
                Size(glow, 1.3f, 1.3f);
                ShrinkAndFade(glow, 0f);

                var swipe = root.AddComponent<FxSwipe>();
                Set(swipe, "line", ribbon);
                Set(swipe, "tip", tip.transform);
            }

            // Chain bruiser swing: only the Batslash blade (same effect as the R/Q spin), aimed, sized and timed
            // per swing by FxSlash. Its hit sparks and dust are switched off so a swing reads as one clean cut.
            public static void ChainSlash(GameObject root, Mats m)
            {
                const string sourcePath = "Assets/Members/HJH/Effects/Batslash/Batslash_2.prefab";
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                Transform holder = Child(root, "Holder (aimed per swing)");

                ParticleSystem[] slashSystems = Array.Empty<ParticleSystem>();
                if (source != null)
                {
                    var blade = (GameObject)PrefabUtility.InstantiatePrefab(source, holder);

                    foreach (Transform child in blade.GetComponentsInChildren<Transform>(true))
                    {
                        if (child.name == "dust" || child.name == "hit" || child.name == "hit_1")
                            child.gameObject.SetActive(false);
                    }

                    ParticleSystem[] all = blade.GetComponentsInChildren<ParticleSystem>(true);
                    slashSystems = Array.FindAll(all, s => s.name == "slash_alp" || s.name == "slash_add");
                    foreach (ParticleSystem system in all)
                        Untinted(root, system);
                }
                else
                {
                    Debug.LogWarning($"[LearnFX] {sourcePath} 가 없어 칼날 없이 빈 프리팹을 만듭니다.");
                }

                var slash = root.AddComponent<FxSlash>();
                Set(slash, "holder", holder);
                SetArray(slash, "slashSystems", Array.ConvertAll(slashSystems, s => (Object)s));
            }

            // Held: moved every frame, Kill stops emission and the trail fades.
            public static void Orb(GameObject root, Mats m)
            {
                // Core: emitted every frame in world space, so moving the orb leaves a glowing wake.
                ParticleSystem core = Layer(root, "Core", m.Glow, ParticleSystemRenderMode.Billboard);
                Loop(core, 60f);
                Life(core, 0.12f, 0.15f);
                Size(core, 0.9f, 1f);
                ShrinkAndFade(core, 0f);

                ParticleSystem heart = Layer(root, "Heart", m.Glow, ParticleSystemRenderMode.Billboard);
                Loop(heart, 40f);
                Life(heart, 0.05f, 0.06f);
                Size(heart, 0.45f, 0.5f);
                Untinted(root, heart);

                ParticleSystem sparkles = Layer(root, "Sparkles", m.Spark, ParticleSystemRenderMode.Billboard);
                ParticleSystem.EmissionModule emission = sparkles.emission;
                emission.rateOverTime = 0f;
                emission.rateOverDistance = 12f;
                Life(sparkles, 0.2f, 0.4f);
                Speed(sparkles, 0.5f, 2.5f);
                Size(sparkles, 0.05f, 0.1f);
                Sphere(sparkles, 0.2f);
                ShrinkAndFade(sparkles, 0f);

                Trail(root, m.Trail, 0.2f, 0.5f);
            }

            public static void ProjectileImpact(GameObject root, Mats m)
            {
                Flash(root, "Flash", m.Glow, 2f, 0.12f);
                ParticleSystem core = Flash(root, "Core", m.Glow, 0.9f, 0.07f);
                Untinted(root, core);

                ParticleSystem sparks = Layer(root, "Sparks", m.Spark, ParticleSystemRenderMode.Stretch);
                Burst(sparks, 6, 10);
                Life(sparks, 0.12f, 0.22f);
                Speed(sparks, 5f, 9f);
                Size(sparks, 0.06f, 0.1f);
                Cone(sparks, 70f);
                Drag(sparks, 4f);
                Stretch(sparks, 0.05f);
                ShrinkAndFade(sparks, 0.2f);
            }

            // ------------------------------------------------------------- movement

            public static void DashTrail(GameObject root, Mats m)
            {
                TrailRenderer trail = Trail(root, m.Trail, 0.2f, 1f);
                trail.colorGradient = FadeGradient(Color.white, 0.8f, 0f);
                root.AddComponent<FxFollow>();
                Set(root.AddComponent<FxTrailToggle>(), "trail", trail);
            }

            public static void TeleportStreak(GameObject root, Mats m)
            {
                LineRenderer body = Line(root, "Body", m.Line);
                LineRenderer core = Line(root, "Core", m.Line);

                Transform start = Child(root, "Start");
                Flash(start.gameObject, "StartFlash", m.Glow, 2f, 0.15f);
                Transform end = Child(root, "End");
                ParticleSystem endFlash = Flash(end.gameObject, "EndFlash", m.Glow, 1.6f, 0.12f);
                Untinted(root, endFlash);

                var line = root.AddComponent<FxLine>();
                SetArray(line, "lines", body, core);
                SetFloats(line, "widthScale", 1f, 0.35f);
                SetArray(line, "untinted", core);
                Set(line, "startPoint", start);
                Set(line, "endPoint", end);
            }

            public static void BlinkOut(GameObject root, Mats m)
            {
                ParticleSystem implode = Layer(root, "Implode", m.Spark, ParticleSystemRenderMode.Stretch, 0.5f);
                Burst(implode, 10, 12);
                Life(implode, 0.15f, 0.15f);
                Size(implode, 0.1f, 0.14f);
                Circle(implode, 1.5f);
                Speed(implode, -10f, -10f);
                Stretch(implode, 0.04f);
                ShrinkAndFade(implode, 0.3f);

                SparkBurst(root, "Sparks", m.Spark, 8, 12, 4f, 8f, 0.15f, 0.25f, 0.9f);
            }

            public static void BlinkIn(GameObject root, Mats m)
            {
                ParticleSystem flash = Flash(root, "Flash", m.Glow, 2f, 0.12f, height: 0.9f);
                Untinted(root, flash);
                GroundRing(root, "Ring", m.Ring, 0.2f, 1.6f);

                ParticleSystem sparks = Layer(root, "Sparks", m.Spark, ParticleSystemRenderMode.Stretch, 0.9f);
                Burst(sparks, 6, 10);
                Life(sparks, 0.15f, 0.25f);
                Speed(sparks, 5f, 9f);
                Size(sparks, 0.08f, 0.12f);
                Cone(sparks, 50f);
                Drag(sparks, 4f);
                Stretch(sparks, 0.05f);
                ShrinkAndFade(sparks, 0.2f);
            }

            // ------------------------------------------------------------- skills

            // Held: core grows with the charge (FxScaleByValue), particles keep flowing into it.
            public static void Charge(GameObject root, Mats m)
            {
                Transform coreRoot = Child(root, "Core (scaled by charge)");
                ParticleSystem core = Layer(coreRoot.gameObject, "Glow", m.Glow, ParticleSystemRenderMode.Billboard);
                Loop(core, 40f);
                Life(core, 0.06f, 0.06f);
                Size(core, 1f, 1f);

                ParticleSystem inflow = Layer(root, "Inflow", m.Spark, ParticleSystemRenderMode.Stretch);
                Loop(inflow, 25f);
                Life(inflow, 0.1f, 0.1f);
                Size(inflow, 0.06f, 0.1f);
                Circle(inflow, 1.2f);
                Speed(inflow, -12f, -12f);
                Stretch(inflow, 0.03f);
                ShrinkAndFade(inflow, 0.3f);

                var scale = root.AddComponent<FxScaleByValue>();
                Set(scale, "target", coreRoot);
            }

            public static void Beam(GameObject root, Mats m)
            {
                LineRenderer body = Line(root, "Body", m.Line);
                LineRenderer core = Line(root, "Core", m.Line);

                Transform start = Child(root, "Start");
                Flash(start.gameObject, "MuzzleFlash", m.Glow, 2.6f, 0.15f);

                ParticleSystem along = Layer(root, "SparksAlongBeam", m.Spark, ParticleSystemRenderMode.Stretch);
                Burst(along, 16, 22);
                Life(along, 0.15f, 0.3f);
                Speed(along, 6f, 14f);
                Size(along, 0.08f, 0.12f);
                ParticleSystem.ShapeModule shape = along.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.randomDirectionAmount = 0.4f;
                Drag(along, 4f);
                Stretch(along, 0.05f);
                ShrinkAndFade(along, 0.2f);

                var line = root.AddComponent<FxLine>();
                SetArray(line, "lines", body, core);
                SetFloats(line, "widthScale", 1f, 0.3f);
                SetArray(line, "untinted", core);
                Set(line, "startPoint", start);
                SetArray(line, "alongLine", along);
            }

            // Gunner R charge: a barrel made of energy in front of the pistols (no model change needed).
            //   Body/Core lines  - the barrel itself (FxLine; thickness follows the charge)
            //   Segments         - glowing rings drifting along the barrel (box shape stretched by FxLine)
            //   Tip              - ring + core glow + particles pulled in, grows with the charge (FxScaleByValue)
            public static void EnergyCannon(GameObject root, Mats m)
            {
                LineRenderer body = Line(root, "Body", m.Line);
                LineRenderer core = Line(root, "Core", m.Line);

                ParticleSystem segments = Layer(root, "Segments", m.Ring, ParticleSystemRenderMode.Billboard);
                Loop(segments, 18f);
                Life(segments, 0.2f, 0.25f);
                Size(segments, 0.6f, 0.8f);
                ShrinkAndFade(segments, 0.4f);

                Transform tip = Child(root, "Tip (grows with charge)");
                ParticleSystem tipRing = Layer(tip.gameObject, "TipRing", m.Ring, ParticleSystemRenderMode.Billboard);
                Loop(tipRing, 8f);
                Life(tipRing, 0.25f, 0.25f);
                Size(tipRing, 1.4f, 1.4f);
                ShrinkAndFade(tipRing, 0.2f);

                ParticleSystem tipGlow = Layer(tip.gameObject, "TipGlow", m.Glow, ParticleSystemRenderMode.Billboard);
                Loop(tipGlow, 40f);
                Life(tipGlow, 0.06f, 0.06f);
                Size(tipGlow, 1.2f, 1.2f);

                ParticleSystem inflow = Layer(tip.gameObject, "Inflow", m.Spark, ParticleSystemRenderMode.Stretch);
                Loop(inflow, 30f);
                Life(inflow, 0.12f, 0.12f);
                Size(inflow, 0.06f, 0.1f);
                Sphere(inflow, 1.4f);
                Speed(inflow, -11f, -11f);
                Stretch(inflow, 0.03f);
                ShrinkAndFade(inflow, 0.3f);

                var line = root.AddComponent<FxLine>();
                SetArray(line, "lines", body, core);
                SetFloats(line, "widthScale", 1f, 0.35f);
                SetArray(line, "untinted", core);
                Set(line, "endPoint", tip);
                SetArray(line, "alongLine", segments);

                var scale = root.AddComponent<FxScaleByValue>();
                Set(scale, "target", tip);
                SetFloat(scale, "minScale", 0.4f);
                SetFloat(scale, "maxScale", 1.2f);
            }

            public static void CannonMuzzle(GameObject root, Mats m)
            {
                Flash(root, "Flash", m.Glow, 3.5f, 0.15f);
                ParticleSystem core = Flash(root, "Core", m.Glow, 1.5f, 0.08f);
                Untinted(root, core);

                ParticleSystem sparks = Layer(root, "Sparks", m.Spark, ParticleSystemRenderMode.Stretch);
                Burst(sparks, 16, 20);
                Life(sparks, 0.15f, 0.3f);
                Speed(sparks, 9f, 18f);
                Size(sparks, 0.1f, 0.16f);
                Cone(sparks, 30f);
                Drag(sparks, 4f);
                Stretch(sparks, 0.05f);
                ShrinkAndFade(sparks, 0.2f);

                Dust(root, "Smoke", m.Smoke, 5, 7, 0.3f, 0.8f, 1.4f);
            }

            public static void MagicCircle(GameObject root, Mats m)
            {
                // The circle itself is a flat quad drawn by the SG_FX_MagicCircle Shader Graph.
                var quad = new GameObject("Circle (Shader Graph)");
                quad.transform.SetParent(root.transform, false);
                quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                quad.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                var renderer = quad.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = m.Magic;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                // Motes rising from the rim.
                ParticleSystem motes = Layer(root, "Motes", m.Glow, ParticleSystemRenderMode.Billboard);
                Loop(motes, 25f);
                Life(motes, 0.5f, 1f);
                Size(motes, 0.04f, 0.08f);
                Circle(motes, 0.5f);
                Velocity(motes, 1f, 2f);
                ShrinkAndFade(motes, 0f);

                Set(root.AddComponent<FxMagicCircleDriver>(), "circle", renderer);
            }

            // Particles spawned on a circle (radius from the game) flying into the center.
            public static void Pull(GameObject root, Mats m, bool loop)
            {
                ParticleSystem inflow = Layer(root, "Inflow", m.Spark, ParticleSystemRenderMode.Stretch, 0.5f);
                if (loop)
                    Loop(inflow, 30f);
                else
                    Burst(inflow, 22, 26);
                Life(inflow, loop ? 0.3f : 0.12f, loop ? 0.3f : 0.12f);
                Size(inflow, 0.1f, 0.14f);
                Circle(inflow, 1f);
                Stretch(inflow, 0.03f);
                ShrinkAndFade(inflow, 0.3f);

                var reach = root.AddComponent<FxReach>();
                SetEnum(reach, "mode", (int)FxReach.Mode.Inward);
                SetArray(reach, "systems", inflow);
            }

            public static void RuneCast(GameObject root, Mats m)
            {
                Flash(root, "Flash", m.Glow, 1.5f, 0.15f);
                Flash(root, "Flare", m.Star, 1.2f, 0.1f, randomRotation: true);

                ParticleSystem sparks = Layer(root, "Sparks", m.Spark, ParticleSystemRenderMode.Stretch);
                Burst(sparks, 6, 10);
                Life(sparks, 0.12f, 0.2f);
                Speed(sparks, 6f, 10f);
                Size(sparks, 0.06f, 0.1f);
                Cone(sparks, 25f);
                Stretch(sparks, 0.05f);
                ShrinkAndFade(sparks, 0.2f);
            }

            public static void RuneGain(GameObject root, Mats m)
            {
                Flash(root, "Flash", m.Glow, 1.2f, 0.12f);
                SparkBurst(root, "Sparks", m.Spark, 3, 5, 2f, 4f, 0.15f, 0.3f, 0f);
            }

            // ------------------------------------------------------------- passives

            // Drops flying from the target to the player (FxReach Toward sets the speed from the distance).
            public static void Lifesteal(GameObject root, Mats m)
            {
                ParticleSystem drops = Layer(root, "Drops", m.Glow, ParticleSystemRenderMode.Billboard);
                Burst(drops, 3, 4);
                Life(drops, 0.3f, 0.3f);
                Size(drops, 0.12f, 0.18f);
                Cone(drops, 15f);
                ShrinkAndFade(drops, 0.4f);

                var reach = root.AddComponent<FxReach>();
                SetEnum(reach, "mode", (int)FxReach.Mode.Toward);
                SetArray(reach, "systems", drops);
            }

            public static void FrenzyStack(GameObject root, Mats m)
            {
                GroundRing(root, "Ring", m.Ring, 0.3f, 1.5f);
                Flash(root, "Flash", m.Glow, 2f, 0.2f, height: 1f);
            }

            // Held on the player: embers rising around the feet, rate = stacks x 8 (FxEmissionByValue).
            public static void FrenzyAura(GameObject root, Mats m)
            {
                ParticleSystem embers = Layer(root, "Embers", m.Spark, ParticleSystemRenderMode.Billboard, 0.3f);
                Loop(embers, 0f);
                Life(embers, 0.3f, 0.5f);
                Size(embers, 0.06f, 0.1f);
                Circle(embers, 0.6f);
                Velocity(embers, 1.5f, 3f);
                ShrinkAndFade(embers, 0f);

                root.AddComponent<FxFollow>();
                var rate = root.AddComponent<FxEmissionByValue>();
                SetArray(rate, "systems", embers);
            }

            // ------------------------------------------------------------- building blocks

            private static ParticleSystem Layer(GameObject root, string name, Material material, ParticleSystemRenderMode mode, float height = 0f)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = Vector3.up * height;

                var system = go.AddComponent<ParticleSystem>();
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                ParticleSystem.MainModule main = system.main;
                main.duration = 1f;
                main.loop = false;
                main.playOnAwake = false;
                main.startSpeed = 0f;
                main.startColor = Color.white;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.maxParticles = 200;

                ParticleSystem.EmissionModule emission = system.emission;
                emission.rateOverTime = 0f;

                ParticleSystem.ShapeModule shape = system.shape;
                shape.enabled = false;

                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.renderMode = mode;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                return system;
            }

            private static ParticleSystem Flash(GameObject root, string name, Material material, float size, float life, bool randomRotation = false, float height = 0f)
            {
                ParticleSystem flash = Layer(root, name, material, ParticleSystemRenderMode.Billboard, height);
                Burst(flash, 1, 1);
                Life(flash, life, life);
                Size(flash, size, size);
                if (randomRotation)
                {
                    ParticleSystem.MainModule main = flash.main;
                    main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                }

                ShrinkAndFade(flash, 0f);
                return flash;
            }

            private static void SparkBurst(GameObject root, string name, Material material, int min, int max, float minSpeed, float maxSpeed,
                float minLife, float maxLife, float height)
            {
                ParticleSystem sparks = Layer(root, name, material, ParticleSystemRenderMode.Stretch, height);
                Burst(sparks, min, max);
                Life(sparks, minLife, maxLife);
                Speed(sparks, minSpeed, maxSpeed);
                Size(sparks, 0.1f, 0.18f);
                Sphere(sparks, 0.2f);
                Drag(sparks, 4f);
                Stretch(sparks, 0.06f);
                ShrinkAndFade(sparks, 0.2f);
            }

            // Alpha-blended smoke puffs that keep their own grey color.
            private static void Dust(GameObject root, string name, Material material, int min, int max, float radius, float minSize, float maxSize)
            {
                ParticleSystem dust = Layer(root, name, material, ParticleSystemRenderMode.Billboard, 0.3f);
                Burst(dust, min, max);
                Life(dust, 0.6f, 1.1f);
                Speed(dust, 0.8f, 2f);
                Size(dust, minSize, maxSize);
                Sphere(dust, radius);
                ParticleSystem.MainModule main = dust.main;
                main.startColor = new Color(0.6f, 0.58f, 0.55f, 0.55f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                Grow(dust, 0.6f, 1.3f);
                FadeOut(dust);
                Untinted(root, dust);
            }

            // Flat ring lying on the ground that grows outward (Horizontal Billboard = faces up).
            // The ring texture sits at 78% of the quad, so size 2/0.78 reaches radius 1 at scale 1.
            private static void GroundRing(GameObject root, string name, Material material, float life, float radius = 1f)
            {
                ParticleSystem ring = Layer(root, name, material, ParticleSystemRenderMode.HorizontalBillboard, 0.05f);
                Burst(ring, 1, 1);
                Life(ring, life, life);
                float size = radius * 2f / 0.78f;
                Size(ring, size, size);
                Grow(ring, 0.15f, 1f);
                FadeOut(ring);
            }

            private static LineRenderer Line(GameObject root, string name, Material material)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.SetPositions(new[] { Vector3.zero, Vector3.forward });
                line.textureMode = LineTextureMode.Stretch;
                line.numCapVertices = 4;
                line.numCornerVertices = 2;
                line.widthMultiplier = 0.2f;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                return line;
            }

            private static TrailRenderer Trail(GameObject root, Material material, float time, float width)
            {
                var trail = root.AddComponent<TrailRenderer>();
                trail.sharedMaterial = material;
                trail.time = time;
                trail.minVertexDistance = 0.05f;
                trail.widthMultiplier = width;
                trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                trail.colorGradient = FadeGradient(Color.white, 1f, 0f);
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.emitting = false;
                return trail;
            }

            private static Transform Child(GameObject root, string name)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                return go.transform;
            }

            private static void Burst(ParticleSystem system, int min, int max)
            {
                ParticleSystem.EmissionModule emission = system.emission;
                emission.enabled = true;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)min, (short)max) });
            }

            private static void Loop(ParticleSystem system, float rate)
            {
                ParticleSystem.MainModule main = system.main;
                main.loop = true;
                ParticleSystem.EmissionModule emission = system.emission;
                emission.enabled = true;
                emission.rateOverTime = rate;
            }

            private static void Life(ParticleSystem system, float min, float max)
            {
                ParticleSystem.MainModule main = system.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(min, max);
            }

            private static void Speed(ParticleSystem system, float min, float max)
            {
                ParticleSystem.MainModule main = system.main;
                main.startSpeed = new ParticleSystem.MinMaxCurve(min, max);
            }

            private static void Size(ParticleSystem system, float min, float max)
            {
                ParticleSystem.MainModule main = system.main;
                main.startSize = new ParticleSystem.MinMaxCurve(min, max);
            }

            private static void Cone(ParticleSystem system, float angle)
            {
                ParticleSystem.ShapeModule shape = system.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = angle;
                shape.radius = 0.05f;
            }

            private static void Sphere(ParticleSystem system, float radius)
            {
                ParticleSystem.ShapeModule shape = system.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = radius;
            }

            // Ring on the ground (XZ). Particles start on the edge; with a negative speed they fly inward.
            private static void Circle(ParticleSystem system, float radius)
            {
                ParticleSystem.ShapeModule shape = system.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = radius;
                shape.radiusThickness = 0f;
                shape.rotation = new Vector3(90f, 0f, 0f);
            }

            // Straight up in world space (all three axes must use the same curve mode).
            private static void Velocity(ParticleSystem system, float minUp, float maxUp)
            {
                ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
                velocity.y = new ParticleSystem.MinMaxCurve(minUp, maxUp);
                velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            }

            private static void Drag(ParticleSystem system, float drag)
            {
                ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.drag = drag;
                limit.multiplyDragByParticleSize = false;
                limit.multiplyDragByParticleVelocity = true;
            }

            private static void Stretch(ParticleSystem system, float velocityScale)
            {
                var renderer = system.GetComponent<ParticleSystemRenderer>();
                renderer.velocityScale = velocityScale;
                renderer.lengthScale = 1f;
            }

            // Size: 1 -> endSize, alpha: 1 -> 0.
            private static void ShrinkAndFade(ParticleSystem system, float endSize)
            {
                ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, endSize)));
                FadeOut(system);
            }

            private static void Grow(ParticleSystem system, float from, float to)
            {
                ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
                size.enabled = true;
                var curve = new AnimationCurve(new Keyframe(0f, from, 0f, 3f), new Keyframe(1f, to, 0f, 0f));
                size.size = new ParticleSystem.MinMaxCurve(1f, curve);
            }

            private static void FadeOut(ParticleSystem system)
            {
                ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
                color.enabled = true;
                color.color = FadeGradient(Color.white, 1f, 0f);
            }

            private static void Untinted(GameObject root, ParticleSystem system)
            {
                // Flashes under child points (Start/End) still belong to the prefab root.
                GameObject prefabRoot = root.transform.root.gameObject;
                if (!prefabRoot.TryGetComponent(out FxPrefabInstance instance))
                    instance = prefabRoot.AddComponent<FxPrefabInstance>();

                var so = new SerializedObject(instance);
                SerializedProperty list = so.FindProperty("untinted");
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = system;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            private static Gradient FadeGradient(Color color, float from, float to)
            {
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                    new[] { new GradientAlphaKey(from, 0f), new GradientAlphaKey(Mathf.Lerp(from, to, 0.4f), 0.6f), new GradientAlphaKey(to, 1f) });
                return gradient;
            }

            // ------------------------------------------------------------- serialized field helpers

            private static void Set(Object target, string field, Object value)
            {
                var so = new SerializedObject(target);
                so.FindProperty(field).objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            private static void SetFloat(Object target, string field, float value)
            {
                var so = new SerializedObject(target);
                so.FindProperty(field).floatValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            private static void SetEnum(Object target, string field, int value)
            {
                var so = new SerializedObject(target);
                so.FindProperty(field).intValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            private static void SetArray(Object target, string field, params Object[] values)
            {
                var so = new SerializedObject(target);
                SerializedProperty list = so.FindProperty(field);
                list.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            private static void SetFloats(Object target, string field, params float[] values)
            {
                var so = new SerializedObject(target);
                SerializedProperty list = so.FindProperty(field);
                list.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                    list.GetArrayElementAtIndex(i).floatValue = values[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // =====================================================================================
        // Painted textures (white RGB, shape in alpha) so the materials' Tint / vertex color decide the color.
        // =====================================================================================
        private static class Textures
        {
            public static Texture2D Load(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureRoot}/{name}.png");

            public static void EnsureAll()
            {
                Paint("T_Glow", 128, 128, (u, v) => Mathf.Pow(Saturate(1f - Radius(u, v)), 2.2f));
                Paint("T_Spark", 64, 64, (u, v) => Mathf.Pow(Saturate(1f - Radius(u, v)), 4f) * 1.4f);
                Paint("T_Star", 256, 256, Star);
                Paint("T_Ring", 256, 256, (u, v) =>
                {
                    float d = Radius(u, v);
                    return Gauss(d - 0.78f, 0.05f) + Gauss(d - 0.72f, 0.18f) * 0.25f;
                });
                Paint("T_Smoke", 128, 128, (u, v) => Saturate(Fbm(u * 4f, v * 4f) * 1.4f - 0.2f) * Mathf.Pow(Saturate(1f - Radius(u, v)), 1.5f));
                Paint("T_MagicCircle", 512, 512, MagicCircle);
                Paint("T_EnergyStreak", 256, 64, EnergyStreak, TextureWrapMode.Repeat);
                Paint("T_LineMask", 128, 64, (u, v) => Gauss(v - 0.5f, 0.22f) * Saturate(u / 0.06f) * Saturate((1f - u) / 0.06f));
                Paint("T_Slash", 256, 64, (u, v) =>
                {
                    float d = v - 0.5f;
                    float core = Mathf.Exp(-(d * d) / (0.2f * 0.2f)) + 0.6f * Mathf.Exp(-(d * d) / (0.05f * 0.05f));
                    float tail = Saturate(u / 0.35f);
                    return core * tail * tail * (1f - 0.5f * Saturate((u - 0.94f) / 0.06f));
                });
            }

            private static void Paint(string name, int width, int height, Func<float, float, float> alpha,
                TextureWrapMode wrap = TextureWrapMode.Clamp)
            {
                string path = $"{TextureRoot}/{name}.png";
                if (File.Exists(path))
                    return;

                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float a = Saturate(alpha((x + 0.5f) / width, (y + 0.5f) / height));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }

                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);

                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.alphaIsTransparency = true;
                    importer.wrapMode = wrap;
                    importer.mipmapEnabled = true;
                    importer.SaveAndReimport();
                }
            }

            private static float Radius(float u, float v)
            {
                float x = u * 2f - 1f;
                float y = v * 2f - 1f;
                return Mathf.Sqrt(x * x + y * y);
            }

            private static float Saturate(float v) => Mathf.Clamp01(v);
            private static float Gauss(float x, float width) => Mathf.Exp(-(x * x) / (width * width));

            private static float Star(float u, float v)
            {
                float x = Mathf.Abs(u * 2f - 1f);
                float y = Mathf.Abs(v * 2f - 1f);
                float arms = Mathf.Exp(-y * 40f) * Saturate(1f - x) + Mathf.Exp(-x * 40f) * Saturate(1f - y);
                float diagonal = Mathf.Exp(-Mathf.Abs(x - y) * 30f) * Saturate(1f - (x + y)) * 0.4f;
                return arms + diagonal + Mathf.Pow(Saturate(1f - Radius(u, v)), 3f);
            }

            private static float MagicCircle(float u, float v)
            {
                float x = u * 2f - 1f;
                float y = v * 2f - 1f;
                float d = Mathf.Sqrt(x * x + y * y);
                float angle = Mathf.Atan2(y, x);
                const float line = 0.012f;

                float a = Gauss(d - 0.95f, line * 1.4f) + Gauss(d - 0.88f, line) + Gauss(d - 0.55f, line);

                // Ticks between the two outer rings.
                float tick = Mathf.Abs(Mathf.Repeat(angle / (Mathf.PI * 2f) * 36f, 1f) - 0.5f);
                if (d > 0.88f && d < 0.95f)
                    a += Gauss(tick, 0.06f) * 0.8f;

                // Hexagram: two triangles inscribed in r = 0.88.
                for (int t = 0; t < 2; t++)
                for (int i = 0; i < 3; i++)
                {
                    float a0 = Mathf.PI / 2f + t * Mathf.PI / 3f + i * Mathf.PI * 2f / 3f;
                    float a1 = a0 + Mathf.PI * 2f / 3f;
                    var p0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * 0.88f;
                    var p1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * 0.88f;
                    a += Gauss(SegmentDistance(new Vector2(x, y), p0, p1), line);
                }

                // Rune dots on the middle ring.
                float dotAngle = Mathf.Repeat(angle / (Mathf.PI * 2f) * 12f, 1f) - 0.5f;
                a += Gauss(d - 0.72f, 0.03f) * Gauss(dotAngle, 0.12f);

                return a * Saturate((1f - d) * 30f + 1f);
            }

            // Tileable along U: only integer frequencies of u.
            private static float EnergyStreak(float u, float v)
            {
                float value = 0f;
                for (int i = 0; i < 6; i++)
                {
                    float lane = 0.2f + 0.6f * Hash(i * 7.13f);
                    float width = 0.04f + 0.06f * Hash(i * 3.71f);
                    int frequency = 1 + i % 3;
                    float pulse = 0.5f + 0.5f * Mathf.Sin((u * frequency + Hash(i * 1.37f)) * Mathf.PI * 2f);
                    value += Gauss(v - lane, width) * Mathf.Pow(pulse, 2f);
                }

                return 0.25f + value;
            }

            private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
            {
                Vector2 ab = b - a;
                float t = Saturate(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                return Vector2.Distance(p, a + ab * t);
            }

            private static float Hash(float n) => Mathf.Repeat(Mathf.Sin(n * 127.1f) * 43758.5453f, 1f);

            private static float Noise(float x, float y)
            {
                int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
                float fx = x - ix, fy = y - iy;
                float a = Hash(ix + iy * 57f), b = Hash(ix + 1 + iy * 57f);
                float c = Hash(ix + (iy + 1) * 57f), d = Hash(ix + 1 + (iy + 1) * 57f);
                float sx = fx * fx * (3f - 2f * fx), sy = fy * fy * (3f - 2f * fy);
                return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
            }

            private static float Fbm(float x, float y) =>
                Noise(x, y) * 0.5f + Noise(x * 2f, y * 2f) * 0.25f + Noise(x * 4f, y * 4f) * 0.125f + 0.125f;
        }
    }
}

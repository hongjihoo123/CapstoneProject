using System;
using System.IO;
using Assets.Members.HJH._02.Scripts.Char.Character;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Chain;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Gunner;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Player_1;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.RuneMage;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.KYR._01_Scripts;
using Members.KYR._01_Scripts.Stats;
using RobotWeapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Assets.Members.HJH._02.Scripts.EditorTools
{
    // HJH > Build Character Roster: creates the three characters' weapons, skills, passives, kits and
    // CharacterData assets (only the missing ones; existing assets keep their tuning), plus temporary icons.
    // ApplyToHjhScene additionally adds the CharacterSwitcher and the attack speed animator driver to the test scene.
    public static class CharacterRosterBuilder
    {
        private const string DataRoot = "Assets/Members/HJH/Data";
        private const string SkillRoot = DataRoot + "/Skill";
        private const string KitRoot = DataRoot + "/Kit";
        private const string WeaponRoot = DataRoot + "/Weapon";
        private const string CharacterRoot = DataRoot + "/Character";
        private const string IconRoot = "Assets/Members/HJH/UI/Icons/Skills";
        private const string ChainKitPath = KitRoot + "/ChainKit.asset";
        private const string ScenePath = "Assets/Members/HJH/Scene/WeaponSkillTest.unity";
        private const int EnemyMask = 1 << 9 | 1 << 10; // Enemy + Weakpoint

        // Batch entry: Unity.exe -batchmode -quit -executeMethod Assets.Members.HJH._02.Scripts.EditorTools.CharacterRosterBuilder.ApplyToHjhScene
        public static void ApplyToHjhScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            CharacterData[] roster = BuildAssets();
            WireScene(roster);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[CharacterRoster] HJH 씬 적용 완료");
        }

        [MenuItem("HJH/Build Character Roster")]
        public static void BuildAssetsMenu() => BuildAssets();

        // Same as the batch entry, but on whatever scene is open in the editor (save it afterwards).
        [MenuItem("HJH/Setup Fun Test Scene (Characters + VFX)")]
        public static void BuildAndWireOpenScene()
        {
            WireScene(BuildAssets());
            Debug.Log("[CharacterRoster] 열린 씬에 캐릭터 전환 + 이펙트/타격감 설정을 추가했습니다. 씬을 저장하세요 (Ctrl+S).");
        }

        public static CharacterData[] BuildAssets()
        {
            foreach (string folder in new[] { WeaponRoot, CharacterRoot, SkillRoot + "/Gunner", SkillRoot + "/RuneMage", SkillRoot + "/Chain" })
                Directory.CreateDirectory(folder);

            IconPainter.EnsureIcons(IconRoot);

            CharacterData gunner = BuildGunner();
            CharacterData chain = BuildChain();
            CharacterData runeMage = BuildRuneMage();
            FunFeelSetup.ApplyWeaponVisualDefaults();

            AssetDatabase.SaveAssets();
            return new[] { gunner, chain, runeMage };
        }

        // ---------------------------------------------------------------- Character 1: gunner

        private static CharacterData BuildGunner()
        {
            var weapon = LoadOrCreate<HitscanGunData>($"{WeaponRoot}/Gunner_DualPistols.asset", out bool created);
            if (created)
            {
                weapon.weaponName = "쌍권총";
                weapon.type = WeaponType.MainDealer;
                weapon.description = "좌우 교대 사격. 빠른 연사, 낮은 한 발 피해. 탄창이 비면 자동 재장전.";
                weapon.resourceMax = 12f;
                weapon.reloadDuration = 1f;
                weapon.damagePerShot = 8f;
                weapon.fireRate = 8f;
                weapon.range = 14f;
                weapon.hitMask = EnemyMask;
                EditorUtility.SetDirty(weapon);
            }

            SkillData beam = Skill<PiercingBeamSkillData>("Gunner/Gunner_PiercingBeam", null, "GunnerBeam",
                "관통 빔", "커서 방향 일직선으로 모든 적을 꿰뚫는 빔. 쏘는 동안에도 이동 가능(감속).",
                cooldown: 6f, duration: 0.3f, allowsMove: true, allowsFire: false, moveSpeed: 0.6f);

            SkillData dash = Skill<DashSkillData>("Gunner/Gunner_Dash", null, "GunnerDash",
                "질주", "이동 방향으로 짧게 대시하고 즉시 재장전. 2회 충전, 대시 중 사격 가능.",
                cooldown: 3f, duration: 0.2f, allowsMove: false, allowsFire: true, charges: 2,
                extra: so =>
                {
                    so.FindProperty("dashForce").floatValue = 22f;
                    so.FindProperty("useMoveInputDirection").boolValue = true;
                    so.FindProperty("reloadOnDash").boolValue = true;
                });

            SkillData cannon = Skill<ShockCannonSkillData>("Gunner/Gunner_ShockCannon", null, "GunnerCannon",
                "충격포", "권총 앞에 에너지 포신을 만들어 충전한 뒤 한 발을 쏜다. 처음 맞은 적에게 큰 피해 후 사방으로 충격탄이 퍼진다. 쏜 직후 반동으로 뒤로 빠진다.",
                cooldown: 40f, duration: 0.45f, allowsMove: false, allowsFire: false);

            PassiveData passive = Passive<RunAndGunPassiveData>("Gunner/Gunner_RunAndGun", "GunnerPassive",
                "질주 사격", "대시 후 다음 2발이 강화탄(피해 1.6배). 강화탄을 맞히면 대시 충전 시간 0.6초 감소.");

            WeaponKitData kit = Kit("GunnerKit", weapon, dash, beam, cannon, passive);

            return Character("Char1_Gunner", "건슬링어", "원거리 카이팅 · 단일 특화",
                "셋 중 가장 빠르고 가벼운 캐릭터. 2회 충전 대시와 대시 중 사격으로 거리를 유지하며 한 대상을 녹인다.",
                new Color(0.45f, 0.85f, 1f), kit,
                new[]
                {
                    Bonus(PlayerStatId.WalkSpeed, 0.15f),
                    Bonus(PlayerStatId.RunSpeed, 0.15f)
                });
        }

        // ---------------------------------------------------------------- Character 2: chain bruiser (existing kit)

        private static CharacterData BuildChain()
        {
            var kit = AssetDatabase.LoadAssetAtPath<WeaponKitData>(ChainKitPath);
            PassiveData passive = Passive<BloodFrenzyPassiveData>("Chain/Chain_BloodFrenzy", "ChainPassive",
                "피의 광란", "준 피해의 8%를 회복(스킬 포함). 적 처치 시 공격속도 +10% (3초, 최대 5중첩, 처치마다 갱신).");

            if (kit != null && kit.passive == null)
            {
                kit.passive = passive;
                EditorUtility.SetDirty(kit);
            }

            return Character("Char2_Chain", "체인 브루저", "근접 브루저 · 치고 빠지기",
                "도끼와 철퇴를 사슬로 이은 무기. 대시 타격으로 파고들고, 피흡과 처치 공속으로 버티며 싸운다.",
                new Color(1f, 0.55f, 0.3f), kit, Array.Empty<CharacterData.StatBonus>());
        }

        // ---------------------------------------------------------------- Character 3: rune mage

        private static CharacterData BuildRuneMage()
        {
            var weapon = LoadOrCreate<HitscanGunData>($"{WeaponRoot}/RuneMage_Bolt.asset", out bool created);
            if (created)
            {
                weapon.weaponName = "룬 마법탄";
                weapon.type = WeaponType.SubDealer;
                weapon.description = "느리지만 묵직한 마법탄. 적중할 때마다 룬이 하나 생긴다.";
                weapon.resourceMax = 20f;
                weapon.reloadDuration = 1.2f;
                weapon.damagePerShot = 10f;
                weapon.fireRate = 3f;
                weapon.range = 11f;
                weapon.shotRadius = 0.35f;
                weapon.hitMask = EnemyMask;
                weapon.muzzleIds = new[] { "Staff" };
                weapon.muzzleSideOffsets = new[] { 0f };
                weapon.fireAnimIds = new[] { "Rune_Cast" };
                weapon.tracerWidth = 0.18f;
                weapon.projectileSpeed = 26f;
                weapon.fxColor = new Color(0.7f, 0.4f, 1f);
                EditorUtility.SetDirty(weapon);
            }

            SkillData burst = Skill<RuneBurstSkillData>("RuneMage/RuneMage_RuneBurst", null, "RuneBurst",
                "룬 방출", "모은 룬을 전부 커서 지점으로 날려 각각 폭발시킨다. 룬이 많을수록 강하다(없어도 1발).",
                cooldown: 6f, duration: 0.2f, allowsMove: true, allowsFire: false, moveSpeed: 0.8f);

            SkillData blink = Skill<BlinkSkillData>("RuneMage/RuneMage_Blink", null, "RuneBlink",
                "블링크", "커서 방향으로 순간이동. 출발 자리에 1초 뒤 폭발하는 룬 함정을 남긴다.",
                cooldown: 5f, duration: 0.1f, allowsMove: false, allowsFire: true);

            SkillData collapse = Skill<ArcaneCollapseSkillData>("RuneMage/RuneMage_ArcaneCollapse", null, "RuneCollapse",
                "마력 붕괴", "지점에 마법진을 그리고 그곳으로 순간이동. 적을 중심으로 끌어당기며 3번 폭발(마지막이 가장 강함).",
                cooldown: 45f, duration: 1.6f, allowsMove: false, allowsFire: false);

            PassiveData passive = Passive<RuneResonancePassiveData>("RuneMage/RuneMage_Resonance", "RunePassive",
                "룬 공명", "무기 적중마다 룬 1개(최대 5)가 주변을 돈다. 궤도 안의 적에게 0.5초마다 룬당 5 피해. 6초간 적중이 없으면 하나씩 사라진다.");

            WeaponKitData kit = Kit("RuneMageKit", weapon, blink, burst, collapse, passive);

            return Character("Char3_RuneMage", "룬 마검사", "원거리 견제 → 진입 · 다수 특화",
                "멀리서 마법탄으로 룬을 모으고, 블링크로 파고들어 주변을 도는 룬과 룬 방출로 여러 적을 쓸어버린다.",
                new Color(0.75f, 0.55f, 1f), kit, Array.Empty<CharacterData.StatBonus>());
        }

        // ---------------------------------------------------------------- scene

        private static void WireScene(CharacterData[] roster)
        {
            PlayerAgent player = Object.FindFirstObjectByType<PlayerAgent>();
            if (player == null)
            {
                Debug.LogError("[CharacterRoster] PlayerAgent가 씬에 없습니다.");
                return;
            }

            if (!player.TryGetComponent(out AttackSpeedAnimatorDriver driver))
                driver = player.gameObject.AddComponent<AttackSpeedAnimatorDriver>();
            SetReference(driver, "player", player);

            CharacterSwitcher switcher = Object.FindFirstObjectByType<CharacterSwitcher>();
            if (switcher == null)
                switcher = new GameObject("CharacterSwitcher").AddComponent<CharacterSwitcher>();

            var so = new SerializedObject(switcher);
            so.FindProperty("player").objectReferenceValue = player;
            SerializedProperty list = so.FindProperty("roster");
            list.arraySize = roster.Length;
            for (int i = 0; i < roster.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = roster[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            FunFeelSetup.ApplyToScene(player);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        }

        // ---------------------------------------------------------------- helpers

        private static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            created = asset == null;
            if (created)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        private static SkillData Skill<T>(string path, ElementType? element, string icon, string displayName, string description,
            float cooldown, float duration, bool allowsMove, bool allowsFire, float moveSpeed = 1f, int charges = 1,
            Action<SerializedObject> extra = null) where T : SkillData
        {
            var skill = LoadOrCreate<T>($"{SkillRoot}/{path}.asset", out bool created);
            if (!created)
                return skill;

            var so = new SerializedObject(skill);
            so.FindProperty("cooldown").floatValue = cooldown;
            so.FindProperty("charges").intValue = charges;
            so.FindProperty("duration").floatValue = duration;
            so.FindProperty("allowsMove").boolValue = allowsMove;
            so.FindProperty("allowsFire").boolValue = allowsFire;
            so.FindProperty("moveSpeedMultiplier").floatValue = moveSpeed;
            so.FindProperty("grantsElement").boolValue = element.HasValue;
            so.FindProperty("element").intValue = (int)element.GetValueOrDefault();
            so.FindProperty("icon").objectReferenceValue = IconPainter.Load(IconRoot, icon);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("description").stringValue = description;
            extra?.Invoke(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            return skill;
        }

        private static PassiveData Passive<T>(string path, string icon, string displayName, string description) where T : PassiveData
        {
            var passive = LoadOrCreate<T>($"{SkillRoot}/{path}.asset", out bool created);
            if (!created)
                return passive;

            var so = new SerializedObject(passive);
            so.FindProperty("icon").objectReferenceValue = IconPainter.Load(IconRoot, icon);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("description").stringValue = description;
            so.ApplyModifiedPropertiesWithoutUndo();
            return passive;
        }

        private static WeaponKitData Kit(string name, WeaponData weapon, SkillData dash, SkillData weaponSkill, SkillData ultimate,
            PassiveData passive)
        {
            var kit = LoadOrCreate<WeaponKitData>($"{KitRoot}/{name}.asset", out bool created);
            if (created)
            {
                kit.weapon = weapon;
                kit.dash = dash;
                kit.weaponSkill = weaponSkill;
                kit.ultimate = ultimate;
                kit.passive = passive;
                EditorUtility.SetDirty(kit);
            }

            return kit;
        }

        private static CharacterData Character(string name, string displayName, string role, string description, Color theme,
            WeaponKitData kit, CharacterData.StatBonus[] bonuses)
        {
            var character = LoadOrCreate<CharacterData>($"{CharacterRoot}/{name}.asset", out bool created);
            if (!created)
                return character;

            var so = new SerializedObject(character);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("role").stringValue = role;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("themeColor").colorValue = theme;
            so.FindProperty("kit").objectReferenceValue = kit;

            SerializedProperty list = so.FindProperty("statBonuses");
            list.arraySize = bonuses.Length;
            for (int i = 0; i < bonuses.Length; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("stat").intValue = (int)bonuses[i].stat;
                entry.FindPropertyRelative("type").intValue = (int)bonuses[i].type;
                entry.FindPropertyRelative("value").floatValue = bonuses[i].value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return character;
        }

        private static CharacterData.StatBonus Bonus(PlayerStatId stat, float percent) =>
            new() { stat = stat, type = StatModifierType.PercentAdd, value = percent };

        private static void SetReference(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

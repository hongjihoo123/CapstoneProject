using System.Collections.Generic;
using System.IO;
using Assets.Members.HJH._02.Scripts.Element;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.KYR._01.Scripts.Stats;
using UnityEditor;
using UnityEngine;
using Mod = Assets.Members.HJH._02.Scripts.Element.ComboStatBuff.Mod;

namespace HJH.EditorTools
{
    // 임시 콤보 목록 + 효과 에셋을 만든다 (다시 실행하면 같은 에셋을 덮어씀).
    //  - 효과: 05.Data/Combo/Buffs/ComboBuff_*.asset (ComboStatBuff)
    //  - 목록: 05.Data/Resources/HJH/ComboCatalog.asset
    // 숫자는 전부 임시값. 에셋을 직접 고쳐도 되고, 여기 표를 고친 뒤 다시 실행해도 됨
    public static class ComboCatalogMenu
    {
        private const string BuffFolder = "Assets/Members/HJH/05.Data/Combo/Buffs";
        private const string CatalogPath = "Assets/Members/HJH/05.Data/Resources/HJH/ComboCatalog.asset";

        private const ElementType F = ElementType.Fire, W = ElementType.Water, A = ElementType.Wind,
            L = ElementType.Electric, E = ElementType.Earth;
        private const StatModifierType Pct = StatModifierType.PercentAdd, Flat = StatModifierType.Flat;

        private sealed class Row
        {
            public string id, name, effect;
            public ElementType[] seq;
            public float duration;
            public bool alwaysOn;
            public Mod[] mods = new Mod[0];
            public float heal, shield;
        }


        private static Row Buff(string id, string name, string effect, float duration, ElementType[] seq, params Mod[] mods) => new()
        {
            id = id, name = name, effect = effect, duration = duration, seq = seq, mods = mods
        };

        private static Row Instant(string id, string name, string effect, ElementType[] seq, float heal, float shield) => new()
        {
            id = id, name = name, effect = effect, duration = 1f, seq = seq, heal = heal, shield = shield
        };

        private static List<Row> Table() => new()
        {

            // 고르는 콤보. 앞의 4개가 처음 기본 선택
            Buff("FireElectric", "점화", "공격력 +10%", 10f, new[] { F, L }, new Mod(PlayerStatId.Damage, Pct, 0.10f)),
            Instant("WaterWater", "치유의 물결", "체력 8% 회복", new[] { W, W }, 0.08f, 0f),
            Instant("EarthWater", "진흙 갑옷", "쉴드 20", new[] { E, W }, 0f, 20f),
            Buff("ElectricElectric", "과전류", "공격 속도 +20%", 15f, new[] { L, L }, new Mod(PlayerStatId.AttackSpeed, Pct, 0.20f)),

            Buff("FireFire", "불씨", "공격력 +15%", 8f, new[] { F, F }, new Mod(PlayerStatId.Damage, Pct, 0.15f)),
            Buff("FireFireWater", "증기 폭발", "공격력 +25%", 6f, new[] { F, F, W }, new Mod(PlayerStatId.Damage, Pct, 0.25f)),
            Buff("FireElectricFire", "연쇄 폭발", "공격력 +20% · 이속 +10%", 8f, new[] { F, L, F },
                new Mod(PlayerStatId.Damage, Pct, 0.20f), new Mod(PlayerStatId.MoveSpeed, Pct, 0.10f)),
            Buff("WindWind", "순풍", "이동 속도 +20%", 10f, new[] { A, A }, new Mod(PlayerStatId.MoveSpeed, Pct, 0.20f)),
            Buff("WindElectric", "질풍뇌우", "공속 +12% · 이속 +12%", 10f, new[] { A, L },
                new Mod(PlayerStatId.AttackSpeed, Pct, 0.12f), new Mod(PlayerStatId.MoveSpeed, Pct, 0.12f)),
            Buff("WindFire", "들불", "마법 공격력 +15%", 10f, new[] { A, F }, new Mod(PlayerStatId.MagicDamage, Pct, 0.15f)),
            Buff("WaterWind", "물안개", "대시 속도 +25%", 10f, new[] { W, A }, new Mod(PlayerStatId.DashSpeed, Pct, 0.25f)),
            Buff("EarthEarth", "바위 피부", "받는 피해 -15%", 10f, new[] { E, E }, new Mod(PlayerStatId.IncomingDamageTaken, Pct, -0.15f)),
            Buff("EarthFire", "용암 갑주", "물방 +20 · 마방 +20", 12f, new[] { E, F },
                new Mod(PlayerStatId.ArmorDefense, Flat, 20f), new Mod(PlayerStatId.MagicDefense, Flat, 20f)),
            Buff("ElectricFireElectric", "과부하", "공격 속도 +35%", 8f, new[] { L, F, L }, new Mod(PlayerStatId.AttackSpeed, Pct, 0.35f)),
            Instant("WaterEarthWater", "대지의 샘", "쉴드 40 · 회복 5%", new[] { W, E, W }, 0.05f, 40f),
            Buff("ElectricWater", "감전 지대", "마법 관통 +10", 10f, new[] { L, W }, new Mod(PlayerStatId.MagicPenetration, Flat, 10f)),
        };

        [MenuItem("HJH/콤보/임시 콤보 목록 만들기")]
        public static void Build()
        {
            Directory.CreateDirectory(BuffFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath)!);
            AssetDatabase.Refresh();

            var entries = new List<ComboCatalog.Entry>();
            foreach (Row row in Table())
            {
                string path = $"{BuffFolder}/ComboBuff_{row.id}.asset";
                var buff = AssetDatabase.LoadAssetAtPath<ComboStatBuff>(path);
                if (buff == null)
                {
                    buff = ScriptableObject.CreateInstance<ComboStatBuff>();
                    AssetDatabase.CreateAsset(buff, path);
                }

                buff.Configure(row.mods, row.heal, row.shield);
                var so = new SerializedObject(buff);
                so.FindProperty("displayName").stringValue = row.name;
                so.FindProperty("duration").floatValue = row.duration;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(buff);

                entries.Add(new ComboCatalog.Entry
                {
                    name = row.name,
                    sequence = row.seq,
                    effect = row.effect,
                    buff = buff,
                    duration = row.duration,
                    alwaysOn = row.alwaysOn
                });
            }

            var catalog = AssetDatabase.LoadAssetAtPath<ComboCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ComboCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.SetEntries(entries);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Debug.Log($"[ComboCatalog] 콤보 {entries.Count}개 저장: {CatalogPath}");
        }
    }
}

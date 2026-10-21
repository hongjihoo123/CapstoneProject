using UnityEngine;

namespace Members.JJH._02_Scripts.Systems.UtilSystems.FolderColor.Editor
{
    /// <summary>
    /// 우클릭 메뉴(Assets/Folder Color)에 노출되는 프리셋 색상 팔레트.
    /// 서로 구분이 쉬우면서도 장시간 봐도 눈이 편하도록 채도를 적당히 낮춘 톤으로 구성했다.
    /// 메뉴 라벨(<see cref="FolderColorMenu"/>)과 이 배열의 순서/이름이 서로 대응한다.
    /// </summary>
    public static class FolderColorPalette
    {
        public readonly struct Preset
        {
            public readonly string Name;
            public readonly Color Color;

            public Preset(string name, Color color)
            {
                Name = name;
                Color = color;
            }
        }

        public static readonly Preset[] Presets =
        {
            new Preset("Red",    Hex(0xE0, 0x6C, 0x75)),
            new Preset("Orange", Hex(0xE0, 0x93, 0x5C)),
            new Preset("Yellow", Hex(0xE5, 0xC0, 0x7B)),
            new Preset("Green",  Hex(0x98, 0xC3, 0x79)),
            new Preset("Mint",   Hex(0x7F, 0xC8, 0xA9)),
            new Preset("Teal",   Hex(0x56, 0xB6, 0xC2)),
            new Preset("Blue",   Hex(0x61, 0xAF, 0xEF)),
            new Preset("Indigo", Hex(0x7C, 0x83, 0xE8)),
            new Preset("Purple", Hex(0xC6, 0x78, 0xDD)),
            new Preset("Pink",   Hex(0xE0, 0x6C, 0x9A)),
        };

        private static Color Hex(int r, int g, int b)
            => new Color(r / 255f, g / 255f, b / 255f, 1f);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.EditorTools
{
    // Temporary skill icons in the same style as the existing ones (dark tile, white glyph).
    // Only missing files are painted, so hand-made replacements are never overwritten.
    public static class IconPainter
    {
        private const int Size = 128;
        private static readonly Color Background = new(0.23f, 0.23f, 0.23f, 1f);

        private static readonly Dictionary<string, Action<Canvas>> Glyphs = new()
        {
            ["GunnerBeam"] = c =>
            {
                c.Disc(new Vector2(28, 40), 12);
                c.Line(new Vector2(28, 40), new Vector2(108, 92), 10);
            },
            ["GunnerDash"] = c =>
            {
                c.Line(new Vector2(30, 36), new Vector2(56, 64), 9);
                c.Line(new Vector2(56, 64), new Vector2(30, 92), 9);
                c.Line(new Vector2(64, 36), new Vector2(90, 64), 9);
                c.Line(new Vector2(90, 64), new Vector2(64, 92), 9);
            },
            ["GunnerCannon"] = c =>
            {
                c.Disc(new Vector2(64, 64), 16);
                for (int i = 0; i < 8; i++)
                {
                    Vector2 dir = Rotate(Vector2.up, i * 45f);
                    c.Line(new Vector2(64, 64) + dir * 27, new Vector2(64, 64) + dir * 46, 6);
                }
            },
            ["GunnerPassive"] = c =>
            {
                c.Line(new Vector2(24, 64), new Vector2(84, 64), 8);
                c.Line(new Vector2(84, 64), new Vector2(66, 82), 8);
                c.Line(new Vector2(84, 64), new Vector2(66, 46), 8);
                c.Disc(new Vector2(102, 64), 8);
            },
            ["ChainPassive"] = c =>
            {
                c.Disc(new Vector2(64, 50), 22);
                c.Line(new Vector2(64, 104), new Vector2(46, 60), 12);
                c.Line(new Vector2(64, 104), new Vector2(82, 60), 12);
            },
            ["RuneBurst"] = c =>
            {
                for (int i = 0; i < 5; i++)
                {
                    Vector2 dir = Rotate(new Vector2(1, 1).normalized, (i - 2) * 12f);
                    c.Disc(new Vector2(30, 30) + dir * 72, 8);
                    c.Line(new Vector2(30, 30) + dir * 30, new Vector2(30, 30) + dir * 58, 3);
                }
            },
            ["RuneBlink"] = c =>
            {
                c.Ring(new Vector2(36, 44), 14, 5);
                c.Disc(new Vector2(92, 84), 15);
                c.Disc(new Vector2(55, 57), 3);
                c.Disc(new Vector2(66, 65), 3);
                c.Disc(new Vector2(77, 73), 3);
            },
            ["RuneCollapse"] = c =>
            {
                c.Ring(new Vector2(64, 64), 46, 5);
                c.Ring(new Vector2(64, 64), 30, 5);
                c.Disc(new Vector2(64, 64), 10);
            },
            ["RunePassive"] = c =>
            {
                c.Ring(new Vector2(64, 64), 34, 3);
                for (int i = 0; i < 5; i++)
                    c.Disc(new Vector2(64, 64) + Rotate(Vector2.up, i * 72f) * 34, 9);
            },
        };

        public static void EnsureIcons(string folder)
        {
            Directory.CreateDirectory(folder);

            foreach (KeyValuePair<string, Action<Canvas>> glyph in Glyphs)
            {
                string path = $"{folder}/{glyph.Key}.png";
                if (File.Exists(path))
                    continue;

                var canvas = new Canvas();
                glyph.Value(canvas);
                File.WriteAllBytes(path, canvas.EncodeToPng());
                AssetDatabase.ImportAsset(path);

                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.SaveAndReimport();
                }
            }
        }

        public static Sprite Load(string folder, string icon) => AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{icon}.png");

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(v.x * Mathf.Cos(r) - v.y * Mathf.Sin(r), v.x * Mathf.Sin(r) + v.y * Mathf.Cos(r));
        }

        // Anti-aliased white shapes from signed distances; coordinates are pixels, origin bottom-left.
        private sealed class Canvas
        {
            private readonly Color[] _pixels = new Color[Size * Size];

            public Canvas()
            {
                for (int i = 0; i < _pixels.Length; i++)
                    _pixels[i] = Background;
            }

            public void Disc(Vector2 center, float radius) => Paint(p => Vector2.Distance(p, center) - radius);

            public void Ring(Vector2 center, float radius, float width) =>
                Paint(p => Mathf.Abs(Vector2.Distance(p, center) - radius) - width * 0.5f);

            public void Line(Vector2 a, Vector2 b, float width) => Paint(p => SegmentDistance(p, a, b) - width * 0.5f);

            public byte[] EncodeToPng()
            {
                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                texture.SetPixels(_pixels);
                texture.Apply();
                byte[] png = texture.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(texture);
                return png;
            }

            private void Paint(Func<Vector2, float> signedDistance)
            {
                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float coverage = Mathf.Clamp01(0.5f - signedDistance(new Vector2(x + 0.5f, y + 0.5f)));
                    if (coverage <= 0f)
                        continue;

                    int index = y * Size + x;
                    _pixels[index] = Color.Lerp(_pixels[index], Color.white, coverage);
                }
            }

            private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
            {
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                return Vector2.Distance(p, a + ab * t);
            }
        }
    }
}

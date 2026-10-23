using System.Collections.Generic;
using UnityEngine;

namespace MineLoot
{
    // Original placeholder art, generated once and reused. No external Minecraft assets.
    public sealed class PixelArt
    {
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private readonly List<Texture2D> textures = new List<Texture2D>();

        public Sprite Get(string name)
        {
            if (sprites.TryGetValue(name, out var cached)) return cached;
            bool cave = name.StartsWith("cave");
            int width = cave ? 256 : name == "glow" ? 64 : 32;
            int height = cave ? 384 : width;
            var t = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = name };
            t.SetPixels(new Color[width * height]);
            void Rect(int x, int y, int w, int h, string color)
            {
                ColorUtility.TryParseHtmlString(color, out var c);
                for (int a = x; a < x + w; a++)
                    for (int b = y; b < y + h; b++)
                        if (a >= 0 && b >= 0 && a < width && b < height) t.SetPixel(a, height - 1 - b, c);
            }
            if (cave)
            {
                string[] colors = name == "cave1" ? new[] { "#112332", "#193b4b", "#24545e", "#5be1da" } : name == "cave2" ? new[] { "#241a29", "#382439", "#55374a", "#ffa25d" } : new[] { "#172a2b", "#203d3b", "#32564a", "#a7d885" };
                Rect(0, 0, width, height, colors[0]);
                var rng = new System.Random(381);
                for (int i = 0; i < 210; i++)
                {
                    int x = rng.Next(width), y = rng.Next(height);
                    Rect(x, y, rng.Next(8, 31), rng.Next(6, 21), i % 3 == 0 ? colors[2] : colors[1]);
                }
                Rect(48, 58, 160, 230, colors[0]);
                for (int i = 0; i < 6; i++)
                {
                    Rect(53 + i * 26, 0, 14, 15 + i * 7, colors[0]);
                    Rect(13 + i * 43, 310 + (i % 2) * 14, 17, 74, colors[2]);
                    Rect(18 + i * 43, 305 + (i % 2) * 14, 5, 12, colors[3]);
                }
                // Receding timber supports and rails give the portrait scene depth.
                Rect(26, 34, 12, 269, "#322f2c"); Rect(218, 34, 12, 269, "#322f2c");
                Rect(24, 30, 208, 13, "#7a6745"); Rect(29, 43, 5, 258, "#7a6745"); Rect(219, 43, 5, 258, "#574a37");
                Rect(55, 66, 7, 200, "#4d4936"); Rect(194, 66, 7, 200, "#4d4936"); Rect(55, 63, 146, 8, "#675a3c");
                for (int i = 0; i < 7; i++) Rect(52 - i * 5, 287 + i * 13, 152 + i * 10, 4, "#534e3c");
                for (int y = 280; y < 384; y++) { int spread = (y - 280) / 3; Rect(89 - spread, y, 3, 1, "#798d85"); Rect(167 + spread, y, 3, 1, "#798d85"); }
                foreach (int x in new[] { 37, 207 })
                {
                    Rect(x, 92, 11, 17, "#332825"); Rect(x + 2, 93, 7, 13, "#e9aa50"); Rect(x + 4, 94, 3, 10, "#fff0aa");
                }
            }
            else if (name == "glow")
            {
                t.filterMode = FilterMode.Bilinear;
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    float a = Mathf.Pow(Mathf.Clamp01(1 - Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32), 2);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            }
            else if (name == "frame")
            {
                Rect(2, 0, 28, 32, "#132b35"); Rect(0, 2, 32, 28, "#132b35");
                Rect(2, 2, 28, 2, "#6b8379"); Rect(2, 4, 2, 24, "#3f5a58");
                Rect(4, 4, 24, 24, "#203742"); Rect(4, 28, 24, 2, "#0c202b");
                foreach (int x in new[] { 3, 26 }) foreach (int y in new[] { 3, 26 }) Rect(x, y, 3, 3, "#d3b46d");
            }
            else if (name.StartsWith("crack"))
            {
                int count = name == "crack2" ? 19 : 11;
                for (int i = 0; i < count; i++)
                {
                    Rect(14 + (i % 4), 3 + i, 2, 2, "#13232edb");
                    if (i > 6) Rect(16 - (i - 6), 7 + i / 2, 2, 2, "#13232edb");
                }
            }
            else if (name.StartsWith("pick"))
            {
                var head = name == "pick4" ? "#63e4dc" : name == "pick2" ? "#d3e5ed" : "#d7a26a";
                for (int k = 0; k < 19; k++)
                {
                    Rect(25 - k, 6 + k, 4, 5, "#342e34");
                    Rect(25 - k, 6 + k, 2, 3, "#bd864d");
                }
                Rect(4, 2, 18, 5, "#213444"); Rect(10, 6, 17, 5, "#213444");
                Rect(22, 10, 7, 9, "#213444"); Rect(26, 17, 4, 5, "#213444");
                Rect(4, 2, 16, 3, head); Rect(10, 5, 15, 4, head);
                Rect(22, 9, 5, 7, head); Rect(26, 16, 2, 5, head);
                Rect(7, 2, 12, 1, "#f1f7e0");
            }
            else if (name == "chest" || name == "chest-open")
            {
                Rect(2, 11, 28, 19, "#352d2d"); Rect(4, 13, 24, 15, "#b47636");
                Rect(4, 12, 24, 5, "#dfa84c"); Rect(2, 20, 28, 3, "#68432e");
                Rect(6, 13, 2, 15, "#f0c06c"); Rect(24, 13, 2, 15, "#83552b");
                Rect(14, 18, 5, 8, "#ffe6a1"); Rect(16, 20, 1, 3, "#845125");
                if (name == "chest-open")
                {
                    Rect(4, 3, 24, 7, "#dfa84c"); Rect(6, 5, 20, 4, "#725031");
                    Rect(6, 11, 20, 6, "#ffe2a0"); Rect(10, 9, 4, 4, "#fff2be");
                }
            }
            else if (name == "coin")
            {
                Rect(7, 3, 18, 26, "#be863c"); Rect(3, 7, 26, 18, "#be863c");
                Rect(7, 6, 18, 20, "#ffd66e"); Rect(6, 9, 20, 14, "#ffd66e");
                Rect(9, 7, 3, 15, "#fff0ad"); Rect(16, 10, 4, 12, "#dfa643");
            }
            else if (name == "ticket")
            {
                Rect(3, 7, 26, 18, "#8662bc"); Rect(5, 9, 22, 14, "#c5a3f5");
                Rect(9, 11, 2, 10, "#8662bc"); Rect(18, 12, 6, 2, "#eee1ff");
                Rect(18, 16, 6, 2, "#eee1ff");
            }
            else
            {
                string body = name == "grass" ? "#93623e" : name == "gold" ? "#77735b" : name == "diamond" ? "#465d6a" : name == "ember" ? "#453c4a" : "#727f88";
                Rect(0, 0, 32, 32, body);
                var noise = new System.Random(91);
                for (int i = 0; i < 36; i++)
                    Rect(noise.Next(32), noise.Next(32), noise.Next(2, 7), noise.Next(2, 5), i % 2 == 0 ? "#ffffff12" : "#00000013");
                // Blend noise manually: Texture.SetPixel does not alpha-composite.
                ColorUtility.TryParseHtmlString(body, out var baseColor);
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                {
                    Color p = t.GetPixel(x, y);
                    if (p.a < .99f) t.SetPixel(x, y, Color.Lerp(baseColor, new Color(p.r, p.g, p.b, 1), p.a));
                }
                Rect(0, 0, 32, 2, "#a0a5a0"); Rect(30, 0, 2, 32, "#44525b"); Rect(0, 30, 32, 2, "#3e4b52");
                if (name == "grass") { Rect(0, 0, 32, 6, "#8ebc55"); Rect(0, 6, 32, 2, "#4e7e3d"); }
                if (name == "iron" || name == "gold" || name == "diamond" || name == "ember")
                {
                    string ore = name == "iron" ? "#d1b49d" : name == "gold" ? "#f3cf63" : name == "ember" ? "#ffa15e" : "#5ee6df";
                    Rect(6, 8, 6, 5, ore); Rect(21, 14, 6, 5, ore); Rect(12, 23, 5, 4, ore); Rect(23, 4, 4, 3, ore);
                }
            }
            t.Apply();
            var sprite = Sprite.Create(t, new Rect(0, 0, width, height), new Vector2(.5f, .5f), name == "frame" ? 100 : 32, 0, SpriteMeshType.FullRect, name == "frame" ? new Vector4(6, 6, 6, 6) : Vector4.zero);
            sprite.name = name;
            textures.Add(t); sprites.Add(name, sprite);
            return sprite;
        }

        public void Dispose()
        {
            foreach (var s in sprites.Values) Object.Destroy(s);
            foreach (var t in textures) Object.Destroy(t);
            sprites.Clear(); textures.Clear();
        }
    }
}

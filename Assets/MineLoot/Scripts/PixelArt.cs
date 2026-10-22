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
            var t = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = name };
            t.SetPixels(new Color[1024]);
            void Rect(int x, int y, int w, int h, string color)
            {
                ColorUtility.TryParseHtmlString(color, out var c);
                for (int a = x; a < x + w; a++)
                    for (int b = y; b < y + h; b++)
                        if (a >= 0 && b >= 0 && a < 32 && b < 32) t.SetPixel(a, 31 - b, c);
            }
            if (name.StartsWith("pick"))
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
                string body = name == "grass" ? "#93623e" : name == "gold" ? "#77735b" : name == "diamond" ? "#465d6a" : "#727f88";
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
                if (name == "iron" || name == "gold" || name == "diamond")
                {
                    string ore = name == "iron" ? "#d1b49d" : name == "gold" ? "#f3cf63" : "#5ee6df";
                    Rect(6, 8, 6, 5, ore); Rect(21, 14, 6, 5, ore); Rect(12, 23, 5, 4, ore); Rect(23, 4, 4, 3, ore);
                }
            }
            t.Apply();
            var sprite = Sprite.Create(t, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 32);
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

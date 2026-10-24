using System.Collections.Generic;
using UnityEngine;

namespace MineLoot
{
    // Original code-authored 16/32px art. Point filtering and hard edges throughout.
    public sealed class PixelArt
    {
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private readonly List<Texture2D> textures = new List<Texture2D>();

        public Texture2D Texture(string name) => Get(name).texture;
        public Sprite Get(string name)
        {
            if (sprites.TryGetValue(name, out var cached)) return cached;
            bool tile = name.StartsWith("tile-") || name.StartsWith("tex-");
            bool ui = name.StartsWith("ui-");
            string kind = tile ? name.Substring(name.IndexOf('-') + 1) : name;
            int size = tile ? 16 : ui ? 12 : 32;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            t.SetPixels(new Color[size * size]);
            void R(int x, int y, int w, int h, string c)
            {
                ColorUtility.TryParseHtmlString(c, out var color);
                for (int a = x; a < x + w; a++) for (int b = y; b < y + h; b++)
                    if (a >= 0 && b >= 0 && a < size && b < size) t.SetPixel(a, size - b - 1, color);
            }
            if (ui)
            {
                // A neutral inventory bevel, tinted by the UI; four corners remain square.
                R(0, 0, 12, 12, "#25282aff"); R(1, 1, 10, 10, "#b7bbc0");
                R(1, 1, 10, 1, "#f5f8fb"); R(1, 1, 1, 10, "#f5f8fb");
                R(2, 10, 9, 1, "#626870"); R(10, 2, 1, 9, "#626870");
                if (name == "ui-timber" || name == "ui-brass")
                {
                    bool brass = name == "ui-brass";
                    R(0, 0, 12, 12, "#231f1b"); R(1, 1, 10, 10, brass ? "#ba8d45" : "#775133");
                    R(1, 1, 10, 1, brass ? "#f6d98c" : "#c59b66"); R(1, 2, 1, 8, brass ? "#dfbb69" : "#ab7c4c");
                    R(2, 10, 9, 1, brass ? "#77532c" : "#422e24"); R(10, 2, 1, 9, brass ? "#77532c" : "#422e24");
                    R(3, 3, 6, 6, brass ? "#d2ad63" : "#493c30");
                    foreach (int x in new[] { 1, 9 }) foreach (int y in new[] { 1, 9 })
                    { R(x, y, 2, 2, "#362b21"); R(x, y, 1, 1, "#e4c788"); }
                }
            }
            else if (tile)
            {
                string body = kind == "slate" ? "#485158" : kind == "beam" ? "#765137" : kind == "metal-dark" ? "#66717a" : kind == "metal-gold" ? "#edc36b" : kind == "grass-top" || kind == "leaves" ? "#67a344" : kind == "grass" || kind == "dirt" ? "#976646" : kind == "planks" || kind == "wood" ? "#b1834e" : kind == "water" ? "#428bce" : kind == "sand" ? "#d8c994" : kind == "ember" ? "#555563" : "#92969a";
                ColorUtility.TryParseHtmlString(body, out var baseColor);
                int seed = 313; foreach (char letter in kind) seed = unchecked(seed * 31 + letter);
                var rng = new System.Random(seed);
                var shades = new float[8, 8];
                for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) shades[x, y] = .86f + (float)rng.NextDouble() * .25f;
                for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                {
                    float shade = shades[x / 2, y / 2] + (float)rng.NextDouble() * .04f;
                    if (kind == "wood" || kind == "beam") shade = .75f + (x % 4) * .1f + (float)rng.NextDouble() * .12f;
                    if (kind == "planks" && y % 4 == 0) shade = .64f;
                    Color c = baseColor * shade; c.a = 1; t.SetPixel(x, y, c);
                }
                if (kind == "grass")
                {
                    R(0, 0, 16, 3, "#80b64b"); R(0, 3, 3, 2, "#70a247"); R(6, 3, 4, 2, "#70a247"); R(13, 3, 3, 1, "#70a247");
                }
                if (kind == "coal" || kind == "iron" || kind == "gold" || kind == "diamond" || kind == "emerald" || kind == "ember")
                {
                    string ore = kind == "coal" ? "#313944" : kind == "iron" ? "#dbb69a" : kind == "gold" ? "#f4d25b" : kind == "emerald" ? "#55d67b" : kind == "ember" ? "#f19a58" : "#5be1db";
                    string dark = kind == "coal" ? "#22272f" : kind == "diamond" ? "#298e98" : kind == "gold" ? "#b28c32" : kind == "emerald" ? "#27824e" : "#79695c";
                    string shine = kind == "coal" ? "#505964" : kind == "iron" ? "#f1d1b7" : kind == "gold" ? "#fff0a6" : kind == "emerald" ? "#a4f7ae" : "#c1fff0";
                    for (int i = 0; i < 5; i++)
                    {
                        int x = 1 + (i * 7 + kind.Length) % 11, y = 1 + i * 3 % 12;
                        R(x, y, 4, 3, dark); R(x, y, 3, 2, ore);
                        if (kind == "emerald") R(x + 1, y - 1, 2, 4, ore);
                        R(x, y, 2, 1, shine);
                    }
                }
                if (kind == "stone" || kind == "slate")
                    foreach (var a in new[] { new Vector2Int(2, 5), new Vector2Int(9, 10), new Vector2Int(4, 14) }) R(a.x, a.y, 4, 1, "#777e83");
                if (kind == "bedrock")
                {
                    R(0, 0, 16, 16, "#5f646a");
                    for (int y = 0; y < 16; y += 3) for (int x = 0; x < 16; x += 4)
                    { int shift = (x + y * 3) % 3; R(x, y + shift, 4, 2, "#242a31"); R(x + 1, y + shift, 2, 1, "#a0a6ab"); }
                }
                if (kind == "obsidian")
                {
                    R(0, 0, 16, 16, "#211c30");
                    foreach (var p in new[] { new Vector2Int(1, 2), new Vector2Int(8, 1), new Vector2Int(5, 8), new Vector2Int(11, 11), new Vector2Int(0, 13) })
                    { R(p.x, p.y, 5, 3, "#403052"); R(p.x, p.y, 3, 1, "#73538e"); R(p.x + 1, p.y + 3, 4, 2, "#30233f"); }
                }
            }
            else if (name == "pick-broken")
            {
                for (int k = 11; k < 19; k++) { R(25-k, 6+k, 4, 5, "#423025"); R(25-k, 6+k, 2, 3, "#b77b40"); }
                R(12, 15, 3, 3, "#e4b66f"); R(15, 17, 2, 2, "#e4b66f");
            }
            else if (name.StartsWith("pick"))
            {
                string metal = name.EndsWith("4") ? "#53e7dc" : name == "pick-gold" ? "#f4cd57" : name.EndsWith("2") ? "#e1e5e8" : name == "pick-stone" ? "#a2abb2" : "#bf985b";
                if (!name.StartsWith("pick-head")) for (int k = 0; k < 19; k++) { R(25-k, 6+k, 4, 5, "#423025"); R(25-k, 6+k, 2, 3, "#b77b40"); }
                R(4, 2, 18, 5, "#263c40"); R(10, 6, 17, 5, "#263c40"); R(22, 10, 7, 9, "#263c40"); R(26, 17, 4, 5, "#263c40");
                R(4, 2, 16, 3, metal); R(10, 5, 15, 4, metal); R(22, 9, 5, 7, metal); R(26, 16, 2, 5, metal); R(7, 2, 12, 1, "#edfff5");
            }
            else if (name == "chest" || name == "chest-open")
            {
                R(2, 10, 28, 20, "#4d3021"); R(4, 12, 24, 16, "#b67d39"); R(4, 11, 24, 6, "#dcaa56");
                R(3, 20, 26, 3, "#674125"); R(6, 13, 2, 15, "#dca74d"); R(24, 13, 2, 15, "#805023");
                R(14, 18, 5, 8, "#ddd7b8"); R(16, 20, 1, 3, "#6b633d");
                if (name == "chest-open") { R(4, 2, 24, 7, "#dba34b"); R(6, 4, 20, 4, "#714b2e"); R(6, 11, 20, 5, "#ffe091"); }
            }
            else if (name == "coin")
            {
                R(7, 3, 18, 26, "#9b6a24"); R(3, 7, 26, 18, "#9b6a24"); R(7, 6, 18, 20, "#f4c74e"); R(6, 9, 20, 14, "#f4c74e"); R(9, 7, 3, 15, "#ffe59d"); R(16, 10, 4, 12, "#d5a238");
            }
            else if (name == "ticket")
            {
                R(3, 7, 26, 18, "#655081"); R(5, 9, 22, 14, "#bd91dc"); R(9, 11, 2, 10, "#765592"); R(17, 12, 6, 2, "#f1dbff"); R(17, 17, 6, 2, "#f1dbff");
            }
            else if (name == "forge")
            {
                R(2, 10, 28, 5, "#4a545f"); R(6, 7, 22, 5, "#9aa7b4"); R(3, 9, 25, 2, "#bac7d0"); R(14, 15, 10, 9, "#647381"); R(11, 24, 16, 4, "#46525e");
            }
            else if (name.StartsWith("crack"))
            {
                int depth = name == "crack1" ? 1 : name == "crack3" ? 3 : 2;
                for (int i = 0; i < 7 + depth * 6; i++)
                {
                    R(13 + i % 4, 3 + i, 2, 2, "#222c32e0");
                    if (i > 6) R(16 - (i - 6), 7 + i / 2, 2, 2, "#222c32e0");
                    if (depth == 3 && i > 10) R(15 + (i - 10), 9 + i / 2, 2, 2, "#222c32e0");
                }
            }
            else if (name == "torch-glow")
            {
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                {
                    float distance = Mathf.Abs(x - 15.5f) / 16 + Mathf.Abs(y - 15.5f) / 16;
                    float alpha = distance < .35f ? .2f : distance < .65f ? .1f : distance < .95f ? .045f : 0;
                    t.SetPixel(x, y, new Color(1, .65f, .22f, alpha));
                }
            }
            else if (name == "shadow")
            {
                for(int y=0;y<32;y++) for(int x=0;x<32;x++) if (Mathf.Abs(x-15.5f)/16 + Mathf.Abs(y-15.5f)/8 < 1) t.SetPixel(x,y,new Color(.07f,.13f,.1f,.3f));
            }
            t.Apply();
            var sprite = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(.5f,.5f), 100, 0, SpriteMeshType.FullRect, ui ? new Vector4(3,3,3,3) : Vector4.zero);
            sprite.name = name; sprites.Add(name, sprite); textures.Add(t); return sprite;
        }
        public void Dispose()
        {
            foreach (var sprite in sprites.Values) Object.Destroy(sprite);
            foreach (var texture in textures) Object.Destroy(texture);
            sprites.Clear(); textures.Clear();
        }
    }
}

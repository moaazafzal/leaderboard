using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AquaGames.League
{
    // Look of the league screens. Every field has a sensible default; set only what you need.
    public class LeagueTheme
    {
        public TMP_FontAsset font;                    // null → TMP default font
        public Material fontMaterial;                 // optional (outline etc.)
        public Color dim = new Color(0, 0, 0, 0.72f);
        public Color panel = new Color(0.12f, 0.15f, 0.30f);
        public Color header = new Color(0.17f, 0.21f, 0.42f);
        public Color row = new Color(0.20f, 0.24f, 0.46f);
        public Color playerRow = new Color(0.24f, 0.55f, 0.30f);
        public Color text = Color.white;
        public Color textDim = new Color(0.72f, 0.77f, 0.95f);
        public Color promotion = new Color(0.35f, 0.85f, 0.35f);
        public Color demotion = new Color(1f, 0.38f, 0.38f);
        public Color[] medals = { new Color(1f, 0.8f, 0.15f), new Color(0.78f, 0.82f, 0.9f), new Color(0.85f, 0.52f, 0.25f) };
        public Color button = new Color(0.25f, 0.6f, 1f);
        public float scale = 1f;                      // multiply all sizes (reference layout is 1080 px wide)
        public Sprite panelSprite, rowSprite, buttonSprite;   // optional 9-sliced sprites; generated if null

        // ---- optional extras to match a game's UI kit (leave default to keep the built-in look) ----
        public Color panelBorder = new Color(0, 0, 0, 0);     // outline drawn behind the panel (alpha 0 = none)
        public float borderWidth = 12f;
        public Sprite ribbonSprite;                           // title banner behind the league name / result headline
        public Color ribbonText = Color.white;
        public Sprite closeSprite, closeIcon;                 // close button background + icon (generated circle + "X" if null)
        public Color closeColor = new Color(1f, 0.38f, 0.38f);
        public Color headerText = Color.white;                // league name when there is no ribbon
        public Color rowText = Color.white;                   // names on normal rows
        public Color rowTextDim = new Color(0.72f, 0.77f, 0.95f);   // XP / rank numbers on normal rows
        public Color playerRowText = Color.white;             // text on the player's row / summary card
        public Color timerColor = new Color(1f, 0.8f, 0.15f);
        public Color buttonText = Color.white;
        public Color lockedBadge = new Color(0.4f, 0.43f, 0.55f);
        public Material titleMaterial;                        // font material for titles and buttons (e.g. heavy outline)

        // ---------- generated sprites (used when none are supplied) ----------
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Rounded(int radius = 24)
        {
            string key = "r" + radius;
            if (cache.TryGetValue(key, out var s)) return s;
            int n = radius * 2 + 4;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Max(0, Mathf.Abs(x + 0.5f - n / 2f) - 2), dy = Mathf.Max(0, Mathf.Abs(y + 0.5f - n / 2f) - 2);
                float d = Mathf.Sqrt(dx * dx + dy * dy) - (radius - 0.5f);
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(0.5f - d)));
            }
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
            cache[key] = s;
            return s;
        }

        public static Sprite Circle() => Shape("circle", (u, v) => Mathf.Sqrt(u * u + v * v) - 0.95f);

        // League badge: shield with an inner bevel.
        public static Sprite Shield() => Shape("shield", (u, v) =>
        {
            float top = Mathf.Max(Mathf.Abs(u) - 0.8f, v - 0.85f);
            float bottom = Mathf.Abs(u) * 0.95f - (v + 0.95f) * 0.72f;
            return v > -0.05f ? top : Mathf.Max(bottom, Mathf.Abs(u) - 0.8f);
        });

        static Sprite Shape(string key, System.Func<float, float, float> sdf, int res = 128)
        {
            if (cache.TryGetValue(key, out var s)) return s;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float u = (x + 0.5f) / res * 2 - 1, v = (y + 0.5f) / res * 2 - 1;
                float d = sdf(u, v);
                float a = Mathf.Clamp01(0.5f - d * res * 0.5f);
                float shade = d < -0.18f ? 1f : 0.82f;    // rim
                tex.SetPixel(x, y, new Color(shade, shade, shade, a));
            }
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
            cache[key] = s;
            return s;
        }
    }
}

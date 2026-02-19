using UnityEngine;

namespace GeoCity3D.Visuals
{
    public static class TextureGenerator
    {
        // ── Indian building color palette ──
        // White-washed, cream, pastels typical of Pune/Indian cities
        private static readonly Color[] WallPalette = new Color[]
        {
            new Color(0.95f, 0.93f, 0.90f), // White-washed
            new Color(0.96f, 0.94f, 0.88f), // Warm white
            new Color(0.93f, 0.90f, 0.82f), // Cream
            new Color(0.90f, 0.88f, 0.80f), // Light beige
            new Color(0.88f, 0.85f, 0.78f), // Sand
            new Color(0.85f, 0.88f, 0.92f), // Light blue-gray
            new Color(0.92f, 0.86f, 0.82f), // Peach
            new Color(0.90f, 0.84f, 0.78f), // Light terracotta
            new Color(0.88f, 0.90f, 0.85f), // Pale green
            new Color(0.94f, 0.88f, 0.82f), // Light sand
            new Color(0.86f, 0.84f, 0.80f), // Warm gray
            new Color(0.92f, 0.92f, 0.92f), // Cool white
            new Color(0.90f, 0.86f, 0.90f), // Lavender hint
            new Color(0.95f, 0.90f, 0.80f), // Golden cream
            new Color(0.82f, 0.80f, 0.78f), // Concrete gray
            new Color(0.88f, 0.82f, 0.76f), // Dusty rose
        };

        // Flat concrete roofs typical in India
        private static readonly Color[] RoofPalette = new Color[]
        {
            new Color(0.58f, 0.56f, 0.52f), // Concrete
            new Color(0.55f, 0.53f, 0.50f), // Weathered concrete
            new Color(0.52f, 0.50f, 0.48f), // Dark concrete
            new Color(0.60f, 0.58f, 0.55f), // Light concrete
            new Color(0.48f, 0.46f, 0.44f), // Old concrete
            new Color(0.50f, 0.50f, 0.52f), // Blue-gray concrete
            new Color(0.56f, 0.54f, 0.50f), // Warm concrete
            new Color(0.62f, 0.60f, 0.56f), // New concrete
        };

        public static Color GetRandomWallColor() => WallPalette[Random.Range(0, WallPalette.Length)];
        public static Color GetRandomRoofColor() => RoofPalette[Random.Range(0, RoofPalette.Length)];

        // ═══════════════════════════════════════════════════
        // FACADE TEXTURE — plastered wall with windows
        // Indian buildings: smooth plaster, painted, with window openings
        // ═══════════════════════════════════════════════════

        public static Texture2D CreateFacadeTexture(int width = 512, int height = 512, Color? baseColor = null)
        {
            Color wallColor = baseColor ?? WallPalette[Random.Range(0, WallPalette.Length)];

            Texture2D tex = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];

            // Derived colors
            Color plasterDark = wallColor * 0.85f; plasterDark.a = 1f;
            Color stainColor = wallColor * 0.72f; stainColor.a = 1f;

            // Window colors
            Color frameDark = new Color(0.22f, 0.22f, 0.24f);
            Color frameLight = new Color(0.38f, 0.38f, 0.40f);
            Color glassDark = new Color(0.08f, 0.12f, 0.22f);
            Color glassMid = new Color(0.14f, 0.20f, 0.32f);
            Color glassReflect = new Color(0.30f, 0.40f, 0.55f);

            // Sill
            Color sillColor = wallColor * 0.68f; sillColor.a = 1f;

            // Window region (normalized)
            float winL = 0.24f, winR = 0.76f;
            float winB = 0.16f, winT = 0.72f;
            float sillB = 0.12f, sillT = winB + 0.01f;
            float sillL = winL - 0.04f, sillR = winR + 0.04f;
            float lintB = winT, lintT = 0.76f;

            float frameThick = 5f / width;
            float mullX = 0.50f, mullY = 0.46f;
            float mullThick = 2.5f / width;

            // Per-tile randomness
            bool isLit = Random.value > 0.55f;
            bool hasCurtain = Random.value > 0.4f;
            float curtainSide = Random.value > 0.5f ? 0f : 1f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width;
                    float v = (float)y / height;
                    Color c;

                    // ── Glass ──
                    if (u > winL + frameThick && u < winR - frameThick &&
                        v > winB + frameThick && v < winT - frameThick)
                    {
                        bool onMullX = Mathf.Abs(u - mullX) < mullThick;
                        bool onMullY = Mathf.Abs(v - mullY) < mullThick;

                        if (onMullX || onMullY)
                        {
                            c = frameLight;
                        }
                        else
                        {
                            float gv = (v - winB) / (winT - winB);
                            float gu = (u - winL) / (winR - winL);

                            c = Color.Lerp(glassDark, glassMid, gv * 0.7f);

                            // Sky reflection
                            float sweep = Mathf.Clamp01(1f - Mathf.Abs(gu - 0.3f + gv * 0.15f) * 1.8f);
                            c = Color.Lerp(c, glassReflect, sweep * 0.22f);

                            if (isLit)
                            {
                                float warm = Mathf.Clamp01(1f - gv * 0.7f) * 0.25f;
                                c = Color.Lerp(c, new Color(0.60f, 0.45f, 0.22f), warm);

                                if (hasCurtain)
                                {
                                    float curtainU = curtainSide == 0 ? gu : 1f - gu;
                                    if (curtainU < 0.35f)
                                    {
                                        c = Color.Lerp(c, new Color(0.70f, 0.65f, 0.55f), 0.3f);
                                    }
                                }
                            }

                            float gn = Random.value * 0.01f;
                            c += new Color(gn * 0.5f, gn * 0.7f, gn);
                        }
                    }
                    // ── Frame ──
                    else if (u >= winL && u <= winR && v >= winB && v <= winT)
                    {
                        c = frameDark;
                        float innerDist = Mathf.Min(
                            Mathf.Min(u - winL, winR - u),
                            Mathf.Min(v - winB, winT - v)) / frameThick;
                        c = Color.Lerp(frameDark, frameLight, Mathf.Clamp01(innerDist * 1.5f));
                        c.a = 1f;
                    }
                    // ── Sill ──
                    else if (u >= sillL && u <= sillR && v >= sillB && v <= sillT)
                    {
                        c = sillColor;
                        float sv = (v - sillB) / Mathf.Max(sillT - sillB, 0.001f);
                        if (sv > 0.7f) c *= 1.12f;
                        if (sv < 0.2f) c *= 0.85f;
                        c.a = 1f;
                    }
                    // ── Lintel ──
                    else if (u >= winL - 0.02f && u <= winR + 0.02f && v >= lintB && v <= lintT)
                    {
                        c = sillColor * 1.05f;
                        float lv = (v - lintB) / Mathf.Max(lintT - lintB, 0.001f);
                        if (lv < 0.2f) c *= 0.80f;
                        c.a = 1f;
                    }
                    // ── Plaster wall ──
                    else
                    {
                        c = wallColor;

                        // Subtle plaster texture (smooth, not brick!)
                        float n1 = Mathf.PerlinNoise(x * 0.08f + 100f, y * 0.08f + 100f);
                        float n2 = Mathf.PerlinNoise(x * 0.3f + 200f, y * 0.3f + 200f);
                        float variation = (n1 - 0.5f) * 0.04f + (n2 - 0.5f) * 0.015f;
                        c += new Color(variation, variation * 0.9f, variation * 0.7f);

                        // Per-pixel grain
                        float grain = (Random.value - 0.5f) * 0.018f;
                        c += new Color(grain, grain, grain);

                        // Weathering/rain stains below window
                        if (u >= winL && u <= winR && v < sillB && v > sillB - 0.07f)
                        {
                            float streak = 1f - (sillB - v) / 0.07f;
                            c = Color.Lerp(c, stainColor, streak * 0.3f);
                        }

                        // Ground-level darkening
                        if (v < 0.06f)
                            c *= Mathf.Lerp(0.85f, 1f, v / 0.06f);

                        c.a = 1f;
                    }

                    pixels[y * width + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        // ═══════════════════════════════════════════════
        // ROOF TEXTURE — flat concrete slab (Indian style)
        // ═══════════════════════════════════════════════

        public static Texture2D CreateRoofTexture(int width = 256, int height = 256, Color? baseColor = null)
        {
            Color roofColor = baseColor ?? RoofPalette[Random.Range(0, RoofPalette.Length)];

            Texture2D tex = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color c = roofColor;

                    // Concrete surface variation
                    float n1 = Mathf.PerlinNoise(x * 0.05f + 500f, y * 0.05f + 500f);
                    float n2 = Mathf.PerlinNoise(x * 0.15f + 600f, y * 0.15f + 600f);
                    float variation = (n1 - 0.5f) * 0.06f + (n2 - 0.5f) * 0.025f;
                    c += new Color(variation, variation, variation);

                    // Waterproofing patches
                    float patch = Mathf.PerlinNoise(x * 0.008f + 300f, y * 0.008f + 300f);
                    if (patch > 0.65f)
                    {
                        float pAmount = (patch - 0.65f) / 0.35f * 0.06f;
                        c -= new Color(pAmount, pAmount * 0.5f, 0);
                    }

                    // Grain
                    float grain = (Random.value - 0.5f) * 0.025f;
                    c += new Color(grain, grain, grain);

                    c.a = 1f;
                    pixels[y * width + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        // ═══════════════════════════════════════════════
        //  SHARED ASPHALT HELPERS
        // ═══════════════════════════════════════════════

        private static Color AsphaltPixel(Color baseAsphalt, int x, int y, float seed = 0f)
        {
            // Multi-octave Perlin for realistic aggregate texture
            float p1 = Mathf.PerlinNoise(x * 0.15f + seed, y * 0.15f + seed) * 0.035f - 0.0175f;
            float p2 = Mathf.PerlinNoise(x * 0.4f + seed + 100f, y * 0.4f + seed + 100f) * 0.018f;
            float p3 = Mathf.PerlinNoise(x * 1.2f + seed + 200f, y * 1.2f + seed + 200f) * 0.008f;
            float grain = (Random.value - 0.5f) * 0.03f;
            float total = grain + p1 + p2 + p3;
            Color c = baseAsphalt + new Color(total, total, total);

            // Subtle crack overlay
            float crack = Mathf.PerlinNoise(x * 0.8f + seed + 400f, y * 0.8f + seed + 400f);
            if (crack > 0.78f)
            {
                float crackStr = (crack - 0.78f) / 0.22f * 0.12f;
                c -= new Color(crackStr, crackStr, crackStr);
            }

            // Tire wear patches (slightly lighter)
            float wear = Mathf.PerlinNoise(x * 0.03f + seed + 300f, y * 0.02f + seed + 300f);
            if (wear > 0.6f)
            {
                float wearStr = (wear - 0.6f) / 0.4f * 0.04f;
                c += new Color(wearStr, wearStr, wearStr);
            }

            c.a = 1f;
            return c;
        }

        private static void DrawEdgeDarkening(Color[] pixels, int width, int height, float edgePct = 0.05f)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = (float)x / width;
                    if (nx < edgePct)
                        pixels[y * width + x] *= Mathf.Lerp(0.78f, 1f, nx / edgePct);
                    else if (nx > 1f - edgePct)
                        pixels[y * width + x] *= Mathf.Lerp(0.78f, 1f, (1f - nx) / edgePct);
                    pixels[y * width + x].a = 1f;
                }
            }
        }

        // ═══════════════════════════════════════════════
        //  MOTORWAY — dark asphalt, 3-lane, yellow median, white lane dividers
        // ═══════════════════════════════════════════════

        public static Texture2D CreateMotorwayTexture(int width = 512, int height = 512)
        {
            Texture2D tex = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];
            Color asphalt = new Color(0.12f, 0.12f, 0.13f); // Very dark
            Color lineWhite = new Color(0.88f, 0.88f, 0.82f);
            Color lineYellow = new Color(0.85f, 0.72f, 0.15f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = (float)x / width;
                    Color c = AsphaltPixel(asphalt, x, y, 50f);

                    // Left edge solid white line
                    if (nx > 0.04f && nx < 0.06f)
                        c = Color.Lerp(c, lineWhite, 0.85f);

                    // Right edge solid white line
                    if (nx > 0.94f && nx < 0.96f)
                        c = Color.Lerp(c, lineWhite, 0.85f);

                    // Yellow center median (double line)
                    if ((nx > 0.48f && nx < 0.49f) || (nx > 0.51f && nx < 0.52f))
                        c = Color.Lerp(c, lineYellow, 0.9f);

                    // Dashed lane dividers (at 1/3 and 2/3)
                    bool isDash = (y % 64) < 40; // 40px dash, 24px gap
                    if (isDash)
                    {
                        if (nx > 0.32f && nx < 0.34f)
                            c = Color.Lerp(c, lineWhite, 0.75f);
                        if (nx > 0.66f && nx < 0.68f)
                            c = Color.Lerp(c, lineWhite, 0.75f);
                    }

                    pixels[y * width + x] = c;
                }
            }

            DrawEdgeDarkening(pixels, width, height);
            tex.SetPixels(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        // ═══════════════════════════════════════════════
        //  PRIMARY ROAD — 2-lane, dashed center line, solid edge lines
        // ═══════════════════════════════════════════════

        public static Texture2D CreatePrimaryRoadTexture(int width = 256, int height = 512)
        {
            Texture2D tex = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];
            Color asphalt = new Color(0.14f, 0.14f, 0.15f);
            Color lineWhite = new Color(0.88f, 0.88f, 0.82f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = (float)x / width;
                    Color c = AsphaltPixel(asphalt, x, y, 150f);

                    // Solid edge lines
                    if (nx > 0.05f && nx < 0.07f)
                        c = Color.Lerp(c, lineWhite, 0.80f);
                    if (nx > 0.93f && nx < 0.95f)
                        c = Color.Lerp(c, lineWhite, 0.80f);

                    // Dashed center line
                    bool isDash = (y % 48) < 30;
                    if (isDash && nx > 0.49f && nx < 0.51f)
                        c = Color.Lerp(c, lineWhite, 0.80f);

                    pixels[y * width + x] = c;
                }
            }

            DrawEdgeDarkening(pixels, width, height);
            tex.SetPixels(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        // ═══════════════════════════════════════════════
        //  RESIDENTIAL — worn asphalt, faint center line
        // ═══════════════════════════════════════════════

        public static Texture2D CreateResidentialRoadTexture(int width = 256, int height = 512)
        {
            Texture2D tex = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];
            Color asphalt = new Color(0.18f, 0.17f, 0.16f); // Lighter, more worn

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = (float)x / width;
                    Color c = AsphaltPixel(asphalt, x, y, 250f);

                    // Very faint, intermittent center line (worn away)
                    bool isDash = (y % 60) < 25;
                    float fadeRandom = Mathf.PerlinNoise(0f, y * 0.01f + 500f);
                    if (isDash && nx > 0.48f && nx < 0.52f && fadeRandom > 0.3f)
                    {
                        float fade = fadeRandom * 0.35f;
                        c = Color.Lerp(c, new Color(0.75f, 0.75f, 0.70f), fade);
                    }

                    pixels[y * width + x] = c;
                }
            }

            DrawEdgeDarkening(pixels, width, height, 0.04f);
            tex.SetPixels(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        // ═══════════════════════════════════════════════
        //  FOOTPATH — interlocking brick paver pattern
        // ═══════════════════════════════════════════════

        public static Texture2D CreateFootpathTexture(int width = 256, int height = 256)
        {
            Texture2D tex = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];

            Color brick1 = new Color(0.62f, 0.48f, 0.38f); // Reddish brown
            Color brick2 = new Color(0.58f, 0.52f, 0.42f); // Tan
            Color brick3 = new Color(0.55f, 0.45f, 0.35f); // Dark brick
            Color mortar = new Color(0.50f, 0.48f, 0.44f);  // Joint/mortar

            int brickW = 24; // Brick width
            int brickH = 12; // Brick height
            int mortarSize = 2;

            Color[] brickColors = { brick1, brick2, brick3 };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Herringbone offset: alternate rows shifted by half
                    int row = y / brickH;
                    int offsetX = (row % 2 == 0) ? 0 : brickW / 2;
                    int localX = (x + offsetX) % brickW;
                    int localY = y % brickH;

                    Color c;

                    // Mortar joints
                    if (localX < mortarSize || localY < mortarSize)
                    {
                        c = mortar;
                        float mortarNoise = (Random.value - 0.5f) * 0.02f;
                        c += new Color(mortarNoise, mortarNoise, mortarNoise);
                    }
                    else
                    {
                        // Pick a consistent brick color based on position
                        int brickIdx = ((x + offsetX) / brickW + row * 7) % brickColors.Length;
                        c = brickColors[brickIdx];

                        // Brick surface variation
                        float n = Mathf.PerlinNoise(x * 0.3f + 800f, y * 0.3f + 800f);
                        c += new Color((n - 0.5f) * 0.04f, (n - 0.5f) * 0.03f, (n - 0.5f) * 0.02f);

                        // Per-pixel grain
                        float grain = (Random.value - 0.5f) * 0.015f;
                        c += new Color(grain, grain, grain);
                    }

                    c.a = 1f;
                    pixels[y * width + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        // ═══════════════════════════════════════════════
        //  CROSSWALK — zebra stripe pattern
        // ═══════════════════════════════════════════════

        public static Texture2D CreateCrosswalkTexture(int width = 256, int height = 128)
        {
            Texture2D tex = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];
            Color asphalt = new Color(0.14f, 0.14f, 0.15f);
            Color stripe = new Color(0.92f, 0.92f, 0.88f);

            int stripeWidth = width / 8;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color c = AsphaltPixel(asphalt, x, y, 350f);

                    // Zebra stripes (perpendicular to road)
                    int stripeIdx = x / stripeWidth;
                    if (stripeIdx % 2 == 0)
                    {
                        // Worn stripe — not perfectly clean
                        float wear = Mathf.PerlinNoise(x * 0.1f + 900f, y * 0.1f + 900f);
                        float blendStr = 0.85f - wear * 0.15f;
                        c = Color.Lerp(c, stripe, blendStr);
                    }

                    c.a = 1f;
                    pixels[y * width + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        /// <summary>
        /// Backward-compatible — creates a primary road texture.
        /// </summary>
        public static Texture2D CreateRoadTexture(int width = 256, int height = 512)
        {
            return CreatePrimaryRoadTexture(width, height);
        }

        // ═══════════════════════════════════════════════
        // GROUND TEXTURE — earth/sparse grass
        // ═══════════════════════════════════════════════

        public static Texture2D CreateGroundTexture(int width = 512, int height = 512)
        {
            Texture2D tex = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];

            Color earth = new Color(0.32f, 0.30f, 0.22f);      // Dark earth
            Color earthLight = new Color(0.40f, 0.36f, 0.26f);  // Lighter earth
            Color grassDark = new Color(0.22f, 0.35f, 0.15f);   // Dark green
            Color grassLight = new Color(0.32f, 0.45f, 0.20f);  // Green patches

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float n1 = Mathf.PerlinNoise(x * 0.012f + 100f, y * 0.012f + 100f);
                    float n2 = Mathf.PerlinNoise(x * 0.05f + 50f, y * 0.05f + 50f);

                    // Mix earth and grass
                    Color c = Color.Lerp(earth, earthLight, n1);
                    c = Color.Lerp(c, Color.Lerp(grassDark, grassLight, n2), Mathf.Clamp01(n1 * 0.6f));

                    // Grain
                    float grain = (Random.value - 0.5f) * 0.025f;
                    c += new Color(grain, grain * 0.8f, grain * 0.5f);

                    c.a = 1f;
                    pixels[y * width + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        // ═══════════════════════════════════
        // PARK — lush tropical green
        // ═══════════════════════════════════

        public static Texture2D CreateParkTexture(int width = 512, int height = 512)
        {
            Texture2D tex = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];

            Color dark = new Color(0.12f, 0.32f, 0.08f);
            Color mid = new Color(0.20f, 0.45f, 0.14f);
            Color light = new Color(0.30f, 0.55f, 0.18f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float n1 = Mathf.PerlinNoise(x * 0.02f + 200f, y * 0.02f + 200f);
                    float n2 = Mathf.PerlinNoise(x * 0.08f + 300f, y * 0.08f + 300f);

                    Color c = Color.Lerp(dark, mid, n1);
                    c = Color.Lerp(c, light, n2 * 0.4f);

                    float grain = (Random.value - 0.5f) * 0.02f;
                    c += new Color(grain * 0.3f, grain, grain * 0.2f);

                    c.a = 1f;
                    pixels[y * width + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        // ═══════════════════════════════════
        // WATER — deep blue
        // ═══════════════════════════════════

        public static Texture2D CreateWaterTexture(int width = 512, int height = 512)
        {
            Texture2D tex = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];

            Color deep = new Color(0.06f, 0.18f, 0.38f);
            Color mid = new Color(0.12f, 0.28f, 0.46f);
            Color light = new Color(0.20f, 0.38f, 0.52f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float w1 = Mathf.PerlinNoise(x * 0.02f + 500f, y * 0.04f + 500f);
                    float w2 = Mathf.PerlinNoise(x * 0.06f + 700f, y * 0.015f + 700f);

                    Color c = Color.Lerp(deep, mid, w1 * 0.6f + w2 * 0.4f);
                    c = Color.Lerp(c, light, Mathf.Clamp01(w1 * w2 * 2f - 0.3f));

                    float grain = (Random.value - 0.5f) * 0.01f;
                    c += new Color(grain * 0.3f, grain * 0.6f, grain);

                    c.a = 1f;
                    pixels[y * width + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        // ═══════════════════════════════════
        // SIDEWALK — concrete paver
        // ═══════════════════════════════════

        public static Texture2D CreateSidewalkTexture(int width = 256, int height = 256)
        {
            Texture2D tex = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];

            Color concrete = new Color(0.65f, 0.63f, 0.58f);
            Color joint = new Color(0.50f, 0.48f, 0.44f);

            int slabSize = width / 4;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int sx = x % slabSize;
                    int sy = y % slabSize;

                    float noise = (Random.value - 0.5f) * 0.025f;
                    Color c = concrete + new Color(noise, noise, noise);

                    if (sx < 2 || sy < 2)
                    {
                        c = joint + new Color(noise * 0.5f, noise * 0.5f, noise * 0.5f);
                    }

                    float slabVar = Mathf.PerlinNoise((x / slabSize) * 3.1f + 50f, (y / slabSize) * 3.1f + 50f);
                    c *= 0.95f + slabVar * 0.1f;

                    c.a = 1f;
                    pixels[y * width + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }
    }
}

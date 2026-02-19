using UnityEngine;

namespace GeoCity3D.Visuals
{
    public static class TextureGenerator
    {
        public static Texture2D CreateFacadeTexture(int width = 256, int height = 256)
        {
            Texture2D tex = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];
            Color wallColor = new Color(0.7f, 0.7f, 0.7f);
            Color windowColor = new Color(0.2f, 0.2f, 0.3f);
            Color frameColor = new Color(0.5f, 0.5f, 0.5f);

            int windowWidth = 20;
            int windowHeight = 30;
            int gapX = 15;
            int gapY = 15;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Grid pattern
                    int localX = x % (windowWidth + gapX);
                    int localY = y % (windowHeight + gapY);

                    if (localX < windowWidth && localY < windowHeight)
                    {
                        // Window with simple frame
                        if (localX < 2 || localX > windowWidth - 3 || localY < 2 || localY > windowHeight - 3)
                        {
                            pixels[y * width + x] = frameColor;
                        }
                        else
                        {
                            pixels[y * width + x] = windowColor; // Window glass
                            // Add some noise/reflection hint
                            if (Random.value > 0.9f) pixels[y * width + x] += new Color(0.1f, 0.1f, 0.1f);
                        }
                    }
                    else
                    {
                        // Wall
                        pixels[y * width + x] = wallColor * (0.95f + Random.value * 0.1f); // Concrete noise
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D CreateRoofTexture(int width = 128, int height = 128)
        {
            Texture2D tex = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];
            Color baseColor = new Color(0.35f, 0.35f, 0.35f);

            for (int i = 0; i < pixels.Length; i++)
            {
                float noise = Random.value * 0.1f;
                pixels[i] = baseColor + new Color(noise, noise, noise);
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D CreateRoadTexture(int width = 128, int height = 512)
        {
            Texture2D tex = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];
            Color asphaltColor = new Color(0.2f, 0.2f, 0.2f);
            Color lineColor = new Color(0.9f, 0.9f, 0.9f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float normalizedX = (float)x / width;
                    
                     // Asphalt noise
                    Color c = asphaltColor * (0.9f + Random.value * 0.2f);

                    // Side lines
                    if (normalizedX > 0.05f && normalizedX < 0.08f || normalizedX > 0.92f && normalizedX < 0.95f)
                    {
                        c = lineColor;
                    }

                    // Center dash line
                    if (normalizedX > 0.48f && normalizedX < 0.52f)
                    {
                        // Dash pattern every 40 pixels
                        if ((y / 40) % 2 == 0)
                        {
                            c = lineColor;
                        }
                    }

                    pixels[y * width + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}

using System;

namespace Spotlighter;

/// <summary>
/// Rasterized, anti-aliased industrial brass pendant lamp. The shade is constructed
/// in a rotated local coordinate system so the aperture tracks the mouse, while
/// the hanging cable remains anchored to the display's top edge. Design-size
/// frames are resampled by LampRaster for the physical monitor height.
/// </summary>
internal static unsafe class LampPainter
{
    internal static void Draw(uint* pixels, int width, int height, float aimX, float aimY, bool lit = true, float temperature = 0)
    {
        float anchorX = width * 0.5f;
        const float anchorY = 78f;
        float dx = aimX - anchorX, dy = aimY - anchorY;
        if (dx * dx + dy * dy < 100f) { dx = 0f; dy = 1f; }
        float inverse = 1f / MathF.Sqrt(dx * dx + dy * dy);
        float forwardX = dx * inverse, forwardY = dy * inverse;

        float amount=LightMask.TintAmount(temperature);
        float warm=temperature>0?amount:0,cool=temperature<0?amount:0;
        float bulbR=255-85*cool,bulbG=241-49*warm-21*cool,bulbB=198-103*warm+57*cool;
        float haloR=255-100*cool,haloG=220-50*warm-15*cool,haloB=147-82*warm+108*cool;

        int left = Math.Max(0, (int)anchorX - 144);
        int right = Math.Min(width, (int)anchorX + 145);
        int bottom = Math.Min(height, 225);

        for (int y = 0; y < bottom; y++)
        {
            int row = y * width;
            for (int x = left; x < right; x++)
            {
                int index = row + x;
                float ux = x - anchorX, uy = y - anchorY;
                float along = ux * forwardX + uy * forwardY;
                float across = ux * forwardY - uy * forwardX;
                float absSide = MathF.Abs(across);

                // A slim braided power cord fixed in screen space.
                if (y <= 76)
                {
                    float cordDistance = MathF.Abs(x - anchorX);
                    float cordOpacity = Math.Clamp(2.2f - cordDistance, 0f, 1f) * 0.85f;
                    if (cordOpacity > 0f)
                    {
                        float glint = (MathF.Sin(y * 0.21f) + 1f) * 0.5f;
                        Blend(pixels, index, 47f + glint * 13f, 39f + glint * 8f, 32f, cordOpacity);
                    }
                    if (y < 9f && MathF.Abs(x - anchorX) < 11f)
                        Blend(pixels, index, 88f, 76f, 61f, 0.93f);
                }

                // Emission uses the same neutral-centered curve as the desktop light.
                float bulbX = ux - forwardX * 40f;
                float bulbY = uy - forwardY * 40f;
                float halo2 = bulbX * bulbX + bulbY * bulbY;
                if (lit && halo2 < 5500f)
                {
                    float bloom = 0.24f / (1f + halo2 * 0.008f);
                    Blend(pixels, index, haloR, haloG, haloB, bloom);
                }

                // Main spun metal shell; a cone/frustum of finite thickness.
                if (along >= -21f && along <= 33.2f)
                {
                    float normalized = (along + 21f) / 54.2f;
                    float halfWidth = 8f + 22f * MathF.Pow(normalized, 0.85f);
                    float edge = halfWidth - absSide;
                    float coverage = Math.Clamp(edge + 0.5f, 0f, 1f);
                    if (coverage > 0f)
                    {
                        float facet = 1f - absSide / halfWidth;
                        float brushed = MathF.Sin(along * 0.64f + across * 0.18f) * 3.5f;
                        float highlight = MathF.Pow(MathF.Max(0f, facet), 2f);
                        float rimLight = MathF.Max(0f, (along - 26f) / 8f);
                        float red = 103f + highlight * 68f + normalized * 27f + brushed + rimLight * 22f;
                        float green = 75f + highlight * 52f + normalized * 19f + brushed * 0.8f + rimLight * 17f;
                        float blue = 49f + highlight * 36f + normalized * 12f + brushed * 0.6f + rimLight * 9f;
                        Blend(pixels, index, red, green, blue, coverage);
                    }
                }

                // Bright enamel/brass lip enclosing the lamp aperture.
                if (along > 31f && along < 36f)
                {
                    float extent = 30.2f - absSide;
                    float coverage = Math.Clamp(extent + 0.5f, 0f, 1f);
                    if (coverage > 0f)
                    {
                        float thickness = 1f - MathF.Abs(along - 33.5f) / 2.5f;
                        Blend(pixels, index, 230f, 186f, 114f, coverage * Math.Clamp(thickness, 0f, 0.93f));
                    }
                }

                // Light source: not a circle gradient alone; it sits inside the rotated shade.
                float bulbAlong = (along - 37f) / 8f;
                float bulbAcross = across / 11f;
                float bulb2 = bulbAlong * bulbAlong + bulbAcross * bulbAcross;
                if (bulb2 < 1.18f)
                {
                    float intensity = Math.Clamp((1.18f - bulb2) * 2.2f, 0f, 1f);
                    if(lit) Blend(pixels, index, bulbR, bulbG, bulbB, intensity);
                    else Blend(pixels, index, 72f, 65f, 53f, intensity);
                }

                // Hardware pivot and a concentric turned-metal joint.
                float pivot2 = ux * ux + uy * uy;
                if (pivot2 < 130f)
                {
                    float ring = MathF.Sqrt(pivot2);
                    float brightness = MathF.Max(0f, 1f - ring / 12f);
                    float r = 92f + 67f * brightness;
                    float g = 69f + 51f * brightness;
                    float b = 45f + 38f * brightness;
                    Blend(pixels, index, r, g, b, Math.Clamp((11.4f - ring) * 1.25f, 0f, 1f));
                    if (ring < 3.5f)
                        Blend(pixels, index, 48f, 41f, 35f, 0.92f);
                }
            }
        }
    }

    private static void Blend(uint* pixels, int index, float red, float green, float blue, float opacity)
    {
        opacity = Math.Clamp(opacity, 0f, 1f);
        if (opacity <= 0f) return;
        uint dst = pixels[index];
        float remaining = 1f - opacity;
        int oldA = (int)(dst >> 24);
        int oldR = (int)((dst >> 16) & 255u);
        int oldG = (int)((dst >> 8) & 255u);
        int oldB = (int)(dst & 255u);
        int a = Clamp255(255f * opacity + oldA * remaining);
        // destination is premultiplied BGRA as required by ULW_ALPHA
        int r = Clamp255(red * opacity + oldR * remaining);
        int g = Clamp255(green * opacity + oldG * remaining);
        int b = Clamp255(blue * opacity + oldB * remaining);
        pixels[index] = (uint)(a << 24 | r << 16 | g << 8 | b);
    }

    private static int Clamp255(float value) => (int)Math.Clamp(value, 0f, 255f);
}

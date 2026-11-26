using System;

namespace ZombieCity.City.Core
{
    /// <summary>
    /// IEC 61966-2-1 sRGB ↔ linear for 8-bit channels (ADR-004 §3–§4).
    /// Building vertex colors stay sRGB in the mesh; the shader converts them.
    /// </summary>
    public static class ColorSpaceUtil
    {
        /// <summary>8-bit sRGB channel to linear 0..1.</summary>
        public static float SrgbToLinear(byte channel)
        {
            if (channel == 0)
            {
                return 0f;
            }

            if (channel == 255)
            {
                return 1f;
            }

            float c = channel / 255f;
            if (c <= 0.04045f)
            {
                return c / 12.92f;
            }

            return MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
        }

        /// <summary>Linear 0..1 back to the nearest 8-bit sRGB channel.</summary>
        public static byte LinearToSrgb(float linear)
        {
            if (linear <= 0f)
            {
                return 0;
            }

            if (linear >= 1f)
            {
                return 255;
            }

            float s = linear <= 0.0031308f
                ? linear * 12.92f
                : 1.055f * MathF.Pow(linear, 1f / 2.4f) - 0.055f;
            int q = (int)Math.Round(s * 255.0, MidpointRounding.AwayFromZero);
            if (q < 0)
            {
                return 0;
            }

            if (q > 255)
            {
                return 255;
            }

            return (byte)q;
        }
    }
}

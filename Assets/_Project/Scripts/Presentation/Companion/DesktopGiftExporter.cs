using System;
using System.IO;
using AnimeAssistant.Domain;
using UnityEngine;

namespace AnimeAssistant.Presentation
{
    /// <summary>
    /// Procedurally drawn gift visuals: small billboard icons for the in-world
    /// gift moment, and real PNG keepsakes (omamori charm / handmade chocolate)
    /// dropped into the user's Desktop\Airi folder — the 2D↔3D bridge.
    /// </summary>
    public static class DesktopGiftExporter
    {
        private const int IconSize = 64;
        private const int PngSize = 256;

        public static Texture2D CreateGiftIcon(GiftKind gift)
        {
            var texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false)
            {
                name = $"Gift Icon {gift}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[IconSize * IconSize];
            for (var y = 0; y < IconSize; y++)
            {
                for (var x = 0; x < IconSize; x++)
                {
                    var u = (x + 0.5f) / IconSize * 2f - 1f; // -1..1
                    var v = (y + 0.5f) / IconSize * 2f - 1f;
                    pixels[y * IconSize + x] = GiftIconPixel(gift, u, v);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>Writes the keepsake PNG; returns the path or null on failure.</summary>
        public static string SaveGiftPng(GiftKind gift, DateTime localDate)
        {
            try
            {
                var directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Airi");
                Directory.CreateDirectory(directory);
                var fileName = gift == GiftKind.Charm
                    ? $"Bua may man {localDate:yyyy-MM-dd}.png"
                    : $"So-co-la Airi {localDate:yyyy-MM-dd}.png";
                var path = Path.Combine(directory, fileName);
                File.WriteAllBytes(path, CreateGiftPng(gift).EncodeToPNG());
                return path;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"[Companion] Gift PNG failed: {exception.Message}");
                return null;
            }
        }

        private static Texture2D CreateGiftPng(GiftKind gift)
        {
            var size = PngSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            var isCharm = gift == GiftKind.Charm;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var u = (x + 0.5f) / size * 2f - 1f;
                    var v = (y + 0.5f) / size * 2f - 1f;
                    pixels[y * size + x] = isCharm ? CharmPixel(u, v) : ChocolatePixel(u, v);
                }
            }
            texture.SetPixels32(pixels);
            // Stay CPU-readable: EncodeToPNG() needs the pixel data.
            texture.Apply(false, false);
            return texture;
        }

        private static Color32 GiftIconPixel(GiftKind gift, float u, float v)
        {
            switch (gift)
            {
                case GiftKind.Chocolate:
                case GiftKind.Charm:
                    return InHeart(u, v, 0.62f)
                        ? (Color32)new Color32(196, 74, 108, 255)
                        : Clear;
                case GiftKind.Flowers:
                    var petal = Mathf.Sin(6f * Mathf.Atan2(v, u)) * 0.36f;
                    var radius = Mathf.Sqrt(u * u + v * v);
                    return radius is > 0.16f && radius < 0.55f + petal
                        ? (Color32)new Color32(250, 158, 194, 255)
                        : radius <= 0.16f ? (Color32)new Color32(255, 214, 120, 255) : Clear;
                case GiftKind.Letter:
                    return Mathf.Abs(u) < 0.6f && Mathf.Abs(v) < 0.4f
                        ? (Color32)new Color32(245, 240, 225, 255)
                        : Clear;
                default: // Tea
                    return v < 0.25f && v > -0.35f && u > -0.45f && u < 0.45f
                        ? (Color32)new Color32(150, 96, 60, 255)
                        : Clear;
            }
        }

        private static Color32 CharmPixel(float u, float v)
        {
            // Omamori: rounded body, gold band, dark top flap, red core.
            if (!(Mathf.Abs(u) < 0.62f && Mathf.Abs(v) < 0.8f))
            {
                return Clear;
            }
            if (v > 0.55f)
            {
                return new Color32(70, 52, 44, 255); // top cloth
            }
            if (Mathf.Abs(v - 0.45f) < 0.07f)
            {
                return new Color32(226, 182, 88, 255); // gold band
            }
            if (Mathf.Abs(u) < 0.4f && Mathf.Abs(v) < 0.3f)
            {
                return new Color32(196, 52, 66, 255); // red core
            }
            return new Color32(238, 232, 220, 255); // body
        }

        private static Color32 ChocolatePixel(float u, float v)
        {
            if (InHeart(u, v, 0.78f))
            {
                var shade = (byte)(120 + Mathf.Clamp01((v + 1f) / 2f) * 70);
                return new Color32(shade, 54, 84, 255);
            }
            return Clear;
        }

        private static bool InHeart(float u, float v, float scale)
        {
            var x = u / scale;
            var y = v / scale + 0.18f;
            var term = x * x + y * y - 1f;
            return term * term * term - x * x * y * y * y <= 0f;
        }

        private static readonly Color32 Clear = new(0, 0, 0, 0);
    }
}

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AnimeAssistant.Presentation
{
    /// <summary>
    /// "Cửa vẫn mở về thế giới bên kia" — while the companion is home behind
    /// the door, the Windows desktop wallpaper becomes her world (procedural
    /// isekai sky matching the portal palette, with mountains and a moon).
    /// The user's original wallpaper is captured once and restored when she
    /// comes back out or the app exits.
    /// </summary>
    public static class DesktopWallpaperWorld
    {
        private const uint SpiGetDeskWallpaper = 0x0073;
        private const uint SpiSetDeskWallpaper = 0x0014;
        private const uint SpifUpdateIniFile = 0x01;
        private const uint SpifSendChange = 0x02;

        private static string WallpaperPngPath =>
            Path.Combine(CompanionStore.RootDirectory, "isekai_wallpaper.png");

        private static string OriginalWallpaperFile =>
            Path.Combine(CompanionStore.RootDirectory, "original_wallpaper.txt");

        private static string OriginalColorFile =>
            Path.Combine(CompanionStore.RootDirectory, "original_wallpaper_color.txt");

        private static string SolidColorPngPath =>
            Path.Combine(CompanionStore.RootDirectory, "original_solid_color.png");

        /// <summary>Sets the isekai wallpaper. Only runs in the Windows player.</summary>
        public static bool SwapToWorld(IsekaiPortalBackdrop.SkyPalette palette)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                System.IO.Directory.CreateDirectory(CompanionStore.RootDirectory);
                SaveOriginalIfNeeded();

                // Prefer a real photo shipped in RuntimeContent/Effects; keep the
                // procedural render as the fallback so the feature always works.
                var photoPath = FindEffectPhoto(palette.ImageFile);
                string targetPath;
                if (photoPath != null)
                {
                    targetPath = photoPath;
                }
                else
                {
                    if (!File.Exists(WallpaperPngPath))
                    {
                        File.WriteAllBytes(WallpaperPngPath, GenerateWorldTexture(palette).EncodeToPNG());
                    }
                    targetPath = WallpaperPngPath;
                }

                var ok = SystemParametersInfo(SpiSetDeskWallpaper, 0u, targetPath,
                    SpifUpdateIniFile | SpifSendChange);
                Debug.Log(ok
                    ? $"[Companion] Wallpaper swapped to the other world ({Path.GetFileName(targetPath)})."
                    : "[Companion] SystemParametersInfo refused the wallpaper swap.");
                return ok;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Companion] Wallpaper swap failed: {exception.Message}");
                return false;
            }
#else
            return false;
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static string FindEffectPhoto(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            try
            {
                var projectDirectory = Path.GetDirectoryName(Application.dataPath);
                if (string.IsNullOrEmpty(projectDirectory))
                {
                    return null;
                }

                var candidates = new[]
                {
                    Path.Combine(projectDirectory, "RuntimeContent", "Effects", fileName),
                    Path.Combine(projectDirectory, "Effects", fileName)
                };
                foreach (var candidate in candidates)
                {
                    if (File.Exists(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Companion] Effect photo lookup failed: {exception.Message}");
            }

            return null;
        }
#endif

        /// <summary>Puts the user's original wallpaper back (no-op if never swapped).</summary>
        public static bool RestoreOriginal()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                if (!File.Exists(OriginalWallpaperFile))
                {
                    return false;
                }

                var original = File.ReadAllText(OriginalWallpaperFile)?.Trim();
                if (!string.IsNullOrEmpty(original))
                {
                    return SystemParametersInfo(SpiSetDeskWallpaper, 0u, original,
                        SpifUpdateIniFile | SpifSendChange);
                }

                // The original was a solid color: SystemParametersInfo cannot set
                // one by path, so write a solid PNG matching the captured color.
                if (!File.Exists(OriginalColorFile))
                {
                    return false;
                }

                var parts = File.ReadAllText(OriginalColorFile)?.Trim().Split(' ');
                if (parts == null || parts.Length != 3 ||
                    !byte.TryParse(parts[0], out var r) || !byte.TryParse(parts[1], out var g) ||
                    !byte.TryParse(parts[2], out var b))
                {
                    return false;
                }

                System.IO.File.WriteAllBytes(SolidColorPngPath, CreateSolidTexture(r, g, b).EncodeToPNG());
                return SystemParametersInfo(SpiSetDeskWallpaper, 0u, SolidColorPngPath,
                    SpifUpdateIniFile | SpifSendChange);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Companion] Wallpaper restore failed: {exception.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        private static void SaveOriginalIfNeeded()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (File.Exists(OriginalWallpaperFile))
            {
                return;
            }

            var builder = new StringBuilder(520);
            SystemParametersInfo(SpiGetDeskWallpaper, (uint)builder.Capacity, builder, 0u);
            File.WriteAllText(OriginalWallpaperFile, builder.ToString());
            Debug.Log($"[Companion] Original wallpaper captured: '{builder}'.");

            try
            {
                var builder2 = new StringBuilder(256);
                var dataSize = (uint)(builder2.Capacity * 2);
                var result = RegGetValueW(HkeyCurrentUser, @"Control Panel\Colors",
                    "Background", RrfRtRegSz, out _, builder2, ref dataSize);
                var background = result == 0 ? builder2.ToString() : null;
                if (!string.IsNullOrEmpty(background))
                {
                    File.WriteAllText(OriginalColorFile, background);
                    Debug.Log($"[Companion] Original desktop color captured: '{background}'.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Companion] Could not capture desktop color: {exception.Message}");
            }
#endif
        }

        private static Texture2D CreateSolidTexture(byte r, byte g, byte b)
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            var pixels = new Color32[16 * 16];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(r, g, b, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>
        /// A fuller rendering of the portal sky for a full desktop: vertical
        /// gradient, horizon glow, a moon, two mountain silhouette layers and
        /// seasonal sparkles. Generated once per install, reused per session.
        /// </summary>
        private static Texture2D GenerateWorldTexture(IsekaiPortalBackdrop.SkyPalette palette)
        {
            const int width = 1920;
            const int height = 1080;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Isekai Desktop World",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[width * height];
            var horizon = height * 0.68f;
            var moonX = width * 0.72f;
            var moonY = height * 0.22f;
            var moonRadius = height * 0.055f;
            var seed = (uint)(palette.Top.GetHashCode() * 31 + palette.Bottom.GetHashCode());

            for (var y = 0; y < height; y++)
            {
                var t = y / (float)(height - 1);
                var sky = Color.Lerp(palette.Top, palette.Bottom, Mathf.Pow(t, 1.4f));
                var glow = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(y - horizon) / (height * 0.4f)), 3f);
                var rowBase = Color.Lerp(sky, palette.Sparkle, glow * 0.35f);
                for (var x = 0; x < width; x++)
                {
                    var color = rowBase;

                    // Distant cloud bands drifting across the upper sky.
                    if (y < horizon)
                    {
                        var cloud = Mathf.Sin(x * 0.004f + Mathf.Sin(y * 0.01f) * 2f + y * 0.02f);
                        if (cloud > 0.72f)
                        {
                            color = Color.Lerp(color, palette.Sparkle, (cloud - 0.72f) * 0.9f);
                        }
                    }

                    // Two mountain silhouette layers above the horizon.
                    var ridge1 = horizon - height * (0.05f + 0.045f * Mathf.Sin(x * 0.006f + 1.7f) +
                                                     0.02f * Mathf.Sin(x * 0.017f));
                    var ridge2 = horizon - height * (0.11f + 0.06f * Mathf.Sin(x * 0.0035f) +
                                                     0.025f * Mathf.Sin(x * 0.011f + 0.6f));
                    if (y > ridge2)
                    {
                        color = Color.Lerp(palette.Top, Color.black, 0.45f);
                        if (y > ridge1)
                        {
                            color = Color.Lerp(palette.Top, Color.black, 0.68f);
                        }
                    }
                    else if (y > ridge1)
                    {
                        color = Color.Lerp(palette.Top, Color.black, 0.45f);
                    }

                    // Moon with a soft halo.
                    var dx = (x - moonX) / moonRadius;
                    var dy = (y - moonY) / moonRadius;
                    var moonDistance = Mathf.Sqrt(dx * dx + dy * dy);
                    if (moonDistance < 1f)
                    {
                        color = palette.Sparkle;
                    }
                    else if (moonDistance < 2.6f)
                    {
                        color = Color.Lerp(color, palette.Sparkle,
                            Mathf.Pow(1f - (moonDistance - 1f) / 1.6f, 2f) * 0.5f);
                    }

                    pixels[y * width + x] = color;
                }
            }

            // Seasonal sparkles scattered over the open sky.
            for (var i = 0; i < 220; i++)
            {
                seed = seed * 1664525u + 1013904223u;
                var sx = (int)((seed >> 8) % width);
                seed = seed * 1664525u + 1013904223u;
                var sy = (int)((seed >> 8) % (int)horizon);
                seed = seed * 1664525u + 1013904223u;
                var brightness = 0.4f + (seed >> 12) % 1000 / 1000f * 0.6f;
                var radius = 1 + (int)((seed >> 8) % 2);
                for (var dy = -radius; dy <= radius; dy++)
                {
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        var px = Mathf.Clamp(sx + dx, 0, width - 1);
                        var py = Mathf.Clamp(sy + dy, 0, height - 1);
                        pixels[py * width + px] = Color.Lerp(
                            pixels[py * width + px], palette.Sparkle, brightness);
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false); // must stay CPU-readable for EncodeToPNG
            return texture;
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static readonly UIntPtr HkeyCurrentUser = new UIntPtr(0x80000002u);
        private const uint RrfRtRegSz = 0x00000002u;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegGetValueW(UIntPtr hkey, string subKey, string valueName,
            uint flags, out uint type, StringBuilder data, ref uint dataSize);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SystemParametersInfo(uint action, uint uiParam,
            string param, uint fWinIni);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SystemParametersInfo(uint action, uint uiParam,
            StringBuilder param, uint fWinIni);
#endif
    }
}

using System;
using System.IO;
using AnimeAssistant.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimeAssistant.Presentation
{
    /// <summary>
    /// "Thế giới bên kia" — a procedurally textured backdrop quad placed just
    /// behind the castle-door opening. The doorway reads as a window into a
    /// fantasy world whose sky changes with the season (and festival family),
    /// visible whenever the door leaf is open or collapsed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IsekaiPortalBackdrop : MonoBehaviour
    {
        private MeshRenderer quadRenderer;
        private Material material;

        public static IsekaiPortalBackdrop EnsureForDoor()
        {
            var existing = FindFirstObjectByType<IsekaiPortalBackdrop>();
            if (existing != null)
            {
                return existing;
            }

            var clickZone = GameObject.Find("DoorClickZone");
            if (clickZone == null)
            {
                return null;
            }

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Isekai Backdrop";
            quad.transform.SetParent(clickZone.transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, -0.45f);
            quad.transform.localScale = new Vector3(2.85f, 3.5f, 1f);
            var component = quad.AddComponent<IsekaiPortalBackdrop>();
            component.Build();
            return component;
        }

        private void Build()
        {
            quadRenderer = GetComponent<MeshRenderer>();
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Texture");
            material = new Material(shader) { name = "Isekai Backdrop Material" };
            material.renderQueue = (int)RenderQueue.Background + 10;
            quadRenderer.sharedMaterial = material;
            currentImageFile = "";
            ApplySky(SkyPalette.For(DateTime.Today));
        }

        private string currentImageFile = "";

        /// <summary>Regenerates the sky when the season or festival family changes.</summary>
        public void SetSeason(DateTime localDate, FestivalPetals festivalFamily)
        {
            ApplySky(SkyPalette.For(localDate, festivalFamily));
        }

        private void ApplySky(SkyPalette palette)
        {
            if (material == null)
            {
                return;
            }

            // Prefer a real photo from RuntimeContent/Effects; fall back to the
            // procedural gradient sky when the file is not shipped.
            var photo = TryLoadEffectTexture(palette.ImageFile);
            if (photo != null)
            {
                if (material.HasProperty("_BaseMap"))
                {
                    var previous = material.GetTexture("_BaseMap");
                    if (previous != null && previous.name == photo.name && currentImageFile == palette.ImageFile)
                    {
                        Destroy(photo);
                        return;
                    }
                    if (previous != null)
                    {
                        Destroy(previous);
                    }
                    material.SetTexture("_BaseMap", photo);
                }

                // CSS-like cover crop: show the central slice of a landscape
                // photo inside the portrait doorway quad.
                var quadAspect = transform.localScale.x / Mathf.Max(0.01f, transform.localScale.y);
                var photoAspect = photo.width / Mathf.Max(1f, photo.height);
                if (material.HasProperty("_BaseMap") && photoAspect > quadAspect)
                {
                    var visible = Mathf.Clamp01(quadAspect / photoAspect);
                    material.mainTextureScale = new Vector2(visible, 1f);
                    material.mainTextureOffset = new Vector2((1f - visible) * 0.5f, 0f);
                }
                else
                {
                    material.mainTextureScale = Vector2.one;
                    material.mainTextureOffset = Vector2.zero;
                }
                currentImageFile = palette.ImageFile;
                return;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.mainTextureScale = Vector2.one;
                material.mainTextureOffset = Vector2.zero;
                var previous = material.GetTexture("_BaseMap");
                if (previous != null && previous.name != PaletteName(palette))
                {
                    Destroy(previous);
                    material.SetTexture("_BaseMap", GenerateTexture(palette));
                }
                else if (previous == null)
                {
                    material.SetTexture("_BaseMap", GenerateTexture(palette));
                }
            }
            currentImageFile = "";
        }

        private static string PaletteName(SkyPalette palette)
        {
            return $"Isekai Sky {palette.Top:x8}{palette.Bottom:x8}{palette.Sparkle:x8}";
        }

        private Texture2D GenerateTexture(SkyPalette palette)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = PaletteName(palette),
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[size * size];
            var seed = (uint)(palette.Top.GetHashCode() ^ palette.Bottom.GetHashCode() << 7);
            for (var y = 0; y < size; y++)
            {
                var t = y / (float)(size - 1); // top row = high sky
                var baseColor = Color.Lerp(palette.Top, palette.Bottom, t);
                for (var x = 0; x < size; x++)
                {
                    var color = baseColor;
                    // Soft horizon glow near the bottom.
                    var glow = Mathf.Pow(Mathf.Clamp01(1f - t / 0.35f), 2.5f);
                    color = Color.Lerp(color, palette.Sparkle, glow * 0.45f);
                    pixels[y * size + x] = color;
                }
            }

            // Sprinkle stars/petals/snow depending on the palette's sparkle color.
            for (var i = 0; i < palette.SparkleCount; i++)
            {
                seed = seed * 1664525u + 1013904223u;
                var x = (int)((seed >> 8) % size);
                seed = seed * 1664525u + 1013904223u;
                var y = (int)((seed >> 8) % size);
                seed = seed * 1664525u + 1013904223u;
                var brightness = 0.45f + (seed >> 12) % 1000 / 1000f * 0.55f;
                var radius = 1 + (int)((seed >> 8) % 2);
                for (var dy = -radius; dy <= radius; dy++)
                {
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        var px = Mathf.Clamp(x + dx, 0, size - 1);
                        var py = Mathf.Clamp(y + dy, 0, size - 1);
                        var existing = pixels[py * size + px];
                        var sparkled = Color.Lerp(existing, palette.Sparkle, brightness * 0.85f);
                        pixels[py * size + px] = sparkled;
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        public readonly struct SkyPalette
        {
            public SkyPalette(Color top, Color bottom, Color sparkle, int sparkleCount,
                string imageFile)
            {
                Top = top;
                Bottom = bottom;
                Sparkle = sparkle;
                SparkleCount = sparkleCount;
                ImageFile = imageFile;
            }

            public Color Top { get; }
            public Color Bottom { get; }
            public Color Sparkle { get; }
            public int SparkleCount { get; }
            /// <summary>Photo from RuntimeContent/Effects used for this sky (may not exist).</summary>
            public string ImageFile { get; }

            public static SkyPalette For(DateTime date, FestivalPetals festival = FestivalPetals.None)
            {
                switch (festival)
                {
                    case FestivalPetals.Pink:
                        return new SkyPalette(new Color(0.24f, 0.12f, 0.3f),
                            new Color(1f, 0.62f, 0.74f), new Color(1f, 0.85f, 0.92f), 110,
                            "sky_spring.jpg");
                    case FestivalPetals.Blue:
                        return new SkyPalette(new Color(0.04f, 0.05f, 0.22f),
                            new Color(0.3f, 0.55f, 0.95f), new Color(0.85f, 0.92f, 1f), 150,
                            "sky_summer.jpg");
                    case FestivalPetals.Gold:
                        return new SkyPalette(new Color(0.1f, 0.06f, 0.22f),
                            new Color(0.95f, 0.72f, 0.3f), new Color(1f, 0.93f, 0.6f), 140,
                            "sky_festival.jpg");
                }

                return date.Month switch
                {
                    12 or 1 or 2 => Winter(),
                    3 or 4 or 5 => Spring(),
                    6 or 7 or 8 => Summer(),
                    _ => Autumn()
                };
            }

            private static SkyPalette Winter() => new(
                new Color(0.16f, 0.22f, 0.4f), new Color(0.85f, 0.9f, 0.98f),
                new Color(1f, 1f, 1f), 130, "sky_winter.jpg");

            private static SkyPalette Spring() => new(
                new Color(0.35f, 0.18f, 0.4f), new Color(1f, 0.72f, 0.8f),
                new Color(1f, 0.88f, 0.95f), 100, "sky_spring.jpg");

            private static SkyPalette Summer() => new(
                new Color(0.05f, 0.08f, 0.3f), new Color(0.2f, 0.45f, 0.85f),
                new Color(1f, 0.95f, 0.7f), 160, "sky_summer.jpg");

            private static SkyPalette Autumn() => new(
                new Color(0.18f, 0.1f, 0.28f), new Color(0.95f, 0.55f, 0.25f),
                new Color(1f, 0.8f, 0.5f), 110, "sky_autumn.jpg");
        }

        /// <summary>
        /// Loads a photo from RuntimeContent/Effects (Editor: RuntimeContent/Effects,
        /// player: Effects next to the exe). Returns null when the file is missing.
        /// </summary>
        public static Texture2D TryLoadEffectTexture(string fileName)
        {
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
                    if (!File.Exists(candidate))
                    {
                        continue;
                    }

                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                    {
                        name = $"Effect {fileName}",
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp
                    };
                    if (texture.LoadImage(File.ReadAllBytes(candidate)))
                    {
                        return texture;
                    }

                    Destroy(texture);
                    return null;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Companion] Could not load effect texture '{fileName}': {exception.Message}");
            }

            return null;
        }
    }
}

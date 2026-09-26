using System;
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
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", GenerateTexture(SkyPalette.For(DateTime.Today)));
            }
            material.renderQueue = (int)RenderQueue.Background + 10;
            quadRenderer.sharedMaterial = material;
        }

        /// <summary>Regenerates the sky when the season or festival family changes.</summary>
        public void SetSeason(DateTime localDate, FestivalPetals festivalFamily)
        {
            if (material == null)
            {
                return;
            }

            var palette = SkyPalette.For(localDate, festivalFamily);
            var previous = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
            if (previous != null && previous.name == PaletteName(palette))
            {
                return;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", GenerateTexture(palette));
            }
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
            public SkyPalette(Color top, Color bottom, Color sparkle, int sparkleCount)
            {
                Top = top;
                Bottom = bottom;
                Sparkle = sparkle;
                SparkleCount = sparkleCount;
            }

            public Color Top { get; }
            public Color Bottom { get; }
            public Color Sparkle { get; }
            public int SparkleCount { get; }

            public static SkyPalette For(DateTime date, FestivalPetals festival = FestivalPetals.None)
            {
                switch (festival)
                {
                    case FestivalPetals.Pink:
                        return new SkyPalette(new Color(0.24f, 0.12f, 0.3f),
                            new Color(1f, 0.62f, 0.74f), new Color(1f, 0.85f, 0.92f), 110);
                    case FestivalPetals.Blue:
                        return new SkyPalette(new Color(0.04f, 0.05f, 0.22f),
                            new Color(0.3f, 0.55f, 0.95f), new Color(0.85f, 0.92f, 1f), 150);
                    case FestivalPetals.Gold:
                        return new SkyPalette(new Color(0.1f, 0.06f, 0.22f),
                            new Color(0.95f, 0.72f, 0.3f), new Color(1f, 0.93f, 0.6f), 140);
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
                new Color(1f, 1f, 1f), 130);

            private static SkyPalette Spring() => new(
                new Color(0.35f, 0.18f, 0.4f), new Color(1f, 0.72f, 0.8f),
                new Color(1f, 0.88f, 0.95f), 100);

            private static SkyPalette Summer() => new(
                new Color(0.05f, 0.08f, 0.3f), new Color(0.2f, 0.45f, 0.85f),
                new Color(1f, 0.95f, 0.7f), 160);

            private static SkyPalette Autumn() => new(
                new Color(0.18f, 0.1f, 0.28f), new Color(0.95f, 0.55f, 0.25f),
                new Color(1f, 0.8f, 0.5f), 110);
        }
    }
}

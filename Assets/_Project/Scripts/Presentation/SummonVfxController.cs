using AnimeAssistant.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimeAssistant.Presentation
{
    /// <summary>Procedural portal and arrival particles synchronized to lifecycle state.</summary>
    [DisallowMultipleComponent]
    public sealed class SummonVfxController : MonoBehaviour
    {
        [SerializeField] private GreyboxSummonController controller;
        [SerializeField] private Transform portalAnchor;
        [SerializeField] private Transform avatar;

        private ParticleSystem portalParticles;
        private ParticleSystem celebrationParticles;
        private Material particleMaterial;
        private Texture2D particleTexture;
        private SummonState previousState = SummonState.DoorClosed;
        private float emissionAccumulator;

        /// <summary>Small portal spark burst used as the on-beat visual for dancing.</summary>
        public void PortalBeatBurst()
        {
            if (portalParticles != null)
            {
                portalParticles.Emit(14);
            }
        }

        public void PlayAvatarClickFireworks()
        {
            if (celebrationParticles == null)
            {
                return;
            }

            celebrationParticles.Emit(95);
            Invoke(nameof(PlayAvatarClickFireworksEcho), 0.16f);
        }

        private ParticleSystem ambientPetals;

        /// <summary>
        /// Seasonal ambient particles (cherry blossom, tanabata, new year)
        /// falling across the whole view while the avatar is active.
        /// </summary>
        public void EnsureAmbientPetals(Color colorA, Color colorB)
        {
            if (particleMaterial == null)
            {
                return;
            }

            if (ambientPetals == null)
            {
                var camera = Camera.main;
                if (camera == null)
                {
                    return;
                }

                ambientPetals = CreateSystem("Seasonal Ambient Petals", camera.transform);
                ambientPetals.transform.localPosition = new Vector3(0f, 5.5f, 7.5f);
                var main = ambientPetals.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 7f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.13f);
                main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 220;
                main.gravityModifier = 0.04f;

                var shape = ambientPetals.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(14f, 0.4f, 9f);

                var velocity = ambientPetals.velocityOverLifetime;
                velocity.enabled = true;
                velocity.y = new ParticleSystem.MinMaxCurve(-0.55f, -0.3f);
                velocity.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);

                var rotation = ambientPetals.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-1.6f, 1.6f);

                var color = ambientPetals.colorOverLifetime;
                color.enabled = true;
                color.color = FadeGradient();

                var emission = ambientPetals.emission;
                emission.enabled = true;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(7f);
                ambientPetals.Play();
            }
            else
            {
                var main = ambientPetals.main;
                main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
                if (!ambientPetals.isPlaying)
                {
                    ambientPetals.Play();
                }
            }
        }

        public void ClearAmbientPetals()
        {
            if (ambientPetals != null && ambientPetals.isPlaying)
            {
                ambientPetals.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void PlayAvatarClickFireworksEcho()
        {
            if (celebrationParticles != null)
            {
                celebrationParticles.Emit(55);
            }
        }

        public void Configure(GreyboxSummonController configuredController, Transform configuredPortalAnchor,
            Transform configuredAvatar)
        {
            controller = configuredController;
            portalAnchor = configuredPortalAnchor;
            avatar = configuredAvatar;
        }

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponent<GreyboxSummonController>();
            }

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                         Shader.Find("Particles/Standard Unlit") ??
                         Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("Portal particles disabled because no compatible runtime shader was included.", this);
                enabled = false;
                return;
            }
            particleMaterial = new Material(shader)
            {
                name = "Runtime Portal Spark Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            particleTexture = CreateSoftParticleTexture();
            particleMaterial.SetTexture("_BaseMap", particleTexture);
            particleMaterial.SetColor("_BaseColor", Color.white);
            particleMaterial.SetFloat("_Surface", 1f);
            particleMaterial.SetFloat("_Blend", 0f);
            particleMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            particleMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            particleMaterial.SetFloat("_ZWrite", 0f);
            particleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            particleMaterial.SetOverrideTag("RenderType", "Transparent");
            particleMaterial.renderQueue = (int)RenderQueue.Transparent;
            portalParticles = CreatePortalParticles();
            celebrationParticles = CreateCelebrationParticles();
        }

        private void OnDestroy()
        {
            if (particleMaterial != null)
            {
                Destroy(particleMaterial);
            }
            if (particleTexture != null)
            {
                Destroy(particleTexture);
            }
        }

        private void Update()
        {
            if (controller == null)
            {
                return;
            }

            var state = controller.State;
            if (state != previousState)
            {
                OnStateEntered(state);
                previousState = state;
            }

            if (state == SummonState.DoorOpening || state == SummonState.AvatarExiting ||
                state == SummonState.AvatarReturning || state == SummonState.AvatarEntering ||
                state == SummonState.DoorClosing)
            {
                emissionAccumulator += Time.unscaledDeltaTime * 36f;
                var count = Mathf.FloorToInt(emissionAccumulator);
                if (count > 0)
                {
                    portalParticles.Emit(count);
                    emissionAccumulator -= count;
                }
            }
        }

        private void OnStateEntered(SummonState state)
        {
            switch (state)
            {
                case SummonState.DoorOpening:
                    portalParticles.Emit(70);
                    break;
                case SummonState.AvatarExiting:
                    portalParticles.Emit(42);
                    break;
                case SummonState.AvatarActive:
                    celebrationParticles.Emit(85);
                    break;
                case SummonState.AvatarReturning:
                    celebrationParticles.Emit(28);
                    portalParticles.Emit(35);
                    break;
                case SummonState.DoorClosing:
                    portalParticles.Emit(55);
                    break;
            }
        }

        private ParticleSystem CreatePortalParticles()
        {
            var particles = CreateSystem("Portal Arc Particles", portalAnchor);
            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.095f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.08f, 0.82f, 1f, 1f), new Color(1f, 0.67f, 0.18f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 240;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.15f;
            shape.radiusThickness = 0.12f;

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            // Unity requires all three orbital axes to use the same curve mode.
            // A constant arc is deterministic and avoids runtime particle errors.
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(0f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(1.45f);
            velocity.radial = new ParticleSystem.MinMaxCurve(-0.35f, 0.15f);

            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = FadeGradient();
            return particles;
        }

        private ParticleSystem CreateCelebrationParticles()
        {
            var particles = CreateSystem("Avatar Celebration Sparkles", avatar);
            particles.transform.localPosition = Vector3.up * 1.05f;
            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.65f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.11f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.2f, 1f, 0.95f, 1f), new Color(1f, 0.52f, 0.82f, 1f));
            main.gravityModifier = 0.12f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 180;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.52f;
            shape.radiusThickness = 0.65f;

            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = FadeGradient();
            return particles;
        }

        private ParticleSystem CreateSystem(string objectName, Transform parent)
        {
            var instance = new GameObject(objectName);
            instance.transform.SetParent(parent != null ? parent : transform, false);
            var particles = instance.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var emission = particles.emission;
            emission.enabled = false;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return particles;
        }

        private static ParticleSystem.MinMaxGradient FadeGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.45f, 0.92f, 1f), 0.55f),
                    new GradientColorKey(new Color(1f, 0.52f, 0.18f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0f, 1f)
                });
            return new ParticleSystem.MinMaxGradient(gradient);
        }

        private static Texture2D CreateSoftParticleTexture()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "Runtime Soft Spark",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[size * size];
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / center;
                    var dy = (y - center) / center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.2f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}

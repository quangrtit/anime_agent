using System;
using System.Collections.Generic;
using System.Linq;
using AnimeAssistant.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AnimeAssistant.Editor
{
    /// <summary>
    /// Installs the official Unity-Chan model and her same-rig motion set without
    /// bringing the package's legacy scripts or render-pipeline-specific shaders
    /// into the application.
    /// </summary>
    public static class UnityChanSceneIntegrator
    {
        private const string ScenePath = "Assets/_Project/Scenes/DesktopPetPrototype.unity";
        private const string ModelPath = "Assets/ThirdParty/UnityChan/Models/unitychan.fbx";
        private const string MotionDirectory = "Assets/ThirdParty/UnityChan/Animations";
        private const string MaterialDirectory = "Assets/_Project/Art/Characters/UnityChan/Materials";
        private const string TextureDirectory = "Assets/ThirdParty/UnityChan/Models/Texture";
        private const float TargetAvatarHeight = 2.05f;

        private static readonly Dictionary<string, MaterialRecipe> MaterialRecipes =
            new Dictionary<string, MaterialRecipe>(StringComparer.OrdinalIgnoreCase)
            {
                ["body"] = new MaterialRecipe("body_01.tga", "body_01_NRM.tga"),
                ["hair"] = new MaterialRecipe("hair_01.tga", "hair_01_NRM.tga", alphaClip: true, doubleSided: true),
                ["skin1"] = new MaterialRecipe("skin_01.tga"),
                ["face"] = new MaterialRecipe("face_00.tga", alphaClip: true),
                ["eye_L1"] = new MaterialRecipe("eye_iris_L_00.tga", alphaClip: true),
                ["eye_R1"] = new MaterialRecipe("eye_iris_R_00.tga", alphaClip: true),
                ["eyeline"] = new MaterialRecipe("eyeline_00.tga", alphaClip: true, doubleSided: true),
                ["eyebase"] = new MaterialRecipe("eyeline_00.tga", alphaClip: true, doubleSided: true),
                ["mat_cheek"] = new MaterialRecipe("cheek_00.tga", transparent: true, doubleSided: true),
                ["Left"] = new MaterialRecipe(new Color(0.08f, 0.71f, 0.87f, 1f)),
                ["Right"] = new MaterialRecipe(new Color(1f, 0.04f, 0.04f, 1f)),
            };

        [MenuItem("Anime Assistant/Install Unity-Chan Same-Rig Avatar")]
        public static void Integrate()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureNormalTexture("body_01_NRM.tga");
            ConfigureNormalTexture("hair_01_NRM.tga");
            AssetDatabase.ImportAsset(ModelPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                throw new InvalidOperationException($"Unity-Chan model did not import from {ModelPath}.");
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var motionRoot = GameObject.Find("HeroAvatarMotionRoot")?.transform;
            var visualSlot = GameObject.Find("AvatarVisualFacing")?.transform;
            if (motionRoot == null || visualSlot == null)
            {
                throw new InvalidOperationException("The scene is missing the avatar motion root or visual slot.");
            }

            for (var index = visualSlot.childCount - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(visualSlot.GetChild(index).gameObject);
            }

            var instance = PrefabUtility.InstantiatePrefab(model, scene) as GameObject;
            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(model);
                SceneManager.MoveGameObjectToScene(instance, scene);
            }

            instance.name = "UnityChanAvatar";
            instance.transform.SetParent(visualSlot, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            InstallUrpMaterials(instance);
            FitToSlot(instance);

            var animator = instance.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                throw new InvalidOperationException("Unity-Chan did not import as a valid Humanoid avatar.");
            }

            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var motion = instance.GetComponent<HumanoidAvatarMotion>() ??
                         instance.AddComponent<HumanoidAvatarMotion>();
            motion.ConfigureNativeClips(
                LoadMotion("WAIT00"),
                LoadMotion("WAIT02"),
                LoadMotion("WALK00_F"),
                LoadMotion("RUN00_F"),
                LoadMotion("HANDUP00_R"),
                LoadMotion("JUMP00"),
                LoadMotion("WIN00"),
                LoadMotion("REFLESH00"));

            EditorUtility.SetDirty(instance);
            EditorUtility.SetDirty(motion);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            var bounds = CalculateBounds(instance);
            Debug.Log($"[UnityChan] Installed same-rig avatar. Height={bounds.size.y:F2}m, " +
                      $"Renderers={instance.GetComponentsInChildren<Renderer>(true).Length}, " +
                      "NativeClips=WAIT00,WAIT02,WALK00_F,RUN00_F,HANDUP00_R,JUMP00,WIN00,REFLESH00.");
        }

        private static AnimationClip LoadMotion(string motionName)
        {
            var path = $"{MotionDirectory}/unitychan_{motionName}.fbx";
            AssetDatabase.ImportAsset(path,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate => candidate.name == motionName) ??
                AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(candidate => !candidate.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (clip == null)
            {
                throw new InvalidOperationException($"No animation clip was imported from {path}.");
            }

            return clip;
        }

        private static void InstallUrpMaterials(GameObject avatar)
        {
            EnsureAssetFolder(MaterialDirectory);
            var materialCache = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            foreach (var renderer in avatar.GetComponentsInChildren<Renderer>(true))
            {
                var sourceMaterials = renderer.sharedMaterials;
                var replacements = new Material[sourceMaterials.Length];
                for (var index = 0; index < sourceMaterials.Length; index++)
                {
                    var sourceName = sourceMaterials[index] == null ? "Fallback" : sourceMaterials[index].name;
                    sourceName = sourceName.Replace(" (Instance)", string.Empty);
                    if (!materialCache.TryGetValue(sourceName, out var replacement))
                    {
                        replacement = CreateOrUpdateMaterial(sourceName);
                        materialCache[sourceName] = replacement;
                    }

                    replacements[index] = replacement;
                }

                renderer.sharedMaterials = replacements;
            }
        }

        private static Material CreateOrUpdateMaterial(string sourceName)
        {
            var safeName = string.Concat(sourceName.Select(character =>
                char.IsLetterOrDigit(character) || character == '_' || character == '-' ? character : '_'));
            var assetPath = $"{MaterialDirectory}/{safeName}_URP.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException("URP Lit shader was not found.");
            }

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, assetPath);
            }
            else
            {
                material.shader = shader;
            }

            var recipe = MaterialRecipes.TryGetValue(sourceName, out var configured)
                ? configured
                : new MaterialRecipe(Color.white);
            material.name = $"UnityChan {sourceName} URP";
            material.SetColor("_BaseColor", recipe.Color);
            material.SetFloat("_Smoothness", sourceName.IndexOf("eye", StringComparison.OrdinalIgnoreCase) >= 0 ? 0.55f : 0.2f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Cull", recipe.DoubleSided ? (float)CullMode.Off : (float)CullMode.Back);

            if (!string.IsNullOrEmpty(recipe.BaseTexture))
            {
                material.SetTexture("_BaseMap",
                    AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDirectory}/{recipe.BaseTexture}"));
            }

            if (!string.IsNullOrEmpty(recipe.NormalTexture))
            {
                material.SetTexture("_BumpMap",
                    AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDirectory}/{recipe.NormalTexture}"));
                material.SetFloat("_BumpScale", 0.72f);
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.DisableKeyword("_NORMALMAP");
            }

            ConfigureSurface(material, recipe.AlphaClip, recipe.Transparent);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureSurface(Material material, bool alphaClip, bool transparent)
        {
            material.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
            material.SetFloat("_Cutoff", alphaClip ? 0.32f : 0.5f);
            if (alphaClip)
            {
                material.EnableKeyword("_ALPHATEST_ON");
            }
            else
            {
                material.DisableKeyword("_ALPHATEST_ON");
            }

            if (transparent)
            {
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                material.SetOverrideTag("RenderType", alphaClip ? "TransparentCutout" : "Opaque");
                material.SetFloat("_Surface", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
                material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                material.SetFloat("_ZWrite", 1f);
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = alphaClip ? (int)RenderQueue.AlphaTest : -1;
            }
        }

        private static void ConfigureNormalTexture(string fileName)
        {
            var path = $"{TextureDirectory}/{fileName}";
            if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
                importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
        }

        private static void FitToSlot(GameObject avatar)
        {
            var initialBounds = CalculateBounds(avatar);
            if (initialBounds.size.y <= 0.01f)
            {
                throw new InvalidOperationException("Unity-Chan has no renderable bounds.");
            }

            avatar.transform.localScale = Vector3.one * (TargetAvatarHeight / initialBounds.size.y);
            var fittedBounds = CalculateBounds(avatar);
            avatar.transform.position += new Vector3(
                avatar.transform.parent.position.x - fittedBounds.center.x,
                avatar.transform.parent.position.y + 0.02f - fittedBounds.min.y,
                avatar.transform.parent.position.z - fittedBounds.center.z);
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => !(renderer is ParticleSystemRenderer))
                .ToArray();
            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position, Vector3.zero);
            }

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static void EnsureAssetFolder(string path)
        {
            var current = "Assets";
            foreach (var segment in path.Substring("Assets/".Length).Split('/'))
            {
                var next = $"{current}/{segment}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segment);
                }

                current = next;
            }
        }

        private readonly struct MaterialRecipe
        {
            public MaterialRecipe(string baseTexture, string normalTexture = null,
                bool alphaClip = false, bool transparent = false, bool doubleSided = false)
            {
                BaseTexture = baseTexture;
                NormalTexture = normalTexture;
                AlphaClip = alphaClip;
                Transparent = transparent;
                DoubleSided = doubleSided;
                Color = Color.white;
            }

            public MaterialRecipe(Color color)
            {
                BaseTexture = null;
                NormalTexture = null;
                AlphaClip = false;
                Transparent = false;
                DoubleSided = false;
                Color = color;
            }

            public string BaseTexture { get; }
            public string NormalTexture { get; }
            public bool AlphaClip { get; }
            public bool Transparent { get; }
            public bool DoubleSided { get; }
            public Color Color { get; }
        }
    }
}

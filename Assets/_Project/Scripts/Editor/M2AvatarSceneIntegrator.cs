using System;
using System.IO;
using System.Linq;
using AnimeAssistant.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimeAssistant.Editor
{
    public static class M2AvatarSceneIntegrator
    {
        private const string ScenePath = "Assets/_Project/Scenes/DesktopPetPrototype.unity";
        private const string VrmPath = "Assets/ThirdParty/UserProvided/Michan/Source/7066744897306419891.vrm";
        private const string DoorGltfPath = "Assets/ThirdParty/PolyHaven/LargeCastleDoor/Source/large_castle_door_2k.gltf";
        private const string DoorDiffusePath = "Assets/ThirdParty/PolyHaven/LargeCastleDoor/Source/textures/large_castle_door_diff_2k.jpg";
        private const string DoorNormalPath = "Assets/ThirdParty/PolyHaven/LargeCastleDoor/Source/textures/large_castle_door_nor_gl_2k.jpg";
        private const string DoorMaterialPath = "Assets/_Project/Art/Environment/CastleDoor/CastleDoorPbr.mat";
        private const string EvidencePath = "Docs/Evidence/M2_Michan_Unity.png";
        private const string CloseupEvidencePath = "Docs/Evidence/M2_Michan_Closeup.png";
        private const float TargetAvatarHeight = 2.05f;

        [MenuItem("Anime Assistant/Integrate M2 Production Avatar")]
        public static void Integrate()
        {
            AssetDatabase.ImportAsset(VrmPath, ImportAssetOptions.ForceUpdate);
            var avatarAsset = AssetDatabase.LoadAssetAtPath<GameObject>(VrmPath);
            if (avatarAsset == null)
            {
                var available = string.Join(", ", AssetDatabase.LoadAllAssetsAtPath(VrmPath)
                    .Select(asset => asset == null ? "null" : asset.GetType().FullName));
                throw new InvalidOperationException($"VRM did not import as a GameObject. Sub-assets: {available}");
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var oldAvatar = GameObject.Find("HeroAvatarMotionRoot") ?? GameObject.Find("AiriAvatarMotionRoot") ??
                GameObject.Find("MichanAvatar") ?? GameObject.Find("ShinoAvatar") ?? GameObject.Find("AiriAvatar") ??
                GameObject.Find("Avatar_Capsule");
            if (oldAvatar != null)
            {
                UnityEngine.Object.DestroyImmediate(oldAvatar);
            }

            var instance = PrefabUtility.InstantiatePrefab(avatarAsset, scene) as GameObject;
            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(avatarAsset);
                SceneManager.MoveGameObjectToScene(instance, scene);
            }

            var motionRoot = new GameObject("HeroAvatarMotionRoot");
            SceneManager.MoveGameObjectToScene(motionRoot, scene);
            var visualFacing = new GameObject("AvatarVisualFacing");
            SceneManager.MoveGameObjectToScene(visualFacing, scene);
            visualFacing.transform.SetParent(motionRoot.transform, false);
            visualFacing.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            instance.name = "MichanAvatar";
            instance.transform.SetParent(visualFacing.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            AvatarMaterialOptimizer.Optimize(instance);

            var initialBounds = CalculateBounds(instance);
            if (initialBounds.size.y <= 0.01f)
            {
                throw new InvalidOperationException("Imported avatar has no renderable bounds.");
            }

            instance.transform.localScale = Vector3.one * (TargetAvatarHeight / initialBounds.size.y);
            var fittedBounds = CalculateBounds(instance);
            instance.transform.position += new Vector3(-fittedBounds.center.x, 0.02f - fittedBounds.min.y,
                -fittedBounds.center.z);
            var motion = instance.GetComponent<HumanoidAvatarMotion>() ?? instance.AddComponent<HumanoidAvatarMotion>();
            var materialSummary = string.Join("; ", instance.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .Distinct()
                .Select(material => $"{material.name}[{(material.mainTexture == null ? "no-texture" : material.mainTexture.name)}]"));
            Debug.Log($"Production avatar materials: {materialSummary}");
            Debug.Log("Production avatar renderers: " + string.Join("; ",
                instance.GetComponentsInChildren<Renderer>(true)
                    .Select(renderer => $"{renderer.name}({renderer.sharedMaterials.Length} slots)")));

            var camera = GameObject.Find("Main Camera")?.GetComponent<Camera>();
            var door = InstallCastleDoor(scene);
            var doorPivot = door.Primary;
            var secondaryDoorPivot = door.Secondary;
            var handle = door.Handle;
            var portalLight = GameObject.Find("Portal Light")?.GetComponent<Light>();
            var clickZone = GameObject.Find("DoorClickZone")?.GetComponent<Collider>();
            var controller = UnityEngine.Object.FindFirstObjectByType<GreyboxSummonController>();
            if (camera == null || doorPivot == null || handle == null || portalLight == null || clickZone == null || controller == null)
            {
                throw new InvalidOperationException("The M1 scene hierarchy is incomplete; M2 integration was not saved.");
            }

            var hidden = new Vector3(1.72f, 0.10f, 0.72f);
            var threshold = new Vector3(1.72f, 0.10f, -0.68f);
            var active = new Vector3(-1.65f, 0.10f, -0.78f);
            motionRoot.transform.position = hidden;
            controller.Configure(camera, doorPivot, handle, motionRoot.transform, portalLight, clickZone,
                hidden, threshold, active);
            controller.ConfigureSecondaryDoor(secondaryDoorPivot);
            var vfx = controller.GetComponent<SummonVfxController>() ?? controller.gameObject.AddComponent<SummonVfxController>();
            vfx.Configure(controller, GameObject.Find("PortalGlow")?.transform ?? portalLight.transform,
                motionRoot.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            var animator = instance.GetComponentInChildren<Animator>(true);
            var humanoid = animator != null && animator.avatar != null && animator.avatar.isHuman;
            var animatorPresent = animator != null;
            var integratedHeight = CalculateBounds(instance).size.y;
            CaptureEvidence(camera, doorPivot, secondaryDoorPivot, handle, motionRoot.transform, portalLight, motion,
                active);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            Debug.Log($"M2 production avatar integrated. VRM={avatarAsset.name}, humanoid={humanoid}, " +
                      $"animator={animatorPresent}, height={integratedHeight:0.00}m");
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position, Vector3.zero);
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static DoorInstallation InstallCastleDoor(Scene scene)
        {
            AssetDatabase.ImportAsset(DoorGltfPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var doorAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DoorGltfPath);
            if (doorAsset == null)
            {
                var subAssets = string.Join(", ", AssetDatabase.LoadAllAssetsAtPath(DoorGltfPath)
                    .Select(asset => asset == null ? "null" : asset.GetType().Name));
                throw new InvalidOperationException($"Castle-door glTF did not import as a GameObject: {subAssets}");
            }

            var oldArt = GameObject.Find("CastleDoorArt");
            if (oldArt != null)
            {
                UnityEngine.Object.DestroyImmediate(oldArt);
            }

            foreach (var oldName in new[] { "DoorPivot", "DoorPivotSecondary", "Frame_Left", "Frame_Right", "Frame_Top", "Threshold" })
            {
                var oldObject = GameObject.Find(oldName);
                if (oldObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(oldObject);
                }
            }

            var setRoot = GameObject.Find("M1_Greybox_Set")?.transform;
            if (setRoot == null)
            {
                throw new InvalidOperationException("M1_Greybox_Set was not found.");
            }

            var art = PrefabUtility.InstantiatePrefab(doorAsset, scene) as GameObject;
            if (art == null)
            {
                art = UnityEngine.Object.Instantiate(doorAsset);
                SceneManager.MoveGameObjectToScene(art, scene);
            }

            art.name = "CastleDoorArt";
            art.transform.SetParent(setRoot, false);
            art.transform.localPosition = Vector3.zero;
            art.transform.localRotation = Quaternion.identity;
            art.transform.localScale = Vector3.one;

            var initialBounds = CalculateBounds(art);
            var scale = 3.25f / initialBounds.size.y;
            art.transform.localScale = Vector3.one * scale;
            var fittedBounds = CalculateBounds(art);
            art.transform.position += new Vector3(1.75f - fittedBounds.center.x, 0.02f - fittedBounds.min.y,
                -0.02f - fittedBounds.center.z);

            var material = CreateDoorMaterial();
            foreach (var renderer in art.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
            }

            var transforms = art.GetComponentsInChildren<Transform>(true);
            var primary = transforms.FirstOrDefault(item => item.name == "large_castle_door_right");
            var secondary = transforms.FirstOrDefault(item => item.name == "large_castle_door_left");
            if (primary == null || secondary == null)
            {
                throw new InvalidOperationException("Castle-door leaf nodes were not found in the imported glTF.");
            }

            primary.name = "DoorPivot";
            secondary.name = "DoorPivotSecondary";
            var handleObject = new GameObject("HandlePivot");
            handleObject.transform.SetParent(primary, false);
            handleObject.transform.localPosition = Vector3.zero;
            return new DoorInstallation(primary, secondary, handleObject.transform);
        }

        private static Material CreateDoorMaterial()
        {
            EnsureAssetFolder("Assets/_Project/Art/Environment/CastleDoor");
            if (AssetImporter.GetAtPath(DoorNormalPath) is TextureImporter normalImporter &&
                normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(DoorMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, DoorMaterialPath);
            }

            material.name = "Castle Door PBR";
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(DoorDiffusePath));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(DoorNormalPath));
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_BumpScale", 0.82f);
            material.SetFloat("_Metallic", 0.28f);
            material.SetFloat("_Smoothness", 0.24f);
            EditorUtility.SetDirty(material);
            return material;
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

        private static void CaptureEvidence(
            Camera camera,
            Transform doorPivot,
            Transform secondaryDoorPivot,
            Transform handle,
            Transform avatar,
            Light portalLight,
            HumanoidAvatarMotion motion,
            Vector3 activePosition)
        {
            doorPivot.localRotation = Quaternion.Euler(0f, -105f, 0f);
            secondaryDoorPivot.localRotation = Quaternion.Euler(0f, 105f, 0f);
            handle.localRotation = Quaternion.Euler(0f, 0f, -42f);
            avatar.position = activePosition;
            avatar.rotation = Quaternion.Euler(0f, -12f, 0f);
            portalLight.intensity = 1.8f;
            motion.ApplyEditorPreviewPose();

            RenderToPng(camera, EvidencePath);

            camera.transform.position = avatar.position + new Vector3(-0.05f, 1.08f, -3.7f);
            camera.transform.LookAt(avatar.position + Vector3.up * 1.02f);
            camera.fieldOfView = 35f;
            portalLight.intensity = 0f;
            RenderToPng(camera, CloseupEvidencePath);
        }

        private static void RenderToPng(Camera camera, string evidencePath)
        {
            var renderTexture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            camera.targetTexture = renderTexture;
            camera.useOcclusionCulling = false;
            camera.Render();
            camera.Render();
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
            texture.Apply();

            var absolutePath = Path.GetFullPath(evidencePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());

            camera.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }

        private readonly struct DoorInstallation
        {
            public DoorInstallation(Transform primary, Transform secondary, Transform handle)
            {
                Primary = primary;
                Secondary = secondary;
                Handle = handle;
            }

            public Transform Primary { get; }
            public Transform Secondary { get; }
            public Transform Handle { get; }
        }
    }
}

using System.IO;
using AnimeAssistant.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AnimeAssistant.Editor
{
    public static class M1GreyboxSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/DesktopPetPrototype.unity";
        private const string MaterialFolder = "Assets/_Project/Art/Materials/GeneratedM1";
        private const string EvidencePath = "Docs/Evidence/M1_Greybox.png";

        [MenuItem("Anime Assistant/Build M1 Greybox Scene")]
        public static void Build()
        {
            EnsureFolder("Assets/_Project/Art/Materials", "GeneratedM1");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "DesktopPetPrototype";

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.27f);

            var doorMaterial = Material("DoorIndigo", new Color(0.12f, 0.10f, 0.28f), 0.35f, 0.55f);
            var frameMaterial = Material("FrameViolet", new Color(0.28f, 0.19f, 0.46f), 0.65f, 0.38f);
            var metalMaterial = Material("HandleGold", new Color(0.95f, 0.58f, 0.16f), 0.85f, 0.28f);
            var avatarMaterial = Material("AvatarCoral", new Color(1.0f, 0.30f, 0.53f), 0.05f, 0.5f);
            var darkMaterial = Material("AvatarDark", new Color(0.035f, 0.025f, 0.08f), 0.1f, 0.6f);
            var eyeMaterial = EmissiveMaterial("EyeCyan", new Color(0.25f, 1.0f, 1.0f), 2.4f);
            var portalMaterial = EmissiveMaterial("PortalCyan", new Color(0.05f, 0.76f, 1.0f), 4.5f);
            var groundMaterial = Material("GroundMidnight", new Color(0.035f, 0.045f, 0.09f), 0.15f, 0.72f);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0.9f, 2.65f, -9.6f);
            camera.transform.LookAt(new Vector3(0.8f, 1.35f, 0f));
            camera.fieldOfView = 34f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.022f, 0.055f, 1f);
            camera.allowHDR = true;

            var keyObject = new GameObject("Key Light");
            var key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(0.72f, 0.78f, 1f);
            key.intensity = 1.25f;
            key.shadows = LightShadows.Soft;
            keyObject.transform.rotation = Quaternion.Euler(42f, -28f, 0f);

            Primitive("Ground", PrimitiveType.Cube, null, new Vector3(0.8f, -0.18f, 0.4f),
                new Vector3(9.5f, 0.3f, 5.2f), groundMaterial);
            Primitive("BackPlate", PrimitiveType.Cube, null, new Vector3(0.8f, 2.2f, 1.6f),
                new Vector3(9.5f, 4.8f, 0.18f), groundMaterial);

            var setRoot = new GameObject("M1_Greybox_Set").transform;
            var portalGlow = Primitive("PortalGlow", PrimitiveType.Cube, setRoot,
                new Vector3(1.75f, 1.53f, 0.25f), new Vector3(1.9f, 2.46f, 0.08f), portalMaterial);
            RemoveCollider(portalGlow);
            portalGlow.GetComponent<Renderer>().enabled = false;

            Primitive("Frame_Left", PrimitiveType.Cube, setRoot,
                new Vector3(0.55f, 1.55f, 0f), new Vector3(0.30f, 3.10f, 0.38f), frameMaterial);
            Primitive("Frame_Right", PrimitiveType.Cube, setRoot,
                new Vector3(2.95f, 1.55f, 0f), new Vector3(0.30f, 3.10f, 0.38f), frameMaterial);
            Primitive("Frame_Top", PrimitiveType.Cube, setRoot,
                new Vector3(1.75f, 3.02f, 0f), new Vector3(2.70f, 0.30f, 0.38f), frameMaterial);
            Primitive("Threshold", PrimitiveType.Cube, setRoot,
                new Vector3(1.75f, 0.13f, -0.02f), new Vector3(2.65f, 0.26f, 0.52f), metalMaterial);

            var doorPivot = new GameObject("DoorPivot").transform;
            doorPivot.SetParent(setRoot, false);
            doorPivot.position = new Vector3(0.75f, 0.25f, -0.10f);
            var doorLeaf = Primitive("DoorLeaf", PrimitiveType.Cube, doorPivot,
                new Vector3(0.98f, 1.42f, 0f), new Vector3(1.96f, 2.64f, 0.22f), doorMaterial);

            var inset = Primitive("DoorInset", PrimitiveType.Cube, doorLeaf.transform,
                new Vector3(0f, 0.18f, -0.56f), new Vector3(0.70f, 0.62f, 0.06f), frameMaterial);
            RemoveCollider(inset);
            var lowerInset = Primitive("DoorInsetLower", PrimitiveType.Cube, doorLeaf.transform,
                new Vector3(0f, -0.25f, -0.56f), new Vector3(0.70f, 0.22f, 0.06f), frameMaterial);
            RemoveCollider(lowerInset);

            var handlePivot = new GameObject("HandlePivot").transform;
            handlePivot.SetParent(doorLeaf.transform, false);
            handlePivot.localPosition = new Vector3(0.34f, 0f, -0.62f);
            var handle = Primitive("Handle", PrimitiveType.Cylinder, handlePivot,
                new Vector3(0f, 0f, 0f), new Vector3(0.07f, 0.22f, 0.07f), metalMaterial);
            handle.transform.localRotation = Quaternion.Euler(90f, 0f, 90f);
            RemoveCollider(handle);

            var clickZoneObject = new GameObject("DoorClickZone");
            clickZoneObject.transform.SetParent(setRoot, false);
            clickZoneObject.transform.position = new Vector3(1.75f, 1.55f, -0.35f);
            var clickZone = clickZoneObject.AddComponent<BoxCollider>();
            clickZone.size = new Vector3(2.45f, 3.1f, 0.5f);

            var portalLightObject = new GameObject("Portal Light");
            portalLightObject.transform.SetParent(setRoot, false);
            portalLightObject.transform.position = new Vector3(1.75f, 1.55f, -0.45f);
            var portalLight = portalLightObject.AddComponent<Light>();
            portalLight.type = LightType.Point;
            portalLight.color = new Color(0.15f, 0.86f, 1f);
            portalLight.range = 6f;
            portalLight.intensity = 0f;
            portalLight.shadows = LightShadows.Soft;

            var avatarHidden = new Vector3(1.72f, 1.20f, 0.55f);
            var avatarThreshold = new Vector3(1.12f, 1.20f, -0.45f);
            var avatarActive = new Vector3(-1.15f, 1.20f, -0.78f);
            var avatar = Primitive("Avatar_Capsule", PrimitiveType.Capsule, setRoot,
                avatarHidden, new Vector3(0.68f, 1.05f, 0.68f), avatarMaterial);

            var hair = Primitive("Hair", PrimitiveType.Sphere, avatar.transform,
                new Vector3(0f, 0.40f, 0.02f), new Vector3(1.04f, 0.62f, 1.02f), darkMaterial);
            RemoveCollider(hair);
            var leftEye = Primitive("Eye_L", PrimitiveType.Sphere, avatar.transform,
                new Vector3(-0.17f, 0.25f, -0.49f), new Vector3(0.12f, 0.17f, 0.08f), eyeMaterial);
            RemoveCollider(leftEye);
            var rightEye = Primitive("Eye_R", PrimitiveType.Sphere, avatar.transform,
                new Vector3(0.17f, 0.25f, -0.49f), new Vector3(0.12f, 0.17f, 0.08f), eyeMaterial);
            RemoveCollider(rightEye);
            var bowLeft = Primitive("Bow_L", PrimitiveType.Cube, avatar.transform,
                new Vector3(-0.18f, -0.17f, -0.52f), new Vector3(0.24f, 0.16f, 0.08f), metalMaterial);
            bowLeft.transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
            RemoveCollider(bowLeft);
            var bowRight = Primitive("Bow_R", PrimitiveType.Cube, avatar.transform,
                new Vector3(0.18f, -0.17f, -0.52f), new Vector3(0.24f, 0.16f, 0.08f), metalMaterial);
            bowRight.transform.localRotation = Quaternion.Euler(0f, 0f, -25f);
            RemoveCollider(bowRight);

            var controllerObject = new GameObject("DesktopPetPrototype");
            controllerObject.AddComponent<SceneEntryPoint>();
            var controller = controllerObject.AddComponent<GreyboxSummonController>();
            controller.Configure(camera, doorPivot, handlePivot, avatar.transform, portalLight, clickZone,
                avatarHidden, avatarThreshold, avatarActive);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/_Project/Scenes/Bootstrap.unity", true),
                new EditorBuildSettingsScene(ScenePath, true)
            };

            CaptureEvidence(camera, doorPivot, handlePivot, avatar.transform, portalLight, avatarActive);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"M1 greybox scene and evidence generated: {ScenePath}, {EvidencePath}");
        }

        private static GameObject Primitive(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            var instance = GameObject.CreatePrimitive(type);
            instance.name = name;
            if (parent != null)
            {
                instance.transform.SetParent(parent, false);
            }

            instance.transform.localPosition = position;
            instance.transform.localScale = scale;
            instance.GetComponent<Renderer>().sharedMaterial = material;
            return instance;
        }

        private static Material Material(string name, Color color, float metallic, float smoothness)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.name = name;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material EmissiveMaterial(string name, Color color, float intensity)
        {
            var material = Material(name, color * 0.45f, 0.1f, 0.55f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * intensity);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void RemoveCollider(GameObject instance)
        {
            var collider = instance.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static void CaptureEvidence(
            Camera camera,
            Transform doorPivot,
            Transform handle,
            Transform avatar,
            Light portalLight,
            Vector3 avatarActive)
        {
            doorPivot.localRotation = Quaternion.Euler(0f, -105f, 0f);
            handle.localRotation = Quaternion.Euler(0f, 0f, -42f);
            avatar.position = avatarActive;
            avatar.localRotation = Quaternion.Euler(0f, -12f, 0f);
            portalLight.intensity = 7f;

            var renderTexture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
            texture.Apply();

            var absolutePath = Path.GetFullPath(EvidencePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());

            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(renderTexture);
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}

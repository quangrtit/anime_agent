using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnimeAssistant.Tests.EditMode
{
    public sealed class PortalDoorAssetTests
    {
        private const string DoorPath =
            "Assets/ThirdParty/PolyHaven/LargeCastleDoor/Source/large_castle_door_2k.gltf";
        private const string ScenePath = "Assets/_Project/Scenes/DesktopPetPrototype.unity";

        [Test]
        public void CastleDoor_ImportsWithFrameAndTwoIndependentLeaves()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPath);
            Assert.That(prefab, Is.Not.Null, "The audited Poly Haven glTF must import as a prefab.");

            var names = prefab.GetComponentsInChildren<Transform>(true)
                .Select(item => item.name)
                .ToArray();
            Assert.That(names, Does.Contain("large_castle_door_frame"));
            Assert.That(names, Does.Contain("large_castle_door_left"));
            Assert.That(names, Does.Contain("large_castle_door_right"));

            var triangleCount = prefab.GetComponentsInChildren<MeshFilter>(true)
                .Select(filter => filter.sharedMesh)
                .Where(mesh => mesh != null)
                .Distinct()
                .Sum(mesh => mesh.triangles.Length / 3);
            Assert.That(triangleCount, Is.InRange(12_000, 13_000));
        }

        [Test]
        public void IntegratedScene_UsesDoubleDoorAndPortalEffects()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Assert.That(GameObject.Find("CastleDoorArt"), Is.Not.Null);
            Assert.That(GameObject.Find("DoorPivot"), Is.Not.Null);
            Assert.That(GameObject.Find("DoorPivotSecondary"), Is.Not.Null);
            var hasPortalVfx = Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(component => component.GetType().Name == "SummonVfxController");
            Assert.That(hasPortalVfx, Is.True);
        }
    }
}

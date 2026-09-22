using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnimeAssistant.Tests.EditMode
{
    public sealed class M2AvatarAssetTests
    {
        private const string AvatarPath = "Assets/ThirdParty/UnityChan/Models/unitychan.fbx";
        private const string ScenePath = "Assets/_Project/Scenes/DesktopPetPrototype.unity";

        [Test]
        public void ProductionAvatar_ImportsAsHumanoidWithinGeometryAndMaterialBudget()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPath);
            Assert.That(prefab, Is.Not.Null, "Unity-Chan must import as a GameObject.");

            var animator = prefab.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isHuman, Is.True);
            Assert.That(animator.avatar.isValid, Is.True);

            var meshes = prefab.GetComponentsInChildren<MeshFilter>(true)
                .Select(filter => filter.sharedMesh)
                .Where(mesh => mesh != null)
                .Concat(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Select(renderer => renderer.sharedMesh)
                    .Where(mesh => mesh != null))
                .Distinct()
                .ToArray();
            var triangles = meshes.Sum(mesh => mesh.triangles.Length / 3);
            Assert.That(triangles, Is.InRange(10_000, 120_000));

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var sceneAvatar = GameObject.Find("UnityChanAvatar");
            Assert.That(sceneAvatar, Is.Not.Null, "The production scene must contain the licensed avatar.");
            var materials = sceneAvatar.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .Distinct()
                .Count();

            Assert.That(materials, Is.LessThanOrEqualTo(12));
        }
    }
}

using System.Collections;
using AnimeAssistant.Domain;
using AnimeAssistant.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AnimeAssistant.Tests.PlayMode
{
    public sealed class GreyboxCyclePlayModeTests
    {
        [UnityTest]
        public IEnumerator PresentationHarness_CompletesOneHundredCycles()
        {
            var fixture = CreateFixture();

            for (var cycle = 0; cycle < 100; cycle++)
            {
                fixture.Controller.SimulateDoorClick();
                Advance(fixture.Controller, 3.1f);
                Assert.That(fixture.Controller.State, Is.EqualTo(SummonState.AvatarActive), $"exit cycle {cycle + 1}");

                fixture.Controller.SimulateDoorClick();
                Advance(fixture.Controller, 4.3f);
                Assert.That(fixture.Controller.State, Is.EqualTo(SummonState.DoorClosed), $"return cycle {cycle + 1}");
            }

            Object.Destroy(fixture.Root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TwentyClickSpam_IsCoalescedIntoOneSafeReturn()
        {
            var fixture = CreateFixture();
            fixture.Controller.SimulateDoorClick();

            for (var click = 0; click < 20; click++)
            {
                fixture.Controller.SimulateDoorClick();
            }

            Assert.That(fixture.Controller.PendingReturn, Is.True);
            Assert.That(fixture.Controller.State, Is.EqualTo(SummonState.DoorOpening));
            Advance(fixture.Controller, 7.4f);
            Assert.That(fixture.Controller.State, Is.EqualTo(SummonState.DoorClosed));
            Assert.That(fixture.Controller.PendingReturn, Is.False);

            Object.Destroy(fixture.Root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator IntegratedProductionAvatarScene_CompletesOneHundredCycles()
        {
            SceneManager.LoadScene("DesktopPetPrototype", LoadSceneMode.Single);
            yield return null;

            var controller = Object.FindFirstObjectByType<GreyboxSummonController>();
            var avatar = GameObject.Find("UnityChanAvatar");
            Assert.That(controller, Is.Not.Null);
            Assert.That(avatar, Is.Not.Null);
            Assert.That(avatar.GetComponent<HumanoidAvatarMotion>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<SummonVfxController>(), Is.Not.Null);
            Assert.That(HumanoidAvatarMotion.BehaviourTemplateCount, Is.EqualTo(7));

            for (var cycle = 0; cycle < 100; cycle++)
            {
                controller.SimulateDoorClick();
                Advance(controller, 3.1f);
                Assert.That(controller.State, Is.EqualTo(SummonState.AvatarActive), $"avatar exit cycle {cycle + 1}");
                controller.SimulateDoorClick();
                Advance(controller, 4.3f);
                Assert.That(controller.State, Is.EqualTo(SummonState.DoorClosed), $"avatar return cycle {cycle + 1}");
            }
        }

        [UnityTest]
        public IEnumerator IntegratedDoor_AlternatesBetweenPortalAndRecallButton()
        {
            SceneManager.LoadScene("DesktopPetPrototype", LoadSceneMode.Single);
            yield return null;

            var controller = Object.FindFirstObjectByType<GreyboxSummonController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.IsDoorVisible, Is.True, "The full door must be visible on first launch.");
            Assert.That(controller.IsRecallButtonVisible, Is.False);
            Assert.That(controller.IsAvatarVisible, Is.False, "The avatar must be fully hidden behind the initial closed door.");

            controller.SimulateDoorClick();
            Advance(controller, 3.8f);
            Assert.That(controller.State, Is.EqualTo(SummonState.AvatarActive));
            Assert.That(controller.IsDoorVisible, Is.False, "The door must hide once the avatar is outside.");
            Assert.That(controller.IsRecallButtonVisible, Is.True, "A compact recall button must replace the door.");
            Assert.That(controller.IsAvatarVisible, Is.True);

            controller.SimulateDoorClick();
            Assert.That(controller.IsDoorVisible, Is.True, "Clicking recall must restore the door before return travel.");
            Assert.That(controller.IsRecallButtonVisible, Is.False);
            Advance(controller, 4.3f);
            Assert.That(controller.State, Is.EqualTo(SummonState.DoorClosed));
            Assert.That(controller.IsDoorVisible, Is.False, "After entry, the compact button remains for the next cycle.");
            Assert.That(controller.IsRecallButtonVisible, Is.True);
            Assert.That(controller.IsAvatarVisible, Is.False, "The avatar must disappear completely after entering.");

            controller.SimulateDoorClick();
            Assert.That(controller.State, Is.EqualTo(SummonState.DoorOpening));
            Assert.That(controller.IsDoorVisible, Is.True, "The next button click must restore and open the full door.");
            Assert.That(controller.IsRecallButtonVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator IntegratedAvatar_ReturnsFacingItsDirectionInsteadOfBackingUp()
        {
            SceneManager.LoadScene("DesktopPetPrototype", LoadSceneMode.Single);
            yield return null;

            var controller = Object.FindFirstObjectByType<GreyboxSummonController>();
            var motionRoot = GameObject.Find("HeroAvatarMotionRoot");
            controller.SimulateDoorClick();
            Advance(controller, 3.1f);
            Assert.That(controller.State, Is.EqualTo(SummonState.AvatarActive));

            controller.SimulateDoorClick();
            Advance(controller, 0.5f);
            var firstPosition = motionRoot.transform.position;
            Advance(controller, 0.2f);
            var travel = motionRoot.transform.position - firstPosition;
            travel.y = 0f;
            var visibleForward = -motionRoot.transform.forward;
            visibleForward.y = 0f;

            Assert.That(controller.State, Is.EqualTo(SummonState.AvatarReturning));
            Assert.That(travel.sqrMagnitude, Is.GreaterThan(0.001f));
            Assert.That(Vector3.Dot(visibleForward.normalized, travel.normalized), Is.GreaterThan(0.8f),
                "The avatar should turn and face the door while travelling, never slide backwards.");
        }

        [UnityTest]
        public IEnumerator IntegratedAvatar_CrossesCenteredThresholdOnlyAfterBothDoorLeavesOpen()
        {
            SceneManager.LoadScene("DesktopPetPrototype", LoadSceneMode.Single);
            yield return null;

            var controller = Object.FindFirstObjectByType<GreyboxSummonController>();
            var motionRoot = GameObject.Find("HeroAvatarMotionRoot");
            var primaryLeaf = GameObject.Find("DoorPivot");
            var secondaryLeaf = GameObject.Find("DoorPivotSecondary");

            Assert.That(controller, Is.Not.Null);
            Assert.That(motionRoot, Is.Not.Null);
            Assert.That(primaryLeaf, Is.Not.Null);
            Assert.That(secondaryLeaf, Is.Not.Null);

            controller.SimulateDoorClick();
            Advance(controller, 2.0f);

            Assert.That(controller.State, Is.EqualTo(SummonState.AvatarExiting));
            Assert.That(Quaternion.Angle(Quaternion.identity, primaryLeaf.transform.localRotation),
                Is.GreaterThan(90f), "The right leaf must clear the passage before the avatar crosses it.");
            Assert.That(Quaternion.Angle(Quaternion.identity, secondaryLeaf.transform.localRotation),
                Is.GreaterThan(90f), "The left leaf must clear the passage before the avatar crosses it.");
            Assert.That(motionRoot.transform.position.x, Is.EqualTo(1.72f).Within(0.2f),
                "The avatar must pass through the center of the double-door opening.");
            Assert.That(motionRoot.transform.position.z, Is.InRange(-0.9f, -0.45f),
                "The avatar should be at the doorway threshold during the clearance check.");
        }

        private static Fixture CreateFixture()
        {
            var root = new GameObject("GreyboxTestFixture");
            root.SetActive(false);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform);
            var doorPivot = new GameObject("DoorPivot").transform;
            doorPivot.SetParent(root.transform);
            var handle = new GameObject("Handle").transform;
            handle.SetParent(doorPivot);
            var avatar = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
            avatar.SetParent(root.transform);
            var light = new GameObject("PortalLight").AddComponent<Light>();
            light.transform.SetParent(root.transform);
            var clickZone = new GameObject("ClickZone").AddComponent<BoxCollider>();
            clickZone.transform.SetParent(root.transform);

            var controller = root.AddComponent<GreyboxSummonController>();
            controller.Configure(camera, doorPivot, handle, avatar, light, clickZone,
                new Vector3(1.7f, 1.2f, 0.55f),
                new Vector3(1.1f, 1.2f, -0.45f),
                new Vector3(-1.15f, 1.2f, -0.78f));
            root.SetActive(true);
            return new Fixture(root, controller);
        }

        private static void Advance(GreyboxSummonController controller, float duration)
        {
            const float step = 0.05f;
            for (var elapsed = 0f; elapsed < duration; elapsed += step)
            {
                controller.AdvanceSimulation(step);
            }
        }

        private readonly struct Fixture
        {
            public Fixture(GameObject root, GreyboxSummonController controller)
            {
                Root = root;
                Controller = controller;
            }

            public GameObject Root { get; }
            public GreyboxSummonController Controller { get; }
        }
    }
}

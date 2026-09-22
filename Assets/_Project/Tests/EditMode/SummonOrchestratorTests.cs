using System.Collections.Generic;
using AnimeAssistant.Domain;
using NUnit.Framework;

namespace AnimeAssistant.Tests.EditMode
{
    public sealed class SummonOrchestratorTests
    {
        [Test]
        public void CompleteCycle_FollowsTheRequiredTransitionOrder()
        {
            var machine = new SummonOrchestrator();
            var visited = new List<SummonState>();
            machine.Transitioned += transition => visited.Add(transition.To);

            CompleteCycle(machine);

            CollectionAssert.AreEqual(new[]
            {
                SummonState.DoorOpening,
                SummonState.AvatarExiting,
                SummonState.AvatarActive,
                SummonState.AvatarReturning,
                SummonState.AvatarEntering,
                SummonState.DoorClosing,
                SummonState.DoorClosed
            }, visited);
        }

        [TestCase(SummonEvent.AvatarExitCompleted)]
        [TestCase(SummonEvent.AvatarReadyToEnter)]
        [TestCase(SummonEvent.AvatarFullyOccluded)]
        [TestCase(SummonEvent.DoorCloseCompleted)]
        public void InvalidPresentationEvent_IsRejectedWithoutMutation(SummonEvent invalidEvent)
        {
            var machine = new SummonOrchestrator();

            Assert.That(machine.Handle(invalidEvent), Is.False);
            Assert.That(machine.State, Is.EqualTo(SummonState.DoorClosed));
        }

        [Test]
        public void ClickDuringOpening_IsCoalescedAndReturnsOnlyAfterExitCompletes()
        {
            var machine = new SummonOrchestrator();
            machine.OnDoorClicked();

            for (var index = 0; index < 20; index++)
            {
                machine.OnDoorClicked();
            }

            Assert.That(machine.State, Is.EqualTo(SummonState.DoorOpening));
            Assert.That(machine.PendingReturn, Is.True);
            machine.Handle(SummonEvent.DoorPassageClear);
            Assert.That(machine.State, Is.EqualTo(SummonState.AvatarExiting));
            machine.Handle(SummonEvent.AvatarExitCompleted);
            Assert.That(machine.State, Is.EqualTo(SummonState.AvatarReturning));
            Assert.That(machine.PendingReturn, Is.False);
        }

        [Test]
        public void ClickDuringExit_QueuesReturnWithoutUnsafeDirectTransition()
        {
            var machine = new SummonOrchestrator();
            machine.OnDoorClicked();
            machine.Handle(SummonEvent.DoorPassageClear);

            machine.OnDoorClicked();

            Assert.That(machine.State, Is.EqualTo(SummonState.AvatarExiting));
            machine.Handle(SummonEvent.AvatarExitCompleted);
            Assert.That(machine.State, Is.EqualTo(SummonState.AvatarReturning));
        }

        [Test]
        public void DoorTimeouts_RecoverToClosed()
        {
            var timeouts = new SummonTimeouts(0.1f, 0.1f, 0.1f, 0.1f, 0.1f);
            var machine = new SummonOrchestrator(timeouts);
            machine.OnDoorClicked();

            machine.Tick(0.11f);
            Assert.That(machine.State, Is.EqualTo(SummonState.DoorClosing));
            machine.Tick(0.11f);
            Assert.That(machine.State, Is.EqualTo(SummonState.DoorClosed));
        }

        [TestCase(SummonState.AvatarExiting)]
        [TestCase(SummonState.AvatarReturning)]
        [TestCase(SummonState.AvatarEntering)]
        public void UnsafeMissingEvents_EnterFaultedState(SummonState target)
        {
            var machine = MoveTo(target);

            machine.Tick(10f);

            Assert.That(machine.State, Is.EqualTo(SummonState.Faulted));
        }

        [Test]
        public void OneHundredAutomatedCycles_DoNotStickOrFault()
        {
            var machine = new SummonOrchestrator();

            for (var cycle = 0; cycle < 100; cycle++)
            {
                CompleteCycle(machine);
                Assert.That(machine.State, Is.EqualTo(SummonState.DoorClosed), $"cycle {cycle + 1}");
                Assert.That(machine.PendingReturn, Is.False, $"cycle {cycle + 1}");
            }
        }

        [Test]
        public void ResumeFromSuspended_RecoversToKnownClosedPose()
        {
            var machine = new SummonOrchestrator();
            machine.OnDoorClicked();

            Assert.That(machine.Suspend(), Is.True);
            Assert.That(machine.State, Is.EqualTo(SummonState.Suspended));
            Assert.That(machine.Resume(), Is.True);
            Assert.That(machine.State, Is.EqualTo(SummonState.DoorClosed));
        }

        private static void CompleteCycle(SummonOrchestrator machine)
        {
            Assert.That(machine.OnDoorClicked(), Is.True);
            Assert.That(machine.Handle(SummonEvent.DoorPassageClear), Is.True);
            Assert.That(machine.Handle(SummonEvent.AvatarExitCompleted), Is.True);
            Assert.That(machine.OnDoorClicked(), Is.True);
            Assert.That(machine.Handle(SummonEvent.AvatarReadyToEnter), Is.True);
            Assert.That(machine.Handle(SummonEvent.AvatarFullyOccluded), Is.True);
            Assert.That(machine.Handle(SummonEvent.DoorCloseCompleted), Is.True);
        }

        private static SummonOrchestrator MoveTo(SummonState target)
        {
            var machine = new SummonOrchestrator(new SummonTimeouts(1f, 1f, 1f, 1f, 1f));
            machine.OnDoorClicked();
            machine.Handle(SummonEvent.DoorPassageClear);
            if (target == SummonState.AvatarExiting) return machine;
            machine.Handle(SummonEvent.AvatarExitCompleted);
            machine.OnDoorClicked();
            if (target == SummonState.AvatarReturning) return machine;
            machine.Handle(SummonEvent.AvatarReadyToEnter);
            return machine;
        }
    }
}

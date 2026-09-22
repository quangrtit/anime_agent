using AnimeAssistant.Domain;
using NUnit.Framework;

namespace AnimeAssistant.Tests.EditMode
{
    public sealed class M0ArchitectureTests
    {
        [Test]
        public void SummonState_ContainsRequiredSafeAndFaultStates()
        {
            Assert.That(SummonState.DoorClosed, Is.Not.EqualTo(SummonState.Faulted));
            Assert.That(System.Enum.IsDefined(typeof(SummonState), SummonState.Suspended), Is.True);
            Assert.That(System.Enum.IsDefined(typeof(SummonState), SummonState.Faulted), Is.True);
        }

        [Test]
        public void ReturnHomeCommand_DefaultsToHighPriority()
        {
            Assert.That(new ReturnHomeCommand().Priority, Is.EqualTo(100));
        }
    }
}


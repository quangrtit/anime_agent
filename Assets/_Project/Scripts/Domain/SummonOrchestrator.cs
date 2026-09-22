using System;

namespace AnimeAssistant.Domain
{
    /// <summary>
    /// Authoritative, engine-independent summon lifecycle. Presentation reports named
    /// completion events; this class never inspects animator states or waits on wall time.
    /// </summary>
    public sealed class SummonOrchestrator
    {
        private readonly SummonTimeouts timeouts;

        public SummonOrchestrator() : this(new SummonTimeouts())
        {
        }

        public SummonOrchestrator(SummonTimeouts timeouts)
        {
            this.timeouts = timeouts ?? throw new ArgumentNullException(nameof(timeouts));
            State = SummonState.DoorClosed;
        }

        public event Action<SummonTransition> Transitioned = delegate { };

        public SummonState State { get; private set; }
        public bool PendingReturn { get; private set; }
        public float ElapsedInState { get; private set; }
        public SummonEvent? LastAcceptedEvent { get; private set; }
        public float TimeoutRemaining => Math.Max(0f, timeouts.For(State) - ElapsedInState);

        public bool OnDoorClicked()
        {
            switch (State)
            {
                case SummonState.DoorClosed:
                    TransitionTo(SummonState.DoorOpening, SummonTransitionReason.DoorClick, null);
                    return true;
                case SummonState.AvatarActive:
                    TransitionTo(SummonState.AvatarReturning, SummonTransitionReason.DoorClick, null);
                    return true;
                case SummonState.DoorOpening:
                case SummonState.AvatarExiting:
                    PendingReturn = true;
                    return true;
                default:
                    return false;
            }
        }

        public bool Handle(SummonEvent signal)
        {
            SummonState target;
            switch (State)
            {
                case SummonState.DoorOpening when signal == SummonEvent.DoorPassageClear:
                    target = SummonState.AvatarExiting;
                    break;
                case SummonState.AvatarExiting when signal == SummonEvent.AvatarExitCompleted:
                    target = SummonState.AvatarActive;
                    break;
                case SummonState.AvatarReturning when signal == SummonEvent.AvatarReadyToEnter:
                    target = SummonState.AvatarEntering;
                    break;
                case SummonState.AvatarEntering when signal == SummonEvent.AvatarFullyOccluded:
                    target = SummonState.DoorClosing;
                    break;
                case SummonState.DoorClosing when signal == SummonEvent.DoorCloseCompleted:
                    target = SummonState.DoorClosed;
                    break;
                default:
                    return false;
            }

            LastAcceptedEvent = signal;
            TransitionTo(target, SummonTransitionReason.PresentationEvent, signal);

            if (target == SummonState.AvatarActive && PendingReturn)
            {
                PendingReturn = false;
                TransitionTo(SummonState.AvatarReturning, SummonTransitionReason.PendingReturn, signal);
            }

            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds < 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }

            if (State == SummonState.Suspended || State == SummonState.Faulted)
            {
                return;
            }

            ElapsedInState += deltaSeconds;
            if (ElapsedInState < timeouts.For(State))
            {
                return;
            }

            switch (State)
            {
                case SummonState.DoorOpening:
                    TransitionTo(SummonState.DoorClosing, SummonTransitionReason.TimeoutRecovery, null);
                    break;
                case SummonState.DoorClosing:
                    TransitionTo(SummonState.DoorClosed, SummonTransitionReason.TimeoutRecovery, null);
                    break;
                case SummonState.AvatarExiting:
                case SummonState.AvatarReturning:
                case SummonState.AvatarEntering:
                    TransitionTo(SummonState.Faulted, SummonTransitionReason.Fault, null);
                    break;
            }
        }

        public bool Suspend()
        {
            if (State == SummonState.Suspended || State == SummonState.Faulted)
            {
                return false;
            }

            TransitionTo(SummonState.Suspended, SummonTransitionReason.Suspended, null);
            return true;
        }

        public bool Resume()
        {
            if (State != SummonState.Suspended)
            {
                return false;
            }

            TransitionTo(SummonState.DoorClosed, SummonTransitionReason.Resumed, null);
            return true;
        }

        private void TransitionTo(
            SummonState next,
            SummonTransitionReason reason,
            SummonEvent? sourceEvent)
        {
            var previous = State;
            State = next;
            ElapsedInState = 0f;

            if (next == SummonState.DoorClosed || next == SummonState.Faulted || next == SummonState.Suspended)
            {
                PendingReturn = false;
            }

            Transitioned(new SummonTransition(previous, next, reason, sourceEvent));
        }
    }
}

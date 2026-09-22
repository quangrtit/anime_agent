namespace AnimeAssistant.Domain
{
    public enum SummonTransitionReason
    {
        DoorClick,
        PresentationEvent,
        PendingReturn,
        TimeoutRecovery,
        Suspended,
        Resumed,
        Fault
    }

    public readonly struct SummonTransition
    {
        public SummonTransition(
            SummonState from,
            SummonState to,
            SummonTransitionReason reason,
            SummonEvent? sourceEvent)
        {
            From = from;
            To = to;
            Reason = reason;
            SourceEvent = sourceEvent;
        }

        public SummonState From { get; }
        public SummonState To { get; }
        public SummonTransitionReason Reason { get; }
        public SummonEvent? SourceEvent { get; }
    }
}

namespace AnimeAssistant.Domain
{
    /// <summary>The authoritative lifecycle state shared by presentation and platform adapters.</summary>
    public enum SummonState
    {
        DoorClosed,
        DoorOpening,
        AvatarExiting,
        AvatarActive,
        AvatarReturning,
        AvatarEntering,
        DoorClosing,
        Suspended,
        Faulted
    }
}


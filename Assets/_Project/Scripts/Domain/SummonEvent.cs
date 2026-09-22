namespace AnimeAssistant.Domain
{
    /// <summary>Named presentation events accepted by the summon state machine.</summary>
    public enum SummonEvent
    {
        DoorPassageClear,
        AvatarExitCompleted,
        AvatarReadyToEnter,
        AvatarFullyOccluded,
        DoorCloseCompleted
    }
}

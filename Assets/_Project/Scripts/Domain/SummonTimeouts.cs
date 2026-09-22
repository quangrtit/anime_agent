using System;

namespace AnimeAssistant.Domain
{
    public sealed class SummonTimeouts
    {
        public SummonTimeouts(
            float doorOpeningSeconds = 3f,
            float avatarExitingSeconds = 4f,
            float avatarReturningSeconds = 4f,
            float avatarEnteringSeconds = 4f,
            float doorClosingSeconds = 3f)
        {
            DoorOpeningSeconds = Positive(doorOpeningSeconds, nameof(doorOpeningSeconds));
            AvatarExitingSeconds = Positive(avatarExitingSeconds, nameof(avatarExitingSeconds));
            AvatarReturningSeconds = Positive(avatarReturningSeconds, nameof(avatarReturningSeconds));
            AvatarEnteringSeconds = Positive(avatarEnteringSeconds, nameof(avatarEnteringSeconds));
            DoorClosingSeconds = Positive(doorClosingSeconds, nameof(doorClosingSeconds));
        }

        public float DoorOpeningSeconds { get; }
        public float AvatarExitingSeconds { get; }
        public float AvatarReturningSeconds { get; }
        public float AvatarEnteringSeconds { get; }
        public float DoorClosingSeconds { get; }

        public float For(SummonState state)
        {
            switch (state)
            {
                case SummonState.DoorOpening: return DoorOpeningSeconds;
                case SummonState.AvatarExiting: return AvatarExitingSeconds;
                case SummonState.AvatarReturning: return AvatarReturningSeconds;
                case SummonState.AvatarEntering: return AvatarEnteringSeconds;
                case SummonState.DoorClosing: return DoorClosingSeconds;
                default: return float.PositiveInfinity;
            }
        }

        private static float Positive(float value, string name)
        {
            if (value <= 0f || float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, "Timeouts must be finite and greater than zero.");
            }

            return value;
        }
    }
}

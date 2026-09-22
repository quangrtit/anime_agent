namespace AnimeAssistant.Domain
{
    public abstract class AvatarCommand
    {
        protected AvatarCommand(int priority)
        {
            Priority = priority;
        }

        public int Priority { get; }
    }

    public sealed class PlayBehaviourCommand : AvatarCommand
    {
        public PlayBehaviourCommand(string name, int priority) : base(priority)
        {
            Name = name;
        }

        public string Name { get; }
    }

    public sealed class ReturnHomeCommand : AvatarCommand
    {
        public ReturnHomeCommand(int priority = 100) : base(priority) { }
    }

    public interface IAvatarCommandBus
    {
        void Publish(AvatarCommand command);
    }
}


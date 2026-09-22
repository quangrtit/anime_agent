namespace AnimeAssistant.Platform
{
    public readonly struct SurfaceBounds
    {
        public SurfaceBounds(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
    }

    public interface IAvatarSurface
    {
        void Show();
        void Hide();
        void SetInteractionEnabled(bool enabled);
        SurfaceBounds GetBounds();
    }
}


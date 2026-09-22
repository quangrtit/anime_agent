using UnityEngine;

namespace AnimeAssistant.Platform
{
    public interface IDesktopOverlay
    {
        void Initialize();
        void SetTopmost(bool value);
        void SetInteractiveRegion(RectInt screenRegion);
        void SetClickThrough(bool value);
        RectInt GetWorkAreaForCurrentMonitor();
        float GetDpiScaleForCurrentMonitor();
        void MoveAndResize(RectInt screenRect);
        void SuspendRendering();
        void ResumeRendering();
    }
}


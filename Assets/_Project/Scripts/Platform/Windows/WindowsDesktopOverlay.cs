using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using AnimeAssistant.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimeAssistant.Platform.Windows
{
    /// <summary>
    /// Standalone-Windows adapter for the compact desktop overlay. Win32 calls stay
    /// isolated in this assembly; the Unity Editor intentionally keeps its normal
    /// opaque Game view for art authoring.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class WindowsDesktopOverlay : MonoBehaviour, IDesktopOverlay
    {
        private const int FallbackWidth = 960;
        private const int FallbackHeight = 540;
        private const int GwlStyle = -16;
        private const int GwlExStyle = -20;
        private const long WsCaption = 0x00C00000L;
        private const long WsThickFrame = 0x00040000L;
        private const long WsMinimizeBox = 0x00020000L;
        private const long WsMaximizeBox = 0x00010000L;
        private const long WsSysMenu = 0x00080000L;
        private const long WsPopup = unchecked((long)0x80000000);
        private const long WsExLayered = 0x00080000L;
        private const long WsExToolWindow = 0x00000080L;
        private const long WsExTransparent = 0x00000020L;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpFrameChanged = 0x0020;
        private const uint SwpShowWindow = 0x0040;
        private const uint MonitorDefaultToNearest = 0x00000002;
        private const uint LwaAlpha = 0x00000002;
        private const uint WmHotkey = 0x0312;
        private const uint PmRemove = 0x0001;
        private const uint ModControl = 0x0002;
        private const uint ModShift = 0x0004;
        private const uint ModNoRepeat = 0x4000;
        private const int ExitHotkeyId = 0xADA1;
        private const int SwHide = 0;
        private const int SwShowNoActivate = 4;
        private static readonly IntPtr HwndTopmost = new IntPtr(-1);
        private static readonly IntPtr HwndNotTopmost = new IntPtr(-2);

        private readonly List<Renderer> doorRenderers = new List<Renderer>();
        private readonly List<Renderer> avatarRenderers = new List<Renderer>();
        private IntPtr windowHandle;
        private Camera overlayCamera;
        private GreyboxSummonController summonController;
        private RectInt interactiveRegion;
        private RectInt targetWindowRect;
        private bool initialized;
        private bool sceneConfigured;
        private bool desktopCompositionApplied;
        private bool contentReady;
        private bool revealQueued;
        private bool clickThrough;
        private float hitTestCountdown;
        private float rendererRefreshCountdown;
        private float windowStabilizationRemaining;
        private float windowRefreshCountdown;
        private bool cursorOverInteractiveContent;
        private bool cursorOverDoor;
        private bool exitHotkeyWasDown;
        private bool leftMouseWasDown;
        private bool rightMouseWasDown;
        private bool hotkeyRegistered;
        private int compositionScreenWidth;
        private int compositionScreenHeight;

        public bool IsInitialized => initialized;
        public bool IsClickThrough => clickThrough;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void CreateRuntimeAdapter()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (FindFirstObjectByType<WindowsDesktopOverlay>() != null)
            {
                return;
            }

            var host = new GameObject("Windows Desktop Overlay");
            DontDestroyOnLoad(host);
            host.AddComponent<WindowsDesktopOverlay>();
#endif
        }

        private void Awake()
        {
            Application.runInBackground = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            Initialize();
#endif
        }

        private void Start()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            Initialize();
#endif
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (hotkeyRegistered)
            {
                UnregisterHotKey(IntPtr.Zero, ExitHotkeyId);
            }
#endif
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StopAllCoroutines();
            contentReady = false;
            revealQueued = false;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (windowHandle != IntPtr.Zero)
            {
                SetLayeredWindowAttributes(windowHandle, 0, 0, LwaAlpha);
                ShowWindow(windowHandle, SwHide);
            }
#endif
            sceneConfigured = false;
            desktopCompositionApplied = false;
        }

        private void Update()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!initialized)
            {
                Initialize();
                return;
            }

            if (desktopCompositionApplied &&
                (compositionScreenWidth != Screen.width || compositionScreenHeight != Screen.height))
            {
                desktopCompositionApplied = false;
            }

            if (!sceneConfigured || !desktopCompositionApplied)
            {
                ConfigureTransparentScene();
            }

            if (windowStabilizationRemaining > 0f)
            {
                windowStabilizationRemaining -= Time.unscaledDeltaTime;
                windowRefreshCountdown -= Time.unscaledDeltaTime;
                if (windowRefreshCountdown <= 0f)
                {
                    windowRefreshCountdown = 0.2f;
                    ApplyTransparentWindowFrameAndPlacement();
                }
            }

            hitTestCountdown -= Time.unscaledDeltaTime;
            if (sceneConfigured && hitTestCountdown <= 0f)
            {
                hitTestCountdown = 1f / 30f;
                UpdateSelectiveHitTest();
            }

            HandleExitInput();
#endif
        }

        public void Initialize()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (initialized)
            {
                return;
            }

            var process = Process.GetCurrentProcess();
            process.Refresh();
            windowHandle = process.MainWindowHandle;
            if (windowHandle == IntPtr.Zero)
            {
                return;
            }

            // Never expose Unity's default opaque player surface. The window is
            // revealed only after the transparent camera and desktop layout exist.
            ShowWindow(windowHandle, SwHide);

            var style = GetWindowLongPtr(windowHandle, GwlStyle).ToInt64();
            style &= ~(WsCaption | WsThickFrame | WsMinimizeBox | WsMaximizeBox | WsSysMenu);
            style |= WsPopup;
            SetWindowLongPtr(windowHandle, GwlStyle, new IntPtr(style));

            var extended = GetWindowLongPtr(windowHandle, GwlExStyle).ToInt64();
            extended |= WsExLayered | WsExToolWindow;
            SetWindowLongPtr(windowHandle, GwlExStyle, new IntPtr(extended));
            SetLayeredWindowAttributes(windowHandle, 0, 0, LwaAlpha);

            var margins = new Margins(-1, -1, -1, -1);
            DwmExtendFrameIntoClientArea(windowHandle, ref margins);
            targetWindowRect = GetWorkAreaForCurrentMonitor();
            if (targetWindowRect.width <= 0 || targetWindowRect.height <= 0)
            {
                targetWindowRect = new RectInt(0, 0, FallbackWidth, FallbackHeight);
            }
            Screen.SetResolution(targetWindowRect.width, targetWindowRect.height, FullScreenMode.Windowed);
            initialized = true;
            hotkeyRegistered = RegisterHotKey(IntPtr.Zero, ExitHotkeyId,
                ModControl | ModShift | ModNoRepeat, 0x51);
            windowStabilizationRemaining = 2.5f;
            windowRefreshCountdown = 0f;
            ApplyTransparentWindowFrameAndPlacement();
#endif
        }

        private void ApplyTransparentWindowFrameAndPlacement()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var currentHandle = Process.GetCurrentProcess().MainWindowHandle;
            if (currentHandle != IntPtr.Zero)
            {
                windowHandle = currentHandle;
            }
            if (windowHandle == IntPtr.Zero)
            {
                return;
            }

            var style = GetWindowLongPtr(windowHandle, GwlStyle).ToInt64();
            style &= ~(WsCaption | WsThickFrame | WsMinimizeBox | WsMaximizeBox | WsSysMenu);
            style |= WsPopup;
            SetWindowLongPtr(windowHandle, GwlStyle, new IntPtr(style));
            var extended = GetWindowLongPtr(windowHandle, GwlExStyle).ToInt64();
            extended |= WsExLayered | WsExToolWindow;
            SetWindowLongPtr(windowHandle, GwlExStyle, new IntPtr(extended));
            var margins = new Margins(-1, -1, -1, -1);
            DwmExtendFrameIntoClientArea(windowHandle, ref margins);
            SetLayeredWindowAttributes(windowHandle, 0, contentReady ? (byte)255 : (byte)0, LwaAlpha);
            var refreshedWorkArea = GetWorkAreaForCurrentMonitor();
            if (!refreshedWorkArea.Equals(targetWindowRect))
            {
                targetWindowRect = refreshedWorkArea;
                desktopCompositionApplied = false;
            }
            if (overlayCamera != null)
            {
                overlayCamera.aspect = targetWindowRect.width / (float)Mathf.Max(1, targetWindowRect.height);
            }
            MoveAndResize(targetWindowRect);
            SetTopmost(true);
#endif
        }

        public void SetTopmost(bool value)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (windowHandle == IntPtr.Zero)
            {
                return;
            }

            SetWindowPos(windowHandle, value ? HwndTopmost : HwndNotTopmost,
                0, 0, 0, 0, SwpNoActivate | SwpFrameChanged |
                (contentReady ? SwpShowWindow : 0u) | 0x0001 | 0x0002);
#endif
        }

        public void SetInteractiveRegion(RectInt screenRegion)
        {
            interactiveRegion = screenRegion;
        }

        public void SetClickThrough(bool value)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (windowHandle == IntPtr.Zero || clickThrough == value)
            {
                return;
            }

            var extended = GetWindowLongPtr(windowHandle, GwlExStyle).ToInt64();
            extended = value ? extended | WsExTransparent : extended & ~WsExTransparent;
            SetWindowLongPtr(windowHandle, GwlExStyle, new IntPtr(extended));
            SetWindowPos(windowHandle, IntPtr.Zero, 0, 0, 0, 0,
                SwpNoActivate | SwpFrameChanged | 0x0001 | 0x0002 | 0x0004);
            clickThrough = value;
#endif
        }

        public RectInt GetWorkAreaForCurrentMonitor()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var monitor = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
            {
                return new RectInt(info.Work.Left, info.Work.Top,
                    info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top);
            }
#endif
            return new RectInt(0, 0, Display.main.systemWidth, Display.main.systemHeight);
        }

        public float GetDpiScaleForCurrentMonitor()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (windowHandle != IntPtr.Zero)
            {
                try
                {
                    return GetDpiForWindow(windowHandle) / 96f;
                }
                catch (EntryPointNotFoundException)
                {
                    return 1f;
                }
            }
#endif
            return 1f;
        }

        public void MoveAndResize(RectInt screenRect)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (windowHandle != IntPtr.Zero)
            {
                SetWindowPos(windowHandle, HwndTopmost, screenRect.x, screenRect.y,
                    screenRect.width, screenRect.height, SwpNoActivate | SwpFrameChanged |
                    (contentReady ? SwpShowWindow : 0u));
            }
#endif
        }

        public void SuspendRendering()
        {
            Application.targetFrameRate = 1;
        }

        public void ResumeRendering()
        {
            Application.targetFrameRate = 60;
        }

        private void ConfigureTransparentScene()
        {
            overlayCamera = Camera.main;
            summonController = FindFirstObjectByType<GreyboxSummonController>();
            if (overlayCamera == null)
            {
                return;
            }

            foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.aspect = targetWindowRect.width / (float)Mathf.Max(1, targetWindowRect.height);
            }

            DisableBackdrop("Ground");
            DisableBackdrop("BackPlate");
            DisableSurfaceRenderer("PortalGlow");
            RenderSettings.skybox = null;

            doorRenderers.Clear();
            avatarRenderers.Clear();
            CacheRenderers("M1_Greybox_Set", doorRenderers);
            CacheRenderers("HeroAvatarMotionRoot", avatarRenderers);
            sceneConfigured = doorRenderers.Count > 0 && avatarRenderers.Count > 0;
            if (sceneConfigured && !desktopCompositionApplied)
            {
                ApplyDesktopComposition();
            }
        }

        private void ApplyDesktopComposition()
        {
            var doorSet = GameObject.Find("M1_Greybox_Set")?.transform;
            var avatarRoot = GameObject.Find("HeroAvatarMotionRoot")?.transform;
            var controller = FindFirstObjectByType<GreyboxSummonController>();
            if (doorSet == null || avatarRoot == null || controller == null || overlayCamera == null)
            {
                return;
            }

            if (Screen.width != targetWindowRect.width || Screen.height != targetWindowRect.height)
            {
                return;
            }

            var config = DesktopExperienceConfig.Current;
            var compactScale = config.displayScale;
            doorSet.localScale = Vector3.one * compactScale;
            avatarRoot.localScale = Vector3.one * compactScale;
            Physics.SyncTransforms();

            if (!TryGetProjectedBounds(doorRenderers, out var doorBounds))
            {
                return;
            }

            var currentCenter = doorBounds.center;
            var desiredCenter = new Vector2(
                Screen.width * config.doorAnchorViewportX,
                Screen.height * config.doorAnchorViewportY);
            var depth = overlayCamera.WorldToScreenPoint(CalculateWorldBounds(doorRenderers).center).z;
            if (depth <= 0f)
            {
                return;
            }

            var currentWorld = overlayCamera.ScreenToWorldPoint(
                new Vector3(currentCenter.x, currentCenter.y, depth));
            var desiredWorld = overlayCamera.ScreenToWorldPoint(
                new Vector3(desiredCenter.x, desiredCenter.y, depth));
            var doorTranslation = desiredWorld - currentWorld;
            doorSet.position += doorTranslation;
            Physics.SyncTransforms();

            // Face the camera after the preliminary right-edge placement. The
            // required yaw is larger at the edge than it is near scene centre.
            var anchoredDoorCenter = CalculateWorldBounds(doorRenderers).center;
            var towardCamera = overlayCamera.transform.position - anchoredDoorCenter;
            towardCamera.y = 0f;
            if (towardCamera.sqrMagnitude > 0.0001f)
            {
                // The asset's visible front is local -Z. Rotating +Z away from
                // the camera makes the door face the viewer squarely at any X.
                doorSet.rotation = Quaternion.LookRotation(-towardCamera.normalized, Vector3.up);
                Physics.SyncTransforms();

                // Rotation changes the projected footprint, so perform a final
                // pixel-space correction to preserve the configured screen anchor.
                if (TryGetProjectedBounds(doorRenderers, out var rotatedBounds))
                {
                    currentCenter = rotatedBounds.center;
                    desiredCenter = new Vector2(
                        Screen.width * config.doorAnchorViewportX,
                        Screen.height * config.doorAnchorViewportY);
                    depth = overlayCamera.WorldToScreenPoint(CalculateWorldBounds(doorRenderers).center).z;
                    currentWorld = overlayCamera.ScreenToWorldPoint(
                        new Vector3(currentCenter.x, currentCenter.y, depth));
                    desiredWorld = overlayCamera.ScreenToWorldPoint(
                        new Vector3(desiredCenter.x, desiredCenter.y, depth));
                    doorSet.position += desiredWorld - currentWorld;
                    Physics.SyncTransforms();
                }
            }

            // These are the original choreography markers expressed in the
            // door-set's local space. Transforming them after scale/rotation
            // keeps the character centered in the resized doorway.
            var hiddenPosition = doorSet.TransformPoint(new Vector3(1.72f, 0.10f, 0.72f));
            var thresholdPosition = doorSet.TransformPoint(new Vector3(1.72f, 0.10f, -0.68f));
            var activeBasis = doorSet.TransformPoint(new Vector3(1.72f, 0.10f, -0.78f));
            var activeScreen = overlayCamera.WorldToScreenPoint(activeBasis);
            var desiredActive = overlayCamera.ScreenToWorldPoint(new Vector3(
                Screen.width * config.avatarStartViewportX, activeScreen.y, activeScreen.z));
            controller.ConfigureDesktopLayout(hiddenPosition, thresholdPosition, desiredActive);
            Physics.SyncTransforms();
            desktopCompositionApplied = true;
            compositionScreenWidth = Screen.width;
            compositionScreenHeight = Screen.height;
            TryGetProjectedBounds(doorRenderers, out var finalDoorBounds);
            UnityEngine.Debug.Log($"[DesktopLayout] Scale={compactScale:F3}, " +
                                  $"doorAnchor=({config.doorAnchorViewportX:F3}, {config.doorAnchorViewportY:F3}), " +
                                  $"roaming viewport={config.roamMinViewportX:F2}..{config.roamMaxViewportX:F2} " +
                                  $"depth={config.roamNearDepth:F2}..{config.roamFarDepth:F2}, " +
                                  $"workArea={targetWindowRect.width}x{targetWindowRect.height}, " +
                                  $"doorPixels={finalDoorBounds}.");
            if (!revealQueued)
            {
                revealQueued = true;
                StartCoroutine(RevealAfterTransparentFrames());
            }
        }

        private IEnumerator RevealAfterTransparentFrames()
        {
            // Render two complete transparent frames while the native window is
            // hidden. This prevents DWM from ever presenting Unity's initial white buffer.
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            contentReady = true;
            ApplyTransparentWindowFrameAndPlacement();
            ShowWindow(windowHandle, SwShowNoActivate);
            UnityEngine.Debug.Log("[DesktopOverlay] Transparent framebuffer ready; native window revealed.");
        }

        private bool TryGetProjectedBounds(IReadOnlyList<Renderer> renderers, out Rect bounds)
        {
            var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var found = false;
            foreach (var renderer in renderers)
            {
                // Particle bounds describe the whole simulated effect volume and can
                // be thousands of pixels larger than the physical portal geometry.
                // They must never participate in desktop anchoring calculations.
                if (renderer == null || renderer is ParticleSystemRenderer ||
                    !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var worldBounds = renderer.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var world = worldBounds.center + Vector3.Scale(worldBounds.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f));
                    var screen = overlayCamera.WorldToScreenPoint(world);
                    if (screen.z <= 0f)
                    {
                        continue;
                    }

                    minimum = Vector2.Min(minimum, screen);
                    maximum = Vector2.Max(maximum, screen);
                    found = true;
                }
            }

            bounds = found
                ? Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y)
                : default;
            return found;
        }

        private static Bounds CalculateWorldBounds(IReadOnlyList<Renderer> renderers)
        {
            var found = false;
            var combined = default(Bounds);
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer is ParticleSystemRenderer ||
                    !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!found)
                {
                    combined = renderer.bounds;
                    found = true;
                }
                else
                {
                    combined.Encapsulate(renderer.bounds);
                }
            }

            return combined;
        }

        private static void DisableBackdrop(string objectName)
        {
            var target = GameObject.Find(objectName);
            if (target != null)
            {
                target.SetActive(false);
            }
        }

        private static void DisableSurfaceRenderer(string objectName)
        {
            var target = GameObject.Find(objectName);
            if (target != null && target.TryGetComponent<Renderer>(out var surface))
            {
                // Keep the transform active because portal particles use it as
                // their anchor; only remove the opaque cyan panel behind the door.
                surface.enabled = false;
            }
        }

        private static void CacheRenderers(string rootName, List<Renderer> destination)
        {
            var root = GameObject.Find(rootName);
            if (root != null)
            {
                destination.AddRange(root.GetComponentsInChildren<Renderer>(true));
            }
        }

        private void UpdateSelectiveHitTest()
        {
            rendererRefreshCountdown -= 1f / 30f;
            if (rendererRefreshCountdown <= 0f)
            {
                rendererRefreshCountdown = 0.75f;
                avatarRenderers.Clear();
                CacheRenderers("HeroAvatarMotionRoot", avatarRenderers);
            }

            if (!GetCursorPos(out var cursor) || !GetWindowRect(windowHandle, out var windowRect))
            {
                return;
            }

            var overDoor = TryProjectBounds(doorRenderers, windowRect, out var doorRegion) &&
                           doorRegion.Contains(new Vector2Int(cursor.X, cursor.Y));
            var overAvatar = TryProjectBounds(avatarRenderers, windowRect, out var avatarRegion) &&
                             avatarRegion.Contains(new Vector2Int(cursor.X, cursor.Y));
            cursorOverDoor = overDoor;
            cursorOverInteractiveContent = overDoor || overAvatar;
            if (TryUnion(doorRegion, avatarRegion, out var union))
            {
                SetInteractiveRegion(union);
            }

            SetClickThrough(!cursorOverInteractiveContent);
        }

        private void HandleExitInput()
        {
            var controlDown = (GetAsyncKeyState(0x11) & 0x8000) != 0;
            var shiftDown = (GetAsyncKeyState(0x10) & 0x8000) != 0;
            var qDown = (GetAsyncKeyState(0x51) & 0x8000) != 0;
            var exitHotkeyDown = controlDown && shiftDown && qDown;
            var leftMouseDown = (GetAsyncKeyState(0x01) & 0x8000) != 0;
            var rightMouseDown = (GetAsyncKeyState(0x02) & 0x8000) != 0;
            var hotkeyPressed = PeekMessage(out _, IntPtr.Zero, WmHotkey, WmHotkey, PmRemove);
            if (leftMouseDown && !leftMouseWasDown && cursorOverDoor)
            {
                summonController?.HandleUserDoorClick();
            }
            if (hotkeyPressed || (exitHotkeyDown && !exitHotkeyWasDown) ||
                (rightMouseDown && !rightMouseWasDown && cursorOverInteractiveContent))
            {
                Application.Quit();
            }

            exitHotkeyWasDown = exitHotkeyDown;
            leftMouseWasDown = leftMouseDown;
            rightMouseWasDown = rightMouseDown;
        }

        private bool TryProjectBounds(IReadOnlyList<Renderer> renderers, NativeRect windowRect,
            out RectInt projected)
        {
            var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var physicalScaleX = (windowRect.Right - windowRect.Left) / (float)Mathf.Max(1, Screen.width);
            var physicalScaleY = (windowRect.Bottom - windowRect.Top) / (float)Mathf.Max(1, Screen.height);
            var found = false;
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer is ParticleSystemRenderer ||
                    !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var bounds = renderer.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var world = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f));
                    var screen = overlayCamera.WorldToScreenPoint(world);
                    if (screen.z <= 0f)
                    {
                        continue;
                    }

                    var native = new Vector2(windowRect.Left + screen.x * physicalScaleX,
                        windowRect.Top + (Screen.height - screen.y) * physicalScaleY);
                    minimum = Vector2.Min(minimum, native);
                    maximum = Vector2.Max(maximum, native);
                    found = true;
                }
            }

            if (!found)
            {
                projected = default;
                return false;
            }

            const int padding = 18;
            projected = new RectInt(
                Mathf.FloorToInt(minimum.x) - padding,
                Mathf.FloorToInt(minimum.y) - padding,
                Mathf.CeilToInt(maximum.x - minimum.x) + padding * 2,
                Mathf.CeilToInt(maximum.y - minimum.y) + padding * 2);
            return true;
        }

        private static bool TryUnion(RectInt first, RectInt second, out RectInt union)
        {
            if (first.width <= 0 && second.width <= 0)
            {
                union = default;
                return false;
            }

            if (first.width <= 0)
            {
                union = second;
                return true;
            }

            if (second.width <= 0)
            {
                union = first;
                return true;
            }

            var xMin = Mathf.Min(first.xMin, second.xMin);
            var yMin = Mathf.Min(first.yMin, second.yMin);
            var xMax = Mathf.Max(first.xMax, second.xMax);
            var yMax = Mathf.Max(first.yMax, second.yMax);
            union = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
            return true;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Margins
        {
            public Margins(int left, int right, int top, int bottom)
            {
                Left = left;
                Right = right;
                Top = top;
                Bottom = bottom;
            }

            public int Left;
            public int Right;
            public int Top;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MonitorInfo
        {
            public int Size;
            public NativeRect Monitor;
            public NativeRect Work;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMessage
        {
            public IntPtr Window;
            public uint Message;
            public UIntPtr WParam;
            public IntPtr LParam;
            public uint Time;
            public NativePoint Point;
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y,
            int width, int height, uint flags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr window, uint colorKey, byte alpha, uint flags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out NativePoint point);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out NativeRect rectangle);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr window, int id);

        [DllImport("user32.dll")]
        private static extern bool PeekMessage(out NativeMessage message, IntPtr window,
            uint minimumMessage, uint maximumMessage, uint removeMessage);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);
    }
}

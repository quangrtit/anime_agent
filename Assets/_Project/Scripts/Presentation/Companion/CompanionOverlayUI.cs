using AnimeAssistant.Domain;
using UnityEngine;

namespace AnimeAssistant.Presentation
{
    /// <summary>
    /// IMGUI layer for the companion extras: the tsundere diary booklet, the
    /// reminder board, the pomodoro chip and the yandere vignette. Purely
    /// decorative — the overlay window stays click-through everywhere except
    /// the door/avatar hit zones, so these never eat user input.
    /// </summary>
    [DefaultExecutionOrder(9150)]
    public sealed class CompanionOverlayUI : MonoBehaviour
    {
        private static Font uiFont;
        private static GUIStyle titleStyle;
        private static GUIStyle bodyStyle;
        private static GUIStyle chipStyle;
        private static GUIStyle boardStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateRuntimeOverlay()
        {
            if (FindFirstObjectByType<CompanionOverlayUI>() != null)
            {
                return;
            }

            var host = new GameObject("Companion Overlay UI");
            DontDestroyOnLoad(host);
            host.AddComponent<CompanionOverlayUI>();
        }

        private void OnGUI()
        {
            var director = CompanionDirector.Instance;
            if (director == null)
            {
                return;
            }

            EnsureStyles();
            GUI.depth = -900;
            DrawIconGlass(director);
            DrawPomodoroChip(director);
            DrawReminderBoard(director);
            DrawLiveBadge(director);
            DrawDiaryBook(director);
            DrawYandereVignette(director);
        }

        /// <summary>
        /// The "glass pane" over the desktop icons: a translucent sheet that
        /// tells the user clicks here are held by the companion; hovering an
        /// icon lights up its cell and label.
        /// </summary>
        private static void DrawIconGlass(CompanionDirector director)
        {
            var glass = director.IconGlassPhysical;
            if (!glass.HasValue)
            {
                return;
            }

            var rect = PhysicalToGui(glass.Value);
            // Pane body: two soft layers so it reads as glass, not a grey box.
            DrawPanel(rect, new Color(0.65f, 0.82f, 1f, 0.05f));
            DrawPanel(new Rect(rect.x + rect.width * 0.25f, rect.y, rect.width * 0.75f, rect.height),
                new Color(1f, 1f, 1f, 0.03f));
            // Border.
            DrawPanel(new Rect(rect.x, rect.y, rect.width, 2f), new Color(1f, 1f, 1f, 0.30f));
            DrawPanel(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), new Color(1f, 1f, 1f, 0.18f));
            DrawPanel(new Rect(rect.x, rect.y, 2f, rect.height), new Color(1f, 1f, 1f, 0.22f));
            DrawPanel(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), new Color(1f, 1f, 1f, 0.12f));
            // Diagonal shine.
            DrawPanel(new Rect(rect.x + rect.width * 0.16f, rect.y, rect.width * 0.035f, rect.height),
                new Color(1f, 1f, 1f, 0.06f));
            DrawPanel(new Rect(rect.x + rect.width * 0.24f, rect.y, rect.width * 0.012f, rect.height),
                new Color(1f, 1f, 1f, 0.05f));
            // Corner caption.
            var captionStyle = new GUIStyle(bodyStyle)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.9f, 0.96f, 1f, 0.55f) }
            };
            GUI.Label(new Rect(rect.x + 8f, rect.y + 4f, rect.width - 16f, 18f),
                "Đang qua lớp kính — Airi sẽ mở giúp anh", captionStyle);

            // Hover highlight + label chip.
            if (director.IconHoverActive)
            {
                var hover = PhysicalToGui(director.IconHoverPhysical);
                DrawPanel(hover, new Color(1f, 1f, 1f, 0.12f));
                DrawPanel(new Rect(hover.x, hover.y, hover.width, 2f), new Color(1f, 1f, 1f, 0.5f));
                if (!string.IsNullOrEmpty(director.IconHoverLabel))
                {
                    var chipStyle = new GUIStyle(bodyStyle)
                    {
                        fontSize = 11,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = new Color(0.95f, 0.98f, 1f, 0.95f) }
                    };
                    var chipRect = new Rect(hover.x - 10f, hover.yMax + 4f, hover.width + 20f, 24f);
                    DrawPanel(chipRect, new Color(0.08f, 0.1f, 0.16f, 0.82f));
                    GUI.Label(chipRect, director.IconHoverLabel, chipStyle);
                }
            }
        }

        /// <summary>Physical screen coords → IMGUI space (primary work area at origin).</summary>
        private static Rect PhysicalToGui(RectInt physical)
        {
            return new Rect(physical.x, Screen.height - physical.y - physical.height,
                physical.width, physical.height);
        }

        private static void EnsureStyles()
        {
            if (uiFont != null)
            {
                return;
            }

            uiFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Yu Gothic UI", "Meiryo UI", "Segoe UI" }, 14);
            titleStyle = new GUIStyle
            {
                font = uiFont,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.45f, 0.12f, 0.2f) }
            };
            bodyStyle = new GUIStyle
            {
                font = uiFont,
                fontSize = 12,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                normal = { textColor = new Color(0.22f, 0.16f, 0.18f) }
            };
            chipStyle = new GUIStyle
            {
                font = uiFont,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.96f, 0.94f, 0.9f) }
            };
            boardStyle = new GUIStyle
            {
                font = uiFont,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.24f, 0.14f, 0.06f) }
            };
        }

        private static void DrawDiaryBook(CompanionDirector director)
        {
            var now = Time.realtimeSinceStartupAsDouble;
            if (string.IsNullOrEmpty(director.DiaryText) || now >= director.DiaryHideAtReal)
            {
                return;
            }

            const double slideSeconds = 0.35;
            var elapsed = now - director.DiaryOpenReal;
            var remaining = director.DiaryHideAtReal - now;
            var offset = 260f;
            if (elapsed < slideSeconds)
            {
                offset = Mathf.Lerp(260f, 0f, Mathf.Clamp01((float)(elapsed / slideSeconds)));
            }
            else if (remaining < slideSeconds)
            {
                offset = Mathf.Lerp(0f, 260f, Mathf.Clamp01((float)(1.0 - remaining / slideSeconds)));
            }

            var width = Mathf.Min(330f, Screen.width - 24f);
            var height = 178f;
            var x = Screen.width - width - 14f + offset;
            var y = Screen.height - height - 16f;

            // Cover, page block, then content.
            DrawPanel(new Rect(x - 6f, y - 6f, width + 12f, height + 12f),
                new Color(0.62f, 0.13f, 0.22f, 0.96f));
            DrawPanel(new Rect(x, y, width, height), new Color(0.98f, 0.95f, 0.86f, 0.97f));
            GUI.Label(new Rect(x + 12f, y + 8f, width - 24f, 26f), "⊹ Nhật ký của Airi ⊹", titleStyle);
            GUI.Label(new Rect(x + 16f, y + 38f, width - 32f, height - 74f), director.DiaryText, bodyStyle);
            GUI.Label(new Rect(x + 16f, y + height - 32f, width - 32f, 24f),
                "…đang bị giật lại! KHÔNG ĐƯỢC XEM!", bodyStyle);
        }

        private static void DrawReminderBoard(CompanionDirector director)
        {
            var now = Time.realtimeSinceStartupAsDouble;
            if (string.IsNullOrEmpty(director.ReminderText) || now >= director.ReminderHideAtReal)
            {
                return;
            }

            var width = Mathf.Min(430f, Screen.width - 40f);
            var height = 64f;
            var x = (Screen.width - width) * 0.5f;
            var y = 18f;
            DrawPanel(new Rect(x, y, width, height), new Color(0.83f, 0.66f, 0.42f, 0.95f));
            DrawPanel(new Rect(x + 5f, y + 5f, width - 10f, height - 10f),
                new Color(0.96f, 0.9f, 0.78f, 0.97f));
            GUI.Label(new Rect(x + 14f, y + 10f, width - 28f, height - 20f),
                $"📌 Nhắc nè: {director.ReminderText}", boardStyle);
        }

        private static void DrawPomodoroChip(CompanionDirector director)
        {
            var pomodoro = director.Pomodoro;
            if (pomodoro.Phase == PomodoroPhase.Idle)
            {
                return;
            }

            var remaining = pomodoro.RemainingSeconds;
            var label = pomodoro.Phase == PomodoroPhase.Focusing
                ? $"🍅 Tập trung  {FormatClock(remaining)}  —  Alt+P để dừng"
                : $"☕ Nghỉ ngơi  {FormatClock(remaining)}";
            var style = new GUIStyle(chipStyle)
            {
                normal = { textColor = pomodoro.Phase == PomodoroPhase.Focusing
                    ? new Color(0.98f, 0.93f, 0.88f)
                    : new Color(0.9f, 0.97f, 0.92f) }
            };
            var width = 270f;
            var rect = new Rect(16f, Screen.height - 54f, width, 34f);
            DrawPanel(rect, pomodoro.Phase == PomodoroPhase.Focusing
                ? new Color(0.55f, 0.16f, 0.16f, 0.9f)
                : new Color(0.16f, 0.42f, 0.3f, 0.9f));
            GUI.Label(new Rect(rect.x + 14f, rect.y, rect.width - 20f, rect.height), label, style);
        }

        /// <summary>"Buổi live mini" badge while the beat dance is running.</summary>
        private static void DrawLiveBadge(CompanionDirector director)
        {
            if (!director.IsDancing)
            {
                return;
            }

            var pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
            var width = Mathf.Min(360f, Screen.width - 24f);
            var rect = new Rect(Screen.width - width - 14f, 14f, width, 58f);
            DrawPanel(rect, new Color(0.09f, 0.07f, 0.12f, 0.88f));
            var liveStyle = new GUIStyle(boardStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1f, 0.3f, 0.32f) }
            };
            GUI.Label(new Rect(rect.x + 12f, rect.y + 6f, 92f, 24f), $"● LIVE", liveStyle);
            var infoStyle = new GUIStyle(bodyStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.95f, 0.93f, 0.9f) }
            };
            GUI.Label(new Rect(rect.x + 92f, rect.y + 6f, width - 104f, 24f),
                $"♪ {director.LiveTitle}", infoStyle);
            var metaStyle = new GUIStyle(bodyStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.75f, 0.85f, 1f, pulse) }
            };
            GUI.Label(new Rect(rect.x + 12f, rect.y + 30f, width - 24f, 22f),
                $"{director.LiveBpm:F0} BPM · {director.LiveMove} · Alt+N dừng", metaStyle);
        }

        private static void DrawYandereVignette(CompanionDirector director)
        {
            var now = Time.realtimeSinceStartupAsDouble;
            if (now >= director.YandereVignetteHideAtReal)
            {
                return;
            }

            var alpha = 0.14f * Mathf.Clamp01((float)(director.YandereVignetteHideAtReal - now) / 2f);
            var color = new Color(0.32f, 0f, 0.04f, alpha);
            var thickness = Mathf.Max(30f, Screen.height * 0.09f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, thickness), Texture2D.whiteTexture,
                ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);
            GUI.DrawTexture(new Rect(0f, Screen.height - thickness, Screen.width, thickness),
                Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);
            GUI.DrawTexture(new Rect(0f, 0f, thickness, Screen.height), Texture2D.whiteTexture,
                ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);
            GUI.DrawTexture(new Rect(Screen.width - thickness, 0f, thickness, Screen.height),
                Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);
        }

        private static void DrawPanel(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f,
                Color.white, 0f, 10f);
            GUI.color = previous;
        }

        private static string FormatClock(double seconds)
        {
            var total = (int)System.Math.Ceiling(seconds);
            return $"{total / 60:D2}:{total % 60:D2}";
        }
    }
}

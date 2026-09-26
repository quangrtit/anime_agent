using System;

namespace AnimeAssistant.Domain
{
    /// <summary>Keyword rules applied to foreground window titles by the companion's feelings.</summary>
    public static class DesktopSignals
    {
        private static readonly string[] ImageViewerKeywords =
        {
            ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".heic",
            "photos", "photo viewer", "Ảnh", "Xem ảnh", "hình ảnh",
            "instagram", "pinterest", "pixiv", "wallpaper", "deviantart",
            "hình", "girl", "waifu"
        };

        public static bool IsImageRelatedTitle(string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
            {
                return false;
            }

            var title = windowTitle.ToLowerInvariant();
            foreach (var keyword in ImageViewerKeywords)
            {
                if (title.Contains(keyword.ToLowerInvariant()))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Titles that are system chrome, never counted as "user is looking at another girl".</summary>
        public static bool IsIgnoredTitle(string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
            {
                return true;
            }

            var title = windowTitle.ToLowerInvariant();
            return title.Contains("animeassistant") || title.Contains("windows input experience") ||
                   title.Contains("program manager") || title.Contains("nvidia");
        }
    }
}

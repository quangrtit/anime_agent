using System;
using System.IO;
using UnityEngine;

namespace AnimeAssistant.Presentation
{
    [Serializable]
    public sealed class DesktopExperienceConfigData
    {
        public float displayScale = 0.6667f;
        public float doorRightMarginCm = 3.5f;
        public float doorBottomMarginCm = 2f;
        public float avatarStartViewportX = 0.72f;
        public float roamMinViewportX = 0.06f;
        public float roamMaxViewportX = 0.86f;
        public float roamNearDepth = -0.35f;
        public float roamFarDepth = 4.8f;
        public float walkWorldUnitsPerSecond = 0.72f;
        public float runWorldUnitsPerSecond = 1.18f;
        public float doorCollapseSeconds = 0.72f;
        public float recallButtonScale = 0.32f;

        public void Normalize()
        {
            displayScale = Mathf.Clamp(displayScale, 0.35f, 1.25f);
            doorRightMarginCm = Mathf.Clamp(doorRightMarginCm, 0.5f, 10f);
            doorBottomMarginCm = Mathf.Clamp(doorBottomMarginCm, 0.5f, 8f);
            avatarStartViewportX = Mathf.Clamp(avatarStartViewportX, 0.1f, 0.9f);
            roamMinViewportX = Mathf.Clamp(roamMinViewportX, 0.02f, 0.8f);
            roamMaxViewportX = Mathf.Clamp(roamMaxViewportX, roamMinViewportX + 0.05f, 0.96f);
            roamNearDepth = Mathf.Clamp(roamNearDepth, -1.5f, 1f);
            roamFarDepth = Mathf.Clamp(roamFarDepth, roamNearDepth + 0.5f, 9f);
            walkWorldUnitsPerSecond = Mathf.Clamp(walkWorldUnitsPerSecond, 0.25f, 2f);
            runWorldUnitsPerSecond = Mathf.Clamp(runWorldUnitsPerSecond,
                walkWorldUnitsPerSecond + 0.1f, 4f);
            doorCollapseSeconds = Mathf.Clamp(doorCollapseSeconds, 0.25f, 2f);
            recallButtonScale = Mathf.Clamp(recallButtonScale, 0.15f, 0.6f);
        }
    }

    public static class DesktopExperienceConfig
    {
        private const string ConfigDirectoryName = "Config";
        private const string ConfigFileName = "desktop_layout.json";
        private static DesktopExperienceConfigData current;

        public static DesktopExperienceConfigData Current => current ??= Load();

        private static DesktopExperienceConfigData Load()
        {
            var data = new DesktopExperienceConfigData();
            var playerDirectory = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            var path = Path.Combine(playerDirectory, ConfigDirectoryName, ConfigFileName);
            try
            {
                if (File.Exists(path))
                {
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(path), data);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[DesktopConfig] Could not read '{path}'; defaults are active. {exception.Message}");
            }

            data.Normalize();
            Debug.Log($"[DesktopConfig] Loaded '{path}'.");
            return data;
        }
    }
}

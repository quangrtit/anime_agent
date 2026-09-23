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
        public float doorAnchorViewportX = 0.831f;
        public float doorAnchorViewportY = 0.293f;
        public float avatarStartViewportX = 0.72f;
        public float roamMinViewportX = 0.06f;
        public float roamMaxViewportX = 0.86f;
        public float roamNearDepth = -0.35f;
        public float roamFarDepth = 4.8f;
        public float walkWorldUnitsPerSecond = 0.72f;
        public float runWorldUnitsPerSecond = 1.18f;
        public float doorCollapseSeconds = 0.72f;
        public float recallButtonScale = 0.32f;
        public float masterVolume = 0.8f;
        public float effectsVolume = 0.85f;
        public float voiceVolume = 0.65f;
        public bool chatterEnabled = true;
        public float chatterMinSeconds = 14f;
        public float chatterMaxSeconds = 32f;
        public float chatterVolume = 0.72f;
        public bool speechBubbleEnabled = true;
        public float speechBubbleScale = 1f;
        public float speechTextCharactersPerSecond = 22f;

        public void Normalize()
        {
            displayScale = Mathf.Clamp(displayScale, 0.35f, 1.25f);
            doorRightMarginCm = Mathf.Clamp(doorRightMarginCm, 0.5f, 10f);
            doorBottomMarginCm = Mathf.Clamp(doorBottomMarginCm, 0.5f, 8f);
            doorAnchorViewportX = Mathf.Clamp01(doorAnchorViewportX);
            doorAnchorViewportY = Mathf.Clamp01(doorAnchorViewportY);
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
            masterVolume = Mathf.Clamp01(masterVolume);
            effectsVolume = Mathf.Clamp01(effectsVolume);
            voiceVolume = Mathf.Clamp01(voiceVolume);
            chatterMinSeconds = Mathf.Clamp(chatterMinSeconds, 5f, 120f);
            chatterMaxSeconds = Mathf.Clamp(chatterMaxSeconds, chatterMinSeconds + 2f, 240f);
            chatterVolume = Mathf.Clamp01(chatterVolume);
            speechBubbleScale = Mathf.Clamp(speechBubbleScale, 0.65f, 1.5f);
            speechTextCharactersPerSecond = Mathf.Clamp(speechTextCharactersPerSecond, 8f, 60f);
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

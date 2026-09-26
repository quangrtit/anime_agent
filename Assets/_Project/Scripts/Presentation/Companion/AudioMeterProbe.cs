using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AnimeAssistant.Presentation
{
    /// <summary>
    /// Passive system-audio listener for the audio-reactive dance. Uses the
    /// Windows WASAPI loopback peak meter (IAudioMeterInformation) on the
    /// default render endpoint — a tiny COM surface, no capture buffers, no
    /// microphone and no audio ever leaves the machine. Beat detection is
    /// energy-onset based: poll GetPeakValue(), fire an onset when the level
    /// spikes above the rolling average, and estimate BPM from onset spacing.
    /// </summary>
    public sealed class AudioMeterProbe
    {
        private const float LevelFloor = 0.045f;
        private const float OnsetRatio = 1.55f;
        private const float OnsetRefractorySeconds = 0.26f;
        private const int HistorySize = 60; // ~2s at 30 Hz

        private object meterComObject;
        private bool initializationFailed;
        private float pollAccumulator;
        private readonly Queue<float> history = new();
        private readonly Queue<double> onsetTimes = new();
        private float lastOnsetReal = -10f;
        private bool onsetPending;

        public float CurrentPeak { get; private set; }
        public float RecentAverage { get; private set; }
        public float EstimatedBpm { get; private set; } = 128f;
        public bool MusicActive => RecentAverage >= LevelFloor || CurrentPeak >= LevelFloor * 2f;

        public bool ConsumeOnset()
        {
            var pending = onsetPending;
            onsetPending = false;
            return pending;
        }

        /// <summary>Call every frame; the actual COM poll is throttled to ~30 Hz.</summary>
        public void Poll(double now)
        {
            pollAccumulator += 1f;
            if (pollAccumulator < 2f) // ~30 Hz at 60 fps, frame-rate independent enough for a demo
            {
                return;
            }

            pollAccumulator = 0f;
            var peak = ReadPeak();
            if (peak < 0f)
            {
                return; // no device or COM failure — stay silent
            }

            CurrentPeak = peak;
            history.Enqueue(peak);
            while (history.Count > HistorySize)
            {
                history.Dequeue();
            }

            var sum = 0f;
            foreach (var value in history)
            {
                sum += value;
            }
            RecentAverage = sum / history.Count;

            var isOnset = peak > LevelFloor && peak > RecentAverage * OnsetRatio &&
                          now - lastOnsetReal >= OnsetRefractorySeconds;
            if (isOnset)
            {
                var interval = now - lastOnsetReal;
                lastOnsetReal = (float)now;
                onsetPending = true;
                if (interval is > 0.28f and < 0.95f)
                {
                    onsetTimes.Enqueue(now);
                    while (onsetTimes.Count > 8)
                    {
                        onsetTimes.Dequeue();
                    }
                    if (onsetTimes.Count >= 4)
                    {
                        var intervals = new List<double>();
                        double? previous = null;
                        foreach (var t in onsetTimes)
                        {
                            if (previous.HasValue)
                            {
                                intervals.Add(t - previous.Value);
                            }
                            previous = t;
                        }
                        intervals.Sort();
                        var median = intervals[intervals.Count / 2];
                        var bpm = (float)(60.0 / median);
                        // Snap near integer BPM to smooth jitter.
                        bpm = (float)Math.Round(bpm / 2f) * 2f;
                        if (bpm >= 70f && bpm <= 180f)
                        {
                            EstimatedBpm = EstimatedBpm * 0.6f + bpm * 0.4f;
                        }
                    }
                }
            }
        }

        private float ReadPeak()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (initializationFailed)
            {
                return -1f;
            }

            try
            {
                if (meterComObject == null)
                {
                    var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                    enumerator.GetDefaultAudioEndpoint(DataFlowRender, RoleConsole, out var device);
                    var meterIid = MeterIid;
                    device.Activate(ref meterIid, ClsCtxAll, IntPtr.Zero, out meterComObject);
                }

                var meter = (IAudioMeterInformation)meterComObject;
                meter.GetPeakValue(out var value);
                return value;
            }
            catch (Exception)
            {
                initializationFailed = true;
                return -1f;
            }
#else
            return -1f;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private static readonly Guid MeterIid = new("C02216F6-8C67-4B5B-9D00-D008E73E0064");
        private const int DataFlowRender = 0;  // eRender
        private const int RoleConsole = 0;     // eConsole
        private const int ClsCtxAll = 23;      // CLSCTX_INPROC_SERVER|LOCAL_SERVER|REMOTE_SERVER

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject
        {
        }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            // Vtable order matters: EnumAudioEndpoints comes first.
            int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
                [MarshalAs(System.Runtime.InteropServices.UnmanagedType.IUnknown)] out object iface);
        }

        [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioMeterInformation
        {
            int GetPeakValue(out float peak);
        }
#endif
    }
}

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using AnimeAssistant.Domain;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AnimeAssistant.Presentation
{
    /// <summary>
    /// Keeps native speech recognition and automation outside Unity so they
    /// cannot stall rendering. IPC is local-only, one JSON event per line.
    /// </summary>
    [DefaultExecutionOrder(8900)]
    public sealed class VoiceAgentBridge : MonoBehaviour
    {
        [Serializable]
        private sealed class VoiceAgentEvent
        {
            public string type;
            public string text;
            public string detail;
        }

        private readonly ConcurrentQueue<string> pending = new();
        private readonly ManualResetEvent shutdown = new(false);
        private Process agentProcess;
        private Thread pipeThread;
        private bool processFailureShown;
        private bool launched;
        private bool updateConfirmed;
        private GreyboxSummonController summonController;

        private void Start()
        {
            summonController = FindFirstObjectByType<GreyboxSummonController>();
        }

        public void Launch()
        {
            if (launched)
            {
                return;
            }
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var executable = FindAgentExecutable();
            if (string.IsNullOrEmpty(executable))
            {
                Debug.LogWarning("[VoiceAgent] AgentHost is not installed. Run Tools/Agent/Setup-VoiceAgent.ps1.");
                return;
            }

            launched = true;
            processFailureShown = false;
            shutdown.Reset();
            var projectDirectory = Path.GetDirectoryName(Application.dataPath);
            var arguments = $"--parent-pid {Process.GetCurrentProcess().Id} " +
                            $"--project-directory \"{projectDirectory}\"";
            Debug.Log($"[VoiceAgent] Launching '{executable}'.");
            agentProcess = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(executable),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            pipeThread = new Thread(ReadEvents) { IsBackground = true, Name = "Voice Agent IPC" };
            pipeThread.Start();
#endif
        }

        private void Update()
        {
            PumpEvents();
        }

        public void PumpEvents()
        {
            if (!updateConfirmed)
            {
                updateConfirmed = true;
                Debug.Log("[VoiceAgent] Bridge update loop is active.");
            }

            if (summonController == null)
            {
                summonController = FindFirstObjectByType<GreyboxSummonController>();
            }
            var avatarIsActive = summonController != null &&
                                 summonController.State == SummonState.AvatarActive;
            if (avatarIsActive && !launched)
            {
                Launch();
            }
            else if (!avatarIsActive && launched)
            {
                StopAgent();
            }
            if (!launched)
            {
                return;
            }

            while (pending.TryDequeue(out var line))
            {
                var agentEvent = JsonUtility.FromJson<VoiceAgentEvent>(line);
                if (agentEvent == null)
                {
                    continue;
                }
                if (agentEvent.type == "speaking" || agentEvent.type == "error" ||
                    agentEvent.type == "starting" ||
                    (agentEvent.type == "listening" && agentEvent.text.StartsWith("Sẵn sàng")))
                {
                    DesktopAudioController.Instance?.ShowAssistantMessage(agentEvent.text);
                }
                if (agentEvent.type == "error")
                    Debug.LogWarning($"[VoiceAgent] {agentEvent.text} {agentEvent.detail}");
                else if (agentEvent.type != "listening")
                    Debug.Log($"[VoiceAgent] {agentEvent.type}: {agentEvent.text}");
            }

            if (!processFailureShown && agentProcess != null && agentProcess.HasExited &&
                agentProcess.ExitCode != 0)
            {
                processFailureShown = true;
                const string message = "Trợ lý giọng nói chưa khởi động được. Hãy kiểm tra model và microphone.";
                DesktopAudioController.Instance?.ShowAssistantMessage(message);
                Debug.LogWarning($"[VoiceAgent] AgentHost exited with code {agentProcess.ExitCode}.");
            }
        }

        private void ReadEvents()
        {
            try
            {
                while (!shutdown.WaitOne(0) && agentProcess != null && !agentProcess.HasExited)
                {
                    var line = agentProcess.StandardOutput.ReadLine();
                    if (line == null) break;
                    pending.Enqueue(line);
                }
            }
            catch (InvalidOperationException) { }
            catch (IOException) { }
        }

        private static string FindAgentExecutable()
        {
            var root = Path.GetDirectoryName(Application.dataPath);
            var candidates = new[]
            {
                Path.Combine(root, "Agent", "AnimeAssistant.AgentHost.exe"),
                Path.Combine(root, "RuntimeContent", "Agent", "AnimeAssistant.AgentHost.exe")
            };
            foreach (var candidate in candidates)
                if (File.Exists(candidate)) return candidate;
            return null;
        }

        private void OnDestroy()
        {
            StopAgent();
            shutdown.Dispose();
        }

        private void StopAgent()
        {
            if (!launched)
            {
                return;
            }
            shutdown.Set();
            if (agentProcess != null && !agentProcess.HasExited)
            {
                try { agentProcess.Kill(); } catch (InvalidOperationException) { }
            }
            pipeThread?.Join(1500);
            agentProcess?.Dispose();
            agentProcess = null;
            pipeThread = null;
            launched = false;
            while (pending.TryDequeue(out _)) { }
            Debug.Log("[VoiceAgent] Stopped because the avatar is no longer active.");
        }
    }
}

using UnityEngine;

namespace AnimeAssistant.Presentation
{
    /// <summary>Runtime-synthesized one-shot sounds (no audio files shipped).</summary>
    public static class ProceduralAudio
    {
        /// <summary>Three spooky wooden knocks for the midnight visitor event.</summary>
        public static AudioClip CreateKnockClip(int sampleRate = 22050)
        {
            const float thumpSpacing = 0.42f;
            const int thumpSamples = 3600;
            var totalSamples = (int)((thumpSpacing * 2f + 0.35f) * sampleRate);
            var data = new float[totalSamples];
            for (var thump = 0; thump < 3; thump++)
            {
                var start = (int)(thump * thumpSpacing * sampleRate);
                for (var i = 0; i < thumpSamples && start + i < totalSamples; i++)
                {
                    var t = i / (float)sampleRate;
                    var body = Mathf.Sin(2f * Mathf.PI * 82f * t) * Mathf.Exp(-t * 38f);
                    var click = Mathf.Sin(2f * Mathf.PI * 640f * t) * Mathf.Exp(-t * 140f) * 0.35f;
                    data[start + i] += (body + click) * 0.85f;
                }
            }

            var clip = AudioClip.Create("Procedural Knock", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

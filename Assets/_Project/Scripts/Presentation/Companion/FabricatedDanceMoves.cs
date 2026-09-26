using System;

namespace AnimeAssistant.Presentation
{
    /// <summary>
    /// Handmade (fabricated) dance "motion data": each meme move is a small
    /// table of muscle-oscillator parameters driven by the beat phase. No
    /// external mocap files — the choreography lives in code until real motion
    /// data is added later.
    /// </summary>
    public static class FabricatedDanceMoves
    {
        public delegate void MuscleSetter(string muscleName, float value);

        public readonly struct DanceMove
        {
            public DanceMove(string name, Action<MuscleSetter, float> apply)
            {
                Name = name;
                Apply = apply;
            }

            public string Name { get; }
            public Action<MuscleSetter, float> Apply { get; }
        }

        public static readonly DanceMove[] Moves =
        {
            new("Lucky Star Wave", (set, beat) =>
            {
                var up = 0.5f + 0.5f * (float)Math.Sin(beat * Math.PI);
                // Alternate raised arm, kawaii side-to-side sway.
                set("Right Arm Down-Up", -0.95f + up * 1.5f);
                set("Right Forearm Stretch", -0.55f - up * 0.2f);
                set("Right Hand In-Out", (float)Math.Sin(beat * Math.PI * 2f) * 0.6f);
                set("Left Arm Down-Up", -0.75f + (1f - up) * 1.2f);
                set("Left Forearm Stretch", -0.25f);
                set("Spine Left-Right", (float)Math.Sin(beat * Math.PI * 0.5f) * 0.09f);
                set("Head Left-Right", (float)Math.Sin(beat * Math.PI * 0.5f) * 0.14f);
            }),
            new("Renai Swing", (set, beat) =>
            {
                // Alternating arm swings with a bouncy head nod.
                var swing = (float)Math.Sin(beat * Math.PI);
                set("Left Arm Front-Back", swing * 0.55f);
                set("Right Arm Front-Back", -swing * 0.55f);
                set("Left Forearm Stretch", -0.45f + swing * 0.15f);
                set("Right Forearm Stretch", -0.45f - swing * 0.15f);
                set("Left Arm Down-Up", -0.35f);
                set("Right Arm Down-Up", -0.35f);
                set("Head Up-Down", -Math.Abs(swing) * 0.12f);
                set("Spine Front-Back", 0.06f);
            }),
            new("Otagei Pump", (set, beat) =>
            {
                // Fist pumps on every other beat, body twist into the punch.
                var pump = 1f - beat % 2f;
                pump *= pump;
                var even = beat % 4f < 2f;
                var arm = even ? "Right" : "Left";
                set(arm + " Arm Down-Up", -0.8f + pump * 1.7f);
                set(arm + " Forearm Stretch", -0.7f + pump * 0.25f);
                set(even ? "Left Arm Down-Up" : "Right Arm Down-Up", -0.55f);
                set("Spine Left-Right", (even ? 1f : -1f) * pump * 0.12f);
                set("Head Up-Down", -pump * 0.07f);
            }),
            new("Side Step", (set, beat) =>
            {
                // In-place side steps with counter-swinging arms.
                var step = (float)Math.Sin(beat * Math.PI * 0.5f);
                set("Left Upper Leg Front-Back", step * 0.22f);
                set("Right Upper Leg Front-Back", -step * 0.22f);
                set("Left Arm Front-Back", -step * 0.35f);
                set("Right Arm Front-Back", step * 0.35f);
                set("Left Arm Down-Up", -0.5f);
                set("Right Arm Down-Up", -0.5f);
                set("Spine Left-Right", -step * 0.07f);
            })
        };

        public static int MoveForBeat(double beat)
        {
            return (int)(beat / 8) % Moves.Length; // switch move every 8 beats
        }
    }
}

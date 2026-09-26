using System;

namespace AnimeAssistant.Domain
{
    public enum PomodoroPhase
    {
        Idle,
        Focusing,
        OnBreak
    }

    public enum PomodoroEvent
    {
        None,
        FocusStarted,
        FocusCompleted,
        BreakEnded,
        Cancelled,
        OwnerWanderedOff,
        OwnerCameBack
    }

    /// <summary>
    /// Pure focus-timer state machine for the "waifu pomodoro". The presenter
    /// feeds it wall-clock seconds plus how long the user has been away from
    /// the machine; the companion waits (arms crossed) and pouts when the user
    /// wanders off mid-focus.
    /// </summary>
    public sealed class PomodoroMachine
    {
        public const int FocusMinutes = 25;
        public const int BreakMinutes = 5;
        public const double AwayNoticeSeconds = 180;

        private double focusEnd;
        private double breakEnd;
        private bool awayNoticed;

        public PomodoroPhase Phase { get; private set; } = PomodoroPhase.Idle;
        public int CompletedFocusSessions { get; private set; }
        public double LastEventNow { get; private set; } = double.NegativeInfinity;
        public PomodoroEvent LastEvent { get; private set; } = PomodoroEvent.None;
        public bool WasAwayDuringFocus { get; private set; }

        public bool IsRunning => Phase != PomodoroPhase.Idle;
        public double RemainingSeconds
        {
            get
            {
                switch (Phase)
                {
                    case PomodoroPhase.Focusing: return Math.Max(0.0, focusEnd - LastEventNow);
                    case PomodoroPhase.OnBreak: return Math.Max(0.0, breakEnd - LastEventNow);
                    default: return 0.0;
                }
            }
        }

        public bool Start(double now)
        {
            if (IsRunning)
            {
                return false;
            }

            Phase = PomodoroPhase.Focusing;
            WasAwayDuringFocus = false;
            awayNoticed = false;
            focusEnd = now + FocusMinutes * 60.0;
            LastEventNow = now;
            LastEvent = PomodoroEvent.None; // the presenter narrates the start itself
            return true;
        }

        public bool Cancel(double now)
        {
            if (!IsRunning)
            {
                return false;
            }

            Phase = PomodoroPhase.Idle;
            LastEventNow = now;
            LastEvent = PomodoroEvent.Cancelled;
            return true;
        }

        /// <summary>
        /// Advances the machine and drains the single-slot event queue; every
        /// event is delivered to the caller exactly once.
        /// </summary>
        public PomodoroEvent Tick(double now, double userIdleSeconds)
        {
            var outgoing = LastEvent;
            LastEvent = PomodoroEvent.None;

            if (IsRunning)
            {
                var fresh = PomodoroEvent.None;
                switch (Phase)
                {
                    case PomodoroPhase.Focusing:
                        if (!awayNoticed && userIdleSeconds >= AwayNoticeSeconds)
                        {
                            awayNoticed = true;
                            WasAwayDuringFocus = true;
                            fresh = PomodoroEvent.OwnerWanderedOff;
                        }
                        else if (awayNoticed && userIdleSeconds < AwayNoticeSeconds * 0.5)
                        {
                            awayNoticed = false;
                            fresh = PomodoroEvent.OwnerCameBack;
                        }

                        if (now >= focusEnd)
                        {
                            CompletedFocusSessions++;
                            Phase = PomodoroPhase.OnBreak;
                            breakEnd = now + BreakMinutes * 60.0;
                            awayNoticed = false;
                            fresh = PomodoroEvent.FocusCompleted;
                        }
                        break;

                    case PomodoroPhase.OnBreak:
                        if (now >= breakEnd)
                        {
                            Phase = PomodoroPhase.Idle;
                            fresh = PomodoroEvent.BreakEnded;
                        }
                        break;
                }

                if (outgoing == PomodoroEvent.None)
                {
                    outgoing = fresh;
                }
                else if (fresh != PomodoroEvent.None)
                {
                    LastEvent = fresh;
                }

                LastEventNow = now;
            }

            return outgoing;
        }
    }
}

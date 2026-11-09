using System;
using System.Collections.Generic;

namespace Race1.Core
{
    /// <summary>
    /// A point-to-point race against AI rivals on the generated road. The player is entrant 0. Rivals start on a
    /// staggered grid behind the player, spread across the lanes, and follow the car ahead in their own lane with
    /// the Intelligent Driver Model. Nothing collides physically: the car-following gap is the only interaction
    /// between cars, so a rival can pass through the player's car when they share a lane. The race holds still
    /// during the countdown.
    /// </summary>
    public sealed class RivalRace
    {
        /// <summary>Pace spread between rivals of one difficulty, so they do not drive in lockstep.</summary>
        private const double PaceSpread = 0.02;

        private readonly RaceDirector _director;
        private readonly List<RaceEntrant> _rivals = new List<RaceEntrant>();
        private readonly double[] _desiredSpeeds;
        private double _countdown;

        /// <param name="raceLengthMetres">Route length from the start line (m).</param>
        /// <param name="rivalCount">Number of AI rivals; the player is not counted.</param>
        /// <param name="lanes">Lanes the rivals are spread over. The player's lane is reported by the caller.</param>
        /// <param name="difficulty">Sets the rivals' cruising pace.</param>
        /// <param name="referenceSpeed">Cruising speed (m/s) at pace 1. The difficulty scales it.</param>
        /// <param name="gridSpacing">Distance between grid slots behind the start (m).</param>
        /// <param name="countdownSeconds">Time before the race starts. Nobody moves until it ends.</param>
        public RivalRace(double raceLengthMetres, int rivalCount, int lanes, AiDifficulty difficulty,
            double referenceSpeed, double gridSpacing, double countdownSeconds)
        {
            if (rivalCount < 0) throw new ArgumentOutOfRangeException(nameof(rivalCount));
            if (lanes < 1) throw new ArgumentOutOfRangeException(nameof(lanes));
            if (!(gridSpacing > 0.0)) throw new ArgumentOutOfRangeException(nameof(gridSpacing));
            if (!(referenceSpeed > 0.0)) throw new ArgumentOutOfRangeException(nameof(referenceSpeed));
            if (!(countdownSeconds >= 0.0)) throw new ArgumentOutOfRangeException(nameof(countdownSeconds));

            _director = new RaceDirector(raceLengthMetres);
            Lanes = lanes;
            Player = new RaceEntrant { Id = 0, Name = "Player", IsPlayer = true };
            _director.AddEntrant(Player);

            _desiredSpeeds = new double[rivalCount];
            double cruise = AiRacerModel.DesiredSpeed(referenceSpeed, difficulty);
            for (int i = 0; i < rivalCount; i++)
            {
                var rival = new RaceEntrant
                {
                    Id = i + 1,
                    Name = "Rival " + (i + 1),
                    Lane = i % lanes,
                    Progress = -gridSpacing * (i + 1),
                };
                _rivals.Add(rival);
                _director.AddEntrant(rival);
                _desiredSpeeds[i] = cruise * (1.0 - PaceSpread * (i % 3));
            }
            _countdown = countdownSeconds;
        }

        public RaceDirector Director => _director;

        public RaceEntrant Player { get; }

        public IReadOnlyList<RaceEntrant> Rivals => _rivals;

        /// <summary>Number of lanes the rivals are spread over.</summary>
        public int Lanes { get; }

        /// <summary>Seconds left before the start. Zero once the race is running.</summary>
        public double Countdown => _countdown;

        public bool Started => _countdown <= 0.0;

        /// <summary>True when the player has finished, or when everyone has.</summary>
        public bool IsOver => _director.IsOver;

        /// <summary>
        /// Records the player's state before <see cref="Update"/>: distance along the route (m), speed (m/s) and lane.
        /// The lane is any integer; rivals only share a lane with the player when it is in 0..Lanes-1.
        /// </summary>
        public void SetPlayer(double progress, double speed, int lane)
        {
            Player.Progress = progress;
            Player.Speed = Math.Max(0.0, speed);
            Player.Lane = lane;
        }

        /// <summary>Advances the race by <paramref name="dt"/> seconds: the countdown first, then rivals and clock.</summary>
        public void Update(double dt)
        {
            if (!(dt > 0.0)) return;
            if (!Started)
            {
                _countdown = Math.Max(0.0, _countdown - dt);
                return;
            }

            for (int i = 0; i < _rivals.Count; i++)
            {
                AiRacerModel.Step(_director, _rivals[i], dt, _desiredSpeeds[i]);
            }
            _director.Update(dt);
        }

        /// <summary>Lateral position (m, positive to the right) of the centre of <paramref name="lane"/>.</summary>
        public static double LaneCentre(int lane, int lanes, double halfWidth)
        {
            int n = Math.Max(1, lanes);
            int k = Math.Max(0, Math.Min(lane, n - 1));
            double width = 2.0 * halfWidth / n;
            return -halfWidth + (k + 0.5) * width;
        }

        /// <summary>Lane that contains lateral position <paramref name="t"/>, clamped to the road.</summary>
        public static int LaneOf(double t, int lanes, double halfWidth)
        {
            int n = Math.Max(1, lanes);
            double width = 2.0 * halfWidth / n;
            if (!(width > 0.0) || double.IsNaN(t)) return 0;
            double index = Math.Floor((t + halfWidth) / width);
            return (int)Math.Max(0.0, Math.Min(index, n - 1));
        }
    }
}

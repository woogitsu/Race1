using System;

namespace Race1.Core
{
    /// <summary>
    /// Scoring for the Traffic Challenge mode. Safe overtakes earn points, and each consecutive safe pass
    /// raises a multiplier. Near misses are worth a risk bonus, but they break the streak. Collisions cost
    /// points and reset it. Smooth driving adds a bonus per kilometre.
    /// </summary>
    public sealed class TrafficChallengeScorer
    {
        public const double SafePassClearanceM = 2.0;
        public const int OvertakePoints = 100;
        public const int NearMissPoints = 50;
        public const int CollisionPenalty = 250;
        public const int MaxMultiplier = 5;

        public int Score { get; private set; }
        public int Multiplier { get; private set; } = 1;
        public int SafePassStreak { get; private set; }
        public int NearMisses { get; private set; }
        public int Collisions { get; private set; }
        public double SmoothnessBonusAccumulator { get; private set; }

        private double _lastSteer;
        private bool _hasSteer;

        /// <summary>A traffic car was passed. clearance is the lateral gap (m) at the moment of passing.</summary>
        public void OnOvertake(double clearance)
        {
            if (clearance >= SafePassClearanceM)
            {
                SafePassStreak++;
                Multiplier = Math.Min(MaxMultiplier, 1 + SafePassStreak / 2);
                Score += OvertakePoints * Multiplier;
            }
            else
            {
                // Passed, but too close to count as a clean pass.
                SafePassStreak = 0;
                Multiplier = 1;
            }
        }

        public void OnNearMiss(double clearance)
        {
            NearMisses++;
            Score += NearMissPoints * Multiplier;
            SafePassStreak = 0;
            Multiplier = Math.Max(1, Multiplier - 1);
        }

        public void OnCollision()
        {
            Collisions++;
            Score = Math.Max(0, Score - CollisionPenalty);
            SafePassStreak = 0;
            Multiplier = 1;
        }

        /// <summary>Smooth driving: small steering changes per second earn a bonus. Call once per frame.</summary>
        public void OnFrame(double dt, double steer, double distanceMetres)
        {
            if (!_hasSteer)
            {
                _lastSteer = steer;
                _hasSteer = true;
                return;
            }
            double rate = Math.Abs(steer - _lastSteer) / Math.Max(dt, 1e-3);
            _lastSteer = steer;
            if (rate < 1.5)
            {
                SmoothnessBonusAccumulator += distanceMetres * 0.01;
            }
        }

        public int FinalScore => Score + (int)SmoothnessBonusAccumulator;
    }
}

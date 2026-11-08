using System;

namespace Race1.Core
{
    /// <summary>AI difficulty. Each level sets the cruising pace as a share of the reference speed.</summary>
    public enum AiDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
    }

    /// <summary>
    /// Drives AI racers along the race route. The racer follows the nearest car ahead with the Intelligent Driver
    /// Model, so it brakes for slower traffic and never drives through the car in front. Racers do not collide
    /// physically with each other; the car-following gap keeps them apart.
    /// </summary>
    public static class AiRacerModel
    {
        /// <summary>Length of a car used for the bumper-to-bumper gap (m).</summary>
        public const double CarLength = 4.4;

        public static double PaceFactor(AiDifficulty difficulty)
        {
            switch (difficulty)
            {
                case AiDifficulty.Easy: return 0.86;
                case AiDifficulty.Normal: return 0.94;
                default: return 1.0;
            }
        }

        /// <summary>Cruising speed (m/s) for a difficulty, relative to the reference speed of the race.</summary>
        public static double DesiredSpeed(double referenceSpeed, AiDifficulty difficulty)
        {
            return Math.Max(1.0, referenceSpeed) * PaceFactor(difficulty);
        }

        /// <summary>Advances one AI racer by <paramref name="dt"/> seconds towards its cruising speed.</summary>
        public static void Step(RaceDirector race, RaceEntrant racer, double dt, double desiredSpeed)
        {
            if (race == null) throw new ArgumentNullException(nameof(race));
            if (racer == null) throw new ArgumentNullException(nameof(racer));
            if (!(dt > 0.0) || racer.Finished) return;

            IdmParameters parameters = IdmParameters.Default(Math.Max(1.0, desiredSpeed));
            double gap = double.PositiveInfinity;
            double approach = 0.0;
            if (race.NearestAhead(racer, out double rawGap, out double leaderSpeed))
            {
                gap = Math.Max(0.01, rawGap - CarLength);
                approach = racer.Speed - leaderSpeed;
            }

            double accel = IntelligentDriverModel.Acceleration(parameters, racer.Speed, gap, approach);
            racer.Speed = Math.Max(0.0, racer.Speed + accel * dt);
            racer.Progress += racer.Speed * dt;
        }
    }
}

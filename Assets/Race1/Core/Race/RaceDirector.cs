using System;
using System.Collections.Generic;

namespace Race1.Core
{
    /// <summary>One competitor in a race: the player or an AI driver. Progress is distance along the road (m).</summary>
    public sealed class RaceEntrant
    {
        public int Id;
        public string Name = "";
        public bool IsPlayer;
        public int Lane;              // cars only interact with cars in the same lane
        public double Progress;       // metres travelled along the race route
        public double Speed;          // m/s
        public double FinishTime = -1.0;
        public int Place;             // 1 = winner, assigned by RaceDirector
        public double GapToLeader;    // metres behind the leader (finished: seconds behind the winner)

        public bool Finished => FinishTime >= 0.0;
    }

    /// <summary>
    /// Race rules and standings for a point-to-point race along the road. Standings are recomputed every
    /// update: finishers come first in finish order, then the rest by distance covered. Ties are broken by
    /// entrant Id, so the same inputs always give the same result.
    /// </summary>
    public sealed class RaceDirector
    {
        private readonly List<RaceEntrant> _entrants = new List<RaceEntrant>();
        private readonly List<RaceEntrant> _standings = new List<RaceEntrant>();

        public RaceDirector(double raceLengthMetres)
        {
            if (!(raceLengthMetres > 0.0)) throw new ArgumentOutOfRangeException(nameof(raceLengthMetres));
            RaceLength = raceLengthMetres;
        }

        public double RaceLength { get; }
        public double Elapsed { get; private set; }

        /// <summary>Entrants in their current standings order (position 1 first).</summary>
        public IReadOnlyList<RaceEntrant> Standings => _standings;

        public IReadOnlyList<RaceEntrant> Entrants => _entrants;

        /// <summary>The race is over when the player has finished, or when everyone has.</summary>
        public bool IsOver
        {
            get
            {
                RaceEntrant player = FindPlayer();
                if (player != null && player.Finished) return true;
                foreach (RaceEntrant e in _entrants)
                {
                    if (!e.Finished) return false;
                }
                return _entrants.Count > 0;
            }
        }

        public RaceEntrant Player => FindPlayer();

        public void AddEntrant(RaceEntrant entrant)
        {
            if (entrant == null) throw new ArgumentNullException(nameof(entrant));
            foreach (RaceEntrant e in _entrants)
            {
                if (e.Id == entrant.Id) throw new ArgumentException($"duplicate entrant id {entrant.Id}");
            }
            _entrants.Add(entrant);
            Recompute();
        }

        /// <summary>Advances the race clock and records finish times. Call after the entrants' progress has been updated.</summary>
        public void Update(double dt)
        {
            if (!(dt > 0.0)) return;
            Elapsed += dt;
            foreach (RaceEntrant e in _entrants)
            {
                if (!e.Finished && e.Progress >= RaceLength)
                {
                    // Interpolate the crossing time inside this frame so close finishes are not decided by frame order.
                    double overshoot = e.Progress - RaceLength;
                    double speed = Math.Max(1e-6, e.Speed);
                    e.FinishTime = Math.Max(0.0, Elapsed - overshoot / speed);
                }
            }
            Recompute();
        }

        /// <summary>
        /// Nearest unfinished entrant ahead of <paramref name="entrant"/> in the same lane. Returns false if there is none.
        /// Used by the AI to find its car-following gap. Cars in other lanes do not block each other.
        /// </summary>
        public bool NearestAhead(RaceEntrant entrant, out double gapMetres, out double speed)
        {
            gapMetres = double.PositiveInfinity;
            speed = 0.0;
            bool found = false;
            foreach (RaceEntrant other in _entrants)
            {
                if (other == entrant || other.Finished || other.Lane != entrant.Lane) continue;
                double ahead = other.Progress - entrant.Progress;
                if (ahead <= 0.0) continue;
                if (ahead < gapMetres)
                {
                    gapMetres = ahead;
                    speed = other.Speed;
                    found = true;
                }
            }
            return found;
        }

        private RaceEntrant FindPlayer()
        {
            foreach (RaceEntrant e in _entrants)
            {
                if (e.IsPlayer) return e;
            }
            return null;
        }

        private void Recompute()
        {
            _standings.Clear();
            _standings.AddRange(_entrants);
            _standings.Sort(CompareStanding);

            RaceEntrant leader = _standings.Count > 0 ? _standings[0] : null;
            for (int i = 0; i < _standings.Count; i++)
            {
                RaceEntrant e = _standings[i];
                e.Place = i + 1;
                if (leader == null)
                {
                    e.GapToLeader = 0.0;
                }
                else if (e.Finished && leader.Finished)
                {
                    e.GapToLeader = e.FinishTime - leader.FinishTime;
                }
                else
                {
                    e.GapToLeader = leader.Progress - e.Progress;
                }
            }
        }

        private static int CompareStanding(RaceEntrant a, RaceEntrant b)
        {
            if (a.Finished != b.Finished) return a.Finished ? -1 : 1;
            if (a.Finished)
            {
                int byTime = a.FinishTime.CompareTo(b.FinishTime);
                if (byTime != 0) return byTime;
            }
            else
            {
                int byProgress = b.Progress.CompareTo(a.Progress);
                if (byProgress != 0) return byProgress;
            }
            return a.Id.CompareTo(b.Id);
        }
    }
}

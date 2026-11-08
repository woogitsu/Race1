using System;

namespace Race1.Core
{
    /// <summary>
    /// Time attack on a procedural road segment of fixed length. Sector splits are measured at evenly
    /// spaced arc lengths. The timer works on road distance, not frames, so it is consistent with the
    /// physics and with replays.
    /// </summary>
    public sealed class TimeAttackTimer
    {
        private double _startS;
        private double _lengthM;
        private int _sectors;
        private double _elapsed;
        private double[] _splits;
        private int _nextSector;
        private bool _running;

        public double BestSeconds { get; private set; } = -1.0;
        public double LastSeconds { get; private set; } = -1.0;
        public double Elapsed => _elapsed;
        public bool Running => _running;
        public bool Finished { get; private set; }
        public int CompletedSectors => _nextSector;

        /// <param name="lengthM">Segment length in metres.</param>
        /// <param name="sectors">Number of timed sectors (at least 1).</param>
        /// <param name="bestSeconds">Personal best, or negative if none.</param>
        public void Begin(double startS, double lengthM, int sectors, double bestSeconds)
        {
            _startS = startS;
            _lengthM = Math.Max(10.0, lengthM);
            _sectors = Math.Max(1, sectors);
            _splits = new double[_sectors];
            _elapsed = 0.0;
            _nextSector = 0;
            _running = true;
            Finished = false;
            BestSeconds = bestSeconds;
        }

        /// <summary>Advances the clock and records any sector boundary the car has passed.</summary>
        public void Update(double dt, double carS)
        {
            if (!_running) return;
            _elapsed += dt;

            double progress = carS - _startS;
            while (_nextSector < _sectors && progress >= _lengthM * (_nextSector + 1) / _sectors)
            {
                _splits[_nextSector] = _elapsed;
                _nextSector++;
            }

            if (progress >= _lengthM)
            {
                _running = false;
                Finished = true;
                LastSeconds = _elapsed;
                if (BestSeconds < 0.0 || _elapsed < BestSeconds)
                {
                    BestSeconds = _elapsed;
                }
            }
        }

        /// <summary>Time at which a given sector was completed (seconds since start), or -1 if not yet.</summary>
        public double SectorTime(int sector)
        {
            return sector >= 0 && sector < _nextSector ? _splits[sector] : -1.0;
        }

        public void Abort()
        {
            _running = false;
        }
    }
}

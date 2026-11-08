using System;
using System.Collections.Generic;

namespace Race1.Core
{
    /// <summary>A traffic car in road-relative coordinates (same frame as the player's VehicleDynamics).</summary>
    public sealed class TrafficCar
    {
        public int Id;
        public double S;
        public double T;
        public double Speed;
        public int Direction = 1;     // +1 same direction as the player, -1 oncoming
        public int LaneIndex;         // lane number on its side of the road, 0 = nearest the centre line
        public double TargetT;
        public double DesiredSpeed;
        public double LaneChangeCooldown;
        public IdmParameters Idm;
        public const double Length = 4.4;
        public const double Width = 1.9;
    }

    /// <summary>Events reported to the game (scoring, audio, damage).</summary>
    public struct TrafficEvent
    {
        public TrafficEventKind Kind;
        public int CarId;
        public double LateralClearance;
    }

    public enum TrafficEventKind
    {
        NearMiss = 0,
        Overtake = 1,
        PlayerCollision = 2,
    }

    /// <summary>
    /// AI traffic that lives on the endless road. Cars spawn ahead of the player and are recycled when they fall
    /// behind. Car following uses IDM; lane changes are accepted only with a safe gap. The player counts as an
    /// obstacle, so traffic reacts to the player car.
    /// </summary>
    public sealed class TrafficSimulation
    {
        private const double SpawnAheadMin = 120.0;
        private const double SpawnAheadMax = 600.0;
        private const double RecycleBehind = 350.0;
        private const double LaneChangeGap = 18.0;

        private readonly IRoadProvider _road;
        private readonly List<TrafficCar> _cars = new List<TrafficCar>();
        private readonly List<TrafficEvent> _events = new List<TrafficEvent>();
        private DeterministicRng _rng;
        private readonly int _maxCars;
        private int _nextId = 1;
        private bool _enabled = true;
        private readonly HashSet<int> _passedFlags = new HashSet<int>();
        private readonly HashSet<int> _contacts = new HashSet<int>();

        public TrafficSimulation(IRoadProvider road, ulong seed, int maxCars)
        {
            _road = road;
            _maxCars = Math.Max(0, maxCars);
            _rng = new DeterministicRng(DeterministicRng.Combine(seed, 0x72AFF1CUL));
        }

        public IReadOnlyList<TrafficCar> Cars => _cars;

        /// <summary>Events produced by the last Update. Cleared at the start of each update.</summary>
        public IReadOnlyList<TrafficEvent> Events => _events;

        /// <summary>When disabled ("empty road" option) existing cars are removed and no new ones spawn.</summary>
        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (!value) _cars.Clear();
            }
        }

        public void Update(double dt, double playerS, double playerT, double playerSpeed, double playerLength = 4.4)
        {
            _events.Clear();
            if (!_enabled || dt <= 0.0)
            {
                return;
            }

            Recycle(playerS);
            while (_cars.Count < _maxCars)
            {
                if (!TrySpawn(playerS)) break;
            }

            // Cars update in a stable order so results do not depend on list churn.
            foreach (TrafficCar car in _cars)
            {
                bool hasLeader = FindLeader(car, playerS, playerT, playerSpeed, playerLength, out double gap, out double leaderSpeed);
                double approach = car.Speed - leaderSpeed;
                double accel = IntelligentDriverModel.Acceleration(car.Idm, car.Speed, gap, approach);
                car.Speed = Math.Max(0.0, car.Speed + accel * dt);
                car.S += car.Direction * car.Speed * dt;

                // Lateral motion: first-order approach to the target lane centre.
                car.T += (car.TargetT - car.T) * Math.Min(1.0, dt * 1.5);

                car.LaneChangeCooldown = Math.Max(0.0, car.LaneChangeCooldown - dt);
                if (car.LaneChangeCooldown <= 0.0 && hasLeader && gap < LaneChangeGap && car.Speed > leaderSpeed + 2.0)
                {
                    TryLaneChange(car, playerS, playerT, playerSpeed, playerLength);
                }
            }

            DetectPlayerInteractions(playerS, playerT, playerLength);
        }

        private void Recycle(double playerS)
        {
            for (int i = _cars.Count - 1; i >= 0; i--)
            {
                TrafficCar c = _cars[i];
                if (c.Direction > 0 ? c.S < playerS - RecycleBehind : c.S > playerS + SpawnAheadMax * 1.5)
                {
                    _cars.RemoveAt(i);
                }
            }
        }

        private bool TrySpawn(double playerS)
        {
            double ahead = _rng.Range(SpawnAheadMin, SpawnAheadMax);
            double s = playerS + ahead;
            RoadSample road = _road.Sample(s);
            int direction = _rng.Chance(0.5) ? 1 : -1;
            int perSide = Math.Max(1, road.Lanes / 2);
            int lane = _rng.NextInt(0, perSide);
            double targetT = LaneCentre(direction, lane, perSide, road.HalfWidth);
            double desired = road.Biome == BiomeKind.Highway ? _rng.Range(26.0, 33.0) : _rng.Range(12.0, 20.0);

            // Refuse to spawn on top of an existing car.
            foreach (TrafficCar c in _cars)
            {
                if (Math.Abs(c.S - s) < 25.0 && Math.Abs(c.T - targetT) < 2.5) return false;
            }

            _cars.Add(new TrafficCar
            {
                Id = _nextId++,
                S = s,
                T = targetT,
                TargetT = targetT,
                Speed = desired * _rng.Range(0.8, 1.0),
                Direction = direction,
                LaneIndex = lane,
                DesiredSpeed = desired,
                Idm = IdmParameters.Default(desired),
            });
            return true;
        }

        /// <summary>Centre of a lane: lane 0 is nearest the centre line. Right side carries the same direction (right-hand traffic).</summary>
        public static double LaneCentre(int direction, int lane, int lanesPerSide, double halfWidth)
        {
            double laneWidth = halfWidth * 2.0 / Math.Max(1, lanesPerSide * 2);
            double offset = (lane + 0.5) * laneWidth;
            return direction > 0 ? offset : -offset;
        }

        /// <summary>Nearest car ahead in the same lane and direction, including the player. Returns false if none.</summary>
        private bool FindLeader(TrafficCar car, double playerS, double playerT, double playerSpeed, double playerLength,
            out double gap, out double leaderSpeed)
        {
            double bestDist = double.PositiveInfinity;
            gap = double.PositiveInfinity;
            leaderSpeed = 0.0;
            bool found = false;

            foreach (TrafficCar other in _cars)
            {
                if (other == car || other.Direction != car.Direction) continue;
                if (Math.Abs(other.T - car.T) > TrafficCar.Width) continue;
                double ahead = (other.S - car.S) * car.Direction;
                if (ahead <= 0.0 || ahead >= bestDist) continue;
                bestDist = ahead;
                gap = ahead - TrafficCar.Length;
                leaderSpeed = other.Speed;
                found = true;
            }

            // The player is an obstacle for cars travelling in the same direction (player travels +s).
            if (car.Direction > 0 && Math.Abs(playerT - car.T) < TrafficCar.Width)
            {
                double ahead = playerS - car.S;
                if (ahead > 0.0 && ahead < bestDist)
                {
                    gap = ahead - TrafficCar.Length * 0.5 - playerLength * 0.5;
                    leaderSpeed = playerSpeed;
                    found = true;
                }
            }
            return found;
        }

        private void TryLaneChange(TrafficCar car, double playerS, double playerT, double playerSpeed, double playerLength)
        {
            RoadSample road = _road.Sample(car.S);
            int perSide = Math.Max(1, road.Lanes / 2);
            foreach (int candidate in new[] { car.LaneIndex - 1, car.LaneIndex + 1 })
            {
                if (candidate < 0 || candidate >= perSide) continue;
                double targetT = LaneCentre(car.Direction, candidate, perSide, road.HalfWidth);
                if (IsLaneFree(car, targetT, playerS, playerT, playerLength, playerSpeed))
                {
                    car.LaneIndex = candidate;
                    car.TargetT = targetT;
                    car.LaneChangeCooldown = 6.0;
                    return;
                }
            }
        }

        private bool IsLaneFree(TrafficCar car, double targetT, double playerS, double playerT, double playerLength, double playerSpeed)
        {
            foreach (TrafficCar other in _cars)
            {
                if (other == car || other.Direction != car.Direction) continue;
                if (Math.Abs(other.T - targetT) > TrafficCar.Width) continue;
                double dist = (other.S - car.S) * car.Direction;
                if (dist > -LaneChangeGap && dist < LaneChangeGap * 1.5) return false;
            }
            if (car.Direction > 0 && Math.Abs(playerT - targetT) < TrafficCar.Width)
            {
                double dist = playerS - car.S;
                if (dist > -LaneChangeGap && dist < LaneChangeGap * 1.5) return false;
            }
            return true;
        }

        private void DetectPlayerInteractions(double playerS, double playerT, double playerLength)
        {
            foreach (TrafficCar car in _cars)
            {
                double ds = car.S - playerS;
                double dt = Math.Abs(car.T - playerT);
                double lateralClearance = dt - (TrafficCar.Width + 1.9) * 0.5; // 1.9 = player width
                bool longitudinalOverlap = Math.Abs(ds) < (TrafficCar.Length + playerLength) * 0.5;

                bool touching = longitudinalOverlap && lateralClearance < 0.0;
                if (touching)
                {
                    // Edge-triggered: one event when contact starts, not one per frame while overlapping.
                    if (_contacts.Add(car.Id))
                    {
                        _events.Add(new TrafficEvent { Kind = TrafficEventKind.PlayerCollision, CarId = car.Id, LateralClearance = lateralClearance });
                    }
                    continue;
                }
                _contacts.Remove(car.Id);

                // Near miss: the car passes alongside the player with little room to spare.
                if (Math.Abs(ds) < 4.0 && lateralClearance >= 0.0 && lateralClearance < 1.0 && !_passedFlags.Contains(car.Id))
                {
                    _passedFlags.Add(car.Id);
                    _events.Add(new TrafficEvent { Kind = TrafficEventKind.NearMiss, CarId = car.Id, LateralClearance = lateralClearance });
                }

                // Overtake: the car moved from ahead of the player to behind it.
                if (car.Direction > 0 && ds < -playerLength && !_passedFlags.Contains(-car.Id))
                {
                    _passedFlags.Add(-car.Id);
                    _events.Add(new TrafficEvent { Kind = TrafficEventKind.Overtake, CarId = car.Id, LateralClearance = lateralClearance });
                }
            }

            _passedFlags.RemoveWhere(id => Math.Abs(id) < 1 || !_cars.Exists(c => c.Id == Math.Abs(id)));
        }
    }
}

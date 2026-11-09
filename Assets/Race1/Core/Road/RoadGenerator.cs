using System;

namespace Race1.Core
{
    /// <summary>
    /// Complete, copyable state of the road generator. Snapshots of this struct are the checkpoints
    /// that make rewinding to an earlier point of the road cheap and bit-for-bit deterministic.
    /// </summary>
    public struct RoadState
    {
        public long Index;             // next sample index; arc length = Index * SampleSpacing
        public Vector3d Position;      // centre line position at Index
        public double Heading;         // yaw at Index (rad)
        public long KnotStart;         // sample index where the current knot starts
        public long KnotEnd;           // sample index where it ends (exclusive)
        public double CurvatureStart;  // curvature at KnotStart
        public double CurvatureEnd;    // curvature at KnotEnd
        public double GradeStart;
        public double GradeEnd;
        public double WidthStart;      // half width at KnotStart
        public double WidthEnd;        // half width at KnotEnd
        public int Lanes;              // lanes of the current knot
        public long KnotNumber;        // knots created so far (also seeds the knot RNG)
        public bool InTunnel;          // tunnel flag state (switches only after a sustained condition)
        public int TunnelCount;        // consecutive samples that support the next tunnel switch
        public bool OnBridge;          // bridge flag state (switches only after a sustained condition)
        public int BridgeCount;        // consecutive samples that support the next bridge switch
    }

    /// <summary>
    /// Sequential procedural road. The centre line is a chain of "knots": between knots, curvature and
    /// grade vary linearly (clothoid-like ramps), so heading is C1 and position is G2 continuous.
    /// Knot targets are drawn from a RNG seeded by (world seed, knot number) and from the biome found
    /// at the knot's start, which makes every segment reproducible from the seed alone.
    /// </summary>
    public sealed class RoadGenerator
    {
        private readonly RoadSettings _settings;
        private readonly TerrainField _terrain;
        private readonly BiomeField _biomes;
        private readonly double[] _weights = new double[Biomes.Count];
        private RoadState _state;

        public RoadGenerator(RoadSettings settings, TerrainField terrain, BiomeField biomes, RoadState state)
        {
            _settings = settings;
            _terrain = terrain;
            _biomes = biomes;
            _state = state;
        }

        public RoadState State => _state;

        public static RoadState CreateInitialState(RoadSettings settings)
        {
            double halfWidth = 2 * settings.LaneWidth * 0.5;
            return new RoadState
            {
                Index = 0,
                Position = Vector3d.Zero,
                Heading = 0.0,
                KnotStart = 0,
                KnotEnd = 0,
                CurvatureStart = 0.0,
                CurvatureEnd = 0.0,
                GradeStart = 0.0,
                GradeEnd = 0.0,
                WidthStart = halfWidth,
                WidthEnd = halfWidth,
                Lanes = 2,
                KnotNumber = 0,
            };
        }

        /// <summary>Produces the sample at the current index and advances the generator by one step.</summary>
        public RoadSample Next()
        {
            if (_state.Index >= _state.KnotEnd)
            {
                CreateKnot();
            }

            RoadState st = _state;
            double ds = _settings.SampleSpacing;
            double frac = KnotFraction(st, st.Index);
            double kappa = Mathx.Lerp(st.CurvatureStart, st.CurvatureEnd, frac);
            double grade = Mathx.Lerp(st.GradeStart, st.GradeEnd, frac);
            double halfWidth = Mathx.Lerp(st.WidthStart, st.WidthEnd, frac);
            double pitch = Math.Atan(grade);

            _biomes.Weights(st.Position.X, st.Position.Z, _weights);
            Biomes.Blend(_weights, out BiomeKind dominant);
            double ground = _terrain.NaturalHeight(st.Position.X, st.Position.Z);

            // Bridges and tunnels are structures, not single metres. Each switches on only after its condition has held
            // for StructureMinLength, and enter and exit use different heights, so flags never flicker along a ridge.
            double groundAboveRoad = ground - st.Position.Y;
            int minSamples = Math.Max(1, (int)Math.Round(_settings.StructureMinLength / _settings.SampleSpacing));
            st.TunnelCount = Sustain(st.InTunnel ? groundAboveRoad < _settings.TunnelExitHeight : groundAboveRoad > _settings.TunnelHeight,
                st.TunnelCount, minSamples, ref st.InTunnel);
            st.BridgeCount = Sustain(st.OnBridge ? -groundAboveRoad < _settings.BridgeExitHeight : -groundAboveRoad > _settings.BridgeHeight,
                st.BridgeCount, minSamples, ref st.OnBridge);

            var sample = new RoadSample
            {
                S = st.Index * ds,
                Position = st.Position,
                Heading = st.Heading,
                Pitch = pitch,
                Bank = ComputeBank(kappa),
                Curvature = kappa,
                HalfWidth = halfWidth,
                Shoulder = _settings.Shoulder,
                Lanes = st.Lanes,
                Biome = dominant,
                Bridge = st.OnBridge,
                Tunnel = st.InTunnel,
            };

            // Advance to the next sample with a midpoint rule: exact for the linear curvature ramps.
            double fracMid = KnotFraction(st, st.Index + 0.5);
            double kMid = Mathx.Lerp(st.CurvatureStart, st.CurvatureEnd, fracMid);
            double gMid = Mathx.Lerp(st.GradeStart, st.GradeEnd, fracMid);
            double headingMid = st.Heading + 0.5 * kMid * ds;
            double pitchMid = Math.Atan(gMid);
            double horizontal = ds * Math.Cos(pitchMid);

            st.Position = new Vector3d(
                st.Position.X + Math.Sin(headingMid) * horizontal,
                st.Position.Y + ds * Math.Sin(pitchMid),
                st.Position.Z + Math.Cos(headingMid) * horizontal);
            st.Heading += kMid * ds;
            st.Index++;
            _state = st;
            return sample;
        }

        private static double KnotFraction(RoadState st, double index)
        {
            double len = st.KnotEnd - st.KnotStart;
            return len <= 0 ? 0.0 : (index - st.KnotStart) / len;
        }

        /// <summary>
        /// Counts consecutive samples that support a switch. Returns the new count; flips <paramref name="state"/>
        /// once the count reaches <paramref name="minSamples"/>. When the state is already on, the condition is the
        /// exit condition, so a flag cannot be switched off and on again within a few metres.
        /// </summary>
        private static int Sustain(bool supportsSwitch, int count, int minSamples, ref bool state)
        {
            count = supportsSwitch ? count + 1 : 0;
            if (count >= minSamples)
            {
                state = !state;
                return 0;
            }
            return count;
        }

        private double ComputeBank(double kappa)
        {
            double lateral = kappa * _settings.ReferenceSpeed * _settings.ReferenceSpeed / Mathx.Gravity;
            return Mathx.Clamp(Math.Atan(lateral), -_settings.MaxBank, _settings.MaxBank);
        }

        private void CreateKnot()
        {
            RoadState st = _state;
            RoadSettings s = _settings;
            bool first = st.KnotNumber == 0;

            var rng = new DeterministicRng(DeterministicRng.Combine(s.Seed, (ulong)st.KnotNumber + 1UL));
            _biomes.Weights(st.Position.X, st.Position.Z, _weights);
            BiomeProfile p = Biomes.Blend(_weights, out _);

            // Segment length from the biome, then stretched if the targets change too fast to be drivable.
            double lengthM = rng.Range(p.KnotMin, p.KnotMax);
            long spacing = Math.Max(1L, (long)Math.Round(lengthM / s.SampleSpacing));
            if (first)
            {
                spacing = (long)Math.Round(120.0 / s.SampleSpacing);
            }

            // Curvature target: straight, or a radius with alternating direction.
            bool straight = first || rng.NextDouble() < p.StraightChance;
            double curvature = 0.0;
            if (!straight)
            {
                double radius = Math.Exp(rng.Range(Math.Log(p.MinRadius), Math.Log(p.MaxRadius)));
                double prevSign = Math.Sign(st.CurvatureEnd);
                double sign;
                if (prevSign == 0)
                {
                    sign = rng.NextDouble() < 0.5 ? 1.0 : -1.0;
                }
                else
                {
                    sign = rng.NextDouble() < 0.8 ? -prevSign : prevSign;
                }
                // Keep the heading inside a cone so the road keeps moving forward and does not loop.
                if (Math.Abs(st.Heading) > 0.6 * s.MaxHeadingDrift)
                {
                    sign = -Math.Sign(st.Heading);
                }
                curvature = Mathx.Clamp(sign / radius, -s.MaxCurvature, s.MaxCurvature);
            }

            // Grade target: pulled toward the natural ground (road follows the land), plus some wander.
            double ground = _terrain.NaturalHeight(st.Position.X, st.Position.Z);
            double deltaH = st.Position.Y - (ground + s.Clearance); // > 0: road is above the ground
            double pull = Mathx.Clamp(-deltaH / 80.0, -1.0, 1.0) * p.MaxGrade;
            double wander = first ? 0.0 : rng.Range(-1.0, 1.0) * p.MaxGrade * 0.5;
            double grade = Mathx.Clamp(pull + wander, -p.MaxGrade, p.MaxGrade);
            if (first)
            {
                grade = 0.0;
            }
            if (st.Position.Y > s.AltitudeCeiling && grade > 0.0)
            {
                grade = -0.3 * p.MaxGrade;
            }
            else if (st.Position.Y < s.AltitudeFloor && grade < 0.0)
            {
                grade = 0.3 * p.MaxGrade;
            }

            // Lane count only changes on straight knots; the width ramps over the knot.
            int lanes = (straight || first) ? p.Lanes : st.Lanes;
            double widthTarget = lanes * s.LaneWidth * 0.5;

            // Rate limits in samples: the knot must be long enough for curvature, grade and width to change slowly.
            double dK = Math.Abs(curvature - st.CurvatureEnd);
            double dG = Math.Abs(grade - st.GradeEnd);
            double dW = Math.Abs(widthTarget - st.WidthEnd);
            long minK = (long)Math.Ceiling(dK / (s.MaxCurvatureRate * s.SampleSpacing));
            long minG = (long)Math.Ceiling(dG / (s.MaxGradientRate * s.SampleSpacing));
            long minW = (long)Math.Ceiling(dW / (s.MaxWidthRate * s.SampleSpacing));
            long minRate = Math.Max(minK, Math.Max(minG, Math.Max(minW, 1L)));

            // Turn budget: a knot may not turn the car more than MaxTurnPerKnot radians. Without this,
            // a 30 m radius held for 200 m would be a full loop. Rate limits still win if they conflict.
            double meanCurvature = 0.5 * (Math.Abs(st.CurvatureEnd) + Math.Abs(curvature));
            if (meanCurvature > 1e-9)
            {
                long budgetSamples = (long)Math.Floor(s.MaxTurnPerKnot / meanCurvature / s.SampleSpacing);
                spacing = Math.Min(spacing, Math.Max(1L, budgetSamples));
            }
            spacing = Math.Max(spacing, minRate);

            st.KnotStart = st.Index;
            st.KnotEnd = st.Index + spacing;
            st.CurvatureStart = st.CurvatureEnd;
            st.CurvatureEnd = curvature;
            st.GradeStart = st.GradeEnd;
            st.GradeEnd = grade;
            st.WidthStart = st.WidthEnd;
            st.WidthEnd = widthTarget;
            st.Lanes = lanes;
            st.KnotNumber++;
            _state = st;
        }
    }
}

using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class TrafficEvasionTests
    {
        private const double Dt = 1.0 / 60.0;

        /// <summary>A slow car in the player's lane, 200 m ahead, and a faster player approaching at a steady speed.</summary>
        private static (int contacts, double finalCarT, double playerT) RunApproach(IRoadProvider road, double halfWidth, int lanesPerSide)
        {
            var sim = new TrafficSimulation(road, 1UL, maxCars: 0);
            double playerT = TrafficSimulation.LaneCentre(1, 0, lanesPerSide, halfWidth);
            TrafficCar slow = sim.AddCar(200.0, playerT, 1, 12.0, 12.0);
            double playerS = 0.0;
            double playerSpeed = 22.0;
            int contacts = 0;
            for (int i = 0; i < 60 * 25; i++)
            {
                playerS += playerSpeed * Dt;
                sim.Update(Dt, playerS, playerT, playerSpeed);
                foreach (TrafficEvent e in sim.Events)
                {
                    if (e.Kind == TrafficEventKind.PlayerCollision) contacts++;
                }
            }
            return (contacts, slow.T, playerT);
        }

        [Test]
        public void FourLaneRoad_SlowCarMovesAsideForAFasterPlayer()
        {
            var four = new AnalyticRoad { Lanes = 4, HalfWidth = 7.0 };
            (int contacts, double finalCarT, double playerT) = RunApproach(four, 7.0, 2);

            Assert.That(contacts, Is.EqualTo(0), "the car should evade instead of being hit");
            Assert.That(finalCarT, Is.Not.EqualTo(playerT).Within(0.5), "the car should have left the player's lane");
        }

        [Test]
        public void TwoLaneRoad_ConstantSpeedPlayerHasNoSpaceToEvade()
        {
            // One lane per direction: the slow car has nowhere to go, so a player who does not brake makes contact.
            // The roadmap item C4 is about braking drivers; this test documents the limit.
            var two = new AnalyticRoad { Lanes = 2, HalfWidth = 3.5 };
            (int contacts, double finalCarT, double playerT) = RunApproach(two, 3.5, 1);

            Assert.That(contacts, Is.GreaterThanOrEqualTo(1));
            Assert.That(finalCarT, Is.EqualTo(playerT).Within(0.5), "the car stays in the only lane");
        }

        [Test]
        public void BrakingPlayer_TwoMinutesOnGeneratedRoad_HasNoContact()
        {
            RoadTrack road = TestWorld.CreateTrack(77);
            var sim = new TrafficSimulation(road, 77, 12);
            double playerT = TrafficSimulation.LaneCentre(1, 0, 1, 3.5);
            double playerS = 0.0;
            double playerSpeed = 20.0;
            int contacts = 0;
            for (int i = 0; i < 60 * 120; i++)
            {
                // A driver who keeps a safe distance: IDM against the nearest car ahead in the same lane.
                double gap = double.PositiveInfinity;
                double leaderSpeed = 0.0;
                foreach (TrafficCar c in sim.Cars)
                {
                    if (c.Direction <= 0 || System.Math.Abs(c.T - playerT) >= TrafficCar.Width) continue;
                    double ahead = c.S - playerS - TrafficCar.Length;
                    if (ahead <= 0.0 || ahead >= gap) continue;
                    gap = ahead;
                    leaderSpeed = c.Speed;
                }
                double accel = IntelligentDriverModel.Acceleration(IdmParameters.Default(20.0), playerSpeed, gap, playerSpeed - leaderSpeed);
                playerSpeed = System.Math.Max(0.0, playerSpeed + accel * Dt);
                playerS += playerSpeed * Dt;
                sim.Update(Dt, playerS, playerT, playerSpeed);
                foreach (TrafficEvent e in sim.Events)
                {
                    if (e.Kind == TrafficEventKind.PlayerCollision) contacts++;
                }
            }
            Assert.That(contacts, Is.EqualTo(0), "a driver who brakes for traffic ahead must not hit it");
        }
    }
}

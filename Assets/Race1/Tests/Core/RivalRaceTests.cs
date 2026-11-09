using System;
using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class RivalRaceTests
    {
        private const double Dt = 1.0 / 60.0;

        [Test]
        public void Grid_PutsRivalsBehindThePlayerAndSpreadsThemAcrossLanes()
        {
            var race = new RivalRace(3000.0, 5, 2, AiDifficulty.Normal, 50.0, 12.0, 3.0);

            Assert.That(race.Player.IsPlayer, Is.True);
            Assert.That(race.Player.Id, Is.EqualTo(0));
            Assert.That(race.Rivals.Count, Is.EqualTo(5));
            Assert.That(race.Director.Standings.Count, Is.EqualTo(6));
            for (int i = 0; i < race.Rivals.Count; i++)
            {
                RaceEntrant rival = race.Rivals[i];
                Assert.That(rival.IsPlayer, Is.False);
                Assert.That(rival.Progress, Is.EqualTo(-12.0 * (i + 1)).Within(1e-9), $"rival {i} grid slot");
                Assert.That(rival.Lane, Is.EqualTo(i % 2), $"rival {i} lane");
            }
        }

        [Test]
        public void Countdown_HoldsEveryoneUntilItEnds()
        {
            var race = new RivalRace(3000.0, 3, 2, AiDifficulty.Hard, 50.0, 12.0, 3.0);
            race.SetPlayer(0.0, 0.0, 99);

            for (int i = 0; i < 175; i++) race.Update(Dt);   // about 2.92 s
            Assert.That(race.Started, Is.False);
            Assert.That(race.Director.Elapsed, Is.EqualTo(0.0));
            for (int i = 0; i < race.Rivals.Count; i++)
            {
                Assert.That(race.Rivals[i].Progress, Is.EqualTo(-12.0 * (i + 1)).Within(1e-9), $"rival {i} moved during the countdown");
            }

            for (int i = 0; i < 15; i++) race.Update(Dt);   // about 3.17 s in total
            Assert.That(race.Started, Is.True);
            Assert.That(race.Director.Elapsed, Is.GreaterThan(0.0));
            Assert.That(race.Rivals[0].Progress, Is.GreaterThan(-12.0), "rivals do not move after the start");
        }

        [Test]
        public void HarderRivals_FinishSooner()
        {
            double easy = FastestRivalTime(AiDifficulty.Easy);
            double normal = FastestRivalTime(AiDifficulty.Normal);
            double hard = FastestRivalTime(AiDifficulty.Hard);
            TestContext.WriteLine($"fastest rival over 3000 m: easy {easy:F1} s, normal {normal:F1} s, hard {hard:F1} s");

            Assert.That(normal, Is.LessThan(easy));
            Assert.That(hard, Is.LessThan(normal));
        }

        [Test]
        public void Player_WhoFinishesFirst_TakesFirstPlaceAndEndsTheRace()
        {
            var race = new RivalRace(500.0, 3, 2, AiDifficulty.Easy, 50.0, 12.0, 0.0);
            double progress = 0.0;
            for (int i = 0; i < 60 * 120 && !race.IsOver; i++)
            {
                progress += 40.0 * Dt;
                race.SetPlayer(progress, 40.0, 99);
                race.Update(Dt);
            }

            Assert.That(race.Player.Finished, Is.True);
            Assert.That(race.Player.Place, Is.EqualTo(1));
            Assert.That(race.IsOver, Is.True);
        }

        [Test]
        public void Player_StandingStill_IsLastOnceTheRivalsPassIt()
        {
            var race = new RivalRace(3000.0, 3, 2, AiDifficulty.Normal, 50.0, 12.0, 0.0);
            race.SetPlayer(0.0, 0.0, 99);
            for (int i = 0; i < 60 * 30; i++) race.Update(Dt);

            // Every rival has driven past the stationary player, so the player is last.
            Assert.That(race.Player.Place, Is.EqualTo(4));
        }

        [Test]
        public void Lanes_RoundTripThroughLaneCentre()
        {
            for (int lanes = 1; lanes <= 4; lanes++)
            {
                for (int lane = 0; lane < lanes; lane++)
                {
                    double t = RivalRace.LaneCentre(lane, lanes, 3.5);
                    Assert.That(RivalRace.LaneOf(t, lanes, 3.5), Is.EqualTo(lane), $"{lanes} lanes, lane {lane}");
                }
            }
        }

        [Test]
        public void LaneOf_ClampsToTheRoadAndLaneCentresAreSymmetric()
        {
            Assert.That(RivalRace.LaneOf(-100.0, 2, 3.5), Is.EqualTo(0));
            Assert.That(RivalRace.LaneOf(100.0, 2, 3.5), Is.EqualTo(1));
            Assert.That(RivalRace.LaneOf(double.NaN, 2, 3.5), Is.EqualTo(0));
            Assert.That(RivalRace.LaneCentre(0, 2, 3.5), Is.EqualTo(-RivalRace.LaneCentre(1, 2, 3.5)).Within(1e-12));
        }

        [Test]
        public void Constructor_RejectsInvalidInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RivalRace(3000.0, -1, 2, AiDifficulty.Easy, 50.0, 12.0, 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RivalRace(3000.0, 2, 0, AiDifficulty.Easy, 50.0, 12.0, 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RivalRace(3000.0, 2, 2, AiDifficulty.Easy, 50.0, 0.0, 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RivalRace(3000.0, 2, 2, AiDifficulty.Easy, 0.0, 12.0, 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RivalRace(3000.0, 2, 2, AiDifficulty.Easy, 50.0, 12.0, -1.0));
        }

        /// <summary>Finish time of the quickest rival when the player stays out of the way in a lane of its own.</summary>
        private static double FastestRivalTime(AiDifficulty difficulty)
        {
            var race = new RivalRace(3000.0, 5, 2, difficulty, 50.0, 12.0, 0.0);
            for (int i = 0; i < 60 * 600; i++)
            {
                race.SetPlayer(0.0, 0.0, 99);
                race.Update(Dt);
                if (AllRivalsFinished(race)) break;
            }

            double best = double.PositiveInfinity;
            foreach (RaceEntrant rival in race.Rivals)
            {
                Assert.That(rival.Finished, Is.True, "a rival did not finish within 10 minutes");
                best = Math.Min(best, rival.FinishTime);
            }
            return best;
        }

        private static bool AllRivalsFinished(RivalRace race)
        {
            foreach (RaceEntrant rival in race.Rivals)
            {
                if (!rival.Finished) return false;
            }
            return true;
        }
    }
}

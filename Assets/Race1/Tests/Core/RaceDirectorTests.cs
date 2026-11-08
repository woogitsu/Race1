using System;
using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class RaceDirectorTests
    {
        private static RaceEntrant Entrant(int id, double progress, double speed = 20.0, bool player = false) =>
            new RaceEntrant { Id = id, Name = "E" + id, IsPlayer = player, Progress = progress, Speed = speed };

        [Test]
        public void Standings_OrderByDistanceCovered()
        {
            var race = new RaceDirector(1000.0);
            race.AddEntrant(Entrant(1, 300.0));
            race.AddEntrant(Entrant(2, 100.0));
            race.AddEntrant(Entrant(3, 200.0));
            race.Update(0.1);

            Assert.That(race.Standings[0].Id, Is.EqualTo(1));
            Assert.That(race.Standings[1].Id, Is.EqualTo(3));
            Assert.That(race.Standings[2].Id, Is.EqualTo(2));
            Assert.That(race.Standings[0].Place, Is.EqualTo(1));
            Assert.That(race.Standings[2].Place, Is.EqualTo(3));
        }

        [Test]
        public void TiedProgress_IsBrokenByIdDeterministically()
        {
            var race = new RaceDirector(1000.0);
            race.AddEntrant(Entrant(7, 150.0));
            race.AddEntrant(Entrant(4, 150.0));
            race.Update(0.1);
            Assert.That(race.Standings[0].Id, Is.EqualTo(4));
            Assert.That(race.Standings[1].Id, Is.EqualTo(7));
        }

        [Test]
        public void GapToLeader_IsDistanceBehindForRunners()
        {
            var race = new RaceDirector(1000.0);
            race.AddEntrant(Entrant(1, 300.0));
            race.AddEntrant(Entrant(2, 100.0));
            race.Update(0.1);
            Assert.That(race.Standings[0].GapToLeader, Is.EqualTo(0.0).Within(1e-9));
            Assert.That(race.Standings[1].GapToLeader, Is.EqualTo(200.0).Within(1e-9));
        }

        [Test]
        public void Finishers_RankAheadOfRunners_AndByFinishTime()
        {
            var race = new RaceDirector(500.0);
            RaceEntrant fast = Entrant(1, 0.0, 40.0);
            RaceEntrant slow = Entrant(2, 0.0, 10.0);
            RaceEntrant mid = Entrant(3, 0.0, 20.0);
            race.AddEntrant(fast);
            race.AddEntrant(slow);
            race.AddEntrant(mid);

            for (int i = 0; i < 2000 && !race.IsOver; i++)
            {
                fast.Progress += fast.Speed * 0.05;
                mid.Progress += mid.Speed * 0.05;
                slow.Progress += slow.Speed * 0.05;
                race.Update(0.05);
            }

            Assert.That(fast.Finished && mid.Finished, Is.True);
            Assert.That(race.Standings[0].Id, Is.EqualTo(1));
            Assert.That(race.Standings[1].Id, Is.EqualTo(3));
            Assert.That(fast.FinishTime, Is.LessThan(mid.FinishTime));
            Assert.That(fast.Place, Is.EqualTo(1));
        }

        [Test]
        public void FinishTime_IsInterpolatedInsideTheFrame()
        {
            var race = new RaceDirector(100.0);
            RaceEntrant racer = Entrant(1, 0.0, 10.0);
            race.AddEntrant(racer);
            // Covers 100 m in a single 1.0 s frame, so it crosses the line 0.5 s after the frame start.
            racer.Progress = 105.0;
            race.Update(1.0);
            Assert.That(racer.FinishTime, Is.EqualTo(0.5).Within(1e-9));
        }

        [Test]
        public void IsOver_WhenPlayerFinishes()
        {
            var race = new RaceDirector(100.0);
            race.AddEntrant(Entrant(1, 0.0, 10.0, player: true));
            race.AddEntrant(Entrant(2, 0.0, 10.0));
            race.Player.Progress = 120.0;
            race.Update(0.1);
            Assert.That(race.IsOver, Is.True);
        }

        [Test]
        public void NearestAhead_IgnoresFinishedAndCarsBehind()
        {
            var race = new RaceDirector(1000.0);
            RaceEntrant self = Entrant(1, 100.0);
            RaceEntrant behind = Entrant(2, 50.0, speed: 99.0);
            RaceEntrant far = Entrant(3, 400.0, speed: 25.0);
            RaceEntrant near = Entrant(4, 130.0, speed: 22.0);
            race.AddEntrant(self);
            race.AddEntrant(behind);
            race.AddEntrant(far);
            race.AddEntrant(near);

            bool found = race.NearestAhead(self, out double gap, out double speed);
            Assert.That(found, Is.True);
            Assert.That(gap, Is.EqualTo(30.0).Within(1e-9));
            Assert.That(speed, Is.EqualTo(22.0).Within(1e-9));
        }

        [Test]
        public void DuplicateEntrantId_IsRejected()
        {
            var race = new RaceDirector(100.0);
            race.AddEntrant(Entrant(1, 0.0));
            Assert.Throws<ArgumentException>(() => race.AddEntrant(Entrant(1, 0.0)));
        }

        [Test]
        public void AiPace_IsOrderedByDifficulty()
        {
            double easy = AiRacerModel.DesiredSpeed(40.0, AiDifficulty.Easy);
            double normal = AiRacerModel.DesiredSpeed(40.0, AiDifficulty.Normal);
            double hard = AiRacerModel.DesiredSpeed(40.0, AiDifficulty.Hard);
            Assert.That(easy, Is.LessThan(normal));
            Assert.That(normal, Is.LessThan(hard));
            Assert.That(hard, Is.EqualTo(40.0).Within(1e-9));
        }

        [Test]
        public void AiRacer_NeverMovesBackwardsOrHasNegativeSpeed()
        {
            var race = new RaceDirector(2000.0);
            RaceEntrant ai = Entrant(1, 0.0, 0.0);
            race.AddEntrant(ai);
            double lastProgress = 0.0;
            for (int i = 0; i < 6000; i++)
            {
                AiRacerModel.Step(race, ai, 0.02, 30.0);
                Assert.That(ai.Progress, Is.GreaterThanOrEqualTo(lastProgress));
                Assert.That(ai.Speed, Is.GreaterThanOrEqualTo(0.0));
                lastProgress = ai.Progress;
            }
            Assert.That(ai.Speed, Is.EqualTo(30.0).Within(0.5), "should settle at its cruising speed");
        }

        [Test]
        public void AiFollower_KeepsAGapBehindTheLeader()
        {
            var race = new RaceDirector(100000.0);
            RaceEntrant leader = Entrant(1, 60.0, 0.0);
            RaceEntrant follower = Entrant(2, 0.0, 0.0);
            race.AddEntrant(leader);
            race.AddEntrant(follower);

            double minGap = double.PositiveInfinity;
            for (int i = 0; i < 60 * 60 * 3; i++)
            {
                // The leader cruises slowly; the follower wants to go much faster but must queue behind it.
                AiRacerModel.Step(race, leader, 1.0 / 60.0, 14.0);
                AiRacerModel.Step(race, follower, 1.0 / 60.0, 35.0);
                race.Update(1.0 / 60.0);
                minGap = Math.Min(minGap, leader.Progress - follower.Progress);
            }
            Assert.That(minGap, Is.GreaterThan(AiRacerModel.CarLength - 0.5), "follower drove through the leader");
            Assert.That(follower.Speed, Is.EqualTo(leader.Speed).Within(1.0), "follower should match the leader's speed");
        }

        [Test]
        public void CarsInOtherLanes_DoNotBlockEachOther()
        {
            var race = new RaceDirector(1000.0);
            RaceEntrant slowLeader = Entrant(1, 80.0, 0.0);
            RaceEntrant overtaker = Entrant(2, 0.0, 0.0);
            overtaker.Lane = 1;
            race.AddEntrant(slowLeader);
            race.AddEntrant(overtaker);

            Assert.That(race.NearestAhead(overtaker, out _, out _), Is.False, "a car in another lane must not count as ahead");
            for (int i = 0; i < 60 * 30; i++)
            {
                AiRacerModel.Step(race, overtaker, 1.0 / 60.0, 30.0);
                race.Update(1.0 / 60.0);
            }
            Assert.That(overtaker.Progress, Is.GreaterThan(slowLeader.Progress), "the overtaker should pass the slow car");
        }

        [Test]
        public void FullRace_HardestAiWinsAndResultIsDeterministic()
        {
            (int winner, double[] times) Run()
            {
                var race = new RaceDirector(1500.0);
                RaceEntrant player = Entrant(0, 0.0, 0.0, player: true);
                race.AddEntrant(player);
                var ai = new[]
                {
                    Entrant(1, 0.0, 0.0),
                    Entrant(2, 0.0, 0.0),
                    Entrant(3, 0.0, 0.0),
                };
                // Each racer has its own lane, so the race is decided by pace rather than by queueing.
                player.Lane = 0;
                ai[0].Lane = 1;
                ai[1].Lane = 2;
                ai[2].Lane = 3;
                race.AddEntrant(ai[0]);
                race.AddEntrant(ai[1]);
                race.AddEntrant(ai[2]);
                double reference = 34.0;
                double[] desired =
                {
                    AiRacerModel.DesiredSpeed(reference, AiDifficulty.Easy),
                    AiRacerModel.DesiredSpeed(reference, AiDifficulty.Normal),
                    AiRacerModel.DesiredSpeed(reference, AiDifficulty.Hard),
                };
                for (int step = 0; step < 60 * 400 && !race.IsOver; step++)
                {
                    player.Speed = 20.0;
                    player.Progress += player.Speed / 60.0;
                    for (int i = 0; i < 3; i++) AiRacerModel.Step(race, ai[i], 1.0 / 60.0, desired[i]);
                    race.Update(1.0 / 60.0);
                }
                int best = -1;
                var result = new double[4];
                foreach (RaceEntrant e in race.Entrants)
                {
                    result[e.Id] = e.FinishTime;
                    if (e.Place == 1) best = e.Id;
                }
                return (best, result);
            }

            (int winnerA, double[] timesA) = Run();
            (int winnerB, double[] timesB) = Run();

            Assert.That(winnerA, Is.EqualTo(3), "the Hard AI should win against Normal, Easy and a 20 m/s player");
            Assert.That(winnerA, Is.EqualTo(winnerB));
            for (int i = 0; i < 4; i++)
            {
                Assert.That(timesA[i], Is.EqualTo(timesB[i]).Within(1e-12), $"entrant {i} time differs between runs");
            }
        }
    }
}

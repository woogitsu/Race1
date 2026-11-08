using System;
using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class WeatherScheduleTests
    {
        [Test]
        public void SameSeed_GivesTheSameWeather()
        {
            var a = new WeatherSchedule(12345UL);
            var b = new WeatherSchedule(12345UL);
            for (double t = 0.0; t < 24 * 3600.0; t += 60.0)
            {
                Assert.That(a.KindAt(t), Is.EqualTo(b.KindAt(t)), $"weather differs at {t} s");
            }
        }

        [Test]
        public void DifferentSeeds_GiveDifferentWeather()
        {
            var a = new WeatherSchedule(1UL);
            var b = new WeatherSchedule(2UL);
            int differences = 0;
            for (double t = 0.0; t < 12 * 3600.0; t += 60.0)
            {
                if (a.KindAt(t) != b.KindAt(t)) differences++;
            }
            Assert.That(differences, Is.GreaterThan(20));
        }

        [Test]
        public void EverySegmentEndsInADifferentKind()
        {
            var schedule = new WeatherSchedule(77UL);
            double t = 0.0;
            WeatherKind previous = schedule.KindAt(t);
            for (int i = 0; i < 400; i++)
            {
                t = schedule.SegmentEndAt(t);
                WeatherKind next = schedule.KindAt(t);
                Assert.That(next, Is.Not.EqualTo(previous), $"segment {i} repeated its kind");
                previous = next;
            }
        }

        [Test]
        public void Transitions_FollowTheWeatherRules()
        {
            var schedule = new WeatherSchedule(9001UL, allowSnow: true);
            double t = 0.0;
            WeatherKind previous = schedule.KindAt(t);
            for (int i = 0; i < 2000; i++)
            {
                t = schedule.SegmentEndAt(t);
                WeatherKind next = schedule.KindAt(t);
                if (previous == WeatherKind.Storm) Assert.That(next, Is.Not.EqualTo(WeatherKind.Clear), "storm must not clear straight to sun");
                if (previous == WeatherKind.Clear) Assert.That(next, Is.Not.EqualTo(WeatherKind.Storm), "clear cannot become a storm directly");
                if (previous == WeatherKind.Fog) Assert.That(next, Is.Not.EqualTo(WeatherKind.Rain), "fog does not turn into rain directly");
                previous = next;
            }
        }

        [Test]
        public void Snow_OnlyAppearsWhenAllowed()
        {
            var summer = new WeatherSchedule(31UL, allowSnow: false);
            var winter = new WeatherSchedule(31UL, allowSnow: true);
            bool winterHasSnow = false;
            double t = 0.0;
            for (int i = 0; i < 3000; i++)
            {
                Assert.That(summer.KindAt(t), Is.Not.EqualTo(WeatherKind.Snow));
                if (winter.KindAt(t) == WeatherKind.Snow) winterHasSnow = true;
                t = summer.SegmentEndAt(t);
            }
            Assert.That(winterHasSnow, Is.True, "a winter route should see snow over a long run");
        }

        [Test]
        public void AllMainKinds_AreVisitedOverALongRun()
        {
            var schedule = new WeatherSchedule(555UL);
            var seen = new bool[6];
            double t = 0.0;
            for (int i = 0; i < 5000; i++)
            {
                seen[(int)schedule.KindAt(t)] = true;
                t = schedule.SegmentEndAt(t);
            }
            Assert.That(seen[(int)WeatherKind.Clear], Is.True);
            Assert.That(seen[(int)WeatherKind.Cloudy], Is.True);
            Assert.That(seen[(int)WeatherKind.Rain], Is.True);
            Assert.That(seen[(int)WeatherKind.Storm], Is.True);
            Assert.That(seen[(int)WeatherKind.Fog], Is.True);
        }

        [Test]
        public void SegmentLengths_StayWithinTheConfiguredRange()
        {
            var schedule = new WeatherSchedule(4242UL, minSegmentSeconds: 240.0, maxSegmentSeconds: 600.0);
            double segmentStart = 0.0;
            for (int i = 0; i < 300; i++)
            {
                double end = schedule.SegmentEndAt(segmentStart);
                double length = end - segmentStart;
                Assert.That(length, Is.InRange(240.0 - 1e-9, 600.0 + 1e-9), $"segment {i} length {length}");
                segmentStart = end;
            }
        }

        [Test]
        public void HourOfDay_StartsAtTheGivenHourAndWraps()
        {
            const double day = 1440.0;
            Assert.That(WeatherSchedule.HourAt(0.0, 6.0, day), Is.EqualTo(6.0).Within(1e-9));
            Assert.That(WeatherSchedule.HourAt(day / 4.0, 6.0, day), Is.EqualTo(12.0).Within(1e-9));
            Assert.That(WeatherSchedule.HourAt(day, 6.0, day), Is.EqualTo(6.0).Within(1e-9));
            for (double t = 0.0; t < 5 * day; t += 7.0)
            {
                double h = WeatherSchedule.HourAt(t, 22.0, day);
                Assert.That(h, Is.InRange(0.0, 24.0 - 1e-9));
            }
        }

        [Test]
        public void Apply_SetsTheTargetKindOfTheWeatherState()
        {
            var schedule = new WeatherSchedule(606UL);
            var state = new WeatherState();
            for (double t = 0.0; t < 3 * 3600.0; t += 120.0)
            {
                schedule.Apply(state, t);
                Assert.That(state.Kind, Is.EqualTo(schedule.KindAt(t)));
            }
        }

        [Test]
        public void WeatherParameters_ChangeGraduallyThroughTransitions()
        {
            // Six game hours of weather at a 0.5 s step. The blend time constant bounds how fast any parameter may move.
            var schedule = new WeatherSchedule(808UL, allowSnow: true);
            var state = new WeatherState();
            const double dt = 0.5;
            double maxStep = dt / state.TimeConstantSeconds + 1e-9;

            double prevCloud = state.Cloud;
            double prevRain = state.Rain;
            double prevFog = state.Fog;
            double prevSnow = state.Snow;
            for (double t = 0.0; t < 6 * 3600.0; t += dt)
            {
                schedule.Apply(state, t);
                state.Update(dt);

                Assert.That(Math.Abs(state.Cloud - prevCloud), Is.LessThanOrEqualTo(maxStep), $"cloud jumped at {t} s");
                Assert.That(Math.Abs(state.Rain - prevRain), Is.LessThanOrEqualTo(maxStep), $"rain jumped at {t} s");
                Assert.That(Math.Abs(state.Fog - prevFog), Is.LessThanOrEqualTo(maxStep), $"fog jumped at {t} s");
                Assert.That(Math.Abs(state.Snow - prevSnow), Is.LessThanOrEqualTo(maxStep), $"snow jumped at {t} s");

                Assert.That(state.Cloud, Is.InRange(0.0, 1.0));
                Assert.That(state.Rain, Is.InRange(0.0, 1.0));
                Assert.That(state.Fog, Is.InRange(0.0, 1.0));
                Assert.That(state.Snow, Is.InRange(0.0, 1.0));
                Assert.That(state.Wetness, Is.InRange(0.0, 1.0));
                Assert.That(state.FrictionScale, Is.InRange(0.4, 1.0));

                prevCloud = state.Cloud;
                prevRain = state.Rain;
                prevFog = state.Fog;
                prevSnow = state.Snow;
            }
        }
    }
}

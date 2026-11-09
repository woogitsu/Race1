using System;
using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class GarageTuningTests
    {
        private static readonly TuningKnob[] Knobs =
        {
            TuningKnob.Spring, TuningKnob.Damping, TuningKnob.FinalDrive, TuningKnob.Brake,
        };

        [Test]
        public void ToValue_EndsAtTheRangeLimits_AndIsLinear()
        {
            foreach (TuningKnob knob in Knobs)
            {
                float lo = TuningControls.Min(knob), hi = TuningControls.Max(knob);
                Assert.That(TuningControls.ToValue(knob, 0.0), Is.EqualTo(lo).Within(1e-6), knob.ToString());
                Assert.That(TuningControls.ToValue(knob, 1.0), Is.EqualTo(hi).Within(1e-6), knob.ToString());
                Assert.That(TuningControls.ToValue(knob, 0.5), Is.EqualTo((lo + hi) * 0.5f).Within(1e-6), knob.ToString());
            }
        }

        [Test]
        public void Position_RoundTripsThroughValue()
        {
            foreach (TuningKnob knob in Knobs)
            {
                for (double p = 0.0; p <= 1.0001; p += 0.1)
                {
                    double back = TuningControls.ToPosition(knob, TuningControls.ToValue(knob, p));
                    Assert.That(back, Is.EqualTo(Math.Min(1.0, p)).Within(1e-4), $"{knob} at {p}");
                }
            }
        }

        [Test]
        public void NeutralValue_IsInsideEveryRange()
        {
            foreach (TuningKnob knob in Knobs)
            {
                Assert.That(TuningControls.Min(knob), Is.LessThan(1f), knob.ToString());
                Assert.That(TuningControls.Max(knob), Is.GreaterThan(1f), knob.ToString());
            }
        }

        [Test]
        public void OutOfRangePositions_AreClamped_AndNaNIsTheMiddle()
        {
            foreach (TuningKnob knob in Knobs)
            {
                Assert.That(TuningControls.ToValue(knob, -3.0), Is.EqualTo(TuningControls.Min(knob)).Within(1e-6));
                Assert.That(TuningControls.ToValue(knob, 7.0), Is.EqualTo(TuningControls.Max(knob)).Within(1e-6));
                Assert.That(TuningControls.ToValue(knob, double.NaN), Is.EqualTo(TuningControls.ToValue(knob, 0.5)).Within(1e-6));
                Assert.That(TuningControls.ToPosition(knob, TuningControls.Max(knob) + 5f), Is.EqualTo(1.0).Within(1e-9));
                Assert.That(TuningControls.ToPosition(knob, float.NaN), Is.EqualTo(0.5).Within(1e-9));
            }
        }

        [Test]
        public void Readout_ShowsSignedPercentChangeFromNeutral()
        {
            Assert.That(TuningControls.Readout(1.0f), Is.EqualTo("0%"));
            Assert.That(TuningControls.Readout(1.1f), Is.EqualTo("+10%"));
            Assert.That(TuningControls.Readout(0.7f), Is.EqualTo("-30%"));
            Assert.That(TuningControls.Readout(float.NaN), Is.EqualTo("n/a"));
        }

        [Test]
        public void Write_ClampsTheValueAndLeavesOtherKnobsAlone()
        {
            var data = new VehicleTuningData();
            TuningControls.Write(data, TuningKnob.Spring, 99f);
            Assert.That(data.SpringScale, Is.EqualTo(VehicleTuning.MaxSpringScale).Within(1e-6));
            Assert.That(data.DampingScale, Is.EqualTo(1f));
            Assert.That(data.FinalDriveScale, Is.EqualTo(1f));
            Assert.That(data.BrakeScale, Is.EqualTo(1f));

            TuningControls.Write(data, TuningKnob.Brake, -4f);
            Assert.That(data.BrakeScale, Is.EqualTo(VehicleTuning.MinBrakeScale).Within(1e-6));
            Assert.That(TuningControls.Read(data, TuningKnob.Brake), Is.EqualTo(VehicleTuning.MinBrakeScale).Within(1e-6));
            Assert.That(TuningControls.Read(null, TuningKnob.Damping), Is.EqualTo(1f), "missing record reads as neutral");
        }

        [Test]
        public void Tyres_CycleThroughSoftStandardWetAndBack()
        {
            Assert.That(TuningControls.NextTyre(VehicleTuning.TireSoft), Is.EqualTo(VehicleTuning.TireStandard));
            Assert.That(TuningControls.NextTyre(VehicleTuning.TireStandard), Is.EqualTo(VehicleTuning.TireWet));
            Assert.That(TuningControls.NextTyre(VehicleTuning.TireWet), Is.EqualTo(VehicleTuning.TireSoft));
            Assert.That(TuningControls.NextTyre(99), Is.EqualTo(VehicleTuning.TireSoft), "out of range is treated as wet");
            Assert.That(TuningControls.TyreName(VehicleTuning.TireWet), Is.EqualTo("wet"));
        }

        [Test]
        public void Store_ReadDoesNotChangeTheSave()
        {
            var save = new SaveData();
            VehicleTuningData read = VehicleTuningStore.Read(save, VehicleCatalog.AuroraVeloce);
            Assert.That(read.SpringScale, Is.EqualTo(1f));
            Assert.That(save.Tunings.Length, Is.EqualTo(0), "reading must not add a record");
        }

        [Test]
        public void Store_EditsCreateOneRecordPerCar_AndLeaveOthersAlone()
        {
            var save = new SaveData();
            VehicleTuningStore.SetFromSlider(save, VehicleCatalog.AuroraVeloce, TuningKnob.Spring, 1.0);
            VehicleTuningStore.SetFromSlider(save, VehicleCatalog.AuroraVeloce, TuningKnob.Damping, 0.0);
            VehicleTuningStore.CycleTyres(save, VehicleCatalog.AuroraVeloce);

            Assert.That(save.Tunings.Length, Is.EqualTo(1), "one record per car, however many edits");
            VehicleTuningData mine = VehicleTuningStore.Read(save, VehicleCatalog.AuroraVeloce);
            Assert.That(mine.SpringScale, Is.EqualTo(VehicleTuning.MaxSpringScale).Within(1e-6));
            Assert.That(mine.DampingScale, Is.EqualTo(VehicleTuning.MinDampingScale).Within(1e-6));
            Assert.That(mine.TireCompound, Is.EqualTo(VehicleTuning.TireWet).Within(1e-6));

            VehicleTuningStore.SetFromSlider(save, VehicleCatalog.TempestaGtx, TuningKnob.Brake, 0.5);
            Assert.That(save.Tunings.Length, Is.EqualTo(2));
            Assert.That(VehicleTuningStore.Read(save, VehicleCatalog.AuroraVeloce).SpringScale, Is.EqualTo(VehicleTuning.MaxSpringScale).Within(1e-6),
                "editing another car must not change this one");
        }

        [Test]
        public void Store_ResetRestoresNeutral_AndKeepsTheRecord()
        {
            var save = new SaveData();
            VehicleTuningStore.SetFromSlider(save, VehicleCatalog.KestrelSr, TuningKnob.FinalDrive, 0.0);
            VehicleTuningStore.CycleTyres(save, VehicleCatalog.KestrelSr);
            VehicleTuningStore.Reset(save, VehicleCatalog.KestrelSr);

            VehicleTuningData record = VehicleTuningStore.Read(save, VehicleCatalog.KestrelSr);
            Assert.That(record.FinalDriveScale, Is.EqualTo(1f));
            Assert.That(record.TireCompound, Is.EqualTo(VehicleTuning.TireStandard));
            Assert.That(save.Tunings.Length, Is.EqualTo(1));
        }

        [Test]
        public void Store_RandomEdits_NeverLeaveTheRanges()
        {
            var save = new SaveData();
            var rng = new Random(7);
            string[] cars = { VehicleCatalog.AuroraVeloce, VehicleCatalog.TempestaGtx, VehicleCatalog.KestrelSr };
            for (int i = 0; i < 2000; i++)
            {
                string car = cars[rng.Next(cars.Length)];
                int action = rng.Next(3);
                if (action == 0)
                {
                    TuningKnob knob = Knobs[rng.Next(Knobs.Length)];
                    double position = rng.NextDouble() * 2.0 - 0.5;   // deliberately out of range too
                    VehicleTuningStore.SetFromSlider(save, car, knob, position);
                }
                else if (action == 1)
                {
                    VehicleTuningStore.CycleTyres(save, car);
                }
                else
                {
                    VehicleTuningStore.Reset(save, car);
                }
            }

            foreach (string car in cars)
            {
                VehicleTuningData r = VehicleTuningStore.Read(save, car);
                foreach (TuningKnob knob in Knobs)
                {
                    float v = TuningControls.Read(r, knob);
                    Assert.That(v, Is.InRange(TuningControls.Min(knob) - 1e-6f, TuningControls.Max(knob) + 1e-6f), $"{car} {knob}");
                }
                Assert.That(r.TireCompound, Is.InRange(0f, 2f));
            }
            Assert.That(save.Tunings.Length, Is.LessThanOrEqualTo(3), "no duplicate records");
        }

        [Test]
        public void TunedCar_MatchesTheBaseCar_WhenNothingIsEdited()
        {
            var save = new SaveData();
            VehicleSpec baseSpec = VehicleCatalog.Create(VehicleCatalog.TempestaGtx);
            VehicleSpec tuned = VehicleTuning.Apply(VehicleCatalog.Create(VehicleCatalog.TempestaGtx),
                VehicleTuningStore.Read(save, VehicleCatalog.TempestaGtx));
            Assert.That(tuned.SpringFront, Is.EqualTo(baseSpec.SpringFront).Within(1e-6));
            Assert.That(tuned.FinalDrive, Is.EqualTo(baseSpec.FinalDrive).Within(1e-9));
            Assert.That(tuned.GripDry, Is.EqualTo(baseSpec.GripDry).Within(1e-9));
        }

        [Test]
        public void TunedCar_FollowsTheSavedSpring()
        {
            var save = new SaveData();
            VehicleSpec baseSpec = VehicleCatalog.Create(VehicleCatalog.AuroraVeloce);
            VehicleTuningStore.SetFromSlider(save, VehicleCatalog.AuroraVeloce, TuningKnob.Spring, 1.0);
            VehicleSpec stiff = VehicleTuning.Apply(VehicleCatalog.Create(VehicleCatalog.AuroraVeloce),
                VehicleTuningStore.Read(save, VehicleCatalog.AuroraVeloce));
            Assert.That(stiff.SpringFront, Is.EqualTo(baseSpec.SpringFront * VehicleTuning.MaxSpringScale).Within(1e-3));
            Assert.That(stiff.SpringRear, Is.EqualTo(baseSpec.SpringRear * VehicleTuning.MaxSpringScale).Within(1e-3));
        }
    }
}

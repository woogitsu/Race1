using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class VehicleTuningTests
    {
        [Test]
        public void NeutralTuning_LeavesCarUnchanged()
        {
            VehicleSpec baseSpec = VehicleCatalog.Create(VehicleCatalog.AuroraVeloce);
            VehicleSpec tuned = VehicleTuning.Apply(baseSpec, 1f, 1f, 1f, 1f, VehicleTuning.TireStandard);
            Assert.That(tuned.SpringFront, Is.EqualTo(baseSpec.SpringFront).Within(1e-9));
            Assert.That(tuned.DampingRatio, Is.EqualTo(baseSpec.DampingRatio).Within(1e-12));
            Assert.That(tuned.FinalDrive, Is.EqualTo(baseSpec.FinalDrive).Within(1e-12));
            Assert.That(tuned.GripDry, Is.EqualTo(baseSpec.GripDry).Within(1e-12));
        }

        [Test]
        public void Apply_NeverModifiesTheBaseSpec()
        {
            VehicleSpec baseSpec = VehicleCatalog.Create(VehicleCatalog.TempestaGtx);
            double springBefore = baseSpec.SpringRear;
            double driveBefore = baseSpec.FinalDrive;
            VehicleTuning.Apply(baseSpec, 1.3f, 1.2f, 1.1f, 1.1f, VehicleTuning.TireSoft);
            Check.AreEqual(springBefore, baseSpec.SpringRear, 1e-9);
            Check.AreEqual(driveBefore, baseSpec.FinalDrive, 1e-12);
        }

        [Test]
        public void OutOfRangeValues_AreClamped()
        {
            VehicleSpec baseSpec = VehicleCatalog.Create(VehicleCatalog.KestrelSr);
            VehicleSpec tuned = VehicleTuning.Apply(baseSpec, 99f, -5f, 10f, 0f, 7);
            Check.AreEqual(baseSpec.SpringFront * VehicleTuning.MaxSpringScale, tuned.SpringFront, 1e-6);
            Check.AreEqual(baseSpec.DampingRatio * VehicleTuning.MinDampingScale, tuned.DampingRatio, 1e-9);
            Check.AreEqual(baseSpec.FinalDrive * VehicleTuning.MaxFinalDriveScale, tuned.FinalDrive, 1e-9);
            Check.AreEqual(baseSpec.MaxBrakeFront * VehicleTuning.MinBrakeScale, tuned.MaxBrakeFront, 1e-6);
        }

        [Test]
        public void NaN_FallsBackToMidRange()
        {
            float clamped = VehicleTuning.Clamp(float.NaN, 0.7f, 1.4f);
            Assert.That(clamped, Is.InRange(0.7f, 1.4f));
        }

        [Test]
        public void HigherFinalDriveRatio_LowersTopSpeed()
        {
            var straight = new AnalyticRoad();
            VehicleSpec lowerRatio = VehicleTuning.Apply(VehicleCatalog.Create(VehicleCatalog.AuroraVeloce), 1f, 1f, 0.9f, 1f, 1);
            VehicleSpec higherRatio = VehicleTuning.Apply(VehicleCatalog.Create(VehicleCatalog.AuroraVeloce), 1f, 1f, 1.1f, 1f, 1);
            double lowerTop = TopSpeed(lowerRatio, straight);
            double higherTop = TopSpeed(higherRatio, straight);
            Assert.That(higherTop, Is.LessThan(lowerTop), "shorter gearing (higher ratio) should reduce top speed");
        }

        [Test]
        public void SoftCompound_HasMoreDryGripThanWet()
        {
            VehicleSpec baseSpec = VehicleCatalog.Create(VehicleCatalog.AuroraVeloce);
            VehicleSpec soft = VehicleTuning.Apply(baseSpec, 1f, 1f, 1f, 1f, VehicleTuning.TireSoft);
            VehicleSpec wet = VehicleTuning.Apply(baseSpec, 1f, 1f, 1f, 1f, VehicleTuning.TireWet);
            Assert.That(soft.GripDry, Is.GreaterThan(wet.GripDry));
        }

        [Test]
        public void TunedCarsStillValidateAndDrive()
        {
            foreach (string id in VehicleCatalog.Ids)
            {
                VehicleSpec tuned = VehicleTuning.Apply(VehicleCatalog.Create(id), 1.4f, 1.5f, 0.85f, 1.2f, VehicleTuning.TireSoft);
                tuned.Validate();
                var car = new VehicleDynamics(tuned, new AnalyticRoad(), AssistSettings.Sport);
                car.Reset(0, 0, 0);
                for (int i = 0; i < 600; i++) car.Advance(1.0 / 60.0, new VehicleInput { Throttle = 1.0, AutoGearbox = true });
                Assert.That(double.IsFinite(car.Speed) && car.Speed > 5.0, $"{id} does not accelerate after tuning");
            }
        }

        [Test]
        public void SaveRecord_IsNormalisedIntoRange()
        {
            var data = new VehicleTuningData { SpringScale = 9f, DampingScale = 0f, FinalDriveScale = 2f, BrakeScale = -1f, TireCompound = 5f };
            VehicleTuning.Normalise(data);
            Assert.That(data.SpringScale, Is.EqualTo(VehicleTuning.MaxSpringScale).Within(1e-6));
            Assert.That(data.DampingScale, Is.EqualTo(VehicleTuning.MinDampingScale).Within(1e-6));
            Assert.That(data.FinalDriveScale, Is.EqualTo(VehicleTuning.MaxFinalDriveScale).Within(1e-6));
            Assert.That(data.BrakeScale, Is.EqualTo(VehicleTuning.MinBrakeScale).Within(1e-6));
            Assert.That(data.TireCompound, Is.EqualTo(VehicleTuning.TireWet).Within(1e-6));
        }

        [Test]
        public void NullRecord_UsesNeutralTuning()
        {
            VehicleSpec baseSpec = VehicleCatalog.Create(VehicleCatalog.TempestaGtx);
            VehicleSpec tuned = VehicleTuning.Apply(baseSpec, null);
            Check.AreEqual(baseSpec.SpringFront, tuned.SpringFront, 1e-9);
            Check.AreEqual(baseSpec.GripDry, tuned.GripDry, 1e-9);
        }

        private static double TopSpeed(VehicleSpec spec, IRoadProvider road)
        {
            var car = new VehicleDynamics(spec, road, AssistSettings.Simulation);
            car.Reset(0, 0, 0);
            for (int i = 0; i < 60 * 60; i++)
            {
                car.Advance(1.0 / 60.0, new VehicleInput { Throttle = 1.0, AutoGearbox = true });
            }
            return car.Speed;
        }
    }
}

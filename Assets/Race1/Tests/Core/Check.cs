using NUnit.Framework;

namespace Race1.Tests.Core
{
    /// <summary>
    /// Equality helpers written on top of Assert.That, so the tests compile on both NUnit 3 (Unity 6)
    /// and NUnit 4 (plain dotnet test). Classic Assert.AreEqual is not used on purpose.
    /// </summary>
    internal static class Check
    {
        public static void AreEqual(double expected, double actual, double delta = 0.0)
        {
            Assert.That(actual, Is.EqualTo(expected).Within(delta), $"expected {expected} but was {actual}");
        }

        public static void AreEqual(double expected, double actual, double delta, string message)
        {
            Assert.That(actual, Is.EqualTo(expected).Within(delta), message);
        }

        public static void AreEqual<T>(T expected, T actual)
        {
            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}

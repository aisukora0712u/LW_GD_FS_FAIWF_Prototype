using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class SemanticVersionTests
    {
        [TestCase("0.1.0", 0, 1, 0)]
        [TestCase("1.2.3-rc.1", 1, 2, 3)]
        public void TryParse_ValidVersion_ReturnsParts(string value, int major, int minor, int patch)
        {
            Assert.That(SemanticVersion.TryParse(value, out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(new SemanticVersion(major, minor, patch)));
        }

        [TestCase("")]
        [TestCase("1.0")]
        [TestCase("v1.0.0")]
        [TestCase("1.-1.0")]
        public void TryParse_InvalidVersion_ReturnsFalse(string value)
        {
            Assert.That(SemanticVersion.TryParse(value, out _), Is.False);
        }
    }
}

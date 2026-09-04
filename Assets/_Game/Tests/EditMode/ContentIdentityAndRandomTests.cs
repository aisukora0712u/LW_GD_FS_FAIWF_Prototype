using System;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class ContentIdentityAndRandomTests
    {
        [TestCase("card.iron_strike")]
        [TestCase("story.act1_intro")]
        [TestCase("enemy.boss-01")]
        public void ContentId_AcceptsCanonicalIds(string value)
        {
            Assert.That(ContentId.TryParse(value, out var id), Is.True);
            Assert.That(id.Value, Is.EqualTo(value));
        }

        [TestCase("")]
        [TestCase("Card.Strike")]
        [TestCase("card..strike")]
        [TestCase("card/strike")]
        [TestCase("1card.strike")]
        public void ContentId_RejectsUnstableIds(string value)
        {
            Assert.That(ContentId.TryParse(value, out _), Is.False);
            Assert.Throws<ArgumentException>(() => new ContentId(value));
        }

        [Test]
        public void NamedStreams_AreRepeatableAndIndependent()
        {
            var first = new NamedRandomStreams(123456UL);
            var second = new NamedRandomStreams(123456UL);

            var firstDraws = Enumerable.Range(0, 8).Select(_ => first.Get("combat.draw").NextInt(0, 1000)).ToArray();
            first.Get("map.layout").NextInt(0, 1000);
            var secondDraws = Enumerable.Range(0, 8).Select(_ => second.Get("combat.draw").NextInt(0, 1000)).ToArray();

            Assert.That(secondDraws, Is.EqualTo(firstDraws));
            Assert.That(first.CaptureStates().Keys, Is.EquivalentTo(new[] { "combat.draw", "map.layout" }));
        }

        [Test]
        public void Shuffle_WithSameSeedProducesSameOrder()
        {
            var first = Enumerable.Range(0, 20).ToList();
            var second = Enumerable.Range(0, 20).ToList();

            new DeterministicRandom(42UL).Shuffle(first);
            new DeterministicRandom(42UL).Shuffle(second);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Is.Not.EqualTo(Enumerable.Range(0, 20).ToArray()));
        }
    }
}

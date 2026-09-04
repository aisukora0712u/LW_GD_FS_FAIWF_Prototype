using System;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class NarrativeStateMachineTests
    {
        private static readonly ContentId IntroId = new ContentId("story.intro");
        private static readonly ContentId EndId = new ContentId("story.end");
        private static readonly ContentId BraveChoiceId = new ContentId("choice.be_brave");
        private static readonly ContentId RetreatChoiceId = new ContentId("choice.retreat");
        private static readonly ContentId CourageId = new ContentId("variable.courage");
        private static readonly ContentId RewardCardId = new ContentId("card.reward");
        private static readonly ContentId EncounterId = new ContentId("encounter.first_guardian");

        [Test]
        public void Choice_UpdatesVariablesEmitsCommandsAndCompletes()
        {
            var story = CreateStory(initialCourage: 1);
            Assert.That(story.Start(IntroId).Succeeded, Is.True);

            Assert.That(story.AvailableChoices.Select(choice => choice.Id), Is.EquivalentTo(new[] { BraveChoiceId, RetreatChoiceId }));
            Assert.That(story.Choose(BraveChoiceId).Succeeded, Is.True);

            Assert.That(story.CurrentNode.Id, Is.EqualTo(EndId));
            Assert.That(story.IsCompleted, Is.True);
            Assert.That(story.Variables[CourageId].Integer, Is.EqualTo(2));
            var commands = story.DrainPendingCommands();
            Assert.That(commands.Select(command => command.Kind), Is.EqualTo(new[] { NarrativeCommandKind.GrantCard, NarrativeCommandKind.StartCombat }));
            Assert.That(commands[0].Payload, Is.EqualTo(RewardCardId));
            Assert.That(commands[1].Payload, Is.EqualTo(EncounterId));
            Assert.That(story.Events.Select(item => item.Sequence), Is.EqualTo(Enumerable.Range(1, story.Events.Count).Select(value => (long)value)));
        }

        [Test]
        public void UnavailableChoice_DoesNotChangeNodeOrEvents()
        {
            var story = CreateStory(initialCourage: 0);
            story.Start(IntroId);
            var eventCount = story.Events.Count;

            var result = story.Choose(BraveChoiceId);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(story.CurrentNode.Id, Is.EqualTo(IntroId));
            Assert.That(story.Events.Count, Is.EqualTo(eventCount));
            Assert.That(story.AvailableChoices.Select(choice => choice.Id), Is.EqualTo(new[] { RetreatChoiceId }));
        }

        [Test]
        public void MissingChoiceDestination_IsRejectedAtConstruction()
        {
            var missing = new ContentId("story.missing");
            var node = new StoryNode(
                IntroId,
                new ContentId("speaker.guide"),
                new ContentId("text.intro"),
                new[] { new StoryChoice(RetreatChoiceId, new ContentId("text.retreat"), missing) });

            Assert.Throws<ArgumentException>(() => new NarrativeStateMachine(new[] { node }));
        }

        [Test]
        public void Topology_AcceptsMultipleEntriesAndCycleWithExit()
        {
            var intro = new StoryNode(IntroId, new ContentId("speaker.guide"), new ContentId("text.intro"), new[]
            {
                new StoryChoice(BraveChoiceId, new ContentId("text.loop"), IntroId),
                new StoryChoice(RetreatChoiceId, new ContentId("text.exit"), EndId)
            });
            var end = new StoryNode(EndId, new ContentId("speaker.guide"), new ContentId("text.end"), isTerminal: true);
            var other = new StoryNode(new ContentId("story.other"), new ContentId("speaker.guide"), new ContentId("text.other"), isTerminal: true);
            Assert.That(NarrativeTopology.Validate(new[] { intro, end, other }, new[] { IntroId, other.Id }), Is.Empty);
            Assert.That(NarrativeTopology.Validate(new[] { intro, end, other }, new[] { IntroId }),
                Is.EqualTo(new[] { "Unreachable narrative node story.other." }));
        }

        [Test]
        public void Topology_RejectsClosedCycleAndEmptyNonterminalDeterministically()
        {
            var loop = new StoryNode(IntroId, new ContentId("speaker.guide"), new ContentId("text.intro"),
                new[] { new StoryChoice(BraveChoiceId, new ContentId("text.loop"), IntroId) });
            var deadEnd = new StoryNode(EndId, new ContentId("speaker.guide"), new ContentId("text.end"));
            var errors = NarrativeTopology.Validate(new[] { loop, deadEnd }, new[] { IntroId, EndId });
            Assert.That(errors.Count, Is.EqualTo(2));
            Assert.That(errors, Has.All.Contains("no terminal path"));
            Assert.That(NarrativeTopology.Validate(new[] { deadEnd, loop }, new[] { EndId, IntroId }), Is.EqualTo(errors));
        }

        [Test]
        public void Topology_LongNarrativeUsesIterativeTraversal()
        {
            var nodes = Enumerable.Range(0, 10000).Select(i => new StoryNode(
                new ContentId($"story.n{i}"), new ContentId("speaker.guide"), new ContentId("text.line"),
                i == 9999 ? null : new[] { new StoryChoice(new ContentId($"choice.n{i}"), new ContentId("text.next"), new ContentId($"story.n{i + 1}")) },
                isTerminal: i == 9999)).ToArray();
            Assert.That(NarrativeTopology.Validate(nodes, new[] { nodes[0].Id }), Is.Empty);
        }

        private static NarrativeStateMachine CreateStory(int initialCourage)
        {
            var brave = new StoryChoice(
                BraveChoiceId,
                new ContentId("text.be_brave"),
                EndId,
                new[] { new NarrativeCondition(CourageId, NarrativeConditionOperator.GreaterOrEqual, StoryValue.FromInteger(1)) },
                new[]
                {
                    NarrativeCommand.AddInteger(CourageId, 1),
                    NarrativeCommand.GrantCard(RewardCardId)
                });
            var retreat = new StoryChoice(RetreatChoiceId, new ContentId("text.retreat"), EndId);
            var intro = new StoryNode(
                IntroId,
                new ContentId("speaker.guide"),
                new ContentId("text.intro"),
                new[] { brave, retreat },
                new[] { NarrativeCommand.SetVariable(CourageId, StoryValue.FromInteger(initialCourage)) });
            var ending = new StoryNode(
                EndId,
                new ContentId("speaker.guide"),
                new ContentId("text.end"),
                onEnter: new[] { NarrativeCommand.StartCombat(EncounterId) },
                isTerminal: true);
            return new NarrativeStateMachine(new[] { intro, ending });
        }
    }
}

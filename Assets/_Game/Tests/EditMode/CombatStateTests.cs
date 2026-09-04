using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class CombatStateTests
    {
        private static readonly ContentId PlayerId = new ContentId("actor.player");
        private static readonly ContentId EnemyId = new ContentId("enemy.slime");
        private static readonly ContentId StrikeId = new ContentId("card.strike");
        private static readonly ContentId DefendId = new ContentId("card.defend");
        private static readonly ContentId InsightId = new ContentId("card.insight");

        [Test]
        public void SameSeed_ProducesSameHandAndEventSequence()
        {
            var first = CreateCombat(77UL, enemyHealth: 30, openingHand: 2);
            var second = CreateCombat(77UL, enemyHealth: 30, openingHand: 2);

            first.Start();
            second.Start();

            Assert.That(second.Hand, Is.EqualTo(first.Hand));
            Assert.That(second.Events.Select(ToEventSignature), Is.EqualTo(first.Events.Select(ToEventSignature)));
            Assert.That(second.RandomStates, Is.EqualTo(first.RandomStates));
        }

        [Test]
        public void PlayingCards_UpdatesEnergyDamageAndBlock()
        {
            var combat = CreateCombat(10UL, enemyHealth: 20, openingHand: 3);
            combat.Start();

            var defendIndex = combat.Hand.ToList().IndexOf(DefendId);
            Assert.That(combat.PlayCard(defendIndex).Succeeded, Is.True);
            var strikeIndex = combat.Hand.ToList().IndexOf(StrikeId);
            Assert.That(combat.PlayCard(strikeIndex, 0).Succeeded, Is.True);

            Assert.That(combat.Energy, Is.EqualTo(1));
            Assert.That(combat.Player.Block, Is.EqualTo(5));
            Assert.That(combat.Enemies[0].Health, Is.EqualTo(14));
            Assert.That(combat.DiscardPile, Has.Member(DefendId));
            Assert.That(combat.DiscardPile, Has.Member(StrikeId));
        }

        [Test]
        public void InvalidTarget_DoesNotMutateCombat()
        {
            var combat = CreateCombat(11UL, enemyHealth: 20, openingHand: 3);
            combat.Start();
            var strikeIndex = combat.Hand.ToList().IndexOf(StrikeId);
            var eventsBefore = combat.Events.Count;
            var energyBefore = combat.Energy;
            var handBefore = combat.Hand.ToArray();

            var result = combat.PlayCard(strikeIndex, 99);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(combat.Events.Count, Is.EqualTo(eventsBefore));
            Assert.That(combat.Energy, Is.EqualTo(energyBefore));
            Assert.That(combat.Hand, Is.EqualTo(handBefore));
        }

        [Test]
        public void EnemyTurn_ConsumesBlockThenStartsNextTurn()
        {
            var combat = CreateCombat(12UL, enemyHealth: 20, enemyDamage: 3, openingHand: 3);
            combat.Start();
            combat.PlayCard(combat.Hand.ToList().IndexOf(DefendId));

            var result = combat.EndTurn();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(combat.Phase, Is.EqualTo(CombatPhase.PlayerTurn));
            Assert.That(combat.Turn, Is.EqualTo(2));
            Assert.That(combat.Player.Health, Is.EqualTo(30));
            Assert.That(combat.Player.Block, Is.Zero, "remaining block clears at the next player turn");
        }

        [Test]
        public void DrawEffect_MovesAnotherCardIntoHand()
        {
            var combat = new CombatState(
                CreateDefinitions(),
                new[] { InsightId, InsightId, InsightId, StrikeId },
                new CombatantState(PlayerId, 30),
                new[] { new CombatantState(EnemyId, 20) },
                21UL,
                new CombatConfig(3, 2, 1, 10));
            combat.Start();
            var insightIndex = combat.Hand.ToList().IndexOf(InsightId);
            Assert.That(insightIndex, Is.GreaterThanOrEqualTo(0));
            var drawnBefore = combat.Events.Count(item => item.Kind == CombatEventKind.CardDrawn);

            combat.PlayCard(insightIndex);

            Assert.That(combat.Hand.Count, Is.EqualTo(2));
            Assert.That(combat.Events.Count(item => item.Kind == CombatEventKind.CardDrawn), Is.EqualTo(drawnBefore + 1));
        }

        [Test]
        public void LethalEnemyAttack_EndsCombatInDefeat()
        {
            var combat = CreateCombat(22UL, enemyHealth: 20, enemyDamage: 40, openingHand: 1);
            combat.Start();

            combat.EndTurn();

            Assert.That(combat.Phase, Is.EqualTo(CombatPhase.Defeat));
            Assert.That(combat.Player.Health, Is.Zero);
            Assert.That(combat.Events.Last().Kind, Is.EqualTo(CombatEventKind.Defeat));
        }

        [Test]
        public void LethalCard_EndsCombatInVictory()
        {
            var combat = CreateCombat(13UL, enemyHealth: 5, openingHand: 3);
            combat.Start();

            var result = combat.PlayCard(combat.Hand.ToList().IndexOf(StrikeId), 0);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(combat.Phase, Is.EqualTo(CombatPhase.Victory));
            Assert.That(combat.Events.Last().Kind, Is.EqualTo(CombatEventKind.Victory));
        }

        [Test]
        public void DiscardPile_IsDeterministicallyRecycled()
        {
            var definitions = CreateDefinitions();
            var combat = new CombatState(
                definitions,
                new[] { StrikeId, DefendId },
                new CombatantState(PlayerId, 30),
                new[] { new CombatantState(EnemyId, 50) },
                99UL,
                new CombatConfig(3, 1, 1, 10));
            combat.Start();

            combat.EndTurn();
            combat.EndTurn();

            Assert.That(combat.Events.Count(item => item.Kind == CombatEventKind.DeckShuffled), Is.EqualTo(2));
            Assert.That(combat.Turn, Is.EqualTo(3));
            Assert.That(combat.Hand.Count, Is.EqualTo(1));
        }

        [Test]
        public void StatusDamageModifiers_ApplyInDocumentedOrderBeforeBlock()
        {
            var armorId = new ContentId("card.enemy_armor");
            var comboId = new ContentId("card.status_combo");
            var combat = new CombatState(
                new[]
                {
                    new CardDefinition(armorId, 0, new[] { new CardEffect(CardEffectKind.Block, EffectTarget.SingleEnemy, 5) }),
                    new CardDefinition(comboId, 0, new[]
                    {
                        new CardEffect(CardEffectKind.ApplyStatus, EffectTarget.Self, 2, CombatStatusKind.Strength),
                        new CardEffect(CardEffectKind.ApplyStatus, EffectTarget.Self, 1, CombatStatusKind.Weak),
                        new CardEffect(CardEffectKind.ApplyStatus, EffectTarget.SingleEnemy, 1, CombatStatusKind.Vulnerable),
                        new CardEffect(CardEffectKind.Damage, EffectTarget.SingleEnemy, 10)
                    })
                },
                new[] { armorId, comboId },
                new CombatantState(PlayerId, 30),
                new[] { new CombatantState(EnemyId, 30) },
                8UL,
                new CombatConfig(3, 2, 1, 10));
            combat.Start();
            combat.PlayCard(combat.Hand.ToList().IndexOf(armorId), 0);

            var result = combat.PlayCard(combat.Hand.ToList().IndexOf(comboId), 0);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(combat.Enemies[0].Block, Is.Zero);
            Assert.That(combat.Enemies[0].Health, Is.EqualTo(22), "(10 + 2 Strength) * 75% Weak * 150% Vulnerable = 13, then 5 Block");
            Assert.That(combat.Events.Last(item => item.Kind == CombatEventKind.DamageApplied).Value, Is.EqualTo(8));
        }

        [Test]
        public void TimedStatuses_StackDecayAtOwnerEffectWindowsAndEmitEvents()
        {
            var statusCardId = new ContentId("card.statuses");
            var combat = new CombatState(
                new[]
                {
                    new CardDefinition(statusCardId, 0, new[]
                    {
                        new CardEffect(CardEffectKind.ApplyStatus, EffectTarget.Self, 2, CombatStatusKind.Weak),
                        new CardEffect(CardEffectKind.ApplyStatus, EffectTarget.Self, 2, CombatStatusKind.Vulnerable),
                        new CardEffect(CardEffectKind.ApplyStatus, EffectTarget.SingleEnemy, 2, CombatStatusKind.Weak),
                        new CardEffect(CardEffectKind.ApplyStatus, EffectTarget.SingleEnemy, 2, CombatStatusKind.Vulnerable)
                    })
                },
                new[] { statusCardId },
                new CombatantState(PlayerId, 30),
                new[] { new CombatantState(EnemyId, 30, 10) },
                9UL,
                new CombatConfig(3, 1, 1, 10));
            combat.Start();
            combat.PlayCard(0, 0);

            combat.EndTurn();

            Assert.That(combat.Player.Health, Is.EqualTo(20), "10 damage becomes 7 from enemy Weak, then 10 from player Vulnerable");
            Assert.That(combat.Player.GetStatus(CombatStatusKind.Weak), Is.EqualTo(1));
            Assert.That(combat.Player.GetStatus(CombatStatusKind.Vulnerable), Is.EqualTo(1));
            Assert.That(combat.Enemies[0].GetStatus(CombatStatusKind.Weak), Is.EqualTo(1));
            Assert.That(combat.Enemies[0].GetStatus(CombatStatusKind.Vulnerable), Is.EqualTo(1));
            Assert.That(combat.Events.Count(item => item.Kind == CombatEventKind.StatusChanged), Is.EqualTo(8));
            Assert.That(combat.Events.Where(item => item.Kind == CombatEventKind.StatusChanged).All(item => item.StatusKind != CombatStatusKind.None), Is.True);
        }

        [Test]
        public void CardEffect_RejectsMissingOrUnexpectedStatusKind()
        {
            Assert.Throws<System.ArgumentException>(() => new CardEffect(CardEffectKind.ApplyStatus, EffectTarget.Self, 1));
            Assert.Throws<System.ArgumentException>(() => new CardEffect(CardEffectKind.Damage, EffectTarget.SingleEnemy, 1, CombatStatusKind.Strength));
        }

        [Test]
        public void CombatStartEffects_TriggerInOrderAndAffectFirstTurn()
        {
            var wardId = new ContentId("relic.ward");
            var quillId = new ContentId("relic.quill");
            var combat = new CombatState(
                CreateDefinitions(),
                new[] { StrikeId, DefendId, InsightId },
                new CombatantState(PlayerId, 30),
                new[] { new CombatantState(EnemyId, 30) },
                15UL,
                new CombatConfig(3, 3, 1, 10),
                new[]
                {
                    new CombatStartEffect(wardId, CombatStartEffectKind.GainBlock, 3),
                    new CombatStartEffect(quillId, CombatStartEffectKind.ApplyStatus, 2, CombatStatusKind.Strength)
                });

            combat.Start();

            Assert.That(combat.Player.Block, Is.EqualTo(3));
            Assert.That(combat.Player.GetStatus(CombatStatusKind.Strength), Is.EqualTo(2));
            Assert.That(combat.Events.Where(item => item.Kind == CombatEventKind.RelicTriggered).Select(item => item.Source), Is.EqualTo(new[] { wardId, quillId }));
            Assert.That(combat.PlayCard(combat.Hand.ToList().IndexOf(StrikeId), 0).Succeeded, Is.True);
            Assert.That(combat.Enemies[0].Health, Is.EqualTo(22));
        }

        [Test]
        public void EnemyIntents_PreviewExecuteInOrderAndCycle()
        {
            var braceId = new ContentId("intent.slime_brace");
            var strikeId = new ContentId("intent.slime_strike");
            var enemy = new CombatantState(
                EnemyId,
                30,
                intents: new[]
                {
                    new EnemyIntentDefinition(braceId, new[]
                    {
                        new EnemyIntentEffect(EnemyIntentEffectKind.GainBlock, EnemyIntentTarget.Self, 5)
                    }),
                    new EnemyIntentDefinition(strikeId, new[]
                    {
                        new EnemyIntentEffect(EnemyIntentEffectKind.Attack, EnemyIntentTarget.Player, 7)
                    })
                });
            var combat = new CombatState(CreateDefinitions(), new[] { DefendId }, new CombatantState(PlayerId, 30), new[] { enemy }, 41UL, new CombatConfig(3, 1, 1, 10));
            combat.Start();

            Assert.That(enemy.CurrentIntent.Id, Is.EqualTo(braceId));
            combat.EndTurn();
            Assert.That(enemy.Block, Is.EqualTo(5));
            Assert.That(enemy.CurrentIntent.Id, Is.EqualTo(strikeId));
            Assert.That(combat.Events.Last(item => item.Kind == CombatEventKind.EnemyIntentStarted).Content, Is.EqualTo(braceId));

            combat.EndTurn();
            Assert.That(enemy.Block, Is.Zero);
            Assert.That(combat.Player.Health, Is.EqualTo(23));
            Assert.That(enemy.CurrentIntent.Id, Is.EqualTo(braceId));
            Assert.That(combat.Events.Last(item => item.Kind == CombatEventKind.DamageApplied).Content, Is.EqualTo(strikeId));
        }

        [Test]
        public void EnemyIntent_OrderedStatusThenAttackUsesDamagePipeline()
        {
            var intentId = new ContentId("intent.slime_breaker");
            var enemy = new CombatantState(
                EnemyId,
                30,
                intents: new[]
                {
                    new EnemyIntentDefinition(intentId, new[]
                    {
                        new EnemyIntentEffect(EnemyIntentEffectKind.ApplyStatus, EnemyIntentTarget.Player, 2, CombatStatusKind.Vulnerable),
                        new EnemyIntentEffect(EnemyIntentEffectKind.Attack, EnemyIntentTarget.Player, 10)
                    })
                });
            var combat = new CombatState(CreateDefinitions(), new[] { DefendId }, new CombatantState(PlayerId, 30), new[] { enemy }, 42UL, new CombatConfig(3, 1, 1, 10));
            combat.Start();

            combat.EndTurn();

            Assert.That(combat.Player.Health, Is.EqualTo(15));
            Assert.That(combat.Player.GetStatus(CombatStatusKind.Vulnerable), Is.EqualTo(1));
            var intentEvents = combat.Events.Where(item => item.Content == intentId).ToArray();
            Assert.That(intentEvents.Select(item => item.Kind), Is.EqualTo(new[]
            {
                CombatEventKind.EnemyIntentStarted,
                CombatEventKind.StatusChanged,
                CombatEventKind.DamageApplied
            }));
        }

        [Test]
        public void DefeatedEnemy_DoesNotExecuteOrAdvanceIntent()
        {
            var firstIntent = new EnemyIntentDefinition(new ContentId("intent.first"), new[]
            {
                new EnemyIntentEffect(EnemyIntentEffectKind.Attack, EnemyIntentTarget.Player, 20)
            });
            var secondIntent = new EnemyIntentDefinition(new ContentId("intent.second"), new[]
            {
                new EnemyIntentEffect(EnemyIntentEffectKind.Attack, EnemyIntentTarget.Player, 3)
            });
            var first = new CombatantState(new ContentId("enemy.first"), 5, intents: new[] { firstIntent });
            var second = new CombatantState(new ContentId("enemy.second"), 20, intents: new[] { secondIntent });
            var combat = new CombatState(CreateDefinitions(), new[] { StrikeId, DefendId }, new CombatantState(PlayerId, 30), new[] { first, second }, 43UL, new CombatConfig(3, 2, 1, 10));
            combat.Start();
            Assert.That(combat.PlayCard(combat.Hand.ToList().IndexOf(StrikeId), 0).Succeeded, Is.True);

            combat.EndTurn();

            Assert.That(combat.Player.Health, Is.EqualTo(27));
            Assert.That(first.IntentIndex, Is.Zero);
            Assert.That(combat.Events.Where(item => item.Kind == CombatEventKind.EnemyIntentStarted).Select(item => item.Source), Is.EqualTo(new[] { second.Id }));
        }

        [Test]
        public void EnemyIntent_RejectsInvalidTargetsAndDuplicateIds()
        {
            Assert.Throws<System.ArgumentException>(() => new EnemyIntentEffect(EnemyIntentEffectKind.Attack, EnemyIntentTarget.Self, 1));
            Assert.Throws<System.ArgumentException>(() => new EnemyIntentEffect(EnemyIntentEffectKind.GainBlock, EnemyIntentTarget.Player, 1));
            var intent = new EnemyIntentDefinition(new ContentId("intent.duplicate"), new[]
            {
                new EnemyIntentEffect(EnemyIntentEffectKind.Attack, EnemyIntentTarget.Player, 1)
            });
            Assert.Throws<System.ArgumentException>(() => new CombatantState(EnemyId, 10, intents: new[] { intent, intent }));
        }

        private static CombatState CreateCombat(ulong seed, int enemyHealth, int enemyDamage = 0, int openingHand = 3)
        {
            return new CombatState(
                CreateDefinitions(),
                new[] { StrikeId, DefendId, InsightId },
                new CombatantState(PlayerId, 30),
                new[] { new CombatantState(EnemyId, enemyHealth, enemyDamage) },
                seed,
                new CombatConfig(3, openingHand, 1, 10));
        }

        private static CardDefinition[] CreateDefinitions()
        {
            return new[]
            {
                new CardDefinition(StrikeId, 1, new[] { new CardEffect(CardEffectKind.Damage, EffectTarget.SingleEnemy, 6) }),
                new CardDefinition(DefendId, 1, new[] { new CardEffect(CardEffectKind.Block, EffectTarget.Self, 5) }),
                new CardDefinition(InsightId, 1, new[] { new CardEffect(CardEffectKind.Draw, EffectTarget.Self, 1) })
            };
        }

        private static string ToEventSignature(CombatEvent item) => $"{item.Sequence}:{item.Turn}:{item.Kind}:{item.Source}:{item.Target}:{item.Content}:{item.Value}:{item.StatusKind}";
    }
}

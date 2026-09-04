using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Game.Core;
using Game.Gameplay;
using Game.Infrastructure;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class RunSessionVerticalSliceTests
    {
        private static readonly ContentId FightChoiceId = new ContentId("choice.face_wisp");
        private static readonly ContentId PrologueRouteId = new ContentId("route.prologue");
        private static readonly ContentId WispRouteId = new ContentId("route.wisp");
        private static readonly ContentId InterludeRouteId = new ContentId("route.interlude");
        private static readonly ContentId RestRouteId = new ContentId("route.rest");
        private static readonly ContentId ShopRouteId = new ContentId("route.shop");
        private static readonly ContentId WardenRouteId = new ContentId("route.warden");

        [Test]
        public void ApprovedSample_ParsesAndResolvesAllReferences()
        {
            var result = ContentCatalogJson.ParseApproved(ReadSample());

            Assert.That(result.Succeeded, Is.True, string.Join(Environment.NewLine, result.Errors));
            Assert.That(result.Catalog.ContentVersion, Is.EqualTo("vertical-slice.7"));
            Assert.That(result.Catalog.Cards.Count, Is.EqualTo(11));
            Assert.That(result.Catalog.Statuses.Count, Is.EqualTo(3));
            Assert.That(result.Catalog.Relics.Count, Is.EqualTo(1));
            Assert.That(result.Catalog.Shops.Count, Is.EqualTo(1));
            Assert.That(result.Catalog.Encounters.Count, Is.EqualTo(3));
            Assert.That(result.Catalog.RouteNodes.Count, Is.EqualTo(7));
        }

        [Test]
        public void RequiredLocales_HaveCompleteVersionMatchedCoverage()
        {
            var content = ParseSample();
            var english = LocalizationCatalogJson.ParseApproved(ReadLocalization("vertical-slice.en.json"));
            var chinese = LocalizationCatalogJson.ParseApproved(ReadLocalization("vertical-slice.zh-Hans.json"));

            Assert.That(english.Succeeded, Is.True, string.Join(Environment.NewLine, english.Errors));
            Assert.That(chinese.Succeeded, Is.True, string.Join(Environment.NewLine, chinese.Errors));
            var errors = LocalizationCatalogJson.ValidateCoverage(content, new[] { english.Catalog, chinese.Catalog }, new[] { "en", "zh-Hans" });
            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void LocalizationCoverage_RejectsMissingKeysAndVersionDrift()
        {
            var content = ParseSample();
            var english = LocalizationCatalogJson.ParseApproved(ReadLocalization("vertical-slice.en.json"));
            var incompleteJson = ReadLocalization("vertical-slice.zh-Hans.json")
                .Replace("    { \"key\": \"ui.action.restart\", \"value\": \"重新开始\" },\r\n", string.Empty)
                .Replace("    { \"key\": \"ui.action.restart\", \"value\": \"重新开始\" },\n", string.Empty)
                .Replace("vertical-slice.7", "vertical-slice.outdated");
            var incomplete = LocalizationCatalogJson.ParseApproved(incompleteJson);
            Assert.That(incomplete.Succeeded, Is.True, string.Join(Environment.NewLine, incomplete.Errors));

            var errors = LocalizationCatalogJson.ValidateCoverage(content, new[] { english.Catalog, incomplete.Catalog }, new[] { "en", "zh-Hans" });

            Assert.That(errors, Has.Some.Contains("does not match"));
            Assert.That(errors, Has.Some.Contains("ui.action.restart"));
        }

        [Test]
        public void DraftAndMissingReferences_AreRejected()
        {
            var draft = ContentCatalogJson.ParseApproved(ReadSample().Replace("\"status\": \"approved\"", "\"status\": \"draft\""));
            var missingEnemy = ContentCatalogJson.ParseApproved(ReadSample().Replace("\"enemyIds\": [\"enemy.ink_wisp\"]", "\"enemyIds\": [\"enemy.missing\"]"));
            var missingOwner = ContentCatalogJson.ParseApproved(ReadSample().Replace("\"owner\": \"design\"", "\"owner\": \"\""));
            var missingStatus = ContentCatalogJson.ParseApproved(ReadSample().Replace("\"statusId\": \"status.vulnerable\"", "\"statusId\": \"status.missing\""));
            var missingRelic = ContentCatalogJson.ParseApproved(ReadSample().Replace("\"payload\": \"relic.ember_quill\"", "\"payload\": \"relic.missing\""));

            Assert.That(draft.Succeeded, Is.False);
            Assert.That(draft.Errors, Has.Some.Contains("approved"));
            Assert.That(missingEnemy.Succeeded, Is.False);
            Assert.That(missingEnemy.Errors, Has.Some.Contains("missing enemy"));
            Assert.That(missingOwner.Succeeded, Is.False);
            Assert.That(missingOwner.Errors, Has.Some.Contains("metadata.owner"));
            Assert.That(missingStatus.Succeeded, Is.False);
            Assert.That(missingStatus.Errors, Has.Some.Contains("missing status"));
            Assert.That(missingRelic.Succeeded, Is.False);
            Assert.That(missingRelic.Errors, Has.Some.Contains("missing relic"));
        }

        [Test]
        public void NarrativeTopology_IsEnforcedForApprovedAndReviewContent()
        {
            var invalid = ReadSample().Replace("\"nextNodeId\": \"story.wisp_battle\"", "\"nextNodeId\": \"story.crossroads\"");
            Assert.That(invalid, Is.Not.EqualTo(ReadSample()), "Fixture must change a real edge.");
            var runtime = ContentCatalogJson.ParseApproved(invalid);
            var review = ContentCatalogJson.ParseForReview(invalid.Replace("\"status\": \"approved\"", "\"status\": \"draft\""));
            Assert.That(runtime.Succeeded, Is.False);
            Assert.That(review.Succeeded, Is.False);
            Assert.That(runtime.Errors, Has.Some.Contains("Unreachable narrative node story.wisp_battle"));
            Assert.That(review.Errors, Has.Some.Contains("Unreachable narrative node story.wisp_battle"));
        }

        [Test]
        public void ShopContent_RejectsMissingCardsInvalidPricesAndOversizedOffers()
        {
            var missingCard = ContentCatalogJson.ParseApproved(ReadSample().Replace(
                "\"inventoryPool\": [\"card.heavy_strike\",",
                "\"inventoryPool\": [\"card.missing\","));
            var zeroPrice = ContentCatalogJson.ParseApproved(ReadSample().Replace("\"cardPrice\": 12", "\"cardPrice\": 0"));
            var oversizedOffers = ContentCatalogJson.ParseApproved(ReadSample().Replace("\"offerCount\": 3", "\"offerCount\": 99"));

            Assert.That(missingCard.Succeeded, Is.False);
            Assert.That(missingCard.Errors, Has.Some.Contains("missing card"));
            Assert.That(zeroPrice.Succeeded, Is.False);
            Assert.That(zeroPrice.Errors, Has.Some.Contains("valid range"));
            Assert.That(oversizedOffers.Succeeded, Is.False);
            Assert.That(oversizedOffers.Errors, Has.Some.Contains("enough unique cards"));
        }

        [Test]
        public void EnemyIntentContent_RejectsEmptyDuplicateInvalidTargetAndMissingStatus()
        {
            var emptyCycle = ContentCatalogJson.ParseApproved(ReadSample().Replace(
                "\"intents\": [\r\n        { \"id\": \"intent.wisp_scratch\"",
                "\"intents\": [] , \"ignored\": [\r\n        { \"id\": \"intent.wisp_scratch\"").Replace(
                "\"intents\": [\n        { \"id\": \"intent.wisp_scratch\"",
                "\"intents\": [] , \"ignored\": [\n        { \"id\": \"intent.wisp_scratch\""));
            var duplicateId = ContentCatalogJson.ParseApproved(ReadSample().Replace("intent.wisp_haze", "intent.wisp_scratch"));
            var invalidTarget = ContentCatalogJson.ParseApproved(ReadSample().Replace(
                "\"kind\": \"GainBlock\", \"target\": \"Self\", \"amount\": 6",
                "\"kind\": \"GainBlock\", \"target\": \"Player\", \"amount\": 6"));
            var missingStatus = ContentCatalogJson.ParseApproved(ReadSample().Replace(
                "\"statusId\": \"status.weak\" }] }",
                "\"statusId\": \"status.missing\" }] }"));

            Assert.That(emptyCycle.Succeeded, Is.False);
            Assert.That(duplicateId.Succeeded, Is.False);
            Assert.That(duplicateId.Errors, Has.Some.Contains("unique IDs"));
            Assert.That(invalidTarget.Succeeded, Is.False);
            Assert.That(invalidTarget.Errors, Has.Some.Contains("target self"));
            Assert.That(missingStatus.Succeeded, Is.False);
            Assert.That(missingStatus.Errors, Has.Some.Contains("missing status"));
        }

        [TestCase(false, RunPhase.Defeat, TestName = "FullPath_HandOrderBaseline_Seed4242_Defeats")]
        [TestCase(true, RunPhase.Reward, TestName = "FullPath_EffectScore_Seed4242_Completes")]
        public void FullPath_NarrativeCombatRewardAndCompletion(bool scoreBossCards, RunPhase expectedBossOutcome)
        {
            var catalog = ParseSample();
            var session = new RunSession(catalog, 4242UL);

            Assert.That(session.Start().Succeeded, Is.True);
            Assert.That(session.Phase, Is.EqualTo(RunPhase.Map));
            Assert.That(session.AvailableMapNodes.Select(node => node.Id), Is.EqualTo(new[] { PrologueRouteId }));
            Assert.That(session.SelectMapNode(PrologueRouteId).Succeeded, Is.True);
            Assert.That(session.Phase, Is.EqualTo(RunPhase.Narrative));
            Assert.That(session.Resources[new ContentId("resource.gold")], Is.EqualTo(10));
            Assert.That(session.Choose(FightChoiceId).Succeeded, Is.True);
            Assert.That(session.Phase, Is.EqualTo(RunPhase.Map));
            Assert.That(session.AvailableMapNodes.Select(node => node.Id), Does.Contain(WispRouteId));
            Assert.That(session.SelectMapNode(WispRouteId).Succeeded, Is.True);
            Assert.That(session.Phase, Is.EqualTo(RunPhase.Combat));
            Assert.That(session.CreateCheckpoint().Succeeded, Is.False);

            WinCombat(session, catalog);

            Assert.That(session.Phase, Is.EqualTo(RunPhase.Reward));
            Assert.That(session.RewardCandidates.Count, Is.EqualTo(3));
            var deckBefore = session.Deck.Count;
            var selected = session.RewardCandidates[0];
            Assert.That(session.ClaimReward(selected).Succeeded, Is.True);
            Assert.That(session.Phase, Is.EqualTo(RunPhase.Map));
            Assert.That(session.Deck.Count, Is.EqualTo(deckBefore + 1));
            Assert.That(session.Deck.Last(), Is.EqualTo(selected));

            Assert.That(session.SelectMapNode(InterludeRouteId).Succeeded, Is.True);
            Assert.That(session.Choose(new ContentId("choice.enter_archive")).Succeeded, Is.True);
            Assert.That(session.Phase, Is.EqualTo(RunPhase.Map));
            Assert.That(session.Relics, Is.EqualTo(new[] { new ContentId("relic.ember_quill") }));
            Assert.That(session.SelectMapNode(RestRouteId).Succeeded, Is.True);
            Assert.That(session.Phase, Is.EqualTo(RunPhase.Upgrade));
            var deckBeforeUpgrade = session.Deck.ToArray();
            var originalCard = session.Deck[0];
            var expectedUpgrade = catalog.Cards[originalCard].UpgradeToId;
            Assert.That(session.UpgradeCard(0).Succeeded, Is.True);
            Assert.That(session.Deck[0], Is.EqualTo(expectedUpgrade));
            Assert.That(session.Deck.Skip(1), Is.EqualTo(deckBeforeUpgrade.Skip(1)));
            Assert.That(session.Phase, Is.EqualTo(RunPhase.Map));
            Assert.That(session.SelectMapNode(ShopRouteId).Succeeded, Is.True);
            Assert.That(session.ShopOffers.Count, Is.EqualTo(3));
            Assert.That(session.PurchaseShopCard(session.ShopOffers[0]).Succeeded, Is.True);
            Assert.That(session.Resources[new ContentId("resource.gold")], Is.EqualTo(3));
            Assert.That(session.LeaveShop().Succeeded, Is.True);
            Assert.That(session.SelectMapNode(WardenRouteId).Succeeded, Is.True);
            Assert.That(session.Combat.Player.Block, Is.EqualTo(2));
            Assert.That(session.Combat.Player.GetStatus(CombatStatusKind.Strength), Is.EqualTo(1));
            Assert.That(session.Combat.Events.Any(item => item.Kind == CombatEventKind.RelicTriggered && item.Content == new ContentId("relic.ember_quill")), Is.True);
            WinCombat(session, catalog, scoreBossCards, expectedBossOutcome);
            if (expectedBossOutcome == RunPhase.Defeat)
            {
                Assert.That(session.PlayerHealth, Is.Zero);
                Assert.That(session.ClaimReward(new ContentId("card.strike")).Succeeded, Is.False);
                return;
            }
            Assert.That(session.ClaimReward(session.RewardCandidates[0]).Succeeded, Is.True);
            Assert.That(session.Phase, Is.EqualTo(RunPhase.Completed));
        }

        [Test]
        public void SameSeed_ProducesSameCombatOpeningAndRewards()
        {
            var catalog = ParseSample();
            var first = StartFight(catalog, 9001UL);
            var second = StartFight(catalog, 9001UL);

            Assert.That(second.Combat.Hand, Is.EqualTo(first.Combat.Hand));
            WinCombat(first, catalog);
            WinCombat(second, catalog);
            Assert.That(second.RewardCandidates, Is.EqualTo(first.RewardCandidates));
        }

        [Test]
        public void LethalEncounter_EntersDefeatAndRejectsFurtherCommands()
        {
            var lethalJson = ReadSample().Replace(
                "{ \"kind\": \"Attack\", \"target\": \"Player\", \"amount\": 4 }",
                "{ \"kind\": \"Attack\", \"target\": \"Player\", \"amount\": 100 }");
            var parsed = ContentCatalogJson.ParseApproved(lethalJson);
            Assert.That(parsed.Succeeded, Is.True, string.Join(Environment.NewLine, parsed.Errors));
            var session = StartFight(parsed.Catalog, 5UL);

            session.EndTurn();

            Assert.That(session.Phase, Is.EqualTo(RunPhase.Defeat));
            Assert.That(session.PlayerHealth, Is.Zero);
            Assert.That(session.Choose(FightChoiceId).Succeeded, Is.False);
            Assert.That(session.ClaimReward(new ContentId("card.insight")).Succeeded, Is.False);
        }

        [Test]
        public void CheckpointJson_RoundTripsAndRestoresSafeState()
        {
            var catalog = ParseSample();
            var session = StartFight(catalog, ulong.MaxValue - 7);
            WinCombat(session, catalog);
            var checkpointResult = session.CreateCheckpoint();
            Assert.That(checkpointResult.Succeeded, Is.True);

            var json = RunCheckpointJson.Serialize(checkpointResult.Checkpoint);
            var decoded = RunCheckpointJson.Deserialize(json);
            Assert.That(decoded.Succeeded, Is.True, decoded.Error);
            Assert.That(RunSession.TryRestore(catalog, decoded.Checkpoint, out var restored, out var error), Is.True, error);

            Assert.That(restored.RootSeed, Is.EqualTo(session.RootSeed));
            Assert.That(restored.Phase, Is.EqualTo(RunPhase.Reward));
            Assert.That(restored.Deck, Is.EqualTo(session.Deck));
            Assert.That(restored.RewardCandidates, Is.EqualTo(session.RewardCandidates));
            Assert.That(restored.Resources, Is.EqualTo(session.Resources));
            Assert.That(restored.CurrentMapNodeId, Is.EqualTo(session.CurrentMapNodeId));
            Assert.That(restored.VisitedMapNodeIds, Is.EqualTo(session.VisitedMapNodeIds));
            Assert.That(restored.Narrative.CurrentNode.Id, Is.EqualTo(session.Narrative.CurrentNode.Id));
            Assert.That(restored.Narrative.Variables, Is.EqualTo(session.Narrative.Variables));
        }

        [Test]
        public void Restore_RejectsDifferentContentVersion()
        {
            var catalog = ParseSample();
            var session = new RunSession(catalog, 1UL);
            session.Start();
            var checkpoint = session.CreateCheckpoint().Checkpoint;
            var changed = ContentCatalogJson.ParseApproved(ReadSample().Replace("vertical-slice.7", "vertical-slice.changed"));
            Assert.That(changed.Succeeded, Is.True, string.Join(Environment.NewLine, changed.Errors));

            Assert.That(RunSession.TryRestore(changed.Catalog, checkpoint, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("content version"));
        }

        [Test]
        public void Map_RejectsUnknownUnavailableAndVisitedNodes()
        {
            var session = new RunSession(ParseSample(), 19UL);
            Assert.That(session.Start().Succeeded, Is.True);
            Assert.That(session.SelectMapNode(WispRouteId).Succeeded, Is.False);
            Assert.That(session.SelectMapNode(new ContentId("route.missing")).Succeeded, Is.False);
            Assert.That(session.SelectMapNode(PrologueRouteId).Succeeded, Is.True);
            Assert.That(session.Choose(FightChoiceId).Succeeded, Is.True);
            Assert.That(session.SelectMapNode(PrologueRouteId).Succeeded, Is.False);
        }

        [Test]
        public void Upgrade_RejectsWrongPhaseAndPersistsThroughCheckpoint()
        {
            var catalog = ParseSample();
            var session = new RunSession(catalog, 31UL);
            Assert.That(session.Start().Succeeded, Is.True);
            Assert.That(session.UpgradeCard(0).Succeeded, Is.False);
            ReachRest(session, catalog);

            var checkpoint = session.CreateCheckpoint();
            Assert.That(checkpoint.Succeeded, Is.True, checkpoint.Error);
            var decoded = RunCheckpointJson.Deserialize(RunCheckpointJson.Serialize(checkpoint.Checkpoint));
            Assert.That(RunSession.TryRestore(catalog, decoded.Checkpoint, out var restored, out var error), Is.True, error);
            Assert.That(restored.Phase, Is.EqualTo(RunPhase.Upgrade));
            Assert.That(restored.Relics, Is.EqualTo(new[] { new ContentId("relic.ember_quill") }));
            Assert.That(restored.UpgradeCard(0).Succeeded, Is.True);
            Assert.That(restored.Deck[0], Is.EqualTo(new ContentId("card.strike_plus")));
            Assert.That(restored.Phase, Is.EqualTo(RunPhase.Map));
            Assert.That(restored.SelectMapNode(ShopRouteId).Succeeded, Is.True);
            Assert.That(restored.LeaveShop().Succeeded, Is.True);
            Assert.That(restored.SelectMapNode(WardenRouteId).Succeeded, Is.True);
            for (var turn = 0; turn < 2 && !restored.Combat.Hand.Contains(new ContentId("card.strike_plus")); turn++)
            {
                Assert.That(restored.EndTurn().Succeeded, Is.True);
            }

            var upgradedHandIndex = restored.Combat.Hand.ToList().IndexOf(new ContentId("card.strike_plus"));
            Assert.That(upgradedHandIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(restored.PlayCard(upgradedHandIndex, 0).Succeeded, Is.True);
            Assert.That(restored.Combat.Events.Last(item => item.Kind == CombatEventKind.DamageApplied).Value, Is.EqualTo(12), "Strike+ 11 plus Ember Quill Strength 1");
        }

        [Test]
        public void Shop_InventoryIsDeterministicAndFailedCommandsAreAtomic()
        {
            var catalog = ParseSample();
            var first = new RunSession(catalog, 7001UL);
            var second = new RunSession(catalog, 7001UL);
            ReachShop(first, catalog);
            ReachShop(second, catalog);

            Assert.That(second.ShopOffers, Is.EqualTo(first.ShopOffers));
            Assert.That(first.ShopOffers.Distinct().Count(), Is.EqualTo(first.ShopOffers.Count));
            var offer = first.ShopOffers[0];
            var deckCount = first.Deck.Count;
            Assert.That(first.PurchaseShopCard(offer).Succeeded, Is.True);
            Assert.That(first.Deck.Count, Is.EqualTo(deckCount + 1));
            Assert.That(first.Resources[new ContentId("resource.gold")], Is.EqualTo(3));

            var deckAfterPurchase = first.Deck.ToArray();
            var offersAfterPurchase = first.ShopOffers.ToArray();
            var goldAfterPurchase = first.Resources[new ContentId("resource.gold")];
            Assert.That(first.PurchaseShopCard(offer).Succeeded, Is.False);
            Assert.That(first.RemoveShopCard(0).Succeeded, Is.False);
            Assert.That(first.PurchaseShopCard(new ContentId("card.strike")).Succeeded, Is.False);
            Assert.That(first.Deck, Is.EqualTo(deckAfterPurchase));
            Assert.That(first.ShopOffers, Is.EqualTo(offersAfterPurchase));
            Assert.That(first.Resources[new ContentId("resource.gold")], Is.EqualTo(goldAfterPurchase));

            var removalDeckCount = second.Deck.Count;
            Assert.That(second.RemoveShopCard(0).Succeeded, Is.True);
            Assert.That(second.Deck.Count, Is.EqualTo(removalDeckCount - 1));
            Assert.That(second.ShopRemovalUsed, Is.True);
            Assert.That(second.Resources[new ContentId("resource.gold")], Is.EqualTo(7));
            Assert.That(second.RemoveShopCard(0).Succeeded, Is.False);
        }

        [Test]
        public void ShopCheckpointV4_RoundTripsInventoryRemovalAndGold()
        {
            var catalog = ParseSample();
            var session = new RunSession(catalog, 8128UL);
            ReachShop(session, catalog);
            Assert.That(session.RemoveShopCard(0).Succeeded, Is.True);
            var expectedOffers = session.ShopOffers.ToArray();
            var expectedDeck = session.Deck.ToArray();

            var decoded = RunCheckpointJson.Deserialize(RunCheckpointJson.Serialize(session.CreateCheckpoint().Checkpoint));
            Assert.That(decoded.Succeeded, Is.True, decoded.Error);
            Assert.That(decoded.Checkpoint.SchemaVersion, Is.EqualTo(4));
            Assert.That(RunSession.TryRestore(catalog, decoded.Checkpoint, out var restored, out var error), Is.True, error);
            Assert.That(restored.Phase, Is.EqualTo(RunPhase.Shop));
            Assert.That(restored.ActiveShop.Id, Is.EqualTo(new ContentId("shop.ink_market")));
            Assert.That(restored.ShopOffers, Is.EqualTo(expectedOffers));
            Assert.That(restored.ShopRemovalUsed, Is.True);
            Assert.That(restored.Deck, Is.EqualTo(expectedDeck));
            Assert.That(restored.Resources[new ContentId("resource.gold")], Is.EqualTo(7));
            Assert.That(restored.LeaveShop().Succeeded, Is.True);
            Assert.That(restored.Phase, Is.EqualTo(RunPhase.Map));
        }

        [Test]
        public void BalanceSimulation_IsDeterministicAndDetectsLethalContent()
        {
            var catalog = ParseSample();
            var first = JsonUtility.ToJson(RunBalanceSimulator.Simulate(catalog, 40, 100UL));
            var second = JsonUtility.ToJson(RunBalanceSimulator.Simulate(catalog, 40, 100UL));
            Assert.That(second, Is.EqualTo(first));

            var report = RunBalanceSimulator.Simulate(catalog, 40, 100UL);
            Assert.That(report.stalledRuns, Is.Zero);
            Assert.That(report.completedRuns + report.defeatedRuns, Is.EqualTo(40));
            Assert.That(report.routeSelections.Any(item => item.id == "route.warden" && item.count > 0), Is.True);
            Assert.That(BalanceSimulationGate.Passes(report, 0, 1000, 0), Is.True);
            Assert.That(BalanceSimulationGate.Passes(report, 1000, 1000, 0), Is.False);

            var lethal = ContentCatalogJson.ParseApproved(ReadSample()
                .Replace("\"maximumHealth\": 24", "\"maximumHealth\": 1000")
                .Replace("\"maximumHealth\": 30", "\"maximumHealth\": 1000")
                .Replace("\"maximumHealth\": 100", "\"maximumHealth\": 1000")
                .Replace("\"kind\": \"Attack\", \"target\": \"Player\", \"amount\": 4", "\"kind\": \"Attack\", \"target\": \"Player\", \"amount\": 100")
                .Replace("\"kind\": \"Attack\", \"target\": \"Player\", \"amount\": 8", "\"kind\": \"Attack\", \"target\": \"Player\", \"amount\": 100")
                .Replace("\"kind\": \"Attack\", \"target\": \"Player\", \"amount\": 10", "\"kind\": \"Attack\", \"target\": \"Player\", \"amount\": 100")
                .Replace("\"kind\": \"Attack\", \"target\": \"Player\", \"amount\": 16", "\"kind\": \"Attack\", \"target\": \"Player\", \"amount\": 100"));
            Assert.That(lethal.Succeeded, Is.True, string.Join(Environment.NewLine, lethal.Errors));
            var lethalReport = RunBalanceSimulator.Simulate(lethal.Catalog, 20, 1UL);
            Assert.That(lethalReport.defeatedRuns, Is.EqualTo(20));
            Assert.That(lethalReport.completedRuns, Is.Zero);
            Assert.That(lethalReport.stalledRuns, Is.Zero);
        }

        [Test]
        public void CheckpointJson_MigratesV1ThroughV3AndRejectsFutureSchema()
        {
            const string versionOne = "{\"schemaVersion\":1,\"contentVersion\":\"vertical-slice.1\",\"rootSeed\":\"7\",\"phase\":\"Completed\",\"currentNodeId\":\"story.peaceful_end\",\"narrativeCompleted\":true,\"playerHealth\":40,\"encounterIndex\":0,\"deck\":[],\"rewardCandidates\":[],\"variables\":[],\"resources\":[]}";
            var migrated = RunCheckpointJson.Deserialize(versionOne);

            Assert.That(migrated.Succeeded, Is.True, migrated.Error);
            Assert.That(migrated.Checkpoint.SchemaVersion, Is.EqualTo(4));
            Assert.That(migrated.Checkpoint.CurrentMapNodeId.IsEmpty, Is.True);
            Assert.That(migrated.Checkpoint.VisitedMapNodeIds, Is.Empty);
            const string versionTwo = "{\"schemaVersion\":2,\"contentVersion\":\"vertical-slice.2\",\"rootSeed\":\"8\",\"phase\":\"Map\",\"currentNodeId\":\"story.peaceful_end\",\"narrativeCompleted\":true,\"playerHealth\":40,\"encounterIndex\":1,\"currentMapNodeId\":\"route.prologue\",\"visitedMapNodeIds\":[\"route.prologue\"],\"deck\":[],\"rewardCandidates\":[],\"variables\":[],\"resources\":[]}";
            var migratedV2 = RunCheckpointJson.Deserialize(versionTwo);
            Assert.That(migratedV2.Succeeded, Is.True, migratedV2.Error);
            Assert.That(migratedV2.Checkpoint.SchemaVersion, Is.EqualTo(4));
            Assert.That(migratedV2.Checkpoint.CurrentMapNodeId, Is.EqualTo(new ContentId("route.prologue")));
            Assert.That(migratedV2.Checkpoint.Relics, Is.Empty);
            const string versionThree = "{\"schemaVersion\":3,\"contentVersion\":\"vertical-slice.5\",\"rootSeed\":\"9\",\"phase\":\"Map\",\"currentNodeId\":\"story.warden_gate\",\"narrativeCompleted\":true,\"playerHealth\":30,\"encounterIndex\":1,\"currentMapNodeId\":\"route.rest\",\"visitedMapNodeIds\":[\"route.rest\"],\"relics\":[\"relic.ember_quill\"],\"deck\":[],\"rewardCandidates\":[],\"variables\":[],\"resources\":[]}";
            var migratedV3 = RunCheckpointJson.Deserialize(versionThree);
            Assert.That(migratedV3.Succeeded, Is.True, migratedV3.Error);
            Assert.That(migratedV3.Checkpoint.SchemaVersion, Is.EqualTo(4));
            Assert.That(migratedV3.Checkpoint.ActiveShopId.IsEmpty, Is.True);
            Assert.That(migratedV3.Checkpoint.ShopOffers, Is.Empty);
            Assert.That(RunCheckpointJson.Deserialize(versionOne.Replace("\"schemaVersion\":1", "\"schemaVersion\":5")).Succeeded, Is.False);
        }

        [Test]
        public async Task SaveCoordinator_RoundTripsThroughAtomicSaveService()
        {
            var directory = Path.Combine(Path.GetTempPath(), "run-save-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var catalog = ParseSample();
                var session = new RunSession(catalog, 123456789UL);
                session.Start();
                var coordinator = new RunSaveCoordinator(catalog, new JsonSaveService(directory));

                var saved = await coordinator.SaveAsync("slot1", session, CancellationToken.None);
                var loaded = await coordinator.LoadAsync("slot1", CancellationToken.None);

                Assert.That(saved.Succeeded, Is.True, saved.Error);
                Assert.That(loaded.Succeeded, Is.True, loaded.Error);
                Assert.That(loaded.Session.RootSeed, Is.EqualTo(session.RootSeed));
                Assert.That(loaded.Session.Deck, Is.EqualTo(session.Deck));
                Assert.That(loaded.Session.Resources, Is.EqualTo(session.Resources));
                Assert.That(loaded.Session.Phase, Is.EqualTo(RunPhase.Map));
                Assert.That(loaded.Session.CurrentMapNodeId.IsEmpty, Is.True);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public async Task SaveCoordinator_RejectsCombatWithoutWritingSlot()
        {
            var directory = Path.Combine(Path.GetTempPath(), "run-combat-save-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var catalog = ParseSample();
                var session = StartFight(catalog, 88UL);
                var saves = new JsonSaveService(directory);
                var coordinator = new RunSaveCoordinator(catalog, saves);

                var result = await coordinator.SaveAsync("combat", session, CancellationToken.None);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Error, Does.Contain("safe phase"));
                Assert.That(saves.Exists("combat"), Is.False);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static RunSession StartFight(ContentCatalog catalog, ulong seed)
        {
            var session = new RunSession(catalog, seed);
            Assert.That(session.Start().Succeeded, Is.True);
            Assert.That(session.SelectMapNode(PrologueRouteId).Succeeded, Is.True);
            Assert.That(session.Choose(FightChoiceId).Succeeded, Is.True);
            Assert.That(session.SelectMapNode(WispRouteId).Succeeded, Is.True);
            return session;
        }

        private static void ReachRest(RunSession session, ContentCatalog catalog)
        {
            Assert.That(session.SelectMapNode(PrologueRouteId).Succeeded, Is.True);
            Assert.That(session.Choose(FightChoiceId).Succeeded, Is.True);
            Assert.That(session.SelectMapNode(WispRouteId).Succeeded, Is.True);
            WinCombat(session, catalog);
            Assert.That(session.ClaimReward(session.RewardCandidates[0]).Succeeded, Is.True);
            Assert.That(session.SelectMapNode(InterludeRouteId).Succeeded, Is.True);
            Assert.That(session.Choose(new ContentId("choice.enter_archive")).Succeeded, Is.True);
            Assert.That(session.SelectMapNode(RestRouteId).Succeeded, Is.True);
            Assert.That(session.Phase, Is.EqualTo(RunPhase.Upgrade));
        }

        private static void ReachShop(RunSession session, ContentCatalog catalog)
        {
            Assert.That(session.Start().Succeeded, Is.True);
            ReachRest(session, catalog);
            Assert.That(session.UpgradeCard(0).Succeeded, Is.True);
            Assert.That(session.SelectMapNode(ShopRouteId).Succeeded, Is.True);
            Assert.That(session.Phase, Is.EqualTo(RunPhase.Shop));
        }

        private static void WinCombat(RunSession session, ContentCatalog catalog, bool scoreCards = false, RunPhase expectedOutcome = RunPhase.Reward)
        {
            for (var turnBudget = 0; turnBudget < 20 && session.Phase == RunPhase.Combat; turnBudget++)
            {
                while (session.Phase == RunPhase.Combat)
                {
                    var playable = session.Combat.Hand
                        .Select((id, index) => new { id, index, definition = catalog.Cards[id] })
                        .OrderByDescending(item => scoreCards ? DiagnosticCardScore(item.definition) : 0)
                        .FirstOrDefault(item => item.definition.Cost <= session.Combat.Energy);
                    if (playable == null)
                    {
                        break;
                    }

                    var needsEnemy = playable.definition.Effects.Any(effect => effect.Target == EffectTarget.SingleEnemy);
                    var result = session.PlayCard(playable.index, needsEnemy ? 0 : -1);
                    Assert.That(result.Succeeded, Is.True, result.Error);
                }

                if (session.Phase == RunPhase.Combat)
                {
                    Assert.That(session.EndTurn().Succeeded, Is.True);
                }
            }

            var trace = string.Join(Environment.NewLine, session.Combat.Events
                .Where(item => item.Kind == CombatEventKind.CardPlayed ||
                    item.Kind == CombatEventKind.EnemyIntentStarted ||
                    item.Kind == CombatEventKind.DamageApplied ||
                    item.Kind == CombatEventKind.BlockGained)
                .Select(item => $"turn={item.Turn} {item.Kind} source={item.Source} target={item.Target} content={item.Content} value={item.Value}"));
            TestContext.Out.WriteLine($"policy={(scoreCards ? "effect-score-v1" : "hand-order-v1")}; seed={session.RootSeed}; outcome={session.Phase}; playerHP={session.PlayerHealth}" + Environment.NewLine + trace);
            Assert.That(session.Phase, Is.EqualTo(expectedOutcome),
                $"sample encounter outcome; seed={session.RootSeed}; playerHP={session.PlayerHealth}" + Environment.NewLine + trace);
        }

        // Diagnostic comparator only: does not change the production simulation policy or balance gate.
        private static int DiagnosticCardScore(CardDefinition card)
        {
            return card.Effects.Sum(effect =>
            {
                switch (effect.Kind)
                {
                    case CardEffectKind.Damage: return effect.Amount * 100;
                    case CardEffectKind.Block: return effect.Amount * 20;
                    case CardEffectKind.Draw: return effect.Amount * 30;
                    case CardEffectKind.ApplyStatus:
                        return effect.Amount * (effect.StatusKind == CombatStatusKind.Strength ? 300 :
                            effect.StatusKind == CombatStatusKind.Vulnerable ? 250 : 200);
                    default: return 0;
                }
            }) - card.Cost;
        }

        private static ContentCatalog ParseSample()
        {
            var parsed = ContentCatalogJson.ParseApproved(ReadSample());
            Assert.That(parsed.Succeeded, Is.True, string.Join(Environment.NewLine, parsed.Errors));
            return parsed.Catalog;
        }

        private static string ReadSample()
        {
            var path = Path.Combine(Application.dataPath, "_Game", "Content", "Source", "vertical-slice.json");
            return File.ReadAllText(path);
        }

        private static string ReadLocalization(string fileName)
        {
            var path = Path.Combine(Application.dataPath, "_Game", "Content", "Localization", fileName);
            return File.ReadAllText(path);
        }
    }
}

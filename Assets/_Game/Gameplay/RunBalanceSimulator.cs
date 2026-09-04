using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Gameplay
{
    [Serializable]
    public sealed class BalanceCount
    {
        public string id;
        public int count;
    }

    [Serializable]
    public sealed class BalanceSimulationReport
    {
        public int schemaVersion = 1;
        public string contentVersion;
        public string policyVersion = "greedy-v2-shop";
        public int sampleCount;
        public string startSeed;
        public int completedRuns;
        public int defeatedRuns;
        public int stalledRuns;
        public int winRatePermille;
        public int averageRemainingHealthTimes100;
        public int averageCombatTurnsTimes100;
        public int gateMinimumWinRatePermille;
        public int gateMaximumWinRatePermille;
        public int gateMaximumStalledRuns;
        public bool gatePassed;
        public BalanceCount[] routeSelections;
        public BalanceCount[] cardPlays;
    }

    public static class RunBalanceSimulator
    {
        public static BalanceSimulationReport Simulate(ContentCatalog catalog, int sampleCount, ulong startSeed)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (sampleCount <= 0 || sampleCount > 100000) throw new ArgumentOutOfRangeException(nameof(sampleCount));

            var routeCounts = new Dictionary<ContentId, int>();
            var cardCounts = new Dictionary<ContentId, int>();
            var completed = 0;
            var defeated = 0;
            var stalled = 0;
            long remainingHealth = 0;
            long combatTurns = 0;

            for (var index = 0; index < sampleCount; index++)
            {
                var seed = unchecked(startSeed + (ulong)index);
                var outcome = ExecuteRun(catalog, seed, routeCounts, cardCounts);
                combatTurns += outcome.CombatTurns;
                if (outcome.Phase == RunPhase.Completed)
                {
                    completed++;
                    remainingHealth += outcome.RemainingHealth;
                }
                else if (outcome.Phase == RunPhase.Defeat)
                {
                    defeated++;
                }
                else
                {
                    stalled++;
                }
            }

            return new BalanceSimulationReport
            {
                contentVersion = catalog.ContentVersion,
                sampleCount = sampleCount,
                startSeed = startSeed.ToString(),
                completedRuns = completed,
                defeatedRuns = defeated,
                stalledRuns = stalled,
                winRatePermille = completed * 1000 / sampleCount,
                averageRemainingHealthTimes100 = completed == 0 ? 0 : checked((int)(remainingHealth * 100 / completed)),
                averageCombatTurnsTimes100 = checked((int)(combatTurns * 100 / sampleCount)),
                routeSelections = ToCounts(routeCounts),
                cardPlays = ToCounts(cardCounts)
            };
        }

        private static SimulationOutcome ExecuteRun(
            ContentCatalog catalog,
            ulong seed,
            IDictionary<ContentId, int> routeCounts,
            IDictionary<ContentId, int> cardCounts)
        {
            var session = new RunSession(catalog, seed);
            if (!session.Start().Succeeded) return new SimulationOutcome(session.Phase, session.PlayerHealth, 0);
            var totalTurns = 0;
            var activeCombatCounted = false;

            for (var commandBudget = 0; commandBudget < 2000; commandBudget++)
            {
                switch (session.Phase)
                {
                    case RunPhase.Map:
                    {
                        var options = session.AvailableMapNodes;
                        if (options.Count == 0) return new SimulationOutcome(session.Phase, session.PlayerHealth, totalTurns);
                        var selected = options[(int)((seed + (ulong)session.VisitedMapNodeIds.Count) % (ulong)options.Count)];
                        Increment(routeCounts, selected.Id);
                        if (!session.SelectMapNode(selected.Id).Succeeded) return new SimulationOutcome(session.Phase, session.PlayerHealth, totalTurns);
                        activeCombatCounted = false;
                        break;
                    }
                    case RunPhase.Narrative:
                    {
                        var choice = session.Narrative.AvailableChoices.FirstOrDefault();
                        if (choice == null || !session.Choose(choice.Id).Succeeded) return new SimulationOutcome(session.Phase, session.PlayerHealth, totalTurns);
                        break;
                    }
                    case RunPhase.Upgrade:
                    {
                        var index = session.Deck.Select((id, i) => new { id, i }).FirstOrDefault(item => !catalog.Cards[item.id].UpgradeToId.IsEmpty);
                        if (index == null || !session.UpgradeCard(index.i).Succeeded) return new SimulationOutcome(session.Phase, session.PlayerHealth, totalTurns);
                        break;
                    }
                    case RunPhase.Shop:
                    {
                        var goldId = new ContentId("resource.gold");
                        session.Resources.TryGetValue(goldId, out var gold);
                        var offer = session.ShopOffers
                            .OrderByDescending(id => Score(catalog.Cards[id]))
                            .ThenBy(id => id)
                            .FirstOrDefault();
                        if (!offer.IsEmpty && gold >= session.ActiveShop.CardPrice)
                        {
                            if (!session.PurchaseShopCard(offer).Succeeded) return new SimulationOutcome(session.Phase, session.PlayerHealth, totalTurns);
                        }
                        else if (!session.LeaveShop().Succeeded)
                        {
                            return new SimulationOutcome(session.Phase, session.PlayerHealth, totalTurns);
                        }

                        break;
                    }
                    case RunPhase.Combat:
                    {
                        var combat = session.Combat;
                        var candidate = combat.Hand
                            .Select((id, handIndex) => new { id, handIndex, definition = catalog.Cards[id] })
                            .Where(item => item.definition.Cost <= combat.Energy)
                            .OrderByDescending(item => Score(item.definition))
                            .ThenBy(item => item.id)
                            .FirstOrDefault();
                        if (candidate != null)
                        {
                            var target = candidate.definition.Effects.Any(effect => effect.Target == EffectTarget.SingleEnemy)
                                ? FirstLivingEnemy(combat)
                                : -1;
                            Increment(cardCounts, candidate.id);
                            if (!session.PlayCard(candidate.handIndex, target).Succeeded) return new SimulationOutcome(session.Phase, session.PlayerHealth, totalTurns);
                        }
                        else
                        {
                            if (!session.EndTurn().Succeeded) return new SimulationOutcome(session.Phase, session.PlayerHealth, totalTurns);
                        }

                        if (!activeCombatCounted && session.Phase != RunPhase.Combat)
                        {
                            totalTurns += combat.Turn;
                            activeCombatCounted = true;
                        }

                        break;
                    }
                    case RunPhase.Reward:
                    {
                        var reward = session.RewardCandidates.OrderByDescending(id => Score(catalog.Cards[id])).ThenBy(id => id).FirstOrDefault();
                        if (reward.IsEmpty || !session.ClaimReward(reward).Succeeded) return new SimulationOutcome(session.Phase, session.PlayerHealth, totalTurns);
                        break;
                    }
                    case RunPhase.Completed:
                    case RunPhase.Defeat:
                        return new SimulationOutcome(session.Phase, session.PlayerHealth, totalTurns);
                    default:
                        return new SimulationOutcome(session.Phase, session.PlayerHealth, totalTurns);
                }
            }

            return new SimulationOutcome(session.Phase, session.PlayerHealth, totalTurns);
        }

        private static int Score(CardDefinition card)
        {
            return card.Effects.Sum(effect =>
            {
                switch (effect.Kind)
                {
                    case CardEffectKind.Damage: return effect.Amount * 100;
                    case CardEffectKind.Block: return effect.Amount * 20;
                    case CardEffectKind.Draw: return effect.Amount * 30;
                    case CardEffectKind.ApplyStatus:
                        return effect.Amount * (effect.StatusKind == CombatStatusKind.Strength
                            ? 300
                            : effect.StatusKind == CombatStatusKind.Vulnerable ? 250 : 200);
                    default: return 0;
                }
            }) - card.Cost;
        }

        private static int FirstLivingEnemy(CombatState combat)
        {
            for (var index = 0; index < combat.Enemies.Count; index++)
            {
                if (!combat.Enemies[index].IsDefeated) return index;
            }

            return -1;
        }

        private static void Increment(IDictionary<ContentId, int> counts, ContentId id)
        {
            counts.TryGetValue(id, out var count);
            counts[id] = count + 1;
        }

        private static BalanceCount[] ToCounts(IDictionary<ContentId, int> counts)
        {
            return counts.OrderBy(pair => pair.Key).Select(pair => new BalanceCount { id = pair.Key.Value, count = pair.Value }).ToArray();
        }

        private readonly struct SimulationOutcome
        {
            public SimulationOutcome(RunPhase phase, int remainingHealth, int combatTurns)
            {
                Phase = phase;
                RemainingHealth = remainingHealth;
                CombatTurns = combatTurns;
            }

            public RunPhase Phase { get; }
            public int RemainingHealth { get; }
            public int CombatTurns { get; }
        }
    }

    public static class BalanceSimulationGate
    {
        public static bool Passes(BalanceSimulationReport report, int minimumWinRatePermille, int maximumWinRatePermille, int maximumStalledRuns)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (minimumWinRatePermille < 0 || maximumWinRatePermille > 1000 || minimumWinRatePermille > maximumWinRatePermille || maximumStalledRuns < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumWinRatePermille));
            }

            return report.winRatePermille >= minimumWinRatePermille &&
                   report.winRatePermille <= maximumWinRatePermille &&
                   report.stalledRuns <= maximumStalledRuns;
        }
    }
}

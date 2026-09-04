using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Gameplay
{
    public enum RunPhase
    {
        NotStarted,
        Map,
        Upgrade,
        Shop,
        Narrative,
        Combat,
        Reward,
        Completed,
        Defeat
    }

    public readonly struct RunCommandResult
    {
        private RunCommandResult(bool succeeded, string error)
        {
            Succeeded = succeeded;
            Error = error ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Error { get; }
        public static RunCommandResult Success() => new RunCommandResult(true, string.Empty);
        public static RunCommandResult Failure(string error) => new RunCommandResult(false, error);
    }

    public readonly struct RunCheckpointResult
    {
        private RunCheckpointResult(bool succeeded, RunCheckpoint checkpoint, string error)
        {
            Succeeded = succeeded;
            Checkpoint = checkpoint;
            Error = error ?? string.Empty;
        }

        public bool Succeeded { get; }
        public RunCheckpoint Checkpoint { get; }
        public string Error { get; }
        public static RunCheckpointResult Success(RunCheckpoint checkpoint) => new RunCheckpointResult(true, checkpoint, string.Empty);
        public static RunCheckpointResult Failure(string error) => new RunCheckpointResult(false, null, error);
    }

    public sealed class RunCheckpoint
    {
        public const int CurrentSchemaVersion = 4;

        public RunCheckpoint(
            string contentVersion,
            ulong rootSeed,
            RunPhase phase,
            ContentId currentNodeId,
            bool narrativeCompleted,
            IReadOnlyDictionary<ContentId, StoryValue> variables,
            IEnumerable<ContentId> deck,
            int playerHealth,
            IReadOnlyDictionary<ContentId, int> resources,
            IEnumerable<ContentId> rewardCandidates,
            int encounterIndex,
            ContentId currentMapNodeId,
            IEnumerable<ContentId> visitedMapNodeIds,
            IEnumerable<ContentId> relics,
            ContentId activeShopId,
            IEnumerable<ContentId> shopOffers,
            int shopIndex,
            bool shopRemovalUsed)
        {
            SchemaVersion = CurrentSchemaVersion;
            ContentVersion = contentVersion ?? throw new ArgumentNullException(nameof(contentVersion));
            RootSeed = rootSeed;
            Phase = phase;
            CurrentNodeId = currentNodeId;
            NarrativeCompleted = narrativeCompleted;
            Variables = new Dictionary<ContentId, StoryValue>(variables ?? throw new ArgumentNullException(nameof(variables)));
            Deck = (deck ?? throw new ArgumentNullException(nameof(deck))).ToArray();
            PlayerHealth = playerHealth;
            Resources = new Dictionary<ContentId, int>(resources ?? throw new ArgumentNullException(nameof(resources)));
            RewardCandidates = (rewardCandidates ?? throw new ArgumentNullException(nameof(rewardCandidates))).ToArray();
            EncounterIndex = encounterIndex;
            CurrentMapNodeId = currentMapNodeId;
            VisitedMapNodeIds = (visitedMapNodeIds ?? throw new ArgumentNullException(nameof(visitedMapNodeIds))).ToArray();
            Relics = (relics ?? throw new ArgumentNullException(nameof(relics))).ToArray();
            ActiveShopId = activeShopId;
            ShopOffers = (shopOffers ?? throw new ArgumentNullException(nameof(shopOffers))).ToArray();
            ShopIndex = shopIndex;
            ShopRemovalUsed = shopRemovalUsed;
        }

        public int SchemaVersion { get; }
        public string ContentVersion { get; }
        public ulong RootSeed { get; }
        public RunPhase Phase { get; }
        public ContentId CurrentNodeId { get; }
        public bool NarrativeCompleted { get; }
        public IReadOnlyDictionary<ContentId, StoryValue> Variables { get; }
        public IReadOnlyList<ContentId> Deck { get; }
        public int PlayerHealth { get; }
        public IReadOnlyDictionary<ContentId, int> Resources { get; }
        public IReadOnlyList<ContentId> RewardCandidates { get; }
        public int EncounterIndex { get; }
        public ContentId CurrentMapNodeId { get; }
        public IReadOnlyList<ContentId> VisitedMapNodeIds { get; }
        public IReadOnlyList<ContentId> Relics { get; }
        public ContentId ActiveShopId { get; }
        public IReadOnlyList<ContentId> ShopOffers { get; }
        public int ShopIndex { get; }
        public bool ShopRemovalUsed { get; }
    }

    public sealed class RunSession
    {
        private static readonly ContentId PlayerId = new ContentId("actor.player");
        private static readonly ContentId GoldId = new ContentId("resource.gold");
        private readonly ContentCatalog catalog;
        private readonly List<ContentId> deck = new List<ContentId>();
        private readonly Dictionary<ContentId, int> resources = new Dictionary<ContentId, int>();
        private readonly List<ContentId> rewardCandidates = new List<ContentId>();
        private readonly List<ContentId> visitedMapNodeIds = new List<ContentId>();
        private readonly List<ContentId> relics = new List<ContentId>();
        private readonly List<ContentId> shopOffers = new List<ContentId>();
        private NarrativeStateMachine narrative;
        private int encounterIndex;
        private int shopIndex;

        public RunSession(ContentCatalog catalog, ulong rootSeed)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            RootSeed = rootSeed;
            PlayerHealth = catalog.PlayerMaximumHealth;
            deck.AddRange(catalog.InitialDeck);
            relics.AddRange(catalog.InitialRelics);
            narrative = catalog.CreateNarrative();
        }

        public ulong RootSeed { get; }
        public RunPhase Phase { get; private set; }
        public int PlayerHealth { get; private set; }
        public NarrativeStateMachine Narrative => narrative;
        public CombatState Combat { get; private set; }
        public EncounterDefinition ActiveEncounter { get; private set; }
        public IReadOnlyList<ContentId> Deck => deck;
        public IReadOnlyDictionary<ContentId, int> Resources => resources;
        public IReadOnlyList<ContentId> RewardCandidates => rewardCandidates;
        public ContentId CurrentMapNodeId { get; private set; }
        public IReadOnlyList<ContentId> VisitedMapNodeIds => visitedMapNodeIds;
        public IReadOnlyList<ContentId> Relics => relics;
        public ShopDefinition ActiveShop { get; private set; }
        public IReadOnlyList<ContentId> ShopOffers => shopOffers;
        public bool ShopRemovalUsed { get; private set; }
        public IReadOnlyList<RouteNodeDefinition> AvailableMapNodes
        {
            get
            {
                if (Phase != RunPhase.Map) return Array.Empty<RouteNodeDefinition>();
                var ids = CurrentMapNodeId.IsEmpty
                    ? new[] { catalog.StartRouteNodeId }
                    : catalog.RouteNodes[CurrentMapNodeId].NextNodeIds;
                return ids.Where(id => !visitedMapNodeIds.Contains(id)).Select(id => catalog.RouteNodes[id]).ToArray();
            }
        }
        public RunCheckpoint LastSafeCheckpoint { get; private set; }

        public RunCommandResult Start()
        {
            if (Phase != RunPhase.NotStarted)
            {
                return RunCommandResult.Failure("Run has already started.");
            }

            Phase = RunPhase.Map;
            UpdateSafeCheckpoint();
            return RunCommandResult.Success();
        }

        public RunCommandResult SelectMapNode(ContentId nodeId)
        {
            if (Phase != RunPhase.Map)
            {
                return RunCommandResult.Failure("Route nodes can only be selected from the map phase.");
            }

            var routeNode = AvailableMapNodes.FirstOrDefault(node => node.Id == nodeId);
            if (routeNode == null)
            {
                return RunCommandResult.Failure("The route node is not currently available.");
            }

            CurrentMapNodeId = routeNode.Id;
            visitedMapNodeIds.Add(routeNode.Id);
            if (routeNode.Kind == RouteNodeKind.Story)
            {
                var variables = narrative.Variables.ToDictionary(pair => pair.Key, pair => pair.Value);
                narrative = catalog.CreateNarrative();
                var result = narrative.Start(routeNode.PayloadId, variables);
                if (!result.Succeeded) return RunCommandResult.Failure(result.Error);
                Phase = RunPhase.Narrative;
                var processed = ProcessNarrativeCommands();
                if (processed.Succeeded && Phase != RunPhase.Combat) UpdateSafeCheckpoint();
                return processed;
            }

            if (routeNode.Kind == RouteNodeKind.Rest)
            {
                Phase = RunPhase.Upgrade;
                UpdateSafeCheckpoint();
                return RunCommandResult.Success();
            }

            if (routeNode.Kind == RouteNodeKind.Shop)
            {
                BeginShop(routeNode.PayloadId);
                return RunCommandResult.Success();
            }

            BeginCombat(routeNode.PayloadId);
            return RunCommandResult.Success();
        }

        public RunCommandResult UpgradeCard(int deckIndex)
        {
            if (Phase != RunPhase.Upgrade)
            {
                return RunCommandResult.Failure("Cards can only be upgraded at a rest node.");
            }

            if (deckIndex < 0 || deckIndex >= deck.Count)
            {
                return RunCommandResult.Failure("Deck index is outside the current deck.");
            }

            var definition = catalog.Cards[deck[deckIndex]];
            if (definition.UpgradeToId.IsEmpty)
            {
                return RunCommandResult.Failure("The selected card has no upgrade.");
            }

            deck[deckIndex] = definition.UpgradeToId;
            ResolveCurrentRouteNode();
            UpdateSafeCheckpoint();
            return RunCommandResult.Success();
        }

        public RunCommandResult PurchaseShopCard(ContentId cardId)
        {
            if (Phase != RunPhase.Shop || ActiveShop == null) return RunCommandResult.Failure("Cards can only be purchased in an active shop.");
            if (!shopOffers.Contains(cardId)) return RunCommandResult.Failure("The card is not a current shop offer.");
            resources.TryGetValue(GoldId, out var gold);
            if (gold < ActiveShop.CardPrice) return RunCommandResult.Failure("Not enough gold.");
            resources[GoldId] = gold - ActiveShop.CardPrice;
            deck.Add(cardId);
            shopOffers.Remove(cardId);
            UpdateSafeCheckpoint();
            return RunCommandResult.Success();
        }

        public RunCommandResult RemoveShopCard(int deckIndex)
        {
            if (Phase != RunPhase.Shop || ActiveShop == null) return RunCommandResult.Failure("Cards can only be removed in an active shop.");
            if (ShopRemovalUsed) return RunCommandResult.Failure("This shop's card removal has already been used.");
            if (deckIndex < 0 || deckIndex >= deck.Count || deck.Count <= 1) return RunCommandResult.Failure("A valid card is required and the deck cannot become empty.");
            resources.TryGetValue(GoldId, out var gold);
            if (gold < ActiveShop.RemovalPrice) return RunCommandResult.Failure("Not enough gold.");
            resources[GoldId] = gold - ActiveShop.RemovalPrice;
            deck.RemoveAt(deckIndex);
            ShopRemovalUsed = true;
            UpdateSafeCheckpoint();
            return RunCommandResult.Success();
        }

        public RunCommandResult LeaveShop()
        {
            if (Phase != RunPhase.Shop || ActiveShop == null) return RunCommandResult.Failure("There is no active shop to leave.");
            ActiveShop = null;
            shopOffers.Clear();
            ShopRemovalUsed = false;
            ResolveCurrentRouteNode();
            UpdateSafeCheckpoint();
            return RunCommandResult.Success();
        }

        public RunCommandResult Choose(ContentId choiceId)
        {
            if (Phase != RunPhase.Narrative)
            {
                return RunCommandResult.Failure("Narrative choices are only valid during the narrative phase.");
            }

            var result = narrative.Choose(choiceId);
            if (!result.Succeeded)
            {
                return RunCommandResult.Failure(result.Error);
            }

            var processed = ProcessNarrativeCommands();
            if (processed.Succeeded && Phase != RunPhase.Combat)
            {
                UpdateSafeCheckpoint();
            }

            return processed;
        }

        public RunCommandResult PlayCard(int handIndex, int targetEnemyIndex = -1)
        {
            if (Phase != RunPhase.Combat || Combat == null)
            {
                return RunCommandResult.Failure("Cards are only valid during combat.");
            }

            var result = Combat.PlayCard(handIndex, targetEnemyIndex);
            if (!result.Succeeded)
            {
                return RunCommandResult.Failure(result.Error);
            }

            HandleCombatTransition();
            return RunCommandResult.Success();
        }

        public RunCommandResult EndTurn()
        {
            if (Phase != RunPhase.Combat || Combat == null)
            {
                return RunCommandResult.Failure("A turn can only end during combat.");
            }

            var result = Combat.EndTurn();
            if (!result.Succeeded)
            {
                return RunCommandResult.Failure(result.Error);
            }

            HandleCombatTransition();
            return RunCommandResult.Success();
        }

        public RunCommandResult ClaimReward(ContentId cardId)
        {
            if (Phase != RunPhase.Reward)
            {
                return RunCommandResult.Failure("Rewards can only be claimed during the reward phase.");
            }

            if (!rewardCandidates.Contains(cardId))
            {
                return RunCommandResult.Failure("The selected card is not a current reward candidate.");
            }

            deck.Add(cardId);
            rewardCandidates.Clear();
            ResolveCurrentRouteNode();
            UpdateSafeCheckpoint();
            return RunCommandResult.Success();
        }

        public RunCheckpointResult CreateCheckpoint()
        {
            if (Phase == RunPhase.NotStarted || Phase == RunPhase.Combat)
            {
                return RunCheckpointResult.Failure("Checkpoints are only available at safe phase boundaries.");
            }

            return RunCheckpointResult.Success(CaptureCheckpoint());
        }

        public static bool TryRestore(ContentCatalog catalog, RunCheckpoint checkpoint, out RunSession session, out string error)
        {
            session = null;
            error = string.Empty;
            if (catalog == null || checkpoint == null)
            {
                error = "Catalog and checkpoint are required.";
                return false;
            }

            if (checkpoint.SchemaVersion != RunCheckpoint.CurrentSchemaVersion)
            {
                error = "Checkpoint schema version is not supported.";
                return false;
            }

            if (!string.Equals(checkpoint.ContentVersion, catalog.ContentVersion, StringComparison.Ordinal))
            {
                error = "Checkpoint content version does not match the active catalog.";
                return false;
            }

            if (checkpoint.Phase == RunPhase.NotStarted || checkpoint.Phase == RunPhase.Combat)
            {
                error = "Checkpoint phase is not restorable.";
                return false;
            }

            if (checkpoint.PlayerHealth <= 0 && checkpoint.Phase != RunPhase.Defeat)
            {
                error = "A live checkpoint requires positive player health.";
                return false;
            }

            if (checkpoint.Deck.Any(cardId => !catalog.Cards.ContainsKey(cardId)) ||
                checkpoint.RewardCandidates.Any(cardId => !catalog.Cards.ContainsKey(cardId)))
            {
                error = "Checkpoint references cards missing from the active catalog.";
                return false;
            }

            if ((!checkpoint.CurrentMapNodeId.IsEmpty && !catalog.RouteNodes.ContainsKey(checkpoint.CurrentMapNodeId)) ||
                checkpoint.VisitedMapNodeIds.Any(id => !catalog.RouteNodes.ContainsKey(id)))
            {
                error = "Checkpoint references route nodes missing from the active catalog.";
                return false;
            }

            if (checkpoint.Relics.Distinct().Count() != checkpoint.Relics.Count || checkpoint.Relics.Any(id => !catalog.Relics.ContainsKey(id)))
            {
                error = "Checkpoint references duplicate or missing relics in the active catalog.";
                return false;
            }

            if (checkpoint.EncounterIndex < 0 || checkpoint.ShopIndex < 0)
            {
                error = "Checkpoint encounter and shop indices cannot be negative.";
                return false;
            }

            if ((!checkpoint.ActiveShopId.IsEmpty && !catalog.Shops.ContainsKey(checkpoint.ActiveShopId)) || checkpoint.ShopOffers.Any(id => !catalog.Cards.ContainsKey(id)))
            {
                error = "Checkpoint references a missing shop or shop card.";
                return false;
            }

            if (checkpoint.Phase == RunPhase.Shop)
            {
                if (checkpoint.ActiveShopId.IsEmpty || checkpoint.CurrentMapNodeId.IsEmpty ||
                    catalog.RouteNodes[checkpoint.CurrentMapNodeId].Kind != RouteNodeKind.Shop ||
                    catalog.RouteNodes[checkpoint.CurrentMapNodeId].PayloadId != checkpoint.ActiveShopId)
                {
                    error = "A shop checkpoint requires a matching active shop route node.";
                    return false;
                }

                var shop = catalog.Shops[checkpoint.ActiveShopId];
                if (checkpoint.ShopOffers.Count > shop.OfferCount ||
                    checkpoint.ShopOffers.Distinct().Count() != checkpoint.ShopOffers.Count ||
                    checkpoint.ShopOffers.Any(id => !shop.InventoryPool.Contains(id)))
                {
                    error = "Checkpoint shop offers are not a valid subset of the active shop inventory.";
                    return false;
                }
            }
            else if (!checkpoint.ActiveShopId.IsEmpty || checkpoint.ShopOffers.Count != 0 || checkpoint.ShopRemovalUsed)
            {
                error = "Shop state is only valid during the shop phase.";
                return false;
            }

            var restored = new RunSession(catalog, checkpoint.RootSeed);
            restored.deck.Clear();
            restored.deck.AddRange(checkpoint.Deck);
            restored.resources.Clear();
            foreach (var pair in checkpoint.Resources)
            {
                restored.resources.Add(pair.Key, pair.Value);
            }

            restored.rewardCandidates.AddRange(checkpoint.RewardCandidates);
            restored.PlayerHealth = checkpoint.PlayerHealth;
            restored.encounterIndex = checkpoint.EncounterIndex;
            restored.CurrentMapNodeId = checkpoint.CurrentMapNodeId;
            restored.visitedMapNodeIds.AddRange(checkpoint.VisitedMapNodeIds);
            restored.relics.Clear();
            restored.relics.AddRange(checkpoint.Relics);
            restored.shopIndex = checkpoint.ShopIndex;
            restored.shopOffers.AddRange(checkpoint.ShopOffers);
            restored.ShopRemovalUsed = checkpoint.ShopRemovalUsed;
            restored.ActiveShop = checkpoint.ActiveShopId.IsEmpty ? null : catalog.Shops[checkpoint.ActiveShopId];
            if (!checkpoint.CurrentNodeId.IsEmpty)
            {
                var narrativeResult = restored.narrative.Restore(checkpoint.CurrentNodeId, checkpoint.Variables, checkpoint.NarrativeCompleted);
                if (!narrativeResult.Succeeded)
                {
                    error = narrativeResult.Error;
                    return false;
                }
            }

            restored.Phase = checkpoint.Phase;
            restored.LastSafeCheckpoint = checkpoint;
            session = restored;
            return true;
        }

        private RunCommandResult ProcessNarrativeCommands()
        {
            foreach (var command in narrative.DrainPendingCommands())
            {
                switch (command.Kind)
                {
                    case NarrativeCommandKind.GrantCard:
                        deck.Add(command.Payload);
                        break;
                    case NarrativeCommandKind.GrantRelic:
                        if (!relics.Contains(command.Payload)) relics.Add(command.Payload);
                        break;
                    case NarrativeCommandKind.ChangeResource:
                        resources.TryGetValue(command.Payload, out var current);
                        resources[command.Payload] = checked(current + command.Amount);
                        break;
                    case NarrativeCommandKind.StartCombat:
                        if (Phase == RunPhase.Combat)
                        {
                            return RunCommandResult.Failure("Narrative emitted more than one combat command at a time.");
                        }

                        BeginCombat(command.Payload);
                        break;
                    default:
                        return RunCommandResult.Failure($"Unsupported external narrative command: {command.Kind}.");
                }
            }

            if (Phase != RunPhase.Combat)
            {
                if (narrative.IsCompleted)
                {
                    ResolveCurrentRouteNode();
                }
                else
                {
                    Phase = RunPhase.Narrative;
                }
            }

            return RunCommandResult.Success();
        }

        private void BeginCombat(ContentId encounterId)
        {
            ActiveEncounter = catalog.Encounters[encounterId];
            encounterIndex++;
            var enemies = ActiveEncounter.EnemyIds
                .Select(id => catalog.Enemies[id])
                .Select(definition => new CombatantState(definition.Id, definition.MaximumHealth, intents: definition.Intents))
                .ToArray();
            var combatSeed = new NamedRandomStreams(RootSeed).Get($"combat.{encounterIndex}").NextUInt64();
            var startEffects = relics.SelectMany(relicId => catalog.Relics[relicId].Effects.Select(effect =>
                new CombatStartEffect(relicId, effect.Kind, effect.Amount, effect.StatusKind))).ToArray();
            Combat = new CombatState(
                catalog.Cards.Values,
                deck,
                new CombatantState(PlayerId, catalog.PlayerMaximumHealth, currentHealth: PlayerHealth),
                enemies,
                combatSeed,
                startEffects: startEffects);
            Combat.Start();
            Phase = RunPhase.Combat;
        }

        private void BeginShop(ContentId shopId)
        {
            ActiveShop = catalog.Shops[shopId];
            shopIndex++;
            ShopRemovalUsed = false;
            shopOffers.Clear();
            var candidates = ActiveShop.InventoryPool.ToList();
            new NamedRandomStreams(RootSeed).Get($"shop.{shopIndex}").Shuffle(candidates);
            shopOffers.AddRange(candidates.Take(ActiveShop.OfferCount));
            Phase = RunPhase.Shop;
            UpdateSafeCheckpoint();
        }

        private void HandleCombatTransition()
        {
            if (Combat.Phase == CombatPhase.Defeat)
            {
                PlayerHealth = 0;
                Phase = RunPhase.Defeat;
                ActiveEncounter = null;
                UpdateSafeCheckpoint();
                return;
            }

            if (Combat.Phase != CombatPhase.Victory)
            {
                return;
            }

            PlayerHealth = Combat.Player.Health;
            ActiveEncounter = null;
            GenerateRewards();
            Phase = RunPhase.Reward;
            UpdateSafeCheckpoint();
        }

        private void GenerateRewards()
        {
            rewardCandidates.Clear();
            var candidates = catalog.RewardPool.Distinct().ToList();
            new NamedRandomStreams(RootSeed).Get($"reward.{encounterIndex}").Shuffle(candidates);
            rewardCandidates.AddRange(candidates.Take(Math.Min(catalog.RewardChoiceCount, candidates.Count)));
        }

        private void ResolveCurrentRouteNode()
        {
            if (CurrentMapNodeId.IsEmpty)
            {
                Phase = RunPhase.Completed;
                return;
            }

            Phase = catalog.RouteNodes[CurrentMapNodeId].IsFinal ? RunPhase.Completed : RunPhase.Map;
        }

        private void UpdateSafeCheckpoint()
        {
            LastSafeCheckpoint = CaptureCheckpoint();
        }

        private RunCheckpoint CaptureCheckpoint()
        {
            return new RunCheckpoint(
                catalog.ContentVersion,
                RootSeed,
                Phase,
                narrative.CurrentNode?.Id ?? default,
                narrative.IsCompleted,
                narrative.Variables,
                deck,
                PlayerHealth,
                resources,
                rewardCandidates,
                encounterIndex,
                CurrentMapNodeId,
                visitedMapNodeIds,
                relics,
                ActiveShop?.Id ?? default,
                shopOffers,
                shopIndex,
                ShopRemovalUsed);
        }
    }
}

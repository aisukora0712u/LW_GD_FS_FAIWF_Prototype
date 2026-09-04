using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Gameplay
{
    public sealed class LocalizedContentText
    {
        public LocalizedContentText(ContentId contentId, ContentId nameKey, ContentId descriptionKey)
        {
            if (contentId.IsEmpty || nameKey.IsEmpty || descriptionKey.IsEmpty)
            {
                throw new ArgumentException("Localized content text requires content, name, and description IDs.");
            }

            ContentId = contentId;
            NameKey = nameKey;
            DescriptionKey = descriptionKey;
        }

        public ContentId ContentId { get; }
        public ContentId NameKey { get; }
        public ContentId DescriptionKey { get; }
    }

    public sealed class EnemyDefinition
    {
        private readonly EnemyIntentDefinition[] intents;

        public EnemyDefinition(ContentId id, int maximumHealth, IEnumerable<EnemyIntentDefinition> intents)
        {
            if (id.IsEmpty)
            {
                throw new ArgumentException("An enemy requires an ID.", nameof(id));
            }

            if (maximumHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumHealth));
            }

            this.intents = intents?.ToArray() ?? throw new ArgumentNullException(nameof(intents));
            if (this.intents.Length == 0 || this.intents.Any(intent => intent == null) ||
                this.intents.Select(intent => intent.Id).Distinct().Count() != this.intents.Length)
            {
                throw new ArgumentException("An enemy requires a non-empty intent cycle with unique IDs.", nameof(intents));
            }

            Id = id;
            MaximumHealth = maximumHealth;
        }

        public ContentId Id { get; }
        public int MaximumHealth { get; }
        public IReadOnlyList<EnemyIntentDefinition> Intents => intents;
    }

    public sealed class StatusDefinition
    {
        public StatusDefinition(ContentId id, CombatStatusKind kind)
        {
            if (id.IsEmpty || kind == CombatStatusKind.None)
            {
                throw new ArgumentException("A status requires a stable ID and non-empty kind.");
            }

            Id = id;
            Kind = kind;
        }

        public ContentId Id { get; }
        public CombatStatusKind Kind { get; }
    }

    public enum RelicTriggerKind
    {
        CombatStart
    }

    public sealed class RelicEffectDefinition
    {
        public RelicEffectDefinition(CombatStartEffectKind kind, int amount, CombatStatusKind statusKind = CombatStatusKind.None)
        {
            if (amount <= 0 || (kind == CombatStartEffectKind.ApplyStatus) != (statusKind != CombatStatusKind.None))
            {
                throw new ArgumentException("Relic effect fields are inconsistent.");
            }

            Kind = kind;
            Amount = amount;
            StatusKind = statusKind;
        }

        public CombatStartEffectKind Kind { get; }
        public int Amount { get; }
        public CombatStatusKind StatusKind { get; }
    }

    public sealed class RelicDefinition
    {
        private readonly RelicEffectDefinition[] effects;

        public RelicDefinition(ContentId id, RelicTriggerKind trigger, IEnumerable<RelicEffectDefinition> effects)
        {
            if (id.IsEmpty) throw new ArgumentException("A relic requires an ID.", nameof(id));
            this.effects = effects?.ToArray() ?? throw new ArgumentNullException(nameof(effects));
            if (this.effects.Length == 0 || this.effects.Any(effect => effect == null)) throw new ArgumentException("A relic requires effects.", nameof(effects));
            Id = id;
            Trigger = trigger;
        }

        public ContentId Id { get; }
        public RelicTriggerKind Trigger { get; }
        public IReadOnlyList<RelicEffectDefinition> Effects => effects;
    }

    public sealed class ShopDefinition
    {
        private readonly ContentId[] inventoryPool;

        public ShopDefinition(ContentId id, IEnumerable<ContentId> inventoryPool, int cardPrice, int removalPrice, int offerCount)
        {
            if (id.IsEmpty || cardPrice <= 0 || removalPrice <= 0 || offerCount <= 0) throw new ArgumentException("Shop fields are outside their valid range.");
            this.inventoryPool = inventoryPool?.Distinct().ToArray() ?? throw new ArgumentNullException(nameof(inventoryPool));
            if (this.inventoryPool.Length < offerCount || this.inventoryPool.Any(card => card.IsEmpty)) throw new ArgumentException("Shop inventory pool must contain enough unique cards.", nameof(inventoryPool));
            Id = id;
            CardPrice = cardPrice;
            RemovalPrice = removalPrice;
            OfferCount = offerCount;
        }

        public ContentId Id { get; }
        public IReadOnlyList<ContentId> InventoryPool => inventoryPool;
        public int CardPrice { get; }
        public int RemovalPrice { get; }
        public int OfferCount { get; }
    }

    public sealed class EncounterDefinition
    {
        private readonly ContentId[] enemyIds;

        public EncounterDefinition(ContentId id, IEnumerable<ContentId> enemyIds)
        {
            if (id.IsEmpty)
            {
                throw new ArgumentException("An encounter requires an ID.", nameof(id));
            }

            this.enemyIds = enemyIds?.ToArray() ?? throw new ArgumentNullException(nameof(enemyIds));
            if (this.enemyIds.Length == 0 || this.enemyIds.Any(enemyId => enemyId.IsEmpty))
            {
                throw new ArgumentException("An encounter requires at least one enemy ID.", nameof(enemyIds));
            }

            Id = id;
        }

        public ContentId Id { get; }
        public IReadOnlyList<ContentId> EnemyIds => enemyIds;
    }

    public enum RouteNodeKind
    {
        Story,
        Combat,
        Boss,
        Rest,
        Shop
    }

    public sealed class RouteNodeDefinition
    {
        private readonly ContentId[] nextNodeIds;

        public RouteNodeDefinition(ContentId id, RouteNodeKind kind, ContentId payloadId, IEnumerable<ContentId> nextNodeIds, bool isFinal)
        {
            if (id.IsEmpty || (kind != RouteNodeKind.Rest && payloadId.IsEmpty))
            {
                throw new ArgumentException("A route node requires an ID and payload ID.");
            }

            this.nextNodeIds = nextNodeIds?.ToArray() ?? Array.Empty<ContentId>();
            if (this.nextNodeIds.Any(next => next.IsEmpty) || this.nextNodeIds.Distinct().Count() != this.nextNodeIds.Length)
            {
                throw new ArgumentException("Route successors must be unique, non-empty IDs.", nameof(nextNodeIds));
            }

            if (isFinal ? this.nextNodeIds.Length != 0 : this.nextNodeIds.Length == 0)
            {
                throw new ArgumentException("Final route nodes cannot have successors and non-final nodes require successors.", nameof(nextNodeIds));
            }

            Id = id;
            Kind = kind;
            PayloadId = payloadId;
            IsFinal = isFinal;
        }

        public ContentId Id { get; }
        public RouteNodeKind Kind { get; }
        public ContentId PayloadId { get; }
        public bool IsFinal { get; }
        public IReadOnlyList<ContentId> NextNodeIds => nextNodeIds;
    }

    public sealed class ContentCatalog
    {
        private readonly Dictionary<ContentId, CardDefinition> cards;
        private readonly Dictionary<ContentId, EnemyDefinition> enemies;
        private readonly Dictionary<ContentId, StatusDefinition> statuses;
        private readonly Dictionary<ContentId, RelicDefinition> relics;
        private readonly Dictionary<ContentId, ShopDefinition> shops;
        private readonly Dictionary<ContentId, EncounterDefinition> encounters;
        private readonly Dictionary<ContentId, RouteNodeDefinition> routeNodes;
        private readonly StoryNode[] storyNodes;
        private readonly ContentId[] initialDeck;
        private readonly ContentId[] rewardPool;
        private readonly ContentId[] initialRelics;
        private readonly ContentId[] requiredLocalizationKeys;
        private readonly Dictionary<ContentId, LocalizedContentText> localizedText;

        public ContentCatalog(
            string contentVersion,
            IEnumerable<CardDefinition> cards,
            IEnumerable<StatusDefinition> statuses,
            IEnumerable<RelicDefinition> relics,
            IEnumerable<ShopDefinition> shops,
            IEnumerable<EnemyDefinition> enemies,
            IEnumerable<EncounterDefinition> encounters,
            IEnumerable<RouteNodeDefinition> routeNodes,
            IEnumerable<StoryNode> storyNodes,
            ContentId startNodeId,
            ContentId startRouteNodeId,
            IEnumerable<ContentId> initialDeck,
            IEnumerable<ContentId> rewardPool,
            IEnumerable<ContentId> initialRelics,
            IEnumerable<ContentId> requiredLocalizationKeys,
            IEnumerable<LocalizedContentText> localizedText,
            int playerMaximumHealth,
            int rewardChoiceCount = 3)
        {
            if (string.IsNullOrWhiteSpace(contentVersion))
            {
                throw new ArgumentException("A content version is required.", nameof(contentVersion));
            }

            if (playerMaximumHealth <= 0 || rewardChoiceCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerMaximumHealth));
            }

            this.cards = ToUniqueDictionary(cards, card => card.Id, nameof(cards));
            this.statuses = ToUniqueDictionary(statuses, status => status.Id, nameof(statuses));
            this.relics = ToUniqueDictionary(relics, relic => relic.Id, nameof(relics));
            this.shops = ToUniqueDictionary(shops, shop => shop.Id, nameof(shops));
            if (this.statuses.Values.Select(status => status.Kind).Distinct().Count() != this.statuses.Count)
            {
                throw new ArgumentException("Status kinds must be unique in the catalog.", nameof(statuses));
            }
            this.enemies = ToUniqueDictionary(enemies, enemy => enemy.Id, nameof(enemies));
            this.encounters = ToUniqueDictionary(encounters, encounter => encounter.Id, nameof(encounters));
            this.routeNodes = ToUniqueDictionary(routeNodes, node => node.Id, nameof(routeNodes));
            this.storyNodes = storyNodes?.ToArray() ?? throw new ArgumentNullException(nameof(storyNodes));
            if (this.storyNodes.Length == 0 || this.storyNodes.Any(node => node == null))
            {
                throw new ArgumentException("A catalog requires story nodes.", nameof(storyNodes));
            }

            _ = new NarrativeStateMachine(this.storyNodes);
            if (!this.storyNodes.Any(node => node.Id == startNodeId))
            {
                throw new ArgumentException("The start story node does not exist.", nameof(startNodeId));
            }

            foreach (var encounter in this.encounters.Values)
            {
                foreach (var enemyId in encounter.EnemyIds)
                {
                    if (!this.enemies.ContainsKey(enemyId))
                    {
                        throw new ArgumentException($"Encounter {encounter.Id} references missing enemy {enemyId}.", nameof(encounters));
                    }
                }
            }

            if (!this.routeNodes.ContainsKey(startRouteNodeId))
            {
                throw new ArgumentException("The start route node does not exist.", nameof(startRouteNodeId));
            }

            foreach (var routeNode in this.routeNodes.Values)
            {
                foreach (var nextId in routeNode.NextNodeIds)
                {
                    if (!this.routeNodes.ContainsKey(nextId))
                    {
                        throw new ArgumentException($"Route node {routeNode.Id} references missing successor {nextId}.", nameof(routeNodes));
                    }
                }

                if (routeNode.Kind == RouteNodeKind.Story && !this.storyNodes.Any(node => node.Id == routeNode.PayloadId))
                {
                    throw new ArgumentException($"Route node {routeNode.Id} references missing story {routeNode.PayloadId}.", nameof(routeNodes));
                }

                if ((routeNode.Kind == RouteNodeKind.Combat || routeNode.Kind == RouteNodeKind.Boss) && !this.encounters.ContainsKey(routeNode.PayloadId))
                {
                    throw new ArgumentException($"Route node {routeNode.Id} references missing encounter {routeNode.PayloadId}.", nameof(routeNodes));
                }

                if (routeNode.Kind == RouteNodeKind.Shop && !this.shops.ContainsKey(routeNode.PayloadId))
                {
                    throw new ArgumentException($"Route node {routeNode.Id} references missing shop {routeNode.PayloadId}.", nameof(routeNodes));
                }
            }

            var narrativeErrors = NarrativeTopology.Validate(this.storyNodes,
                this.routeNodes.Values.Where(node => node.Kind == RouteNodeKind.Story)
                    .Select(node => node.PayloadId).Concat(new[] { startNodeId }));
            if (narrativeErrors.Count > 0)
                throw new ArgumentException(string.Join(Environment.NewLine, narrativeErrors), nameof(storyNodes));

            foreach (var shop in this.shops.Values)
            {
                if (shop.InventoryPool.Any(card => !this.cards.ContainsKey(card))) throw new ArgumentException($"Shop {shop.Id} references a missing card.", nameof(shops));
            }

            foreach (var card in this.cards.Values)
            {
                if (!card.UpgradeToId.IsEmpty && !this.cards.ContainsKey(card.UpgradeToId))
                {
                    throw new ArgumentException($"Card {card.Id} references missing upgrade {card.UpgradeToId}.", nameof(cards));
                }
            }

            var reachable = new HashSet<ContentId>();
            var pending = new Stack<ContentId>();
            pending.Push(startRouteNodeId);
            while (pending.Count > 0)
            {
                var id = pending.Pop();
                if (!reachable.Add(id)) continue;
                foreach (var next in this.routeNodes[id].NextNodeIds) pending.Push(next);
            }

            if (reachable.Count != this.routeNodes.Count)
            {
                throw new ArgumentException("Every route node must be reachable from the start route node.", nameof(routeNodes));
            }

            this.initialDeck = ValidateCardList(initialDeck, this.cards, "initial deck");
            this.rewardPool = ValidateCardList(rewardPool, this.cards, "reward pool");
            this.initialRelics = (initialRelics ?? throw new ArgumentNullException(nameof(initialRelics))).ToArray();
            if (this.initialRelics.Distinct().Count() != this.initialRelics.Length || this.initialRelics.Any(id => !this.relics.ContainsKey(id)))
            {
                throw new ArgumentException("Initial relics must be unique and reference catalog relics.", nameof(initialRelics));
            }
            if (this.rewardPool.Length == 0)
            {
                throw new ArgumentException("The reward pool cannot be empty.", nameof(rewardPool));
            }

            this.requiredLocalizationKeys = requiredLocalizationKeys?.Distinct().ToArray() ?? throw new ArgumentNullException(nameof(requiredLocalizationKeys));
            if (this.requiredLocalizationKeys.Length == 0 || this.requiredLocalizationKeys.Any(key => key.IsEmpty))
            {
                throw new ArgumentException("The catalog requires localization keys.", nameof(requiredLocalizationKeys));
            }

            this.localizedText = ToUniqueDictionary(localizedText, item => item.ContentId, nameof(localizedText));
            foreach (var id in this.cards.Keys.Concat(this.statuses.Keys).Concat(this.relics.Keys).Concat(this.shops.Keys).Concat(this.enemies.Keys).Concat(this.encounters.Keys).Concat(this.routeNodes.Keys))
            {
                if (!this.localizedText.ContainsKey(id))
                {
                    throw new ArgumentException($"Content {id} has no localized text mapping.", nameof(localizedText));
                }
            }

            foreach (var command in this.storyNodes.SelectMany(node => node.OnEnter.Concat(node.Choices.SelectMany(choice => choice.Commands))))
            {
                if (command.Kind == NarrativeCommandKind.StartCombat && !this.encounters.ContainsKey(command.Payload))
                {
                    throw new ArgumentException($"Story references missing encounter {command.Payload}.", nameof(storyNodes));
                }

                if (command.Kind == NarrativeCommandKind.GrantCard && !this.cards.ContainsKey(command.Payload))
                {
                    throw new ArgumentException($"Story references missing card {command.Payload}.", nameof(storyNodes));
                }

                if (command.Kind == NarrativeCommandKind.GrantRelic && !this.relics.ContainsKey(command.Payload))
                {
                    throw new ArgumentException($"Story references missing relic {command.Payload}.", nameof(storyNodes));
                }
            }

            ContentVersion = contentVersion;
            StartNodeId = startNodeId;
            StartRouteNodeId = startRouteNodeId;
            PlayerMaximumHealth = playerMaximumHealth;
            RewardChoiceCount = rewardChoiceCount;
        }

        public string ContentVersion { get; }
        public ContentId StartNodeId { get; }
        public ContentId StartRouteNodeId { get; }
        public int PlayerMaximumHealth { get; }
        public int RewardChoiceCount { get; }
        public IReadOnlyDictionary<ContentId, CardDefinition> Cards => cards;
        public IReadOnlyDictionary<ContentId, StatusDefinition> Statuses => statuses;
        public IReadOnlyDictionary<ContentId, RelicDefinition> Relics => relics;
        public IReadOnlyDictionary<ContentId, ShopDefinition> Shops => shops;
        public IReadOnlyDictionary<ContentId, EnemyDefinition> Enemies => enemies;
        public IReadOnlyDictionary<ContentId, EncounterDefinition> Encounters => encounters;
        public IReadOnlyDictionary<ContentId, RouteNodeDefinition> RouteNodes => routeNodes;
        public IReadOnlyList<ContentId> InitialDeck => initialDeck;
        public IReadOnlyList<ContentId> RewardPool => rewardPool;
        public IReadOnlyList<ContentId> InitialRelics => initialRelics;
        public IReadOnlyList<ContentId> RequiredLocalizationKeys => requiredLocalizationKeys;
        public IReadOnlyDictionary<ContentId, LocalizedContentText> LocalizedText => localizedText;

        public NarrativeStateMachine CreateNarrative() => new NarrativeStateMachine(storyNodes);

        private static Dictionary<ContentId, T> ToUniqueDictionary<T>(IEnumerable<T> source, Func<T, ContentId> getId, string parameter)
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameter);
            }

            var result = new Dictionary<ContentId, T>();
            foreach (var item in source)
            {
                if (item == null)
                {
                    throw new ArgumentException("Content collections cannot contain null values.", parameter);
                }

                var id = getId(item);
                if (result.ContainsKey(id))
                {
                    throw new ArgumentException($"Duplicate content ID: {id}.", parameter);
                }

                result.Add(id, item);
            }

            if (result.Count == 0)
            {
                throw new ArgumentException("Content collections cannot be empty.", parameter);
            }

            return result;
        }

        private static ContentId[] ValidateCardList(IEnumerable<ContentId> source, IReadOnlyDictionary<ContentId, CardDefinition> definitions, string label)
        {
            var ids = source?.ToArray() ?? throw new ArgumentNullException(label);
            foreach (var id in ids)
            {
                if (!definitions.ContainsKey(id))
                {
                    throw new ArgumentException($"The {label} references missing card {id}.", label);
                }
            }

            return ids;
        }
    }
}

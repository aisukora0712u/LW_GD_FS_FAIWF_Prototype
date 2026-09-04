using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    public readonly struct ContentParseResult
    {
        public ContentParseResult(ContentCatalog catalog, IReadOnlyList<string> errors)
        {
            Catalog = catalog;
            Errors = errors ?? Array.Empty<string>();
        }

        public ContentCatalog Catalog { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool Succeeded => Catalog != null && Errors.Count == 0;
    }

    public static class ContentCatalogJson
    {
        public const int CurrentSchemaVersion = 7;

        public static ContentParseResult ParseApproved(string json) => Parse(json, true);
        public static ContentParseResult ParseForReview(string json) => Parse(json, false);

        private static ContentParseResult Parse(string json, bool requireApproved)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(json))
            {
                errors.Add("Content JSON is empty.");
                return new ContentParseResult(null, errors);
            }

            ContentDocument document;
            try
            {
                document = JsonUtility.FromJson<ContentDocument>(json);
            }
            catch (Exception exception)
            {
                errors.Add("Content JSON could not be parsed: " + exception.Message);
                return new ContentParseResult(null, errors);
            }

            if (document == null)
            {
                errors.Add("Content document is missing.");
                return new ContentParseResult(null, errors);
            }

            if (document.schemaVersion != CurrentSchemaVersion)
            {
                errors.Add($"Unsupported content schema version: {document.schemaVersion}.");
            }

            if (requireApproved && !string.Equals(document.status, "approved", StringComparison.Ordinal))
            {
                errors.Add("Only approved content can enter the runtime catalog.");
            }
            else if (!requireApproved && !string.Equals(document.status, "draft", StringComparison.Ordinal) && !string.Equals(document.status, "review", StringComparison.Ordinal))
            {
                errors.Add("Review candidates must have draft or review status.");
            }

            if (string.IsNullOrWhiteSpace(document.contentVersion))
            {
                errors.Add("contentVersion is required.");
            }

            try
            {
                var statuses = Required(document.statuses, "statuses").Select(ParseStatus).ToArray();
                var statusKinds = statuses.ToDictionary(status => status.Id, status => status.Kind);
                var cards = Required(document.cards, "cards").Select(card => ParseCard(card, statusKinds)).ToArray();
                var relics = Required(document.relics, "relics").Select(relic => ParseRelic(relic, statusKinds)).ToArray();
                var shops = Required(document.shops, "shops").Select(ParseShop).ToArray();
                var enemies = Required(document.enemies, "enemies").Select(enemy => ParseEnemy(enemy, statusKinds)).ToArray();
                var encounters = Required(document.encounters, "encounters").Select(ParseEncounter).ToArray();
                var routeNodes = Required(document.routeNodes, "routeNodes").Select(ParseRouteNode).ToArray();
                var nodes = Required(document.storyNodes, "storyNodes").Select(ParseNode).ToArray();
                var startNode = ParseId(document.startNodeId, "startNodeId");
                var startRouteNode = ParseId(document.startRouteNodeId, "startRouteNodeId");
                var initialDeck = Required(document.initialDeck, "initialDeck").Select(value => ParseId(value, "initialDeck card")).ToArray();
                var rewardPool = Required(document.rewardPool, "rewardPool").Select(value => ParseId(value, "rewardPool card")).ToArray();
                var initialRelics = Optional(document.initialRelics).Select(value => ParseId(value, "initialRelics relic")).ToArray();
                var localizationKeys = CollectLocalizationKeys(document);
                var localizedText = CollectLocalizedText(document);

                if (errors.Count > 0)
                {
                    return new ContentParseResult(null, errors);
                }

                var catalog = new ContentCatalog(
                    document.contentVersion,
                    cards,
                    statuses,
                    relics,
                    shops,
                    enemies,
                    encounters,
                    routeNodes,
                    nodes,
                    startNode,
                    startRouteNode,
                    initialDeck,
                    rewardPool,
                    initialRelics,
                    localizationKeys,
                    localizedText,
                    document.playerMaximumHealth,
                    document.rewardChoiceCount);
                return new ContentParseResult(catalog, errors);
            }
            catch (Exception exception)
            {
                errors.Add(exception.Message);
                return new ContentParseResult(null, errors);
            }
        }

        private static CardDefinition ParseCard(CardDto value, IReadOnlyDictionary<ContentId, CombatStatusKind> statusKinds)
        {
            if (value == null)
            {
                throw new ArgumentException("cards contains null.");
            }

            ValidateAuthoringFields(value.nameKey, value.descriptionKey, value.metadata, $"card {value.id}");

            return new CardDefinition(
                ParseId(value.id, "card.id"),
                value.cost,
                Required(value.effects, $"card {value.id} effects").Select(effect => ParseEffect(effect, statusKinds)),
                value.exhausts,
                string.IsNullOrEmpty(value.upgradeToId) ? default : ParseId(value.upgradeToId, $"card {value.id} upgradeToId"));
        }

        private static CardEffect ParseEffect(CardEffectDto value, IReadOnlyDictionary<ContentId, CombatStatusKind> statusKinds)
        {
            if (value == null)
            {
                throw new ArgumentException("Card effect cannot be null.");
            }

            var kind = ParseEnum<CardEffectKind>(value.kind, "card effect kind");
            var statusKind = CombatStatusKind.None;
            if (kind == CardEffectKind.ApplyStatus)
            {
                var statusId = ParseId(value.statusId, "card effect statusId");
                if (!statusKinds.TryGetValue(statusId, out statusKind))
                {
                    throw new ArgumentException($"Card effect references missing status {statusId}.");
                }
            }
            else if (!string.IsNullOrEmpty(value.statusId))
            {
                throw new ArgumentException("Only ApplyStatus effects may declare statusId.");
            }

            return new CardEffect(kind, ParseEnum<EffectTarget>(value.target, "card effect target"), value.amount, statusKind);
        }

        private static StatusDefinition ParseStatus(StatusDto value)
        {
            if (value == null) throw new ArgumentException("statuses contains null.");
            ValidateAuthoringFields(value.nameKey, value.descriptionKey, value.metadata, $"status {value.id}");
            return new StatusDefinition(ParseId(value.id, "status.id"), ParseEnum<CombatStatusKind>(value.kind, $"status {value.id} kind"));
        }

        private static RelicDefinition ParseRelic(RelicDto value, IReadOnlyDictionary<ContentId, CombatStatusKind> statusKinds)
        {
            if (value == null) throw new ArgumentException("relics contains null.");
            ValidateAuthoringFields(value.nameKey, value.descriptionKey, value.metadata, $"relic {value.id}");
            return new RelicDefinition(
                ParseId(value.id, "relic.id"),
                ParseEnum<RelicTriggerKind>(value.trigger, $"relic {value.id} trigger"),
                Required(value.effects, $"relic {value.id} effects").Select(effect => ParseRelicEffect(effect, statusKinds)));
        }

        private static RelicEffectDefinition ParseRelicEffect(RelicEffectDto value, IReadOnlyDictionary<ContentId, CombatStatusKind> statusKinds)
        {
            if (value == null) throw new ArgumentException("Relic effect cannot be null.");
            var kind = ParseEnum<CombatStartEffectKind>(value.kind, "relic effect kind");
            var statusKind = CombatStatusKind.None;
            if (kind == CombatStartEffectKind.ApplyStatus)
            {
                var statusId = ParseId(value.statusId, "relic effect statusId");
                if (!statusKinds.TryGetValue(statusId, out statusKind)) throw new ArgumentException($"Relic effect references missing status {statusId}.");
            }
            else if (!string.IsNullOrEmpty(value.statusId))
            {
                throw new ArgumentException("Only ApplyStatus relic effects may declare statusId.");
            }

            return new RelicEffectDefinition(kind, value.amount, statusKind);
        }

        private static ShopDefinition ParseShop(ShopDto value)
        {
            if (value == null) throw new ArgumentException("shops contains null.");
            ValidateAuthoringFields(value.nameKey, value.descriptionKey, value.metadata, $"shop {value.id}");
            return new ShopDefinition(
                ParseId(value.id, "shop.id"),
                Required(value.inventoryPool, $"shop {value.id} inventoryPool").Select(id => ParseId(id, "shop card")),
                value.cardPrice,
                value.removalPrice,
                value.offerCount);
        }

        private static EnemyDefinition ParseEnemy(EnemyDto value, IReadOnlyDictionary<ContentId, CombatStatusKind> statusKinds)
        {
            if (value == null)
            {
                throw new ArgumentException("enemies contains null.");
            }

            ValidateAuthoringFields(value.nameKey, value.descriptionKey, value.metadata, $"enemy {value.id}");

            return new EnemyDefinition(
                ParseId(value.id, "enemy.id"),
                value.maximumHealth,
                Required(value.intents, $"enemy {value.id} intents").Select(intent => ParseEnemyIntent(intent, statusKinds)));
        }

        private static EnemyIntentDefinition ParseEnemyIntent(EnemyIntentDto value, IReadOnlyDictionary<ContentId, CombatStatusKind> statusKinds)
        {
            if (value == null) throw new ArgumentException("enemy intents contains null.");
            return new EnemyIntentDefinition(
                ParseId(value.id, "enemy intent.id"),
                Required(value.effects, $"enemy intent {value.id} effects").Select(effect => ParseEnemyIntentEffect(effect, statusKinds)));
        }

        private static EnemyIntentEffect ParseEnemyIntentEffect(EnemyIntentEffectDto value, IReadOnlyDictionary<ContentId, CombatStatusKind> statusKinds)
        {
            if (value == null) throw new ArgumentException("enemy intent effects contains null.");
            var kind = ParseEnum<EnemyIntentEffectKind>(value.kind, "enemy intent effect kind");
            var target = ParseEnum<EnemyIntentTarget>(value.target, "enemy intent effect target");
            var statusKind = CombatStatusKind.None;
            if (kind == EnemyIntentEffectKind.ApplyStatus)
            {
                var statusId = ParseId(value.statusId, "enemy intent effect statusId");
                if (!statusKinds.TryGetValue(statusId, out statusKind)) throw new ArgumentException($"Enemy intent effect references missing status {statusId}.");
            }
            else if (!string.IsNullOrEmpty(value.statusId))
            {
                throw new ArgumentException("Only ApplyStatus enemy intent effects may declare statusId.");
            }

            return new EnemyIntentEffect(kind, target, value.amount, statusKind);
        }

        private static EncounterDefinition ParseEncounter(EncounterDto value)
        {
            if (value == null)
            {
                throw new ArgumentException("encounters contains null.");
            }

            ValidateAuthoringFields(value.nameKey, value.descriptionKey, value.metadata, $"encounter {value.id}");

            return new EncounterDefinition(
                ParseId(value.id, "encounter.id"),
                Required(value.enemyIds, $"encounter {value.id} enemyIds").Select(id => ParseId(id, "encounter enemy ID")));
        }

        private static RouteNodeDefinition ParseRouteNode(RouteNodeDto value)
        {
            if (value == null)
            {
                throw new ArgumentException("routeNodes contains null.");
            }

            ValidateAuthoringFields(value.nameKey, value.descriptionKey, value.metadata, $"route node {value.id}");
            var kind = ParseEnum<RouteNodeKind>(value.kind, $"route node {value.id} kind");
            return new RouteNodeDefinition(
                ParseId(value.id, "route node.id"),
                kind,
                kind == RouteNodeKind.Rest && string.IsNullOrEmpty(value.payloadId) ? default : ParseId(value.payloadId, $"route node {value.id} payloadId"),
                Optional(value.nextNodeIds).Select(id => ParseId(id, $"route node {value.id} successor")),
                value.isFinal);
        }

        private static StoryNode ParseNode(StoryNodeDto value)
        {
            if (value == null)
            {
                throw new ArgumentException("storyNodes contains null.");
            }

            ValidateMetadata(value.metadata, $"story node {value.id}");

            return new StoryNode(
                ParseId(value.id, "story node.id"),
                ParseId(value.speakerKey, $"story node {value.id} speakerKey"),
                ParseId(value.textKey, $"story node {value.id} textKey"),
                Optional(value.choices).Select(ParseChoice),
                Optional(value.onEnter).Select(ParseCommand),
                value.isTerminal);
        }

        private static StoryChoice ParseChoice(StoryChoiceDto value)
        {
            if (value == null)
            {
                throw new ArgumentException("Story choice cannot be null.");
            }

            return new StoryChoice(
                ParseId(value.id, "story choice.id"),
                ParseId(value.textKey, $"story choice {value.id} textKey"),
                ParseId(value.nextNodeId, $"story choice {value.id} nextNodeId"),
                Optional(value.conditions).Select(ParseCondition),
                Optional(value.commands).Select(ParseCommand));
        }

        private static NarrativeCondition ParseCondition(ConditionDto value)
        {
            if (value == null)
            {
                throw new ArgumentException("Narrative condition cannot be null.");
            }

            var operation = ParseEnum<NarrativeConditionOperator>(value.operation, "condition operation");
            return new NarrativeCondition(ParseId(value.variableId, "condition variableId"), operation, ParseStoryValue(value.value));
        }

        private static NarrativeCommand ParseCommand(CommandDto value)
        {
            if (value == null)
            {
                throw new ArgumentException("Narrative command cannot be null.");
            }

            var kind = ParseEnum<NarrativeCommandKind>(value.kind, "narrative command kind");
            switch (kind)
            {
                case NarrativeCommandKind.SetVariable:
                    return NarrativeCommand.SetVariable(ParseId(value.key, "command key"), ParseStoryValue(value.value));
                case NarrativeCommandKind.AddInteger:
                    return NarrativeCommand.AddInteger(ParseId(value.key, "command key"), value.amount);
                case NarrativeCommandKind.GrantCard:
                    return NarrativeCommand.GrantCard(ParseId(value.payload, "command payload"));
                case NarrativeCommandKind.GrantRelic:
                    return NarrativeCommand.GrantRelic(ParseId(value.payload, "command payload"));
                case NarrativeCommandKind.StartCombat:
                    return NarrativeCommand.StartCombat(ParseId(value.payload, "command payload"));
                case NarrativeCommandKind.ChangeResource:
                    return NarrativeCommand.ChangeResource(ParseId(value.payload, "command payload"), value.amount);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static StoryValue ParseStoryValue(StoryValueDto value)
        {
            if (value == null)
            {
                return default;
            }

            var kind = ParseEnum<StoryValueKind>(value.kind, "story value kind");
            switch (kind)
            {
                case StoryValueKind.Boolean:
                    return StoryValue.FromBoolean(value.boolean);
                case StoryValueKind.Integer:
                    return StoryValue.FromInteger(value.integer);
                case StoryValueKind.Text:
                    return StoryValue.FromText(value.text ?? string.Empty);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static ContentId ParseId(string value, string label)
        {
            if (!ContentId.TryParse(value, out var id))
            {
                throw new ArgumentException($"Invalid {label}: '{value}'.");
            }

            return id;
        }

        private static T ParseEnum<T>(string value, string label) where T : struct
        {
            if (!Enum.TryParse(value, false, out T parsed) || !Enum.IsDefined(typeof(T), parsed))
            {
                throw new ArgumentException($"Invalid {label}: '{value}'.");
            }

            return parsed;
        }

        private static T[] Required<T>(T[] values, string label)
        {
            if (values == null || values.Length == 0)
            {
                throw new ArgumentException($"{label} is required and cannot be empty.");
            }

            return values;
        }

        private static T[] Optional<T>(T[] values) => values ?? Array.Empty<T>();

        private static ContentId[] CollectLocalizationKeys(ContentDocument document)
        {
            var values = new List<ContentId>();
            foreach (var card in Required(document.cards, "cards"))
            {
                values.Add(ParseId(card.nameKey, $"card {card.id} nameKey"));
                values.Add(ParseId(card.descriptionKey, $"card {card.id} descriptionKey"));
            }

            foreach (var status in Required(document.statuses, "statuses"))
            {
                values.Add(ParseId(status.nameKey, $"status {status.id} nameKey"));
                values.Add(ParseId(status.descriptionKey, $"status {status.id} descriptionKey"));
            }

            foreach (var relic in Required(document.relics, "relics"))
            {
                values.Add(ParseId(relic.nameKey, $"relic {relic.id} nameKey"));
                values.Add(ParseId(relic.descriptionKey, $"relic {relic.id} descriptionKey"));
            }

            foreach (var shop in Required(document.shops, "shops"))
            {
                values.Add(ParseId(shop.nameKey, $"shop {shop.id} nameKey"));
                values.Add(ParseId(shop.descriptionKey, $"shop {shop.id} descriptionKey"));
            }

            foreach (var enemy in Required(document.enemies, "enemies"))
            {
                values.Add(ParseId(enemy.nameKey, $"enemy {enemy.id} nameKey"));
                values.Add(ParseId(enemy.descriptionKey, $"enemy {enemy.id} descriptionKey"));
            }

            foreach (var encounter in Required(document.encounters, "encounters"))
            {
                values.Add(ParseId(encounter.nameKey, $"encounter {encounter.id} nameKey"));
                values.Add(ParseId(encounter.descriptionKey, $"encounter {encounter.id} descriptionKey"));
            }

            foreach (var routeNode in Required(document.routeNodes, "routeNodes"))
            {
                values.Add(ParseId(routeNode.nameKey, $"route node {routeNode.id} nameKey"));
                values.Add(ParseId(routeNode.descriptionKey, $"route node {routeNode.id} descriptionKey"));
            }

            foreach (var node in Required(document.storyNodes, "storyNodes"))
            {
                values.Add(ParseId(node.speakerKey, $"story node {node.id} speakerKey"));
                values.Add(ParseId(node.textKey, $"story node {node.id} textKey"));
                values.AddRange(Optional(node.choices).Select(choice => ParseId(choice.textKey, $"choice {choice.id} textKey")));
            }

            return values.Distinct().ToArray();
        }

        private static LocalizedContentText[] CollectLocalizedText(ContentDocument document)
        {
            return Required(document.cards, "cards")
                .Select(card => new LocalizedContentText(ParseId(card.id, "card.id"), ParseId(card.nameKey, "card.nameKey"), ParseId(card.descriptionKey, "card.descriptionKey")))
                .Concat(Required(document.statuses, "statuses")
                    .Select(status => new LocalizedContentText(ParseId(status.id, "status.id"), ParseId(status.nameKey, "status.nameKey"), ParseId(status.descriptionKey, "status.descriptionKey"))))
                .Concat(Required(document.relics, "relics")
                    .Select(relic => new LocalizedContentText(ParseId(relic.id, "relic.id"), ParseId(relic.nameKey, "relic.nameKey"), ParseId(relic.descriptionKey, "relic.descriptionKey"))))
                .Concat(Required(document.shops, "shops")
                    .Select(shop => new LocalizedContentText(ParseId(shop.id, "shop.id"), ParseId(shop.nameKey, "shop.nameKey"), ParseId(shop.descriptionKey, "shop.descriptionKey"))))
                .Concat(Required(document.enemies, "enemies")
                    .Select(enemy => new LocalizedContentText(ParseId(enemy.id, "enemy.id"), ParseId(enemy.nameKey, "enemy.nameKey"), ParseId(enemy.descriptionKey, "enemy.descriptionKey"))))
                .Concat(Required(document.encounters, "encounters")
                    .Select(encounter => new LocalizedContentText(ParseId(encounter.id, "encounter.id"), ParseId(encounter.nameKey, "encounter.nameKey"), ParseId(encounter.descriptionKey, "encounter.descriptionKey"))))
                .Concat(Required(document.routeNodes, "routeNodes")
                    .Select(node => new LocalizedContentText(ParseId(node.id, "routeNode.id"), ParseId(node.nameKey, "routeNode.nameKey"), ParseId(node.descriptionKey, "routeNode.descriptionKey"))))
                .ToArray();
        }

        private static void ValidateAuthoringFields(string nameKey, string descriptionKey, MetadataDto metadata, string label)
        {
            ParseId(nameKey, label + " nameKey");
            ParseId(descriptionKey, label + " descriptionKey");
            ValidateMetadata(metadata, label);
        }

        private static void ValidateMetadata(MetadataDto metadata, string label)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.owner) || string.IsNullOrWhiteSpace(metadata.source))
            {
                throw new ArgumentException($"{label} requires metadata.owner and metadata.source.");
            }

            if (metadata.tags == null || metadata.tags.Length == 0 || metadata.tags.Any(string.IsNullOrWhiteSpace) || metadata.tags.Distinct(StringComparer.Ordinal).Count() != metadata.tags.Length)
            {
                throw new ArgumentException($"{label} requires unique, non-empty metadata.tags.");
            }
        }

        [Serializable]
        private sealed class ContentDocument
        {
            public int schemaVersion;
            public string contentVersion;
            public string status;
            public int playerMaximumHealth;
            public int rewardChoiceCount;
            public string startNodeId;
            public string startRouteNodeId;
            public string[] initialDeck;
            public string[] rewardPool;
            public string[] initialRelics;
            public CardDto[] cards;
            public StatusDto[] statuses;
            public RelicDto[] relics;
            public ShopDto[] shops;
            public EnemyDto[] enemies;
            public EncounterDto[] encounters;
            public RouteNodeDto[] routeNodes;
            public StoryNodeDto[] storyNodes;
        }

        [Serializable]
        private sealed class CardDto
        {
            public string id;
            public string nameKey;
            public string descriptionKey;
            public MetadataDto metadata;
            public int cost;
            public bool exhausts;
            public string upgradeToId;
            public CardEffectDto[] effects;
        }

        [Serializable]
        private sealed class CardEffectDto
        {
            public string kind;
            public string target;
            public int amount;
            public string statusId;
        }

        [Serializable]
        private sealed class StatusDto
        {
            public string id;
            public string kind;
            public string nameKey;
            public string descriptionKey;
            public MetadataDto metadata;
        }

        [Serializable]
        private sealed class RelicDto
        {
            public string id;
            public string trigger;
            public string nameKey;
            public string descriptionKey;
            public MetadataDto metadata;
            public RelicEffectDto[] effects;
        }

        [Serializable]
        private sealed class RelicEffectDto
        {
            public string kind;
            public int amount;
            public string statusId;
        }

        [Serializable]
        private sealed class ShopDto
        {
            public string id;
            public string nameKey;
            public string descriptionKey;
            public MetadataDto metadata;
            public string[] inventoryPool;
            public int cardPrice;
            public int removalPrice;
            public int offerCount;
        }

        [Serializable]
        private sealed class EnemyDto
        {
            public string id;
            public string nameKey;
            public string descriptionKey;
            public MetadataDto metadata;
            public int maximumHealth;
            public EnemyIntentDto[] intents;
        }

        [Serializable]
        private sealed class EnemyIntentDto
        {
            public string id;
            public EnemyIntentEffectDto[] effects;
        }

        [Serializable]
        private sealed class EnemyIntentEffectDto
        {
            public string kind;
            public string target;
            public int amount;
            public string statusId;
        }

        [Serializable]
        private sealed class EncounterDto
        {
            public string id;
            public string nameKey;
            public string descriptionKey;
            public MetadataDto metadata;
            public string[] enemyIds;
        }

        [Serializable]
        private sealed class RouteNodeDto
        {
            public string id;
            public string kind;
            public string payloadId;
            public string nameKey;
            public string descriptionKey;
            public MetadataDto metadata;
            public string[] nextNodeIds;
            public bool isFinal;
        }

        [Serializable]
        private sealed class StoryNodeDto
        {
            public string id;
            public string speakerKey;
            public string textKey;
            public MetadataDto metadata;
            public bool isTerminal;
            public CommandDto[] onEnter;
            public StoryChoiceDto[] choices;
        }

        [Serializable]
        private sealed class StoryChoiceDto
        {
            public string id;
            public string textKey;
            public string nextNodeId;
            public ConditionDto[] conditions;
            public CommandDto[] commands;
        }

        [Serializable]
        private sealed class ConditionDto
        {
            public string variableId;
            public string operation;
            public StoryValueDto value;
        }

        [Serializable]
        private sealed class CommandDto
        {
            public string kind;
            public string key;
            public string payload;
            public int amount;
            public StoryValueDto value;
        }

        [Serializable]
        private sealed class StoryValueDto
        {
            public string kind;
            public bool boolean;
            public int integer;
            public string text;
        }

        [Serializable]
        private sealed class MetadataDto
        {
            public string owner;
            public string source;
            public string[] tags;
        }
    }
}

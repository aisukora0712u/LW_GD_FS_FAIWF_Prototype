using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    public readonly struct RunCheckpointJsonResult
    {
        public RunCheckpointJsonResult(RunCheckpoint checkpoint, string error)
        {
            Checkpoint = checkpoint;
            Error = error ?? string.Empty;
        }

        public RunCheckpoint Checkpoint { get; }
        public string Error { get; }
        public bool Succeeded => Checkpoint != null && string.IsNullOrEmpty(Error);
    }

    public static class RunCheckpointJson
    {
        public static string Serialize(RunCheckpoint checkpoint)
        {
            if (checkpoint == null)
            {
                throw new ArgumentNullException(nameof(checkpoint));
            }

            var dto = new CheckpointDto
            {
                schemaVersion = checkpoint.SchemaVersion,
                contentVersion = checkpoint.ContentVersion,
                rootSeed = checkpoint.RootSeed.ToString(CultureInfo.InvariantCulture),
                phase = checkpoint.Phase.ToString(),
                currentNodeId = checkpoint.CurrentNodeId.Value,
                narrativeCompleted = checkpoint.NarrativeCompleted,
                playerHealth = checkpoint.PlayerHealth,
                encounterIndex = checkpoint.EncounterIndex,
                currentMapNodeId = checkpoint.CurrentMapNodeId.Value,
                visitedMapNodeIds = checkpoint.VisitedMapNodeIds.Select(id => id.Value).ToArray(),
                relics = checkpoint.Relics.Select(id => id.Value).ToArray(),
                activeShopId = checkpoint.ActiveShopId.Value,
                shopOffers = checkpoint.ShopOffers.Select(id => id.Value).ToArray(),
                shopIndex = checkpoint.ShopIndex,
                shopRemovalUsed = checkpoint.ShopRemovalUsed,
                deck = checkpoint.Deck.Select(id => id.Value).ToArray(),
                rewardCandidates = checkpoint.RewardCandidates.Select(id => id.Value).ToArray(),
                variables = checkpoint.Variables.OrderBy(pair => pair.Key).Select(pair => new VariableDto
                {
                    id = pair.Key.Value,
                    value = ToDto(pair.Value)
                }).ToArray(),
                resources = checkpoint.Resources.OrderBy(pair => pair.Key).Select(pair => new ResourceDto
                {
                    id = pair.Key.Value,
                    amount = pair.Value
                }).ToArray()
            };
            return JsonUtility.ToJson(dto, true);
        }

        public static RunCheckpointJsonResult Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Failure("Checkpoint JSON is empty.");
            }

            try
            {
                var dto = JsonUtility.FromJson<CheckpointDto>(json);
                if (dto == null)
                {
                    return Failure("Checkpoint document is missing.");
                }

                if (dto.schemaVersion < 1 || dto.schemaVersion > RunCheckpoint.CurrentSchemaVersion)
                {
                    return Failure("Checkpoint schema version is not supported.");
                }

                if (!ulong.TryParse(dto.rootSeed, NumberStyles.None, CultureInfo.InvariantCulture, out var rootSeed))
                {
                    return Failure("Checkpoint root seed is invalid.");
                }

                if (!Enum.TryParse(dto.phase, false, out RunPhase phase) || !Enum.IsDefined(typeof(RunPhase), phase))
                {
                    return Failure("Checkpoint phase is invalid.");
                }

                var variables = new Dictionary<ContentId, StoryValue>();
                foreach (var item in dto.variables ?? Array.Empty<VariableDto>())
                {
                    variables.Add(ParseId(item.id, "variable"), FromDto(item.value));
                }

                var resources = new Dictionary<ContentId, int>();
                foreach (var item in dto.resources ?? Array.Empty<ResourceDto>())
                {
                    resources.Add(ParseId(item.id, "resource"), item.amount);
                }

                var checkpoint = new RunCheckpoint(
                    dto.contentVersion,
                    rootSeed,
                    phase,
                    ParseOptionalId(dto.currentNodeId, "current node"),
                    dto.narrativeCompleted,
                    variables,
                    (dto.deck ?? Array.Empty<string>()).Select(value => ParseId(value, "deck card")),
                    dto.playerHealth,
                    resources,
                    (dto.rewardCandidates ?? Array.Empty<string>()).Select(value => ParseId(value, "reward card")),
                    dto.encounterIndex,
                    dto.schemaVersion == 1 ? default : ParseOptionalId(dto.currentMapNodeId, "current map node"),
                    dto.schemaVersion == 1
                        ? Array.Empty<ContentId>()
                        : (dto.visitedMapNodeIds ?? Array.Empty<string>()).Select(value => ParseId(value, "visited map node")),
                    dto.schemaVersion < 3
                        ? Array.Empty<ContentId>()
                        : (dto.relics ?? Array.Empty<string>()).Select(value => ParseId(value, "relic")),
                    dto.schemaVersion < 4 ? default : ParseOptionalId(dto.activeShopId, "active shop"),
                    dto.schemaVersion < 4 ? Array.Empty<ContentId>() : (dto.shopOffers ?? Array.Empty<string>()).Select(value => ParseId(value, "shop offer")),
                    dto.schemaVersion < 4 ? 0 : dto.shopIndex,
                    dto.schemaVersion >= 4 && dto.shopRemovalUsed);
                return new RunCheckpointJsonResult(checkpoint, string.Empty);
            }
            catch (Exception exception)
            {
                return Failure(exception.Message);
            }
        }

        private static StoryValueDto ToDto(StoryValue value)
        {
            return new StoryValueDto
            {
                kind = value.Kind.ToString(),
                boolean = value.Boolean,
                integer = value.Integer,
                text = value.Text
            };
        }

        private static StoryValue FromDto(StoryValueDto value)
        {
            if (value == null || !Enum.TryParse(value.kind, false, out StoryValueKind kind) || !Enum.IsDefined(typeof(StoryValueKind), kind))
            {
                throw new ArgumentException("Checkpoint story value is invalid.");
            }

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
                throw new ArgumentException($"Checkpoint {label} ID is invalid: '{value}'.");
            }

            return id;
        }

        private static ContentId ParseOptionalId(string value, string label)
        {
            return string.IsNullOrEmpty(value) ? default : ParseId(value, label);
        }

        private static RunCheckpointJsonResult Failure(string error) => new RunCheckpointJsonResult(null, error);

        [Serializable]
        private sealed class CheckpointDto
        {
            public int schemaVersion;
            public string contentVersion;
            public string rootSeed;
            public string phase;
            public string currentNodeId;
            public bool narrativeCompleted;
            public int playerHealth;
            public int encounterIndex;
            public string currentMapNodeId;
            public string[] visitedMapNodeIds;
            public string[] relics;
            public string activeShopId;
            public string[] shopOffers;
            public int shopIndex;
            public bool shopRemovalUsed;
            public string[] deck;
            public string[] rewardCandidates;
            public VariableDto[] variables;
            public ResourceDto[] resources;
        }

        [Serializable]
        private sealed class VariableDto
        {
            public string id;
            public StoryValueDto value;
        }

        [Serializable]
        private sealed class ResourceDto
        {
            public string id;
            public int amount;
        }

        [Serializable]
        private sealed class StoryValueDto
        {
            public string kind;
            public bool boolean;
            public int integer;
            public string text;
        }
    }
}

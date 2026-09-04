using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    public static class VerticalSliceLocalizationKeys
    {
        public static readonly ContentId Title = new ContentId("ui.vertical_slice.title");
        public static readonly ContentId NarrativePhase = new ContentId("ui.phase.narrative");
        public static readonly ContentId MapPhase = new ContentId("ui.phase.map");
        public static readonly ContentId MapPrompt = new ContentId("ui.map.prompt");
        public static readonly ContentId UpgradePhase = new ContentId("ui.phase.upgrade");
        public static readonly ContentId UpgradePrompt = new ContentId("ui.upgrade.prompt");
        public static readonly ContentId ShopPhase = new ContentId("ui.phase.shop");
        public static readonly ContentId ShopPrompt = new ContentId("ui.shop.prompt");
        public static readonly ContentId ShopBuy = new ContentId("ui.shop.buy");
        public static readonly ContentId ShopRemove = new ContentId("ui.shop.remove");
        public static readonly ContentId ShopLeave = new ContentId("ui.shop.leave");
        public static readonly ContentId CombatPhase = new ContentId("ui.phase.combat");
        public static readonly ContentId RewardPhase = new ContentId("ui.phase.reward");
        public static readonly ContentId CompletedPhase = new ContentId("ui.phase.completed");
        public static readonly ContentId DefeatPhase = new ContentId("ui.phase.defeat");
        public static readonly ContentId PlayerStatus = new ContentId("ui.combat.player_status");
        public static readonly ContentId EnemyStatus = new ContentId("ui.combat.enemy_status");
        public static readonly ContentId IntentAttack = new ContentId("ui.intent.attack");
        public static readonly ContentId IntentBlock = new ContentId("ui.intent.block");
        public static readonly ContentId IntentStatusPlayer = new ContentId("ui.intent.status_player");
        public static readonly ContentId IntentStatusSelf = new ContentId("ui.intent.status_self");
        public static readonly ContentId EndTurn = new ContentId("ui.combat.end_turn");
        public static readonly ContentId RewardPrompt = new ContentId("ui.reward.prompt");
        public static readonly ContentId Restart = new ContentId("ui.action.restart");
        public static readonly ContentId SwitchLocale = new ContentId("ui.action.switch_locale");
        public static readonly ContentId ContentError = new ContentId("ui.error.content");

        public static IReadOnlyList<ContentId> All { get; } = new[]
        {
            Title,
            NarrativePhase,
            MapPhase,
            MapPrompt,
            UpgradePhase,
            UpgradePrompt,
            ShopPhase,
            ShopPrompt,
            ShopBuy,
            ShopRemove,
            ShopLeave,
            CombatPhase,
            RewardPhase,
            CompletedPhase,
            DefeatPhase,
            PlayerStatus,
            EnemyStatus,
            IntentAttack,
            IntentBlock,
            IntentStatusPlayer,
            IntentStatusSelf,
            EndTurn,
            RewardPrompt,
            Restart,
            SwitchLocale,
            ContentError
        };
    }

    public sealed class LocalizationCatalog
    {
        private readonly Dictionary<ContentId, string> entries;

        internal LocalizationCatalog(string locale, string localizationVersion, Dictionary<ContentId, string> entries)
        {
            Locale = locale;
            LocalizationVersion = localizationVersion;
            this.entries = entries;
        }

        public string Locale { get; }
        public string LocalizationVersion { get; }
        public IReadOnlyDictionary<ContentId, string> Entries => entries;

        public bool TryGet(ContentId key, out string value) => entries.TryGetValue(key, out value);
        public string Get(ContentId key) => entries.TryGetValue(key, out var value) ? value : $"[{key.Value}]";
    }

    public readonly struct LocalizationParseResult
    {
        public LocalizationParseResult(LocalizationCatalog catalog, IReadOnlyList<string> errors)
        {
            Catalog = catalog;
            Errors = errors ?? Array.Empty<string>();
        }

        public LocalizationCatalog Catalog { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool Succeeded => Catalog != null && Errors.Count == 0;
    }

    public static class LocalizationCatalogJson
    {
        public const int CurrentSchemaVersion = 1;

        public static LocalizationParseResult ParseApproved(string json) => Parse(json, true);
        public static LocalizationParseResult ParseForReview(string json) => Parse(json, false);

        private static LocalizationParseResult Parse(string json, bool requireApproved)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(json))
            {
                errors.Add("Localization JSON is empty.");
                return new LocalizationParseResult(null, errors);
            }

            try
            {
                var document = JsonUtility.FromJson<LocalizationDocument>(json);
                if (document == null)
                {
                    throw new ArgumentException("Localization document is missing.");
                }

                if (document.schemaVersion != CurrentSchemaVersion)
                {
                    errors.Add($"Unsupported localization schema version: {document.schemaVersion}.");
                }

                if (requireApproved && !string.Equals(document.status, "approved", StringComparison.Ordinal))
                {
                    errors.Add("Only approved localization can enter the runtime catalog.");
                }
                else if (!requireApproved && !string.Equals(document.status, "draft", StringComparison.Ordinal) && !string.Equals(document.status, "review", StringComparison.Ordinal))
                {
                    errors.Add("Review localization must have draft or review status.");
                }

                if (string.IsNullOrWhiteSpace(document.locale) || string.IsNullOrWhiteSpace(document.localizationVersion) ||
                    string.IsNullOrWhiteSpace(document.owner) || string.IsNullOrWhiteSpace(document.source))
                {
                    errors.Add("locale, localizationVersion, owner, and source are required.");
                }

                if (document.entries == null || document.entries.Length == 0)
                {
                    errors.Add("Localization entries cannot be empty.");
                }

                var entries = new Dictionary<ContentId, string>();
                foreach (var item in document.entries ?? Array.Empty<LocalizationEntry>())
                {
                    if (item == null || !ContentId.TryParse(item.key, out var key) || string.IsNullOrWhiteSpace(item.value))
                    {
                        errors.Add($"Invalid or empty localization entry: '{item?.key}'.");
                        continue;
                    }

                    if (entries.ContainsKey(key))
                    {
                        errors.Add($"Duplicate localization key: {key}.");
                        continue;
                    }

                    entries.Add(key, item.value);
                }

                return errors.Count == 0
                    ? new LocalizationParseResult(new LocalizationCatalog(document.locale, document.localizationVersion, entries), errors)
                    : new LocalizationParseResult(null, errors);
            }
            catch (Exception exception)
            {
                errors.Add(exception.Message);
                return new LocalizationParseResult(null, errors);
            }
        }

        public static IReadOnlyList<string> ValidateCoverage(ContentCatalog content, IEnumerable<LocalizationCatalog> catalogs, IEnumerable<string> requiredLocales)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            var errors = new List<string>();
            var byLocale = (catalogs ?? Array.Empty<LocalizationCatalog>())
                .Where(catalog => catalog != null)
                .GroupBy(catalog => catalog.Locale, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            var requiredKeys = content.RequiredLocalizationKeys.Concat(VerticalSliceLocalizationKeys.All).Distinct().ToArray();
            foreach (var locale in requiredLocales ?? Array.Empty<string>())
            {
                if (!byLocale.TryGetValue(locale, out var matches))
                {
                    errors.Add($"Required locale is missing: {locale}.");
                    continue;
                }

                if (matches.Length != 1)
                {
                    errors.Add($"Locale must have exactly one catalog: {locale}.");
                    continue;
                }

                var catalog = matches[0];
                if (!string.Equals(catalog.LocalizationVersion, content.ContentVersion, StringComparison.Ordinal))
                {
                    errors.Add($"Locale {locale} version {catalog.LocalizationVersion} does not match content {content.ContentVersion}.");
                }

                foreach (var key in requiredKeys.Where(key => !catalog.Entries.ContainsKey(key)))
                {
                    errors.Add($"Locale {locale} is missing key {key}.");
                }
            }

            return errors;
        }

        [Serializable]
        private sealed class LocalizationDocument
        {
            public int schemaVersion;
            public string localizationVersion;
            public string locale;
            public string status;
            public string owner;
            public string source;
            public LocalizationEntry[] entries;
        }

        [Serializable]
        private sealed class LocalizationEntry
        {
            public string key;
            public string value;
        }
    }
}

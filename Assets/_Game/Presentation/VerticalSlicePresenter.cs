using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Game.Core;
using Game.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Presentation
{
    [DisallowMultipleComponent]
    public sealed class VerticalSlicePresenter : MonoBehaviour
    {
        [SerializeField] private TextAsset contentSource;
        [SerializeField] private TextAsset englishSource;
        [SerializeField] private TextAsset simplifiedChineseSource;
        [SerializeField] private string initialLocale = "zh-Hans";
        [SerializeField] private long runSeed = 4242;

        private readonly Dictionary<string, LocalizationCatalog> localizations = new Dictionary<string, LocalizationCatalog>(StringComparer.Ordinal);
        private readonly List<Button> actionButtons = new List<Button>();
        private ContentCatalog catalog;
        private LocalizationCatalog localization;
        private RunSession session;
        private Font font;
        private Text titleText;
        private Text phaseText;
        private RectTransform contentPanel;
        private Button localeButton;
        private Text localeButtonText;
        private string lastError = string.Empty;

        public bool IsReady { get; private set; }
        public RunPhase CurrentPhase => session?.Phase ?? RunPhase.NotStarted;
        public string CurrentLocale => localization?.Locale ?? string.Empty;
        public int VisibleActionCount => actionButtons.Count;
        public string LastError => lastError;
        public RunSession Session => session;
        public bool ContainsVisibleText(string value)
        {
            return contentPanel != null && contentPanel.GetComponentsInChildren<Text>().Any(item => item.text.Contains(value));
        }

        private void Awake()
        {
            Initialize();
        }

        public bool Initialize()
        {
            if (IsReady)
            {
                return true;
            }

            if (contentSource == null || englishSource == null || simplifiedChineseSource == null)
            {
                return Fail("VerticalSlicePresenter is missing content or localization assets.");
            }

            var contentResult = ContentCatalogJson.ParseApproved(contentSource.text);
            var englishResult = LocalizationCatalogJson.ParseApproved(englishSource.text);
            var chineseResult = LocalizationCatalogJson.ParseApproved(simplifiedChineseSource.text);
            if (!contentResult.Succeeded || !englishResult.Succeeded || !chineseResult.Succeeded)
            {
                var errors = contentResult.Errors.Concat(englishResult.Errors).Concat(chineseResult.Errors);
                return Fail(string.Join(Environment.NewLine, errors));
            }

            catalog = contentResult.Catalog;
            localizations.Add(englishResult.Catalog.Locale, englishResult.Catalog);
            localizations.Add(chineseResult.Catalog.Locale, chineseResult.Catalog);
            var coverageErrors = LocalizationCatalogJson.ValidateCoverage(catalog, localizations.Values, new[] { "en", "zh-Hans" });
            if (coverageErrors.Count > 0)
            {
                return Fail(string.Join(Environment.NewLine, coverageErrors));
            }

            localization = localizations.TryGetValue(initialLocale, out var selected) ? selected : englishResult.Catalog;
            BuildInterface();
            IsReady = true;
            Restart();
            return true;
        }

        public bool InvokeVisibleAction(int index)
        {
            if (index < 0 || index >= actionButtons.Count)
            {
                return false;
            }

            actionButtons[index].onClick.Invoke();
            return true;
        }

        public bool SetLocale(string locale)
        {
            if (!localizations.TryGetValue(locale, out var selected))
            {
                return false;
            }

            localization = selected;
            Render();
            return true;
        }

        public void Restart()
        {
            if (catalog == null)
            {
                return;
            }

            session = new RunSession(catalog, unchecked((ulong)Math.Max(1, runSeed)));
            var result = session.Start();
            lastError = result.Succeeded ? string.Empty : result.Error;
            Render();
        }

        public void Refresh() => Render();

        private bool Fail(string error)
        {
            lastError = error ?? "Unknown vertical slice error.";
            Debug.LogError(lastError, this);
            enabled = false;
            return false;
        }

        private void BuildInterface()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Noto Sans CJK SC", "Arial" }, 28);
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            var canvasObject = new GameObject("VerticalSliceCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var background = CreatePanel("Background", canvasObject.transform, new Color(0.035f, 0.043f, 0.075f, 1f));
            Stretch(background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            titleText = CreateText("Title", background, 34, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.92f, 0.85f, 0.62f));
            SetAnchors(titleText.rectTransform, new Vector2(0.06f, 0.9f), new Vector2(0.72f, 0.98f));
            phaseText = CreateText("Phase", background, 24, FontStyle.Normal, TextAnchor.MiddleRight, Color.white);
            SetAnchors(phaseText.rectTransform, new Vector2(0.68f, 0.9f), new Vector2(0.84f, 0.98f));
            localeButton = CreateButton("Locale", background, string.Empty, () => SetLocale(CurrentLocale == "en" ? "zh-Hans" : "en"));
            SetAnchors(localeButton.GetComponent<RectTransform>(), new Vector2(0.85f, 0.91f), new Vector2(0.95f, 0.97f));
            localeButtonText = localeButton.GetComponentInChildren<Text>();

            var panel = CreatePanel("Content", background, new Color(0.08f, 0.09f, 0.14f, 0.98f));
            SetAnchors(panel, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.88f));
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 30, 30);
            layout.spacing = 14f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            contentPanel = panel;

            EnsureEventSystem();
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventObject.transform.SetParent(transform, false);
            eventObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private void Render()
        {
            if (contentPanel == null || localization == null || session == null)
            {
                return;
            }

            for (var index = contentPanel.childCount - 1; index >= 0; index--)
            {
                Destroy(contentPanel.GetChild(index).gameObject);
            }

            actionButtons.Clear();
            titleText.text = localization.Get(VerticalSliceLocalizationKeys.Title);
            localeButtonText.text = localization.Get(VerticalSliceLocalizationKeys.SwitchLocale);
            switch (session.Phase)
            {
                case RunPhase.Map:
                    phaseText.text = localization.Get(VerticalSliceLocalizationKeys.MapPhase);
                    RenderMap();
                    break;
                case RunPhase.Upgrade:
                    phaseText.text = localization.Get(VerticalSliceLocalizationKeys.UpgradePhase);
                    RenderUpgrade();
                    break;
                case RunPhase.Shop:
                    phaseText.text = localization.Get(VerticalSliceLocalizationKeys.ShopPhase);
                    RenderShop();
                    break;
                case RunPhase.Narrative:
                    phaseText.text = localization.Get(VerticalSliceLocalizationKeys.NarrativePhase);
                    RenderNarrative();
                    break;
                case RunPhase.Combat:
                    phaseText.text = localization.Get(VerticalSliceLocalizationKeys.CombatPhase);
                    RenderCombat();
                    break;
                case RunPhase.Reward:
                    phaseText.text = localization.Get(VerticalSliceLocalizationKeys.RewardPhase);
                    RenderReward();
                    break;
                case RunPhase.Completed:
                    phaseText.text = localization.Get(VerticalSliceLocalizationKeys.CompletedPhase);
                    RenderTerminal(VerticalSliceLocalizationKeys.CompletedPhase);
                    break;
                case RunPhase.Defeat:
                    phaseText.text = localization.Get(VerticalSliceLocalizationKeys.DefeatPhase);
                    RenderTerminal(VerticalSliceLocalizationKeys.DefeatPhase);
                    break;
            }
        }

        private void RenderMap()
        {
            CreateFlowText("MapPrompt", localization.Get(VerticalSliceLocalizationKeys.MapPrompt), 28, FontStyle.Bold, Color.white, 72f);
            foreach (var node in session.AvailableMapNodes)
            {
                var captured = node.Id;
                var text = catalog.LocalizedText[node.Id];
                AddAction($"{localization.Get(text.NameKey)} — {localization.Get(text.DescriptionKey)}", () => Apply(session.SelectMapNode(captured)));
            }
        }

        private void RenderUpgrade()
        {
            CreateFlowText("UpgradePrompt", localization.Get(VerticalSliceLocalizationKeys.UpgradePrompt), 28, FontStyle.Bold, Color.white, 72f);
            for (var index = 0; index < session.Deck.Count; index++)
            {
                var deckIndex = index;
                var cardId = session.Deck[index];
                var definition = catalog.Cards[cardId];
                if (definition.UpgradeToId.IsEmpty) continue;
                var currentText = catalog.LocalizedText[cardId];
                var upgradeText = catalog.LocalizedText[definition.UpgradeToId];
                AddAction($"{localization.Get(currentText.NameKey)} → {localization.Get(upgradeText.NameKey)}", () => Apply(session.UpgradeCard(deckIndex)));
            }
        }

        private void RenderShop()
        {
            var goldId = new ContentId("resource.gold");
            session.Resources.TryGetValue(goldId, out var gold);
            CreateFlowText("ShopPrompt", Format(VerticalSliceLocalizationKeys.ShopPrompt, gold), 26, FontStyle.Bold, Color.white, 64f);
            foreach (var cardId in session.ShopOffers)
            {
                var captured = cardId;
                var text = catalog.LocalizedText[cardId];
                AddAction(
                    Format(VerticalSliceLocalizationKeys.ShopBuy, session.ActiveShop.CardPrice, localization.Get(text.NameKey)),
                    () => Apply(session.PurchaseShopCard(captured)),
                    gold >= session.ActiveShop.CardPrice);
            }

            foreach (var item in session.Deck.Select((id, index) => new { id, index }).GroupBy(item => item.id).Select(group => group.First()))
            {
                var capturedIndex = item.index;
                var text = catalog.LocalizedText[item.id];
                AddAction(
                    Format(VerticalSliceLocalizationKeys.ShopRemove, session.ActiveShop.RemovalPrice, localization.Get(text.NameKey)),
                    () => Apply(session.RemoveShopCard(capturedIndex)),
                    !session.ShopRemovalUsed && gold >= session.ActiveShop.RemovalPrice && session.Deck.Count > 1,
                    new Color(0.34f, 0.18f, 0.2f));
            }

            AddAction(localization.Get(VerticalSliceLocalizationKeys.ShopLeave), () => Apply(session.LeaveShop()), true, new Color(0.25f, 0.25f, 0.28f));
        }

        private void RenderNarrative()
        {
            var node = session.Narrative.CurrentNode;
            CreateFlowText("Speaker", localization.Get(node.SpeakerKey), 24, FontStyle.Bold, new Color(0.65f, 0.82f, 0.95f), 54f);
            CreateFlowText("Story", localization.Get(node.TextKey), 30, FontStyle.Normal, Color.white, 180f);
            foreach (var choice in session.Narrative.AvailableChoices)
            {
                var captured = choice.Id;
                AddAction(localization.Get(choice.TextKey), () => Apply(session.Choose(captured)));
            }
        }

        private void RenderCombat()
        {
            var combat = session.Combat;
            CreateFlowText(
                "PlayerStatus",
                Format(VerticalSliceLocalizationKeys.PlayerStatus, combat.Player.Health, combat.Player.MaximumHealth, combat.Player.Block, combat.Energy, combat.Turn, FormatStatuses(combat.Player), FormatRelics()),
                24,
                FontStyle.Bold,
                new Color(0.72f, 0.95f, 0.78f),
                52f);
            for (var index = 0; index < combat.Enemies.Count; index++)
            {
                var enemy = combat.Enemies[index];
                var text = catalog.LocalizedText[enemy.Id];
                CreateFlowText(
                    "EnemyStatus",
                    Format(VerticalSliceLocalizationKeys.EnemyStatus, localization.Get(text.NameKey), enemy.Health, enemy.MaximumHealth, enemy.Block, FormatIntent(enemy.CurrentIntent), FormatStatuses(enemy)),
                    22,
                    FontStyle.Normal,
                    new Color(1f, 0.68f, 0.66f),
                    48f);
            }

            CreateFlowText("HandLabel", "—", 16, FontStyle.Normal, new Color(0.45f, 0.5f, 0.62f), 24f);
            for (var index = 0; index < combat.Hand.Count; index++)
            {
                var handIndex = index;
                var cardId = combat.Hand[index];
                var definition = catalog.Cards[cardId];
                var text = catalog.LocalizedText[cardId];
                var label = $"[{definition.Cost}] {localization.Get(text.NameKey)} — {localization.Get(text.DescriptionKey)}";
                AddAction(label, () =>
                {
                    var targetIndex = definition.Effects.Any(effect => effect.Target == EffectTarget.SingleEnemy)
                        ? FirstLivingEnemyIndex(combat)
                        : -1;
                    Apply(session.PlayCard(handIndex, targetIndex));
                }, definition.Cost <= combat.Energy);
            }

            AddAction(localization.Get(VerticalSliceLocalizationKeys.EndTurn), () => Apply(session.EndTurn()), true, new Color(0.32f, 0.24f, 0.3f));
        }

        private void RenderReward()
        {
            CreateFlowText("RewardPrompt", localization.Get(VerticalSliceLocalizationKeys.RewardPrompt), 28, FontStyle.Bold, Color.white, 72f);
            foreach (var cardId in session.RewardCandidates)
            {
                var captured = cardId;
                var definition = catalog.Cards[cardId];
                var text = catalog.LocalizedText[cardId];
                AddAction($"[{definition.Cost}] {localization.Get(text.NameKey)} — {localization.Get(text.DescriptionKey)}", () => Apply(session.ClaimReward(captured)));
            }
        }

        private void RenderTerminal(ContentId messageKey)
        {
            CreateFlowText("Terminal", localization.Get(messageKey), 42, FontStyle.Bold, Color.white, 160f);
            AddAction(localization.Get(VerticalSliceLocalizationKeys.Restart), Restart);
        }

        private void Apply(RunCommandResult result)
        {
            lastError = result.Succeeded ? string.Empty : result.Error;
            Render();
        }

        private string Format(ContentId key, params object[] values) => string.Format(CultureInfo.CurrentCulture, localization.Get(key), values);

        private string FormatStatuses(CombatantState combatant)
        {
            var labels = combatant.Statuses
                .Where(pair => pair.Value > 0)
                .OrderBy(pair => pair.Key)
                .Select(pair =>
                {
                    var definition = catalog.Statuses.Values.First(status => status.Kind == pair.Key);
                    return $"{localization.Get(catalog.LocalizedText[definition.Id].NameKey)} {pair.Value}";
                })
                .ToArray();
            return labels.Length == 0 ? "—" : string.Join("  ", labels);
        }

        private string FormatIntent(EnemyIntentDefinition intent)
        {
            if (intent == null) return "—";
            return string.Join("  +  ", intent.Effects.Select(effect =>
            {
                switch (effect.Kind)
                {
                    case EnemyIntentEffectKind.Attack:
                        return Format(VerticalSliceLocalizationKeys.IntentAttack, effect.Amount);
                    case EnemyIntentEffectKind.GainBlock:
                        return Format(VerticalSliceLocalizationKeys.IntentBlock, effect.Amount);
                    case EnemyIntentEffectKind.ApplyStatus:
                        var definition = catalog.Statuses.Values.First(status => status.Kind == effect.StatusKind);
                        var statusName = localization.Get(catalog.LocalizedText[definition.Id].NameKey);
                        return Format(
                            effect.Target == EnemyIntentTarget.Player
                                ? VerticalSliceLocalizationKeys.IntentStatusPlayer
                                : VerticalSliceLocalizationKeys.IntentStatusSelf,
                            statusName,
                            effect.Amount);
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }));
        }

        private string FormatRelics()
        {
            var labels = session.Relics
                .Select(id => localization.Get(catalog.LocalizedText[id].NameKey))
                .ToArray();
            return labels.Length == 0 ? "—" : string.Join("  ", labels);
        }

        private static int FirstLivingEnemyIndex(CombatState combat)
        {
            for (var index = 0; index < combat.Enemies.Count; index++)
            {
                if (!combat.Enemies[index].IsDefeated)
                {
                    return index;
                }
            }

            return -1;
        }

        private void AddAction(string label, Action action, bool interactable = true, Color? color = null)
        {
            var button = CreateButton("Action", contentPanel, label, action, color ?? new Color(0.16f, 0.23f, 0.36f));
            button.interactable = interactable;
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
            actionButtons.Add(button);
        }

        private Text CreateFlowText(string name, string value, int size, FontStyle style, Color color, float height)
        {
            var text = CreateText(name, contentPanel, size, style, TextAnchor.MiddleLeft, color);
            text.text = value;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return text;
        }

        private Button CreateButton(string name, Transform parent, string label, Action action, Color? color = null)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.GetComponent<Image>();
            image.color = color ?? new Color(0.16f, 0.23f, 0.36f);
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            var labelText = CreateText("Label", buttonObject.transform, 22, FontStyle.Normal, TextAnchor.MiddleCenter, Color.white);
            labelText.text = label;
            Stretch(labelText.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 8f), new Vector2(-18f, -8f));
            return button;
        }

        private Text CreateText(string name, Transform parent, int size, FontStyle style, TextAnchor alignment, Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.supportRichText = false;
            return text;
        }

        private static RectTransform CreatePanel(string name, Transform parent, Color color)
        {
            var panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(parent, false);
            panelObject.GetComponent<Image>().color = color;
            return panelObject.GetComponent<RectTransform>();
        }

        private static void SetAnchors(RectTransform transform, Vector2 minimum, Vector2 maximum)
        {
            transform.anchorMin = minimum;
            transform.anchorMax = maximum;
            transform.offsetMin = Vector2.zero;
            transform.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform transform, Vector2 minimum, Vector2 maximum, Vector2 offsetMinimum, Vector2 offsetMaximum)
        {
            transform.anchorMin = minimum;
            transform.anchorMax = maximum;
            transform.offsetMin = offsetMinimum;
            transform.offsetMax = offsetMaximum;
        }
    }
}

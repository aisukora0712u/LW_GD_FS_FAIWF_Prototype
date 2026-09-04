using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public enum CombatPhase
    {
        NotStarted,
        PlayerTurn,
        EnemyTurn,
        Victory,
        Defeat
    }

    public enum CardEffectKind
    {
        Damage,
        Block,
        Draw,
        ApplyStatus
    }

    public enum CombatStatusKind
    {
        None,
        Strength,
        Weak,
        Vulnerable
    }

    public enum EffectTarget
    {
        Self,
        SingleEnemy,
        AllEnemies
    }

    public enum EnemyIntentEffectKind
    {
        Attack,
        GainBlock,
        ApplyStatus
    }

    public enum EnemyIntentTarget
    {
        Self,
        Player
    }

    public sealed class EnemyIntentEffect
    {
        public EnemyIntentEffect(EnemyIntentEffectKind kind, EnemyIntentTarget target, int amount, CombatStatusKind statusKind = CombatStatusKind.None)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (kind == EnemyIntentEffectKind.Attack && target != EnemyIntentTarget.Player)
            {
                throw new ArgumentException("Enemy attacks must target the player.", nameof(target));
            }

            if (kind == EnemyIntentEffectKind.GainBlock && target != EnemyIntentTarget.Self)
            {
                throw new ArgumentException("Enemy block effects must target self.", nameof(target));
            }

            if ((kind == EnemyIntentEffectKind.ApplyStatus) != (statusKind != CombatStatusKind.None))
            {
                throw new ArgumentException("ApplyStatus requires a status kind, and other enemy intent effects cannot carry one.", nameof(statusKind));
            }

            Kind = kind;
            Target = target;
            Amount = amount;
            StatusKind = statusKind;
        }

        public EnemyIntentEffectKind Kind { get; }
        public EnemyIntentTarget Target { get; }
        public int Amount { get; }
        public CombatStatusKind StatusKind { get; }
    }

    public sealed class EnemyIntentDefinition
    {
        private readonly EnemyIntentEffect[] effects;

        public EnemyIntentDefinition(ContentId id, IEnumerable<EnemyIntentEffect> effects)
        {
            if (id.IsEmpty) throw new ArgumentException("An enemy intent requires a stable ID.", nameof(id));
            this.effects = effects?.ToArray() ?? throw new ArgumentNullException(nameof(effects));
            if (this.effects.Length == 0 || this.effects.Any(effect => effect == null))
            {
                throw new ArgumentException("An enemy intent requires at least one valid effect.", nameof(effects));
            }

            Id = id;
        }

        public ContentId Id { get; }
        public IReadOnlyList<EnemyIntentEffect> Effects => effects;
    }

    public enum CombatEventKind
    {
        CombatStarted,
        DeckShuffled,
        TurnStarted,
        TurnEnded,
        CardDrawn,
        CardDiscarded,
        CardPlayed,
        EnergyChanged,
        DamageApplied,
        BlockGained,
        StatusChanged,
        RelicTriggered,
        EnemyIntentStarted,
        Victory,
        Defeat
    }

    public sealed class CardEffect
    {
        public CardEffect(CardEffectKind kind, EffectTarget target, int amount, CombatStatusKind statusKind = CombatStatusKind.None)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Card effect amount must be positive.");
            }

            if (kind == CardEffectKind.Draw && target != EffectTarget.Self)
            {
                throw new ArgumentException("Draw effects must target self.", nameof(target));
            }

            if ((kind == CardEffectKind.ApplyStatus) != (statusKind != CombatStatusKind.None))
            {
                throw new ArgumentException("ApplyStatus requires a status kind, and other effects cannot carry one.", nameof(statusKind));
            }

            Kind = kind;
            Target = target;
            Amount = amount;
            StatusKind = statusKind;
        }

        public CardEffectKind Kind { get; }
        public EffectTarget Target { get; }
        public int Amount { get; }
        public CombatStatusKind StatusKind { get; }
    }

    public sealed class CardDefinition
    {
        private readonly CardEffect[] effects;

        public CardDefinition(ContentId id, int cost, IEnumerable<CardEffect> effects, bool exhausts = false, ContentId upgradeToId = default)
        {
            if (id.IsEmpty)
            {
                throw new ArgumentException("A card requires an ID.", nameof(id));
            }

            if (cost < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cost));
            }

            if (!upgradeToId.IsEmpty && upgradeToId == id)
            {
                throw new ArgumentException("A card cannot upgrade to itself.", nameof(upgradeToId));
            }

            this.effects = effects?.ToArray() ?? throw new ArgumentNullException(nameof(effects));
            if (this.effects.Length == 0 || this.effects.Any(effect => effect == null))
            {
                throw new ArgumentException("A card requires at least one valid effect.", nameof(effects));
            }

            Id = id;
            Cost = cost;
            Exhausts = exhausts;
            UpgradeToId = upgradeToId;
        }

        public ContentId Id { get; }
        public int Cost { get; }
        public bool Exhausts { get; }
        public ContentId UpgradeToId { get; }
        public IReadOnlyList<CardEffect> Effects => effects;
    }

    public sealed class CombatantState
    {
        private readonly Dictionary<CombatStatusKind, int> statuses = new Dictionary<CombatStatusKind, int>();
        private readonly EnemyIntentDefinition[] intents;
        private int intentIndex;

        public CombatantState(
            ContentId id,
            int maximumHealth,
            int attackDamage = 0,
            int? currentHealth = null,
            IEnumerable<EnemyIntentDefinition> intents = null)
        {
            if (id.IsEmpty)
            {
                throw new ArgumentException("A combatant requires an ID.", nameof(id));
            }

            if (maximumHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumHealth));
            }

            if (attackDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attackDamage));
            }

            if (currentHealth.HasValue && (currentHealth.Value <= 0 || currentHealth.Value > maximumHealth))
            {
                throw new ArgumentOutOfRangeException(nameof(currentHealth));
            }

            Id = id;
            MaximumHealth = maximumHealth;
            Health = currentHealth ?? maximumHealth;
            this.intents = intents?.ToArray() ?? (attackDamage > 0
                ? new[]
                {
                    new EnemyIntentDefinition(
                        new ContentId("intent.legacy_attack"),
                        new[] { new EnemyIntentEffect(EnemyIntentEffectKind.Attack, EnemyIntentTarget.Player, attackDamage) })
                }
                : Array.Empty<EnemyIntentDefinition>());
            if (this.intents.Any(intent => intent == null) || this.intents.Select(intent => intent.Id).Distinct().Count() != this.intents.Length)
            {
                throw new ArgumentException("Enemy intents must be non-null and have unique IDs.", nameof(intents));
            }
        }

        public ContentId Id { get; }
        public int MaximumHealth { get; }
        public int Health { get; private set; }
        public int Block { get; private set; }
        public int AttackDamage => CurrentIntent?.Effects.Where(effect => effect.Kind == EnemyIntentEffectKind.Attack).Sum(effect => effect.Amount) ?? 0;
        public EnemyIntentDefinition CurrentIntent => intents.Length == 0 ? null : intents[intentIndex];
        public int IntentIndex => intentIndex;
        public bool IsDefeated => Health <= 0;
        public IReadOnlyDictionary<CombatStatusKind, int> Statuses => statuses;

        public int GetStatus(CombatStatusKind kind) => statuses.TryGetValue(kind, out var amount) ? amount : 0;

        internal void ClearBlock() => Block = 0;

        internal void GainBlock(int amount)
        {
            Block = checked(Block + amount);
        }

        internal int TakeDamage(int amount)
        {
            var absorbed = Math.Min(Block, amount);
            Block -= absorbed;
            var healthDamage = amount - absorbed;
            Health = Math.Max(0, Health - healthDamage);
            return healthDamage;
        }

        internal int AddStatus(CombatStatusKind kind, int amount)
        {
            if (kind == CombatStatusKind.None || amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            var next = checked(GetStatus(kind) + amount);
            statuses[kind] = next;
            return next;
        }

        internal int ReduceStatus(CombatStatusKind kind)
        {
            var current = GetStatus(kind);
            if (current <= 0) return 0;
            var next = current - 1;
            if (next == 0) statuses.Remove(kind); else statuses[kind] = next;
            return next;
        }

        internal void AdvanceIntent()
        {
            if (intents.Length > 0) intentIndex = (intentIndex + 1) % intents.Length;
        }
    }

    public sealed class CombatConfig
    {
        public CombatConfig(int energyPerTurn = 3, int openingHandSize = 5, int drawPerTurn = 5, int maximumHandSize = 10)
        {
            if (energyPerTurn < 0 || openingHandSize < 0 || drawPerTurn < 0 || maximumHandSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(energyPerTurn), "Combat configuration values are outside their valid range.");
            }

            EnergyPerTurn = energyPerTurn;
            OpeningHandSize = openingHandSize;
            DrawPerTurn = drawPerTurn;
            MaximumHandSize = maximumHandSize;
        }

        public int EnergyPerTurn { get; }
        public int OpeningHandSize { get; }
        public int DrawPerTurn { get; }
        public int MaximumHandSize { get; }
    }

    public enum CombatStartEffectKind
    {
        GainBlock,
        ApplyStatus
    }

    public sealed class CombatStartEffect
    {
        public CombatStartEffect(ContentId sourceId, CombatStartEffectKind kind, int amount, CombatStatusKind statusKind = CombatStatusKind.None)
        {
            if (sourceId.IsEmpty || amount <= 0)
            {
                throw new ArgumentException("A combat-start effect requires a source ID and positive amount.");
            }

            if ((kind == CombatStartEffectKind.ApplyStatus) != (statusKind != CombatStatusKind.None))
            {
                throw new ArgumentException("ApplyStatus requires a status kind, and GainBlock cannot carry one.", nameof(statusKind));
            }

            SourceId = sourceId;
            Kind = kind;
            Amount = amount;
            StatusKind = statusKind;
        }

        public ContentId SourceId { get; }
        public CombatStartEffectKind Kind { get; }
        public int Amount { get; }
        public CombatStatusKind StatusKind { get; }
    }

    public readonly struct CombatCommandResult
    {
        private CombatCommandResult(bool succeeded, string error)
        {
            Succeeded = succeeded;
            Error = error ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Error { get; }

        public static CombatCommandResult Success() => new CombatCommandResult(true, string.Empty);
        public static CombatCommandResult Failure(string error) => new CombatCommandResult(false, error);
    }

    public sealed class CombatEvent
    {
        public CombatEvent(long sequence, int turn, CombatEventKind kind, ContentId source, ContentId target, ContentId content, int value, CombatStatusKind statusKind)
        {
            Sequence = sequence;
            Turn = turn;
            Kind = kind;
            Source = source;
            Target = target;
            Content = content;
            Value = value;
            StatusKind = statusKind;
        }

        public long Sequence { get; }
        public int Turn { get; }
        public CombatEventKind Kind { get; }
        public ContentId Source { get; }
        public ContentId Target { get; }
        public ContentId Content { get; }
        public int Value { get; }
        public CombatStatusKind StatusKind { get; }
    }

    public sealed class CombatState
    {
        private readonly Dictionary<ContentId, CardDefinition> definitions;
        private readonly List<ContentId> drawPile;
        private readonly List<ContentId> hand = new List<ContentId>();
        private readonly List<ContentId> discardPile = new List<ContentId>();
        private readonly List<ContentId> exhaustPile = new List<ContentId>();
        private readonly List<CombatantState> enemies;
        private readonly List<CombatEvent> events = new List<CombatEvent>();
        private readonly CombatConfig config;
        private readonly NamedRandomStreams randomStreams;
        private readonly CombatStartEffect[] startEffects;
        private long nextSequence = 1;

        public CombatState(
            IEnumerable<CardDefinition> cardDefinitions,
            IEnumerable<ContentId> deck,
            CombatantState player,
            IEnumerable<CombatantState> enemies,
            ulong rootSeed,
            CombatConfig config = null,
            IEnumerable<CombatStartEffect> startEffects = null)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            definitions = (cardDefinitions ?? throw new ArgumentNullException(nameof(cardDefinitions))).ToDictionary(card => card.Id);
            drawPile = (deck ?? throw new ArgumentNullException(nameof(deck))).ToList();
            if (drawPile.Count == 0 || drawPile.Any(id => !definitions.ContainsKey(id)))
            {
                throw new ArgumentException("The deck must contain at least one card and every ID must have a definition.", nameof(deck));
            }

            this.enemies = (enemies ?? throw new ArgumentNullException(nameof(enemies))).ToList();
            if (this.enemies.Count == 0 || this.enemies.Any(enemy => enemy == null))
            {
                throw new ArgumentException("Combat requires at least one enemy.", nameof(enemies));
            }

            Player = player;
            this.config = config ?? new CombatConfig();
            randomStreams = new NamedRandomStreams(rootSeed);
            this.startEffects = startEffects?.ToArray() ?? Array.Empty<CombatStartEffect>();
            if (this.startEffects.Any(effect => effect == null)) throw new ArgumentException("Combat-start effects cannot contain null.", nameof(startEffects));
        }

        public CombatantState Player { get; }
        public IReadOnlyList<CombatantState> Enemies => enemies;
        public IReadOnlyList<ContentId> DrawPile => drawPile;
        public IReadOnlyList<ContentId> Hand => hand;
        public IReadOnlyList<ContentId> DiscardPile => discardPile;
        public IReadOnlyList<ContentId> ExhaustPile => exhaustPile;
        public IReadOnlyList<CombatEvent> Events => events;
        public IReadOnlyDictionary<string, ulong> RandomStates => randomStreams.CaptureStates();
        public CombatPhase Phase { get; private set; }
        public int Turn { get; private set; }
        public int Energy { get; private set; }

        public CombatCommandResult Start()
        {
            if (Phase != CombatPhase.NotStarted)
            {
                return CombatCommandResult.Failure("Combat has already started.");
            }

            AddEvent(CombatEventKind.CombatStarted, Player.Id, default, default, 0);
            randomStreams.Get("combat.draw").Shuffle(drawPile);
            AddEvent(CombatEventKind.DeckShuffled, default, default, default, drawPile.Count);
            StartPlayerTurn(config.OpeningHandSize);
            ApplyStartEffects();
            return CombatCommandResult.Success();
        }

        private void ApplyStartEffects()
        {
            foreach (var effect in startEffects)
            {
                AddEvent(CombatEventKind.RelicTriggered, effect.SourceId, Player.Id, effect.SourceId, effect.Amount, effect.StatusKind);
                if (effect.Kind == CombatStartEffectKind.GainBlock)
                {
                    Player.GainBlock(effect.Amount);
                    AddEvent(CombatEventKind.BlockGained, effect.SourceId, Player.Id, effect.SourceId, effect.Amount);
                }
                else
                {
                    var next = Player.AddStatus(effect.StatusKind, effect.Amount);
                    AddEvent(CombatEventKind.StatusChanged, effect.SourceId, Player.Id, effect.SourceId, next, effect.StatusKind);
                }
            }
        }

        public CombatCommandResult PlayCard(int handIndex, int targetEnemyIndex = -1)
        {
            if (Phase != CombatPhase.PlayerTurn)
            {
                return CombatCommandResult.Failure("Cards can only be played during the player turn.");
            }

            if (handIndex < 0 || handIndex >= hand.Count)
            {
                return CombatCommandResult.Failure("Hand index is outside the current hand.");
            }

            var cardId = hand[handIndex];
            var definition = definitions[cardId];
            if (definition.Cost > Energy)
            {
                return CombatCommandResult.Failure("Not enough energy.");
            }

            if (definition.Effects.Any(effect => effect.Target == EffectTarget.SingleEnemy) &&
                (targetEnemyIndex < 0 || targetEnemyIndex >= enemies.Count || enemies[targetEnemyIndex].IsDefeated))
            {
                return CombatCommandResult.Failure("A living enemy target is required.");
            }

            hand.RemoveAt(handIndex);
            Energy -= definition.Cost;
            AddEvent(CombatEventKind.EnergyChanged, Player.Id, default, default, -definition.Cost);
            var targetId = targetEnemyIndex >= 0 && targetEnemyIndex < enemies.Count ? enemies[targetEnemyIndex].Id : default;
            AddEvent(CombatEventKind.CardPlayed, Player.Id, targetId, cardId, definition.Cost);

            foreach (var effect in definition.Effects)
            {
                ApplyEffect(effect, targetEnemyIndex, cardId);
                if (Phase == CombatPhase.Defeat)
                {
                    break;
                }
            }

            if (definition.Exhausts)
            {
                exhaustPile.Add(cardId);
            }
            else
            {
                discardPile.Add(cardId);
            }

            CheckVictory();
            return CombatCommandResult.Success();
        }

        public CombatCommandResult EndTurn()
        {
            if (Phase != CombatPhase.PlayerTurn)
            {
                return CombatCommandResult.Failure("Only the active player turn can end.");
            }

            while (hand.Count > 0)
            {
                var card = hand[0];
                hand.RemoveAt(0);
                discardPile.Add(card);
                AddEvent(CombatEventKind.CardDiscarded, Player.Id, default, card, 0);
            }

            AddEvent(CombatEventKind.TurnEnded, Player.Id, default, default, Turn);
            ReduceStatus(Player, CombatStatusKind.Weak);
            foreach (var enemy in enemies) ReduceStatus(enemy, CombatStatusKind.Vulnerable);
            Phase = CombatPhase.EnemyTurn;
            foreach (var enemy in enemies.Where(candidate => !candidate.IsDefeated))
            {
                enemy.ClearBlock();
                ExecuteEnemyIntent(enemy);
                if (Player.IsDefeated)
                {
                    Phase = CombatPhase.Defeat;
                    AddEvent(CombatEventKind.Defeat, Player.Id, default, default, 0);
                    return CombatCommandResult.Success();
                }

                enemy.AdvanceIntent();
            }

            foreach (var enemy in enemies) ReduceStatus(enemy, CombatStatusKind.Weak);
            ReduceStatus(Player, CombatStatusKind.Vulnerable);

            StartPlayerTurn(config.DrawPerTurn);
            return CombatCommandResult.Success();
        }

        private void ExecuteEnemyIntent(CombatantState enemy)
        {
            var intent = enemy.CurrentIntent;
            if (intent == null) return;
            AddEvent(CombatEventKind.EnemyIntentStarted, enemy.Id, default, intent.Id, enemy.IntentIndex);
            foreach (var effect in intent.Effects)
            {
                var target = effect.Target == EnemyIntentTarget.Self ? enemy : Player;
                switch (effect.Kind)
                {
                    case EnemyIntentEffectKind.Attack:
                        ApplyDamage(enemy, Player, effect.Amount, intent.Id);
                        break;
                    case EnemyIntentEffectKind.GainBlock:
                        enemy.GainBlock(effect.Amount);
                        AddEvent(CombatEventKind.BlockGained, enemy.Id, enemy.Id, intent.Id, effect.Amount);
                        break;
                    case EnemyIntentEffectKind.ApplyStatus:
                        var next = target.AddStatus(effect.StatusKind, effect.Amount);
                        AddEvent(CombatEventKind.StatusChanged, enemy.Id, target.Id, intent.Id, next, effect.StatusKind);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (Player.IsDefeated) return;
            }
        }

        private void StartPlayerTurn(int cardsToDraw)
        {
            Phase = CombatPhase.PlayerTurn;
            Turn++;
            Player.ClearBlock();
            Energy = config.EnergyPerTurn;
            AddEvent(CombatEventKind.TurnStarted, Player.Id, default, default, Turn);
            AddEvent(CombatEventKind.EnergyChanged, Player.Id, default, default, Energy);
            DrawCards(cardsToDraw);
        }

        private void ApplyEffect(CardEffect effect, int targetEnemyIndex, ContentId cardId)
        {
            if (effect.Kind == CardEffectKind.Draw)
            {
                DrawCards(effect.Amount);
                return;
            }

            IEnumerable<CombatantState> targets;
            switch (effect.Target)
            {
                case EffectTarget.Self:
                    targets = new[] { Player };
                    break;
                case EffectTarget.SingleEnemy:
                    targets = new[] { enemies[targetEnemyIndex] };
                    break;
                case EffectTarget.AllEnemies:
                    targets = enemies.Where(enemy => !enemy.IsDefeated);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            foreach (var target in targets.ToArray())
            {
                if (effect.Kind == CardEffectKind.ApplyStatus)
                {
                    var next = target.AddStatus(effect.StatusKind, effect.Amount);
                    AddEvent(CombatEventKind.StatusChanged, Player.Id, target.Id, cardId, next, effect.StatusKind);
                }
                else if (effect.Kind == CardEffectKind.Damage)
                {
                    ApplyDamage(Player, target, effect.Amount, cardId);
                }
                else if (effect.Kind == CardEffectKind.Block)
                {
                    target.GainBlock(effect.Amount);
                    AddEvent(CombatEventKind.BlockGained, Player.Id, target.Id, cardId, effect.Amount);
                }
            }
        }

        private void ApplyDamage(CombatantState source, CombatantState target, int amount, ContentId cardId)
        {
            var modified = checked(amount + source.GetStatus(CombatStatusKind.Strength));
            if (source.GetStatus(CombatStatusKind.Weak) > 0) modified = checked(modified * 3 / 4);
            if (target.GetStatus(CombatStatusKind.Vulnerable) > 0) modified = checked(modified * 3 / 2);
            var healthDamage = target.TakeDamage(modified);
            AddEvent(CombatEventKind.DamageApplied, source.Id, target.Id, cardId, healthDamage);
        }

        private void ReduceStatus(CombatantState target, CombatStatusKind kind)
        {
            if (target.GetStatus(kind) <= 0) return;
            var next = target.ReduceStatus(kind);
            AddEvent(CombatEventKind.StatusChanged, target.Id, target.Id, default, next, kind);
        }

        private void DrawCards(int count)
        {
            for (var index = 0; index < count; index++)
            {
                if (!TryTakeDrawCard(out var card))
                {
                    return;
                }

                if (hand.Count >= config.MaximumHandSize)
                {
                    discardPile.Add(card);
                    AddEvent(CombatEventKind.CardDiscarded, Player.Id, default, card, 0);
                }
                else
                {
                    hand.Add(card);
                    AddEvent(CombatEventKind.CardDrawn, Player.Id, default, card, 0);
                }
            }
        }

        private bool TryTakeDrawCard(out ContentId card)
        {
            if (drawPile.Count == 0 && discardPile.Count > 0)
            {
                drawPile.AddRange(discardPile);
                discardPile.Clear();
                randomStreams.Get("combat.draw").Shuffle(drawPile);
                AddEvent(CombatEventKind.DeckShuffled, default, default, default, drawPile.Count);
            }

            if (drawPile.Count == 0)
            {
                card = default;
                return false;
            }

            var last = drawPile.Count - 1;
            card = drawPile[last];
            drawPile.RemoveAt(last);
            return true;
        }

        private void CheckVictory()
        {
            if (Phase != CombatPhase.Defeat && enemies.All(enemy => enemy.IsDefeated))
            {
                Phase = CombatPhase.Victory;
                AddEvent(CombatEventKind.Victory, Player.Id, default, default, Turn);
            }
        }

        private void AddEvent(CombatEventKind kind, ContentId source, ContentId target, ContentId content, int value, CombatStatusKind statusKind = CombatStatusKind.None)
        {
            events.Add(new CombatEvent(nextSequence++, Turn, kind, source, target, content, value, statusKind));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public enum StoryValueKind
    {
        Boolean,
        Integer,
        Text
    }

    public readonly struct StoryValue : IEquatable<StoryValue>
    {
        private StoryValue(StoryValueKind kind, bool boolean, int integer, string text)
        {
            Kind = kind;
            Boolean = boolean;
            Integer = integer;
            Text = text ?? string.Empty;
        }

        public StoryValueKind Kind { get; }
        public bool Boolean { get; }
        public int Integer { get; }
        public string Text { get; }

        public static StoryValue FromBoolean(bool value) => new StoryValue(StoryValueKind.Boolean, value, 0, string.Empty);
        public static StoryValue FromInteger(int value) => new StoryValue(StoryValueKind.Integer, false, value, string.Empty);
        public static StoryValue FromText(string value) => new StoryValue(StoryValueKind.Text, false, 0, value ?? throw new ArgumentNullException(nameof(value)));

        public bool Equals(StoryValue other) => Kind == other.Kind && Boolean == other.Boolean && Integer == other.Integer && string.Equals(Text, other.Text, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is StoryValue other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Kind, Boolean, Integer, Text);
        public override string ToString() => Kind == StoryValueKind.Boolean ? Boolean.ToString() : Kind == StoryValueKind.Integer ? Integer.ToString() : Text;
    }

    public enum NarrativeConditionOperator
    {
        Exists,
        Equals,
        NotEquals,
        GreaterOrEqual
    }

    public sealed class NarrativeCondition
    {
        public NarrativeCondition(ContentId variableId, NarrativeConditionOperator comparison, StoryValue expected = default)
        {
            if (variableId.IsEmpty)
            {
                throw new ArgumentException("A condition requires a variable ID.", nameof(variableId));
            }

            VariableId = variableId;
            Comparison = comparison;
            Expected = expected;
        }

        public ContentId VariableId { get; }
        public NarrativeConditionOperator Comparison { get; }
        public StoryValue Expected { get; }

        public bool Evaluate(IReadOnlyDictionary<ContentId, StoryValue> variables)
        {
            if (!variables.TryGetValue(VariableId, out var actual))
            {
                return false;
            }

            switch (Comparison)
            {
                case NarrativeConditionOperator.Exists:
                    return true;
                case NarrativeConditionOperator.Equals:
                    return actual.Equals(Expected);
                case NarrativeConditionOperator.NotEquals:
                    return !actual.Equals(Expected);
                case NarrativeConditionOperator.GreaterOrEqual:
                    return actual.Kind == StoryValueKind.Integer && Expected.Kind == StoryValueKind.Integer && actual.Integer >= Expected.Integer;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public enum NarrativeCommandKind
    {
        SetVariable,
        AddInteger,
        GrantCard,
        GrantRelic,
        StartCombat,
        ChangeResource
    }

    public sealed class NarrativeCommand
    {
        private NarrativeCommand(NarrativeCommandKind kind, ContentId key, ContentId payload, StoryValue value, int amount)
        {
            Kind = kind;
            Key = key;
            Payload = payload;
            Value = value;
            Amount = amount;
        }

        public NarrativeCommandKind Kind { get; }
        public ContentId Key { get; }
        public ContentId Payload { get; }
        public StoryValue Value { get; }
        public int Amount { get; }
        public bool IsInternal => Kind == NarrativeCommandKind.SetVariable || Kind == NarrativeCommandKind.AddInteger;

        public static NarrativeCommand SetVariable(ContentId variableId, StoryValue value)
        {
            RequireId(variableId, nameof(variableId));
            return new NarrativeCommand(NarrativeCommandKind.SetVariable, variableId, default, value, 0);
        }

        public static NarrativeCommand AddInteger(ContentId variableId, int amount)
        {
            RequireId(variableId, nameof(variableId));
            return new NarrativeCommand(NarrativeCommandKind.AddInteger, variableId, default, default, amount);
        }

        public static NarrativeCommand GrantCard(ContentId cardId)
        {
            RequireId(cardId, nameof(cardId));
            return new NarrativeCommand(NarrativeCommandKind.GrantCard, default, cardId, default, 0);
        }

        public static NarrativeCommand GrantRelic(ContentId relicId)
        {
            RequireId(relicId, nameof(relicId));
            return new NarrativeCommand(NarrativeCommandKind.GrantRelic, default, relicId, default, 0);
        }

        public static NarrativeCommand StartCombat(ContentId encounterId)
        {
            RequireId(encounterId, nameof(encounterId));
            return new NarrativeCommand(NarrativeCommandKind.StartCombat, default, encounterId, default, 0);
        }

        public static NarrativeCommand ChangeResource(ContentId resourceId, int amount)
        {
            RequireId(resourceId, nameof(resourceId));
            return new NarrativeCommand(NarrativeCommandKind.ChangeResource, default, resourceId, default, amount);
        }

        private static void RequireId(ContentId id, string parameter)
        {
            if (id.IsEmpty)
            {
                throw new ArgumentException("A command requires a stable content ID.", parameter);
            }
        }
    }

    public sealed class StoryChoice
    {
        private readonly NarrativeCondition[] conditions;
        private readonly NarrativeCommand[] commands;

        public StoryChoice(ContentId id, ContentId textKey, ContentId nextNodeId, IEnumerable<NarrativeCondition> conditions = null, IEnumerable<NarrativeCommand> commands = null)
        {
            if (id.IsEmpty || textKey.IsEmpty || nextNodeId.IsEmpty)
            {
                throw new ArgumentException("A story choice requires ID, text key, and next node ID.");
            }

            this.conditions = conditions?.ToArray() ?? Array.Empty<NarrativeCondition>();
            this.commands = commands?.ToArray() ?? Array.Empty<NarrativeCommand>();
            if (this.conditions.Any(condition => condition == null) || this.commands.Any(command => command == null))
            {
                throw new ArgumentException("Choice conditions and commands cannot contain null values.");
            }

            Id = id;
            TextKey = textKey;
            NextNodeId = nextNodeId;
        }

        public ContentId Id { get; }
        public ContentId TextKey { get; }
        public ContentId NextNodeId { get; }
        public IReadOnlyList<NarrativeCondition> Conditions => conditions;
        public IReadOnlyList<NarrativeCommand> Commands => commands;

        public bool IsAvailable(IReadOnlyDictionary<ContentId, StoryValue> variables) => conditions.All(condition => condition.Evaluate(variables));
    }

    public sealed class StoryNode
    {
        private readonly StoryChoice[] choices;
        private readonly NarrativeCommand[] onEnter;

        public StoryNode(ContentId id, ContentId speakerKey, ContentId textKey, IEnumerable<StoryChoice> choices = null, IEnumerable<NarrativeCommand> onEnter = null, bool isTerminal = false)
        {
            if (id.IsEmpty || speakerKey.IsEmpty || textKey.IsEmpty)
            {
                throw new ArgumentException("A story node requires ID, speaker key, and text key.");
            }

            this.choices = choices?.ToArray() ?? Array.Empty<StoryChoice>();
            this.onEnter = onEnter?.ToArray() ?? Array.Empty<NarrativeCommand>();
            if (this.choices.Any(choice => choice == null) || this.onEnter.Any(command => command == null))
            {
                throw new ArgumentException("Node choices and commands cannot contain null values.");
            }

            if (this.choices.Select(choice => choice.Id).Distinct().Count() != this.choices.Length)
            {
                throw new ArgumentException("Choice IDs must be unique within a node.", nameof(choices));
            }

            if (isTerminal && this.choices.Length > 0)
            {
                throw new ArgumentException("A terminal node cannot contain choices.", nameof(choices));
            }

            Id = id;
            SpeakerKey = speakerKey;
            TextKey = textKey;
            IsTerminal = isTerminal;
        }

        public ContentId Id { get; }
        public ContentId SpeakerKey { get; }
        public ContentId TextKey { get; }
        public bool IsTerminal { get; }
        public IReadOnlyList<StoryChoice> Choices => choices;
        public IReadOnlyList<NarrativeCommand> OnEnter => onEnter;
    }

    public static class NarrativeTopology
    {
        // Structural analysis only: conditional choices are treated as potential edges.
        public static IReadOnlyList<string> Validate(IEnumerable<StoryNode> source, IEnumerable<ContentId> entryIds)
        {
            var nodes = source.ToDictionary(node => node.Id);
            var errors = new List<string>();
            var predecessors = nodes.Keys.ToDictionary(id => id, id => new List<ContentId>());
            foreach (var node in nodes.Values)
            {
                foreach (var choice in node.Choices)
                {
                    if (!nodes.ContainsKey(choice.NextNodeId))
                        errors.Add($"Choice {choice.Id} references missing node {choice.NextNodeId}.");
                    else predecessors[choice.NextNodeId].Add(node.Id);
                }
            }
            var entries = entryIds.Distinct().ToArray();
            if (entries.Length == 0) errors.Add("Narrative requires an entry node.");
            foreach (var entry in entries)
                if (!nodes.ContainsKey(entry)) errors.Add($"Missing narrative entry {entry}.");
            if (errors.Count > 0) return errors.OrderBy(value => value, StringComparer.Ordinal).ToArray();

            var reachable = new HashSet<ContentId>();
            var pending = new Stack<ContentId>(entries);
            while (pending.Count > 0)
            {
                var id = pending.Pop();
                if (!reachable.Add(id)) continue;
                foreach (var choice in nodes[id].Choices) pending.Push(choice.NextNodeId);
            }
            var canEnd = new HashSet<ContentId>();
            pending = new Stack<ContentId>(nodes.Values.Where(node => node.IsTerminal).Select(node => node.Id));
            while (pending.Count > 0)
            {
                var id = pending.Pop();
                if (!canEnd.Add(id)) continue;
                foreach (var predecessor in predecessors[id]) pending.Push(predecessor);
            }
            foreach (var id in nodes.Keys)
            {
                if (!reachable.Contains(id)) errors.Add($"Unreachable narrative node {id}.");
                if (!canEnd.Contains(id)) errors.Add($"Narrative node {id} has no terminal path.");
            }
            return errors.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }

    public enum NarrativeEventKind
    {
        NodeEntered,
        ChoiceSelected,
        VariableChanged,
        CommandEmitted,
        Completed
    }

    public sealed class NarrativeEvent
    {
        public NarrativeEvent(long sequence, NarrativeEventKind kind, ContentId nodeId, ContentId contentId, int value)
        {
            Sequence = sequence;
            Kind = kind;
            NodeId = nodeId;
            ContentId = contentId;
            Value = value;
        }

        public long Sequence { get; }
        public NarrativeEventKind Kind { get; }
        public ContentId NodeId { get; }
        public ContentId ContentId { get; }
        public int Value { get; }
    }

    public readonly struct NarrativeCommandResult
    {
        private NarrativeCommandResult(bool succeeded, string error)
        {
            Succeeded = succeeded;
            Error = error ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Error { get; }
        public static NarrativeCommandResult Success() => new NarrativeCommandResult(true, string.Empty);
        public static NarrativeCommandResult Failure(string error) => new NarrativeCommandResult(false, error);
    }

    public sealed class NarrativeStateMachine
    {
        private readonly Dictionary<ContentId, StoryNode> nodes;
        private readonly Dictionary<ContentId, StoryValue> variables = new Dictionary<ContentId, StoryValue>();
        private readonly List<NarrativeCommand> pendingCommands = new List<NarrativeCommand>();
        private readonly List<NarrativeEvent> events = new List<NarrativeEvent>();
        private long nextSequence = 1;

        public NarrativeStateMachine(IEnumerable<StoryNode> nodes)
        {
            this.nodes = (nodes ?? throw new ArgumentNullException(nameof(nodes))).ToDictionary(node => node.Id);
            if (this.nodes.Count == 0)
            {
                throw new ArgumentException("A story requires at least one node.", nameof(nodes));
            }

            foreach (var choice in this.nodes.Values.SelectMany(node => node.Choices))
            {
                if (!this.nodes.ContainsKey(choice.NextNodeId))
                {
                    throw new ArgumentException($"Choice {choice.Id} references missing node {choice.NextNodeId}.", nameof(nodes));
                }
            }
        }

        public StoryNode CurrentNode { get; private set; }
        public bool HasStarted { get; private set; }
        public bool IsCompleted { get; private set; }
        public IReadOnlyDictionary<ContentId, StoryValue> Variables => variables;
        public IReadOnlyList<NarrativeEvent> Events => events;
        public IReadOnlyList<StoryChoice> AvailableChoices => CurrentNode == null
            ? Array.Empty<StoryChoice>()
            : CurrentNode.Choices.Where(choice => choice.IsAvailable(variables)).ToArray();

        public NarrativeCommandResult Start(ContentId nodeId)
        {
            return Start(nodeId, null);
        }

        public NarrativeCommandResult Start(ContentId nodeId, IReadOnlyDictionary<ContentId, StoryValue> initialVariables)
        {
            if (HasStarted)
            {
                return NarrativeCommandResult.Failure("Narrative has already started.");
            }

            if (!nodes.TryGetValue(nodeId, out var node))
            {
                return NarrativeCommandResult.Failure("Start node does not exist.");
            }

            variables.Clear();
            if (initialVariables != null)
            {
                foreach (var pair in initialVariables)
                {
                    if (pair.Key.IsEmpty)
                    {
                        return NarrativeCommandResult.Failure("Initial variables contain an empty ID.");
                    }

                    variables.Add(pair.Key, pair.Value);
                }
            }

            HasStarted = true;
            EnterNode(node);
            return NarrativeCommandResult.Success();
        }

        public NarrativeCommandResult Restore(ContentId nodeId, IReadOnlyDictionary<ContentId, StoryValue> restoredVariables, bool completed)
        {
            if (HasStarted)
            {
                return NarrativeCommandResult.Failure("Narrative has already started.");
            }

            if (!nodes.TryGetValue(nodeId, out var node))
            {
                return NarrativeCommandResult.Failure("Checkpoint node does not exist.");
            }

            if (completed && !node.IsTerminal)
            {
                return NarrativeCommandResult.Failure("Only a terminal node can be restored as completed.");
            }

            variables.Clear();
            if (restoredVariables != null)
            {
                foreach (var pair in restoredVariables)
                {
                    if (pair.Key.IsEmpty)
                    {
                        return NarrativeCommandResult.Failure("Checkpoint contains an empty variable ID.");
                    }

                    variables.Add(pair.Key, pair.Value);
                }
            }

            CurrentNode = node;
            HasStarted = true;
            IsCompleted = completed;
            return NarrativeCommandResult.Success();
        }

        public NarrativeCommandResult Choose(ContentId choiceId)
        {
            if (!HasStarted || IsCompleted || CurrentNode == null)
            {
                return NarrativeCommandResult.Failure("Narrative is not awaiting a choice.");
            }

            var choice = CurrentNode.Choices.FirstOrDefault(candidate => candidate.Id == choiceId && candidate.IsAvailable(variables));
            if (choice == null)
            {
                return NarrativeCommandResult.Failure("Choice is missing or unavailable.");
            }

            AddEvent(NarrativeEventKind.ChoiceSelected, CurrentNode.Id, choice.Id, 0);
            ApplyCommands(choice.Commands);
            EnterNode(nodes[choice.NextNodeId]);
            return NarrativeCommandResult.Success();
        }

        public IReadOnlyList<NarrativeCommand> DrainPendingCommands()
        {
            var drained = pendingCommands.ToArray();
            pendingCommands.Clear();
            return drained;
        }

        private void EnterNode(StoryNode node)
        {
            CurrentNode = node;
            AddEvent(NarrativeEventKind.NodeEntered, node.Id, node.TextKey, 0);
            ApplyCommands(node.OnEnter);
            if (node.IsTerminal)
            {
                IsCompleted = true;
                AddEvent(NarrativeEventKind.Completed, node.Id, default, 0);
            }
        }

        private void ApplyCommands(IEnumerable<NarrativeCommand> commands)
        {
            foreach (var command in commands)
            {
                if (command.Kind == NarrativeCommandKind.SetVariable)
                {
                    variables[command.Key] = command.Value;
                    AddEvent(NarrativeEventKind.VariableChanged, CurrentNode.Id, command.Key, command.Value.Kind == StoryValueKind.Integer ? command.Value.Integer : 0);
                }
                else if (command.Kind == NarrativeCommandKind.AddInteger)
                {
                    var current = 0;
                    if (variables.TryGetValue(command.Key, out var existing))
                    {
                        if (existing.Kind != StoryValueKind.Integer)
                        {
                            throw new InvalidOperationException($"Variable {command.Key} is not an integer.");
                        }

                        current = existing.Integer;
                    }

                    var next = checked(current + command.Amount);
                    variables[command.Key] = StoryValue.FromInteger(next);
                    AddEvent(NarrativeEventKind.VariableChanged, CurrentNode.Id, command.Key, next);
                }
                else
                {
                    pendingCommands.Add(command);
                    AddEvent(NarrativeEventKind.CommandEmitted, CurrentNode.Id, command.Payload, command.Amount);
                }
            }
        }

        private void AddEvent(NarrativeEventKind kind, ContentId nodeId, ContentId contentId, int value)
        {
            events.Add(new NarrativeEvent(nextSequence++, kind, nodeId, contentId, value));
        }
    }
}

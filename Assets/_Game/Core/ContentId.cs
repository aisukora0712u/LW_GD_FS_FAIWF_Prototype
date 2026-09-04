using System;

namespace Game.Core
{
    public readonly struct ContentId : IEquatable<ContentId>, IComparable<ContentId>
    {
        private readonly string value;

        public ContentId(string value)
        {
            if (!IsValid(value))
            {
                throw new ArgumentException("Content ID must use lowercase dot-separated segments and be at most 96 characters.", nameof(value));
            }

            this.value = value;
        }

        public string Value => value ?? string.Empty;
        public bool IsEmpty => string.IsNullOrEmpty(value);

        public static bool TryParse(string value, out ContentId id)
        {
            if (IsValid(value))
            {
                id = new ContentId(value);
                return true;
            }

            id = default;
            return false;
        }

        public static bool IsValid(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > 96 || candidate[0] == '.' || candidate[candidate.Length - 1] == '.')
            {
                return false;
            }

            var segmentStart = true;
            for (var index = 0; index < candidate.Length; index++)
            {
                var character = candidate[index];
                if (character == '.')
                {
                    if (segmentStart)
                    {
                        return false;
                    }

                    segmentStart = true;
                    continue;
                }

                if (segmentStart && (character < 'a' || character > 'z'))
                {
                    return false;
                }

                if ((character < 'a' || character > 'z') &&
                    (character < '0' || character > '9') &&
                    character != '_' && character != '-')
                {
                    return false;
                }

                segmentStart = false;
            }

            return !segmentStart;
        }

        public bool Equals(ContentId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ContentId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public int CompareTo(ContentId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public override string ToString() => Value;

        public static bool operator ==(ContentId left, ContentId right) => left.Equals(right);
        public static bool operator !=(ContentId left, ContentId right) => !left.Equals(right);
    }
}

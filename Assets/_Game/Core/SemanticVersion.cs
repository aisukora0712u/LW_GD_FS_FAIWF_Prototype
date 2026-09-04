using System;

namespace Game.Core
{
    public readonly struct SemanticVersion : IEquatable<SemanticVersion>
    {
        public SemanticVersion(int major, int minor, int patch)
        {
            if (major < 0 || minor < 0 || patch < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(major), "Version parts cannot be negative.");
            }

            Major = major;
            Minor = minor;
            Patch = patch;
        }

        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }

        public static bool TryParse(string value, out SemanticVersion version)
        {
            version = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var core = value.Split('-')[0];
            var parts = core.Split('.');
            if (parts.Length != 3 ||
                !int.TryParse(parts[0], out var major) ||
                !int.TryParse(parts[1], out var minor) ||
                !int.TryParse(parts[2], out var patch))
            {
                return false;
            }

            version = new SemanticVersion(major, minor, patch);
            return true;
        }

        public bool Equals(SemanticVersion other) =>
            Major == other.Major && Minor == other.Minor && Patch == other.Patch;

        public override bool Equals(object obj) => obj is SemanticVersion other && Equals(other);
        public override int GetHashCode() => (Major, Minor, Patch).GetHashCode();
        public override string ToString() => $"{Major}.{Minor}.{Patch}";
    }
}

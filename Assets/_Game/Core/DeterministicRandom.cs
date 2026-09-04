using System;
using System.Collections.Generic;

namespace Game.Core
{
    public sealed class DeterministicRandom
    {
        private ulong state;

        public DeterministicRandom(ulong seed)
        {
            state = seed;
        }

        public ulong State => state;

        public ulong NextUInt64()
        {
            state += 0x9E3779B97F4A7C15UL;
            var value = state;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }

        public int NextInt(int minimumInclusive, int maximumExclusive)
        {
            if (maximumExclusive <= minimumInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumExclusive), "Maximum must be greater than minimum.");
            }

            var range = (ulong)((long)maximumExclusive - minimumInclusive);
            var threshold = unchecked(0UL - range) % range;
            ulong sample;
            do
            {
                sample = NextUInt64();
            } while (sample < threshold);

            return (int)(minimumInclusive + (long)(sample % range));
        }

        public void Shuffle<T>(IList<T> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            for (var index = items.Count - 1; index > 0; index--)
            {
                var other = NextInt(0, index + 1);
                (items[index], items[other]) = (items[other], items[index]);
            }
        }
    }

    public sealed class NamedRandomStreams
    {
        private readonly ulong rootSeed;
        private readonly Dictionary<string, DeterministicRandom> streams = new Dictionary<string, DeterministicRandom>(StringComparer.Ordinal);

        public NamedRandomStreams(ulong rootSeed)
        {
            this.rootSeed = rootSeed;
        }

        public ulong RootSeed => rootSeed;

        public DeterministicRandom Get(string streamName)
        {
            if (string.IsNullOrWhiteSpace(streamName))
            {
                throw new ArgumentException("A random stream requires a name.", nameof(streamName));
            }

            if (!streams.TryGetValue(streamName, out var stream))
            {
                stream = new DeterministicRandom(DeriveSeed(rootSeed, streamName));
                streams.Add(streamName, stream);
            }

            return stream;
        }

        public IReadOnlyDictionary<string, ulong> CaptureStates()
        {
            var snapshot = new Dictionary<string, ulong>(StringComparer.Ordinal);
            foreach (var pair in streams)
            {
                snapshot.Add(pair.Key, pair.Value.State);
            }

            return snapshot;
        }

        private static ulong DeriveSeed(ulong seed, string name)
        {
            var hash = 14695981039346656037UL ^ seed;
            for (var index = 0; index < name.Length; index++)
            {
                hash ^= name[index];
                hash *= 1099511628211UL;
            }

            return hash;
        }
    }
}

using System;
using System.Globalization;
using System.IO;
using Game.Gameplay;
using UnityEngine;

namespace Game.Editor
{
    public static class BalanceSimulationBatch
    {
        public static void Run()
        {
            var args = Environment.GetCommandLineArgs();
            var samples = ParseInt(args, "-balanceSamples", 1000, 1, 100000);
            var startSeed = ParseUlong(args, "-balanceStartSeed", 1UL);
            var minimumWinRate = ParseInt(args, "-balanceMinimumWinRatePermille", 100, 0, 1000);
            var maximumWinRate = ParseInt(args, "-balanceMaximumWinRatePermille", 900, 0, 1000);
            var maximumStalled = ParseInt(args, "-balanceMaximumStalledRuns", 0, 0, 100000);
            if (minimumWinRate > maximumWinRate) throw new ArgumentException("The minimum win-rate gate cannot exceed the maximum.");
            var output = ReadArgument(args, "-balanceOutput") ?? "Artifacts/balance-report.json";
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? throw new InvalidOperationException("Project root is unavailable.");
            var outputPath = Path.GetFullPath(Path.IsPathRooted(output) ? output : Path.Combine(projectRoot, output));
            var contentPath = Path.Combine(Application.dataPath, "_Game", "Content", "Source", "vertical-slice.json");
            var parsed = ContentCatalogJson.ParseApproved(File.ReadAllText(contentPath));
            if (!parsed.Succeeded)
            {
                throw new InvalidOperationException("Content catalog is invalid: " + string.Join(Environment.NewLine, parsed.Errors));
            }

            var report = RunBalanceSimulator.Simulate(parsed.Catalog, samples, startSeed);
            report.gateMinimumWinRatePermille = minimumWinRate;
            report.gateMaximumWinRatePermille = maximumWinRate;
            report.gateMaximumStalledRuns = maximumStalled;
            report.gatePassed = BalanceSimulationGate.Passes(report, minimumWinRate, maximumWinRate, maximumStalled);
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(outputPath, JsonUtility.ToJson(report, true));
            Debug.Log($"Balance simulation completed: {samples} runs, {report.completedRuns} completed, {report.defeatedRuns} defeated, {report.stalledRuns} stalled. Report: {outputPath}");
            if (HasArgument(args, "-balanceEnforceGate") && !report.gatePassed)
            {
                throw new InvalidOperationException($"Balance gate failed: win rate {report.winRatePermille} permille (expected {minimumWinRate}-{maximumWinRate}), stalled {report.stalledRuns} (maximum {maximumStalled}).");
            }
        }

        private static int ParseInt(string[] args, string name, int fallback, int minimum, int maximum)
        {
            var raw = ReadArgument(args, name);
            if (raw == null) return fallback;
            if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < minimum || value > maximum)
            {
                throw new ArgumentException($"{name} must be between {minimum} and {maximum}.");
            }

            return value;
        }

        private static ulong ParseUlong(string[] args, string name, ulong fallback)
        {
            var raw = ReadArgument(args, name);
            if (raw == null) return fallback;
            if (!ulong.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                throw new ArgumentException($"{name} must be an unsigned integer.");
            }

            return value;
        }

        private static string ReadArgument(string[] args, string name)
        {
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.Ordinal)) return args[index + 1];
            }

            return null;
        }

        private static bool HasArgument(string[] args, string name)
        {
            foreach (var argument in args)
            {
                if (string.Equals(argument, name, StringComparison.Ordinal)) return true;
            }

            return false;
        }
    }
}

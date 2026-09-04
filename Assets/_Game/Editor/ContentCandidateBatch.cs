using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Game.Core;
using Game.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class ContentCandidateBatch
    {
        private const string ContentFile = "content.json";
        private const string EnglishFile = "localization.en.json";
        private const string ChineseFile = "localization.zh-Hans.json";
        private const string ProvenanceFile = "provenance.json";

        public static void Review()
        {
            var args = Environment.GetCommandLineArgs();
            var directory = RequireDirectory(args, "-candidateDirectory");
            var output = GetArgument(args, "-candidateReviewOutput") ?? Path.Combine(directory, "review-report.json");
            var samples = ParseInt(args, "-candidateSamples", 200, 1, 100000);
            var report = ReviewBundle(directory, samples);
            WriteJson(output, report);
            Debug.Log($"Candidate review {(report.passed ? "passed" : "failed")}: {report.candidateId}. Report: {Path.GetFullPath(output)}");
            if (!report.passed) throw new InvalidOperationException("Candidate review failed: " + string.Join("; ", report.errors));
        }

        public static void Promote()
        {
            var args = Environment.GetCommandLineArgs();
            var directory = RequireDirectory(args, "-candidateDirectory");
            var approvalPath = GetArgument(args, "-candidateApproval") ?? Path.Combine(directory, "approval.json");
            var samples = ParseInt(args, "-candidateSamples", 200, 1, 100000);
            var report = ReviewBundle(directory, samples);
            var approval = JsonUtility.FromJson<ContentCandidateApproval>(File.ReadAllText(approvalPath));
            if (!ContentCandidateGovernance.ValidateApproval(report, approval, out var approvalError))
            {
                throw new InvalidOperationException("Candidate promotion rejected: " + approvalError);
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? throw new InvalidOperationException("Project root is unavailable.");
            var activeContentPath = Path.Combine(Application.dataPath, "_Game", "Content", "Source", "vertical-slice.json");
            var activeEnglishPath = Path.Combine(Application.dataPath, "_Game", "Content", "Localization", "vertical-slice.en.json");
            var activeChinesePath = Path.Combine(Application.dataPath, "_Game", "Content", "Localization", "vertical-slice.zh-Hans.json");
            var candidateContent = PromoteStatus(File.ReadAllText(Path.Combine(directory, ContentFile)));
            var candidateEnglish = PromoteStatus(File.ReadAllText(Path.Combine(directory, EnglishFile)));
            var candidateChinese = PromoteStatus(File.ReadAllText(Path.Combine(directory, ChineseFile)));
            ValidateApprovedBundle(candidateContent, candidateEnglish, candidateChinese, samples);

            var targets = new[] { activeContentPath, activeEnglishPath, activeChinesePath };
            var replacements = new[] { candidateContent, candidateEnglish, candidateChinese };
            try
            {
                ReplaceAllForPromotion(targets, replacements, () =>
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    ProjectValidator.ValidateOrThrow();
                });
                Debug.Log($"Promoted candidate {report.candidateId} to {projectRoot}. Human reviewer: {approval.reviewer}.");
            }
            catch
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                throw;
            }
        }

        public static void ReplaceAllForPromotion(IReadOnlyList<string> targets, IReadOnlyList<string> replacements, Action validateAfterWrite)
        {
            if (targets == null || replacements == null || targets.Count == 0 || targets.Count != replacements.Count) throw new ArgumentException("Promotion targets and replacements must have equal non-zero length.");
            var previous = targets.Select(File.ReadAllText).ToArray();
            try
            {
                for (var index = 0; index < targets.Count; index++) AtomicReplace(targets[index], replacements[index]);
                validateAfterWrite?.Invoke();
            }
            catch
            {
                for (var index = 0; index < targets.Count; index++) File.WriteAllText(targets[index], previous[index]);
                throw;
            }
        }

        public static ContentCandidateReviewReport ReviewBundle(string directory, int samples)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return Failed("Candidate directory does not exist.");
            }

            var paths = new[] { ContentFile, EnglishFile, ChineseFile, ProvenanceFile }.Select(name => Path.Combine(directory, name)).ToArray();
            foreach (var path in paths.Where(path => !File.Exists(path))) errors.Add("Required candidate file is missing: " + Path.GetFileName(path));
            if (errors.Count > 0) return Failed(errors.ToArray());

            var contentJson = File.ReadAllText(paths[0]);
            var englishJson = File.ReadAllText(paths[1]);
            var chineseJson = File.ReadAllText(paths[2]);
            var provenanceJson = File.ReadAllText(paths[3]);
            ContentCandidateProvenance provenance = null;
            try
            {
                provenance = JsonUtility.FromJson<ContentCandidateProvenance>(provenanceJson);
                ValidateProvenance(provenance, errors);
            }
            catch (Exception exception)
            {
                errors.Add("Provenance JSON is invalid: " + exception.Message);
            }

            var content = ContentCatalogJson.ParseForReview(contentJson);
            var english = LocalizationCatalogJson.ParseForReview(englishJson);
            var chinese = LocalizationCatalogJson.ParseForReview(chineseJson);
            errors.AddRange(content.Errors);
            errors.AddRange(english.Errors);
            errors.AddRange(chinese.Errors);
            if (content.Succeeded && english.Succeeded && chinese.Succeeded)
            {
                errors.AddRange(LocalizationCatalogJson.ValidateCoverage(content.Catalog, new[] { english.Catalog, chinese.Catalog }, new[] { "en", "zh-Hans" }));
                if (provenance != null && !string.Equals(content.Catalog.ContentVersion, provenance.targetContentVersion, StringComparison.Ordinal))
                {
                    errors.Add("Candidate contentVersion does not match provenance targetContentVersion.");
                }
            }

            var projectContent = Path.Combine(Application.dataPath, "_Game", "Content", "Source", "vertical-slice.json");
            var active = ContentCatalogJson.ParseApproved(File.ReadAllText(projectContent));
            if (!active.Succeeded) errors.Add("Active content catalog is invalid.");
            if (active.Succeeded && provenance != null && !string.Equals(active.Catalog.ContentVersion, provenance.baseContentVersion, StringComparison.Ordinal))
            {
                errors.Add("Candidate baseContentVersion is stale.");
            }

            BalanceSimulationReport balance = null;
            if (errors.Count == 0)
            {
                balance = RunBalanceSimulator.Simulate(content.Catalog, samples, 1UL);
                balance.gateMinimumWinRatePermille = 100;
                balance.gateMaximumWinRatePermille = 900;
                balance.gateMaximumStalledRuns = 0;
                balance.gatePassed = BalanceSimulationGate.Passes(balance, 100, 900, 0);
                if (!balance.gatePassed) errors.Add("Candidate failed the deterministic balance gate.");
            }

            return new ContentCandidateReviewReport
            {
                candidateId = provenance?.candidateId ?? string.Empty,
                baseContentVersion = provenance?.baseContentVersion ?? string.Empty,
                targetContentVersion = provenance?.targetContentVersion ?? string.Empty,
                sourceDigest = ComputeDigest(paths),
                reviewedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                simulationSamples = samples,
                passed = errors.Count == 0,
                errors = errors.ToArray(),
                balance = balance
            };
        }

        private static void ValidateApprovedBundle(string contentJson, string englishJson, string chineseJson, int samples)
        {
            var content = ContentCatalogJson.ParseApproved(contentJson);
            var english = LocalizationCatalogJson.ParseApproved(englishJson);
            var chinese = LocalizationCatalogJson.ParseApproved(chineseJson);
            var errors = content.Errors.Concat(english.Errors).Concat(chinese.Errors).ToList();
            if (content.Succeeded && english.Succeeded && chinese.Succeeded)
            {
                errors.AddRange(LocalizationCatalogJson.ValidateCoverage(content.Catalog, new[] { english.Catalog, chinese.Catalog }, new[] { "en", "zh-Hans" }));
                var balance = RunBalanceSimulator.Simulate(content.Catalog, samples, 1UL);
                if (!BalanceSimulationGate.Passes(balance, 100, 900, 0)) errors.Add("Promoted bundle failed the deterministic balance gate.");
            }

            if (errors.Count > 0) throw new InvalidOperationException("Approved candidate bundle is invalid: " + string.Join("; ", errors));
        }

        private static void ValidateProvenance(ContentCandidateProvenance value, ICollection<string> errors)
        {
            if (value == null || value.schemaVersion != 1) errors.Add("Provenance schemaVersion must be 1.");
            if (value == null) return;
            if (!ContentId.TryParse(value.candidateId, out _)) errors.Add("candidateId must be a stable content ID.");
            if (string.IsNullOrWhiteSpace(value.baseContentVersion) || string.IsNullOrWhiteSpace(value.targetContentVersion) || value.baseContentVersion == value.targetContentVersion) errors.Add("Provenance requires distinct base and target content versions.");
            if (string.IsNullOrWhiteSpace(value.generator) || string.IsNullOrWhiteSpace(value.model) || string.IsNullOrWhiteSpace(value.source) || string.IsNullOrWhiteSpace(value.license) || string.IsNullOrWhiteSpace(value.owner)) errors.Add("Provenance generator, model, source, license, and owner are required.");
            if (!DateTimeOffset.TryParse(value.generatedAtUtc, out var generated) || generated.Offset != TimeSpan.Zero) errors.Add("generatedAtUtc must be a UTC timestamp.");
            if (string.IsNullOrEmpty(value.promptDigest) || value.promptDigest.Length != 64 || value.promptDigest.Any(character => !Uri.IsHexDigit(character))) errors.Add("promptDigest must be a SHA-256 hex digest, never raw prompt text.");
        }

        private static string PromoteStatus(string json)
        {
            var regex = new Regex("\\\"status\\\"\\s*:\\s*\\\"(?:draft|review)\\\"", RegexOptions.CultureInvariant);
            var result = regex.Replace(json, "\"status\": \"approved\"", 1);
            if (ReferenceEquals(result, json) || result == json) throw new InvalidOperationException("Candidate file has no draft/review status to promote.");
            return result;
        }

        private static string ComputeDigest(IEnumerable<string> paths)
        {
            using var stream = new MemoryStream();
            foreach (var path in paths.OrderBy(Path.GetFileName, StringComparer.Ordinal))
            {
                var name = Encoding.UTF8.GetBytes(Path.GetFileName(path));
                stream.Write(name, 0, name.Length);
                stream.WriteByte(0);
                var bytes = File.ReadAllBytes(path);
                stream.Write(bytes, 0, bytes.Length);
                stream.WriteByte(0);
            }

            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(stream.ToArray()).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static void AtomicReplace(string target, string contents)
        {
            var temporary = target + ".candidate-" + Guid.NewGuid().ToString("N") + ".tmp";
            var backup = target + ".candidate-backup";
            File.WriteAllText(temporary, contents);
            if (File.Exists(backup)) File.Delete(backup);
            File.Replace(temporary, target, backup);
            if (File.Exists(backup)) File.Delete(backup);
        }

        private static ContentCandidateReviewReport Failed(params string[] errors) => new ContentCandidateReviewReport { passed = false, errors = errors, reviewedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture) };
        private static void WriteJson(string path, object value) { var full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full) ?? "."); File.WriteAllText(full, JsonUtility.ToJson(value, true)); }
        private static string RequireDirectory(string[] args, string name) { var value = GetArgument(args, name); if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(name + " is required."); return Path.GetFullPath(value); }
        private static string GetArgument(string[] args, string name) { for (var index = 0; index < args.Length - 1; index++) if (args[index] == name) return args[index + 1]; return null; }
        private static int ParseInt(string[] args, string name, int fallback, int minimum, int maximum) { var raw = GetArgument(args, name); if (raw == null) return fallback; if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < minimum || value > maximum) throw new ArgumentException(name + " is invalid."); return value; }
    }
}

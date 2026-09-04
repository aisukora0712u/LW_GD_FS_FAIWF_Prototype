using System;
using System.IO;
using Game.Editor;
using Game.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class ContentCandidateGovernanceTests
    {
        [Test]
        public void ReviewBundle_IsIsolatedDeterministicAndApprovalIsDigestBound()
        {
            var directory = Path.Combine(Path.GetTempPath(), "content-candidate-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var candidateVersion = "candidate.governance.1";
                var content = ReadProject("Assets", "_Game", "Content", "Source", "vertical-slice.json")
                    .Replace("\"status\": \"approved\"", "\"status\": \"draft\"")
                    .Replace("vertical-slice.7", candidateVersion);
                var english = ReadProject("Assets", "_Game", "Content", "Localization", "vertical-slice.en.json")
                    .Replace("\"status\": \"approved\"", "\"status\": \"draft\"")
                    .Replace("vertical-slice.7", candidateVersion);
                var chinese = ReadProject("Assets", "_Game", "Content", "Localization", "vertical-slice.zh-Hans.json")
                    .Replace("\"status\": \"approved\"", "\"status\": \"draft\"")
                    .Replace("vertical-slice.7", candidateVersion);
                var provenance = "{\"schemaVersion\":1,\"candidateId\":\"candidate.governance_001\",\"baseContentVersion\":\"vertical-slice.7\",\"targetContentVersion\":\"candidate.governance.1\",\"generator\":\"test-fixture\",\"model\":\"deterministic\",\"generatedAtUtc\":\"2026-09-02T00:00:00Z\",\"promptDigest\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"source\":\"original\",\"license\":\"internal-test\",\"owner\":\"qa\"}";
                File.WriteAllText(Path.Combine(directory, "content.json"), content);
                File.WriteAllText(Path.Combine(directory, "localization.en.json"), english);
                File.WriteAllText(Path.Combine(directory, "localization.zh-Hans.json"), chinese);
                File.WriteAllText(Path.Combine(directory, "provenance.json"), provenance);

                Assert.That(ContentCatalogJson.ParseApproved(content).Succeeded, Is.False);
                Assert.That(ContentCatalogJson.ParseForReview(content).Succeeded, Is.True);
                var first = ContentCandidateBatch.ReviewBundle(directory, 20);
                var second = ContentCandidateBatch.ReviewBundle(directory, 20);
                Assert.That(first.passed, Is.True, string.Join(Environment.NewLine, first.errors));
                Assert.That(second.sourceDigest, Is.EqualTo(first.sourceDigest));
                Assert.That(second.balance.completedRuns, Is.EqualTo(first.balance.completedRuns));

                var approval = new ContentCandidateApproval
                {
                    schemaVersion = 1,
                    candidateId = first.candidateId,
                    sourceDigest = first.sourceDigest,
                    decision = "approved",
                    reviewer = "human.qa",
                    approvedAtUtc = "2026-09-02T01:00:00Z",
                    notes = "fixture"
                };
                Assert.That(ContentCandidateGovernance.ValidateApproval(first, approval, out var error), Is.True, error);
                approval.sourceDigest = new string('0', 64);
                Assert.That(ContentCandidateGovernance.ValidateApproval(first, approval, out _), Is.False);

                File.AppendAllText(Path.Combine(directory, "provenance.json"), " ");
                var modified = ContentCandidateBatch.ReviewBundle(directory, 20);
                Assert.That(modified.sourceDigest, Is.Not.EqualTo(first.sourceDigest));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void Approval_RejectsNonHumanOrPendingDecision()
        {
            var report = new ContentCandidateReviewReport { passed = true, candidateId = "candidate.test", sourceDigest = new string('a', 64) };
            var approval = new ContentCandidateApproval
            {
                schemaVersion = 1,
                candidateId = report.candidateId,
                sourceDigest = report.sourceDigest,
                decision = "pending",
                reviewer = string.Empty,
                approvedAtUtc = "2026-09-02T00:00:00+09:00"
            };

            Assert.That(ContentCandidateGovernance.ValidateApproval(report, approval, out var error), Is.False);
            Assert.That(error, Does.Contain("human approval"));
        }

        [Test]
        public void AtomicPromotion_RestoresEverySourceWhenPostWriteValidationFails()
        {
            var directory = Path.Combine(Path.GetTempPath(), "content-promotion-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var paths = new[] { Path.Combine(directory, "content.json"), Path.Combine(directory, "en.json"), Path.Combine(directory, "zh.json") };
                for (var index = 0; index < paths.Length; index++) File.WriteAllText(paths[index], "old-" + index);

                Assert.Throws<InvalidOperationException>(() => ContentCandidateBatch.ReplaceAllForPromotion(
                    paths,
                    new[] { "new-0", "new-1", "new-2" },
                    () => throw new InvalidOperationException("synthetic validation failure")));

                for (var index = 0; index < paths.Length; index++) Assert.That(File.ReadAllText(paths[index]), Is.EqualTo("old-" + index));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static string ReadProject(params string[] parts)
        {
            var root = Directory.GetParent(Application.dataPath)?.FullName ?? throw new InvalidOperationException();
            var path = root;
            foreach (var part in parts) path = Path.Combine(path, part);
            return File.ReadAllText(path);
        }
    }
}

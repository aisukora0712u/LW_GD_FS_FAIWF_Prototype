using System;

namespace Game.Gameplay
{
    [Serializable]
    public sealed class ContentCandidateProvenance
    {
        public int schemaVersion;
        public string candidateId;
        public string baseContentVersion;
        public string targetContentVersion;
        public string generator;
        public string model;
        public string generatedAtUtc;
        public string promptDigest;
        public string source;
        public string license;
        public string owner;
    }

    [Serializable]
    public sealed class ContentCandidateReviewReport
    {
        public int schemaVersion = 1;
        public string candidateId;
        public string baseContentVersion;
        public string targetContentVersion;
        public string sourceDigest;
        public string reviewedAtUtc;
        public int simulationSamples;
        public bool passed;
        public string[] errors;
        public BalanceSimulationReport balance;
    }

    [Serializable]
    public sealed class ContentCandidateApproval
    {
        public int schemaVersion;
        public string candidateId;
        public string sourceDigest;
        public string decision;
        public string reviewer;
        public string approvedAtUtc;
        public string notes;
    }

    public static class ContentCandidateGovernance
    {
        public static bool ValidateApproval(ContentCandidateReviewReport report, ContentCandidateApproval approval, out string error)
        {
            error = string.Empty;
            if (report == null || approval == null)
            {
                error = "Review report and approval are required.";
                return false;
            }

            if (!report.passed)
            {
                error = "The candidate review did not pass.";
                return false;
            }

            if (approval.schemaVersion != 1 || !string.Equals(approval.decision, "approved", StringComparison.Ordinal))
            {
                error = "A schema v1 human approval with decision 'approved' is required.";
                return false;
            }

            if (!string.Equals(report.candidateId, approval.candidateId, StringComparison.Ordinal) ||
                !string.Equals(report.sourceDigest, approval.sourceDigest, StringComparison.OrdinalIgnoreCase))
            {
                error = "Approval identity or source digest does not match the reviewed candidate.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(approval.reviewer) ||
                !DateTimeOffset.TryParse(approval.approvedAtUtc, out var approvedAt) ||
                approvedAt.Offset != TimeSpan.Zero)
            {
                error = "Approval requires a reviewer and UTC approval timestamp.";
                return false;
            }

            return true;
        }
    }
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace TuringSignalRelay.Evaluation;

public sealed class SubmissionReviewService(ContestDb db, ContestPaths paths)
{
    public async Task ReviewAsync(string submissionId, string judgeId, string decision, string reason, CancellationToken cancellation)
    {
        if (decision is not ("approved" or "disqualified") || string.IsNullOrWhiteSpace(reason) || reason.Length > 2000)
            throw new ArgumentException("请选择复核结论并填写1—2000字的理由。");
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellation);
        var submission = await db.Submissions.SingleOrDefaultAsync(x => x.Id == submissionId, cancellation)
            ?? throw new ArgumentException("提交不存在。");
        byte[] source = await File.ReadAllBytesAsync(paths.File("submissions", submission.Id, "ai.cpp"), cancellation);
        if (ContestJson.Hash(source) != submission.SourceHash) throw new InvalidDataException("Source digest mismatch.");
        var scan = SourceScanner.Scan(source);
        db.SubmissionReviews.Add(new SubmissionReview { SubmissionId = submission.Id, SourceHash = submission.SourceHash,
            JudgeId = judgeId, Decision = decision, Reason = reason.Trim(), FindingsJson = JsonSerializer.Serialize(scan, ContestJson.Options) });
        if (decision == "disqualified")
        {
            var team = await db.Teams.SingleAsync(x => x.Id == submission.TeamId, cancellation);
            team.Disqualified = true;
            await db.Matches.Where(x => x.TeamAId == team.Id || x.TeamBId == team.Id).ExecuteUpdateAsync(update => update
                .SetProperty(x => x.Status, "disqualified").SetProperty(x => x.Diagnostic, "裁判已取消关联队伍参赛资格，原因见复核记录。"), cancellation);
        }
        await db.SaveChangesAsync(cancellation);
        await transaction.CommitAsync(cancellation);
    }
}

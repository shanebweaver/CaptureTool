using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Tests.Analysis.Policy;

[TestClass]
public sealed class CaptureAnalysisEligibilityTests
{
    [TestMethod]
    [DataRow(CaptureAnalysisEnrollmentState.Enrolled, CaptureAnalysisExclusionReason.None, true, false)]
    [DataRow(CaptureAnalysisEnrollmentState.Excluded, CaptureAnalysisExclusionReason.MemoryCleared, true, true)]
    [DataRow(CaptureAnalysisEnrollmentState.Excluded, CaptureAnalysisExclusionReason.UserExcluded, false, false)]
    [DataRow(CaptureAnalysisEnrollmentState.Excluded, CaptureAnalysisExclusionReason.PrivateCapture, false, false)]
    [DataRow(CaptureAnalysisEnrollmentState.Excluded, CaptureAnalysisExclusionReason.MissingSource, false, false)]
    [DataRow(CaptureAnalysisEnrollmentState.Forgotten, CaptureAnalysisExclusionReason.HistoryForgotten, false, false)]
    [DataRow(CaptureAnalysisEnrollmentState.Forgotten, CaptureAnalysisExclusionReason.DeleteRequested, false, false)]
    public void ReanalysisEligibility_ShouldAgreeWithStatusCountsAndPreserveExclusions(
        CaptureAnalysisEnrollmentState state, CaptureAnalysisExclusionReason reason, bool eligible, bool cleared)
    {
        var recipe = CaptureAnalysisRecipeDefaults.CreateCaptureMemoryImageRecipe();
        var enrollment = new CaptureAnalysisEnrollment(CaptureId.New(), state, reason, 2, 1, 1,
            state == CaptureAnalysisEnrollmentState.Enrolled ? recipe.Id : null,
            state == CaptureAnalysisEnrollmentState.Enrolled ? recipe.Version : null);
        var policy = CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(
            CaptureAnalysisPolicyDefaults.CreateAuthorizationScope(), 1);
        var snapshot = new CaptureAnalysisPolicySnapshot(CaptureAnalysisPolicySnapshotStatus.Available,
            CaptureAnalysisConsentState.Granted,
            new CaptureAnalysisControlSnapshot(1, new CaptureAnalysisControlState(policy, [enrollment])));

        Assert.AreEqual(eligible, enrollment.CanReanalyze);
        Assert.AreEqual(cleared, enrollment.IsMemoryCleared);
        Assert.AreEqual(eligible ? 1 : 0, snapshot.ReanalyzableCaptureCount);
        Assert.AreEqual(eligible ? 0 : 1, snapshot.ExcludedCaptureCount);
        Assert.AreEqual(state == CaptureAnalysisEnrollmentState.Enrolled ? 1 : 0, snapshot.ActiveCaptureCount);
    }
}

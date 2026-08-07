using CaptureTool.Application.Abstractions.Analysis.Consent;
using CaptureTool.Application.Abstractions.Analysis.Maintenance;
using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Application.Abstractions.Analysis.Privacy;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Tests.Analysis.Domain;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using System.Reflection;

#pragma warning disable IL2026 // Contract tests intentionally inspect untrimmed test assemblies.
#pragma warning disable IL2070 // Contract tests intentionally inspect untrimmed public metadata.
#pragma warning disable IL2075 // Contract tests intentionally inspect untrimmed public metadata.

namespace CaptureTool.Application.Tests.Analysis.Contracts;

[TestClass]
public sealed class CaptureAnalysisConsentAndMaintenanceContractTests
{
    [TestMethod]
    public void PreparationPorts_ShouldSeparateReadinessFromUserInitiatedMutation()
    {
        CollectionAssert.AreEqual(
            new[] { "GetStateAsync" },
            GetMethodNames(typeof(IAnalysisCapabilityPreparationQueryService)));
        CollectionAssert.AreEqual(
            new[] { "PrepareAsync" },
            GetMethodNames(typeof(IUserInitiatedAnalysisCapabilityPreparationService)));

        Type[] compositePorts = typeof(IAnalysisCapabilityPreparationQueryService).Assembly
            .GetExportedTypes()
            .Where(type =>
                type.IsInterface &&
                type.Namespace == typeof(IAnalysisCapabilityPreparationQueryService).Namespace)
            .Where(type =>
            {
                string[] methods = GetMethodNames(type);
                return methods.Contains("GetStateAsync", StringComparer.Ordinal) &&
                    methods.Contains("PrepareAsync", StringComparer.Ordinal);
            })
            .ToArray();

        Assert.IsEmpty(
            compositePorts,
            "No worker-facing preparation port may both inspect readiness and initiate model preparation.");
    }

    [TestMethod]
    public void ConsentDisclosure_ShouldBindPurposePolicyAndDefensivelyCopyCapabilities()
    {
        var capabilities = new List<CapabilityDefinition>
        {
            AnalysisCapabilities.OcrDocumentV1,
            AnalysisCapabilities.ImageDescriptionV1,
        };
        AnalysisProcessingPolicy policy = AnalysisProcessingPolicy.LocalOnly(AnalysisTestData.Purpose);

        var disclosure = new CaptureAnalysisConsentDisclosure(
            AnalysisTestData.Purpose,
            policy,
            capabilities);
        capabilities.Clear();

        Assert.AreEqual(AnalysisTestData.Purpose, disclosure.Purpose);
        Assert.AreSame(policy, disclosure.ProcessingPolicy);
        Assert.HasCount(2, disclosure.Capabilities);
        var response = new CaptureAnalysisConsentResponse(
            disclosure,
            CaptureAnalysisConsentDecision.GrantedForFutureCaptures);
        Assert.AreSame(disclosure, response.Disclosure);
        Assert.AreEqual(CaptureAnalysisConsentDecision.GrantedForFutureCaptures, response.Decision);
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IList<CapabilityDefinition>)disclosure.Capabilities).Add(
                AnalysisCapabilities.MediaPropertiesV1));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisConsentDisclosure(
            default,
            policy,
            [AnalysisCapabilities.OcrDocumentV1]));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisConsentDisclosure(
            AnalysisTestData.Purpose,
            AnalysisProcessingPolicy.LocalOnly(new AnalysisPurpose("another-purpose", 1)),
            [AnalysisCapabilities.OcrDocumentV1]));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisConsentDisclosure(
            AnalysisTestData.Purpose,
            policy,
            []));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisConsentDisclosure(
            AnalysisTestData.Purpose,
            policy,
            [AnalysisCapabilities.OcrDocumentV1, AnalysisCapabilities.OcrDocumentV1]));
        Assert.ThrowsExactly<ArgumentNullException>(() => new CaptureAnalysisConsentDisclosure(
            AnalysisTestData.Purpose,
            null!,
            [AnalysisCapabilities.OcrDocumentV1]));
        Assert.ThrowsExactly<ArgumentNullException>(() => new CaptureAnalysisConsentDisclosure(
            AnalysisTestData.Purpose,
            policy,
            null!));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisConsentDisclosure(
            AnalysisTestData.Purpose,
            policy,
            [default(CapabilityDefinition)]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new CaptureAnalysisConsentResponse(
                disclosure,
                CaptureAnalysisConsentDecision.Unknown));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new CaptureAnalysisConsentResponse(
                null!,
                CaptureAnalysisConsentDecision.Cancelled));
    }

    [TestMethod]
    public void ConsentAndSettingsActions_ShouldKeepBackfillSeparateFromFutureConsent()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                nameof(CaptureAnalysisConsentDecision.Unknown),
                nameof(CaptureAnalysisConsentDecision.Cancelled),
                nameof(CaptureAnalysisConsentDecision.Declined),
                nameof(CaptureAnalysisConsentDecision.GrantedForFutureCaptures),
            },
            Enum.GetNames<CaptureAnalysisConsentDecision>());
        CollectionAssert.AreEqual(
            new[]
            {
                nameof(CaptureAnalysisSettingsAction.Unknown),
                nameof(CaptureAnalysisSettingsAction.AuthorizeExistingCaptureBackfill),
                nameof(CaptureAnalysisSettingsAction.StopAnalyzingNewCaptures),
                nameof(CaptureAnalysisSettingsAction.TurnOffAndErase),
                nameof(CaptureAnalysisSettingsAction.ClearMemory),
                nameof(CaptureAnalysisSettingsAction.RebuildSearchIndex),
                nameof(CaptureAnalysisSettingsAction.ReanalyzeCaptures),
                nameof(CaptureAnalysisSettingsAction.RemoveFromMemory),
                nameof(CaptureAnalysisSettingsAction.DeleteCapture),
            },
            Enum.GetNames<CaptureAnalysisSettingsAction>());

        Assert.IsFalse(
            Enum.GetNames<CaptureAnalysisConsentDecision>().Any(name =>
                name.Contains("Backfill", StringComparison.Ordinal)),
            "Initial consent must not smuggle in existing-capture backfill.");
        Assert.AreEqual(
            nameof(CaptureAnalysisSettingsAction.AuthorizeExistingCaptureBackfill),
            CaptureAnalysisSettingsAction.AuthorizeExistingCaptureBackfill.ToString());
        Assert.AreEqual(
            CaptureAnalysisSettingsAction.TurnOffAndErase,
            new CaptureAnalysisSettingsConfirmationRequest(
                CaptureAnalysisSettingsAction.TurnOffAndErase).Action);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new CaptureAnalysisSettingsConfirmationRequest(CaptureAnalysisSettingsAction.Unknown));
    }

    [TestMethod]
    public void MaintenancePort_ShouldKeepDestructiveAndProjectionCommandsDistinct()
    {
        CollectionAssert.AreEqual(
            new[] { "ClearMemoryAsync", "ReanalyzeCapturesAsync", "RebuildSearchIndexAsync" },
            GetMethodNames(typeof(ICaptureAnalysisMaintenanceService)));

        var succeeded = new CaptureAnalysisMaintenanceResult(
            CaptureAnalysisMaintenanceStatus.Succeeded,
            3);
        var incomplete = new CaptureAnalysisMaintenanceResult(
            CaptureAnalysisMaintenanceStatus.Incomplete,
            2);
        var progress = new CaptureAnalysisMaintenanceProgress(
            CaptureAnalysisMaintenancePhase.PreparingModels,
            0.5);

        Assert.AreEqual(3, succeeded.AffectedCaptureCount);
        Assert.AreEqual(CaptureAnalysisMaintenanceStatus.Succeeded, succeeded.Status);
        Assert.AreEqual(2, incomplete.AffectedCaptureCount);
        Assert.AreEqual(CaptureAnalysisMaintenancePhase.PreparingModels, progress.Phase);
        Assert.AreEqual(0.5, progress.FractionComplete);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new CaptureAnalysisMaintenanceProgress(
                CaptureAnalysisMaintenancePhase.Unknown,
                0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new CaptureAnalysisMaintenanceProgress(
                CaptureAnalysisMaintenancePhase.SchedulingCaptures,
                1.1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new CaptureAnalysisMaintenanceResult(CaptureAnalysisMaintenanceStatus.Unknown));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new CaptureAnalysisMaintenanceResult(CaptureAnalysisMaintenanceStatus.Succeeded, -1));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new CaptureAnalysisMaintenanceResult(CaptureAnalysisMaintenanceStatus.Rejected, 1));
    }

    [TestMethod]
    public void ProjectionMaintenancePort_ShouldRemainMetadataOnly()
    {
        CollectionAssert.AreEqual(
            new[] { "ClearAsync", "RebuildAsync", "RemoveAsync" },
            GetMethodNames(typeof(ICaptureAnalysisProjectionMaintenance)));
        Assert.IsFalse(typeof(ICaptureAnalysisProjectionMaintenance)
            .GetMethods()
            .SelectMany(method => method.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(string)));
    }

    [TestMethod]
    public void CaptureRemovalPort_ShouldRequireIdentityConfirmationAndNeverAcceptAPath()
    {
        var forget = new CaptureAssetRemovalRequest(
            AnalysisTestData.CaptureId,
            CaptureAssetRemovalKind.ForgetHistory);
        var delete = new CaptureAssetRemovalRequest(
            AnalysisTestData.CaptureId,
            CaptureAssetRemovalKind.DeleteRetainedSource,
            isConfirmed: true);

        Assert.IsFalse(forget.IsConfirmed);
        Assert.IsTrue(delete.IsConfirmed);
        Assert.AreEqual(CaptureAssetRemovalKind.DeleteRetainedSource, delete.Kind);
        Assert.IsFalse(typeof(CaptureAssetRemovalRequest).GetProperties().Any(property =>
            property.PropertyType == typeof(string)));
        CollectionAssert.AreEqual(
            new[] { "RemoveAsync" },
            GetMethodNames(typeof(ICaptureAssetRemovalService)));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAssetRemovalRequest(
            default,
            CaptureAssetRemovalKind.ForgetHistory));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CaptureAssetRemovalRequest(
            AnalysisTestData.CaptureId,
            CaptureAssetRemovalKind.Unknown));
    }

    [TestMethod]
    public void ReanalysisRequest_ShouldUseOnlyBoundedCaptureIdentities()
    {
        var mutableIds = new List<CaptureId> { AnalysisTestData.CaptureId };
        var selected = new CaptureAnalysisReanalysisRequest(
            CaptureAnalysisReanalysisScope.SelectedCaptures,
            mutableIds);
        mutableIds.Clear();
        var all = new CaptureAnalysisReanalysisRequest(
            CaptureAnalysisReanalysisScope.AllEnrolledCaptures);

        Assert.HasCount(1, selected.CaptureIds);
        Assert.AreEqual(CaptureAnalysisReanalysisScope.SelectedCaptures, selected.Scope);
        Assert.IsEmpty(all.CaptureIds);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new CaptureAnalysisReanalysisRequest(CaptureAnalysisReanalysisScope.Unknown));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisReanalysisRequest(
            CaptureAnalysisReanalysisScope.SelectedCaptures));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisReanalysisRequest(
            CaptureAnalysisReanalysisScope.AllEnrolledCaptures,
            [AnalysisTestData.CaptureId]));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisReanalysisRequest(
            CaptureAnalysisReanalysisScope.SelectedCaptures,
            [AnalysisTestData.CaptureId, AnalysisTestData.CaptureId]));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisReanalysisRequest(
            CaptureAnalysisReanalysisScope.SelectedCaptures,
            [default(CaptureId)]));
    }

    [TestMethod]
    public void ExclusionContract_ShouldDistinguishUserExclusionFromPrivateCapture()
    {
        var userRequest = new CaptureAnalysisExclusionRequest(
            AnalysisTestData.CaptureId,
            CaptureAnalysisExclusionKind.UserExcluded);
        var privateRequest = new CaptureAnalysisExclusionRequest(
            AnalysisTestData.CaptureId,
            CaptureAnalysisExclusionKind.PrivateCapture);
        var result = new CaptureAnalysisExclusionResult(
            CaptureAnalysisExclusionStatus.Succeeded,
            privateRequest);

        Assert.AreEqual(CaptureAnalysisExclusionKind.UserExcluded, userRequest.Kind);
        Assert.AreEqual(CaptureAnalysisExclusionKind.PrivateCapture, privateRequest.Kind);
        Assert.AreSame(privateRequest, result.Request);
        Assert.AreEqual(CaptureAnalysisExclusionStatus.Succeeded, result.Status);
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisExclusionRequest(
            default,
            CaptureAnalysisExclusionKind.UserExcluded));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CaptureAnalysisExclusionRequest(
            AnalysisTestData.CaptureId,
            CaptureAnalysisExclusionKind.Unknown));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CaptureAnalysisExclusionResult(
            CaptureAnalysisExclusionStatus.Unknown,
            privateRequest));
        Assert.ThrowsExactly<ArgumentNullException>(() => new CaptureAnalysisExclusionResult(
            CaptureAnalysisExclusionStatus.Rejected,
            null!));
    }

    [TestMethod]
    public void NewConsentAndMaintenanceContracts_ShouldBePathFreeAndProviderNeutral()
    {
        Type[] contractTypes = typeof(ICaptureAnalysisConsentDialogService).Assembly
            .GetExportedTypes()
            .Where(type => type.Namespace is
                "CaptureTool.Application.Abstractions.Analysis.Consent" or
                "CaptureTool.Application.Abstractions.Analysis.Maintenance" or
                "CaptureTool.Application.Abstractions.Analysis.Privacy")
            .ToArray();

        Assert.IsNotEmpty(contractTypes);
        foreach (Type type in contractTypes)
        {
            Assert.IsFalse(
                type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Any(member =>
                        member.Name.Contains("Path", StringComparison.OrdinalIgnoreCase) ||
                        member.Name.Contains("File", StringComparison.OrdinalIgnoreCase)),
                $"{type.FullName} exposes a path/file-shaped member.");

            Type[] signatureTypes = type.GetMethods()
                .SelectMany(method =>
                    method.GetParameters().Select(parameter => parameter.ParameterType)
                        .Append(method.ReturnType))
                .Concat(type.GetProperties().Select(property => property.PropertyType))
                .ToArray();
            Assert.IsFalse(
                signatureTypes.Any(signatureType =>
                    (signatureType.Namespace?.StartsWith("Windows", StringComparison.Ordinal) ?? false) ||
                    (signatureType.Namespace?.Contains("Provider", StringComparison.Ordinal) ?? false)),
                $"{type.FullName} exposes a Windows/provider-specific signature.");
        }
    }

    private static string[] GetMethodNames(Type interfaceType)
    {
        return interfaceType
            .GetMethods()
            .Concat(interfaceType.GetInterfaces().SelectMany(inherited => inherited.GetMethods()))
            .Select(method => method.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}

#pragma warning restore IL2075
#pragma warning restore IL2070
#pragma warning restore IL2026

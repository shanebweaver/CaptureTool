#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Core/ResultTypes.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;

namespace CaptureInteropTests
{
    TEST_CLASS(V2CoreResultDiagnosticsTests)
    {
    public:
        TEST_METHOD(CoreResultCode_Values_AreStable)
        {
            Assert::AreEqual(0u, static_cast<uint32_t>(CoreResultCode::Success));
            Assert::AreEqual(1u, static_cast<uint32_t>(CoreResultCode::ValidationFailure));
            Assert::AreEqual(2u, static_cast<uint32_t>(CoreResultCode::InvalidState));
            Assert::AreEqual(3u, static_cast<uint32_t>(CoreResultCode::UnsupportedOperation));
            Assert::AreEqual(4u, static_cast<uint32_t>(CoreResultCode::NotFound));
            Assert::AreEqual(5u, static_cast<uint32_t>(CoreResultCode::NativeFailure));
            Assert::AreEqual(6u, static_cast<uint32_t>(CoreResultCode::RangeError));
        }

        TEST_METHOD(CoreDiagnostic_Error_CapturesStructuredFields)
        {
            const CoreDiagnostic diagnostic = CoreDiagnostic::Error(
                CoreResultCode::NativeFailure,
                "MediaFoundationFileSink",
                "Finalize",
                "Finalize failed",
                -2147467259LL);

            Assert::IsTrue(diagnostic.IsError());
            Assert::IsFalse(diagnostic.IsWarning());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::NativeFailure),
                static_cast<uint32_t>(diagnostic.code));
            Assert::AreEqual("MediaFoundationFileSink", diagnostic.component.c_str());
            Assert::AreEqual("Finalize", diagnostic.operation.c_str());
            Assert::AreEqual("Finalize failed", diagnostic.message.c_str());
            Assert::IsTrue(diagnostic.nativeStatus.has_value());
            Assert::AreEqual(-2147467259LL, diagnostic.nativeStatus.value());
        }

        TEST_METHOD(CoreDiagnostic_Warning_CapturesStructuredFields)
        {
            const CoreDiagnostic diagnostic = CoreDiagnostic::Warning(
                CoreResultCode::UnsupportedOperation,
                "OutputProfileResolver",
                "Resolve",
                "Video source was pruned");

            Assert::IsFalse(diagnostic.IsError());
            Assert::IsTrue(diagnostic.IsWarning());
            Assert::AreEqual(
                static_cast<uint32_t>(DiagnosticSeverity::Warning),
                static_cast<uint32_t>(diagnostic.severity));
            Assert::IsFalse(diagnostic.nativeStatus.has_value());
        }

        TEST_METHOD(OperationResult_Success_HasNoDiagnostic)
        {
            const OperationResult result = OperationResult::Success();

            Assert::IsTrue(result.IsSuccess());
            Assert::IsFalse(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::Success),
                static_cast<uint32_t>(result.code));
            Assert::IsFalse(result.diagnostic.has_value());
        }

        TEST_METHOD(OperationResult_Failure_CarriesSingleDiagnostic)
        {
            const OperationResult result = OperationResult::Failure(
                CoreResultCode::InvalidState,
                "CapturePipelineSession",
                "Pause",
                "Cannot pause before recording");

            Assert::IsFalse(result.IsSuccess());
            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::InvalidState),
                static_cast<uint32_t>(result.code));
            Assert::IsTrue(result.diagnostic.has_value());
            Assert::AreEqual("CapturePipelineSession", result.diagnostic->component.c_str());
            Assert::AreEqual("Pause", result.diagnostic->operation.c_str());
        }

        TEST_METHOD(ValidationResult_Default_IsValid)
        {
            const ValidationResult result;

            Assert::IsTrue(result.IsValid());
            Assert::IsFalse(result.HasWarnings());
            Assert::AreEqual(static_cast<size_t>(0), result.errors.size());
            Assert::AreEqual(static_cast<size_t>(0), result.warnings.size());
        }

        TEST_METHOD(ValidationResult_AddWarning_DoesNotInvalidate)
        {
            ValidationResult result;

            result.AddWarning(
                CoreResultCode::UnsupportedOperation,
                "OutputProfileResolver",
                "Resolve",
                "Pruned incidental video stream");

            Assert::IsTrue(result.IsValid());
            Assert::IsTrue(result.HasWarnings());
            Assert::AreEqual(static_cast<size_t>(0), result.errors.size());
            Assert::AreEqual(static_cast<size_t>(1), result.warnings.size());
            Assert::IsTrue(result.warnings[0].IsWarning());
        }

        TEST_METHOD(ValidationResult_AddErrors_InvalidatesAndAggregates)
        {
            ValidationResult result;

            result.AddError(
                CoreResultCode::ValidationFailure,
                "CapturePipelineConfigValidator",
                "Validate",
                "Output path is required");
            result.AddError(
                CoreResultCode::RangeError,
                "CapturePipelineConfigValidator",
                "Validate",
                "Audio gain is outside the supported range");

            Assert::IsFalse(result.IsValid());
            Assert::AreEqual(static_cast<size_t>(2), result.errors.size());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::ValidationFailure),
                static_cast<uint32_t>(result.errors[0].code));
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::RangeError),
                static_cast<uint32_t>(result.errors[1].code));
        }

        TEST_METHOD(ValidationResult_Merge_PreservesErrorsAndWarnings)
        {
            ValidationResult first;
            first.AddError(
                CoreResultCode::ValidationFailure,
                "Validator",
                "Validate",
                "First error");

            ValidationResult second;
            second.AddWarning(
                CoreResultCode::UnsupportedOperation,
                "Resolver",
                "Resolve",
                "Warning");
            second.AddError(
                CoreResultCode::NotFound,
                "Resolver",
                "Resolve",
                "Missing source");

            first.Merge(std::move(second));

            Assert::IsFalse(first.IsValid());
            Assert::IsTrue(first.HasWarnings());
            Assert::AreEqual(static_cast<size_t>(2), first.errors.size());
            Assert::AreEqual(static_cast<size_t>(1), first.warnings.size());
            Assert::AreEqual("Missing source", first.errors[1].message.c_str());
        }

        TEST_METHOD(ValidationResult_ToOperationResult_UsesFirstError)
        {
            ValidationResult result;
            result.AddError(
                CoreResultCode::RangeError,
                "Validator",
                "Validate",
                "Out of range");

            const OperationResult operationResult = result.ToOperationResult();

            Assert::IsTrue(operationResult.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::RangeError),
                static_cast<uint32_t>(operationResult.code));
            Assert::AreEqual("Out of range", operationResult.diagnostic->message.c_str());
        }

        TEST_METHOD(ValidationResult_ToOperationResult_SucceedsWhenOnlyWarningsExist)
        {
            ValidationResult result;
            result.AddWarning(
                CoreResultCode::UnsupportedOperation,
                "Resolver",
                "Resolve",
                "Pruned incidental stream");

            const OperationResult operationResult = result.ToOperationResult();

            Assert::IsTrue(operationResult.IsSuccess());
            Assert::IsFalse(operationResult.diagnostic.has_value());
        }

        TEST_METHOD(StageEnums_DefaultToNone)
        {
            TeardownStage teardownStage{};
            PipelineFailureStage failureStage{};

            Assert::AreEqual(
                static_cast<uint32_t>(TeardownStage::None),
                static_cast<uint32_t>(teardownStage));
            Assert::AreEqual(
                static_cast<uint32_t>(PipelineFailureStage::None),
                static_cast<uint32_t>(failureStage));
        }
    };
}

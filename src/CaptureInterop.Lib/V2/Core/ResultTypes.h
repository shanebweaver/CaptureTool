#pragma once

#include <cstdint>
#include <optional>
#include <string>
#include <utility>
#include <vector>

namespace CaptureInterop::V2
{
    enum class CoreResultCode : uint32_t
    {
        Success = 0,
        ValidationFailure = 1,
        InvalidState = 2,
        UnsupportedOperation = 3,
        NotFound = 4,
        NativeFailure = 5,
        RangeError = 6
    };

    enum class DiagnosticSeverity : uint32_t
    {
        Error = 0,
        Warning = 1
    };

    enum class TeardownStage : uint32_t
    {
        None = 0,
        StopAcceptingCallbacks,
        StopSources,
        FlushProcessors,
        FlushSink,
        FinalizeSink,
        ReleaseSink,
        ReleaseProcessors,
        ReleaseSources,
        ReleaseInfrastructure
    };

    enum class PipelineFailureStage : uint32_t
    {
        None = 0,
        Validation,
        OutputPlanning,
        GraphConstruction,
        StartSources,
        Running,
        Stop,
        Teardown
    };

    struct CoreDiagnostic
    {
        DiagnosticSeverity severity{ DiagnosticSeverity::Error };
        CoreResultCode code{ CoreResultCode::Success };
        std::string component;
        std::string operation;
        std::optional<int64_t> nativeStatus;
        std::string message;

        static CoreDiagnostic Error(
            CoreResultCode code,
            std::string component,
            std::string operation,
            std::string message,
            std::optional<int64_t> nativeStatus = std::nullopt)
        {
            return CoreDiagnostic{
                DiagnosticSeverity::Error,
                code,
                std::move(component),
                std::move(operation),
                nativeStatus,
                std::move(message)
            };
        }

        static CoreDiagnostic Warning(
            CoreResultCode code,
            std::string component,
            std::string operation,
            std::string message,
            std::optional<int64_t> nativeStatus = std::nullopt)
        {
            return CoreDiagnostic{
                DiagnosticSeverity::Warning,
                code,
                std::move(component),
                std::move(operation),
                nativeStatus,
                std::move(message)
            };
        }

        [[nodiscard]] bool IsError() const noexcept
        {
            return severity == DiagnosticSeverity::Error;
        }

        [[nodiscard]] bool IsWarning() const noexcept
        {
            return severity == DiagnosticSeverity::Warning;
        }
    };

    struct OperationResult
    {
        CoreResultCode code{ CoreResultCode::Success };
        std::optional<CoreDiagnostic> diagnostic;

        static OperationResult Success() noexcept
        {
            return {};
        }

        static OperationResult Failure(CoreDiagnostic diagnostic)
        {
            OperationResult result;
            result.code = diagnostic.code;
            result.diagnostic = std::move(diagnostic);
            return result;
        }

        static OperationResult Failure(
            CoreResultCode code,
            std::string component,
            std::string operation,
            std::string message,
            std::optional<int64_t> nativeStatus = std::nullopt)
        {
            return Failure(CoreDiagnostic::Error(
                code,
                std::move(component),
                std::move(operation),
                std::move(message),
                nativeStatus));
        }

        [[nodiscard]] bool IsSuccess() const noexcept
        {
            return code == CoreResultCode::Success;
        }

        [[nodiscard]] bool IsFailure() const noexcept
        {
            return !IsSuccess();
        }
    };

    struct ValidationResult
    {
        std::vector<CoreDiagnostic> errors;
        std::vector<CoreDiagnostic> warnings;

        static ValidationResult Success()
        {
            return {};
        }

        [[nodiscard]] bool IsValid() const noexcept
        {
            return errors.empty();
        }

        [[nodiscard]] bool HasWarnings() const noexcept
        {
            return !warnings.empty();
        }

        void AddError(CoreDiagnostic error)
        {
            error.severity = DiagnosticSeverity::Error;
            errors.push_back(std::move(error));
        }

        void AddError(
            CoreResultCode code,
            std::string component,
            std::string operation,
            std::string message,
            std::optional<int64_t> nativeStatus = std::nullopt)
        {
            AddError(CoreDiagnostic::Error(
                code,
                std::move(component),
                std::move(operation),
                std::move(message),
                nativeStatus));
        }

        void AddWarning(CoreDiagnostic warning)
        {
            warning.severity = DiagnosticSeverity::Warning;
            warnings.push_back(std::move(warning));
        }

        void AddWarning(
            CoreResultCode code,
            std::string component,
            std::string operation,
            std::string message,
            std::optional<int64_t> nativeStatus = std::nullopt)
        {
            AddWarning(CoreDiagnostic::Warning(
                code,
                std::move(component),
                std::move(operation),
                std::move(message),
                nativeStatus));
        }

        void Merge(ValidationResult other)
        {
            errors.insert(
                errors.end(),
                std::make_move_iterator(other.errors.begin()),
                std::make_move_iterator(other.errors.end()));
            warnings.insert(
                warnings.end(),
                std::make_move_iterator(other.warnings.begin()),
                std::make_move_iterator(other.warnings.end()));
        }

        [[nodiscard]] OperationResult ToOperationResult() const
        {
            if (IsValid())
            {
                return OperationResult::Success();
            }

            return OperationResult::Failure(errors.front());
        }
    };
}

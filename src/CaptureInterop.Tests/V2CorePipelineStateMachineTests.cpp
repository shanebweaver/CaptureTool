#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Core/PipelineStateMachine.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;

namespace CaptureInteropTests
{
    TEST_CLASS(V2CorePipelineStateMachineTests)
    {
    public:
        TEST_METHOD(DefaultState_IsCreated)
        {
            const PipelineStateMachine stateMachine;

            Assert::AreEqual(
                static_cast<int>(PipelineState::Created),
                static_cast<int>(stateMachine.State()));
            Assert::IsFalse(stateMachine.IsActive());
            Assert::IsFalse(stateMachine.IsTerminal());
        }

        TEST_METHOD(Start_IsValidOnlyFromCreated)
        {
            PipelineStateMachine stateMachine;

            Assert::IsTrue(stateMachine.CanApply(PipelineOperation::Start));
            Assert::IsTrue(stateMachine.Start().IsSuccess());
            Assert::AreEqual(
                static_cast<int>(PipelineState::Recording),
                static_cast<int>(stateMachine.State()));

            const OperationResult secondStart = stateMachine.Start();
            Assert::IsTrue(secondStart.IsFailure());
            Assert::AreEqual("Start", secondStart.diagnostic->operation.c_str());
        }

        TEST_METHOD(Pause_IsValidOnlyFromRecording)
        {
            PipelineStateMachine stateMachine;

            Assert::IsFalse(stateMachine.Pause().IsSuccess());
            Assert::IsTrue(stateMachine.Start().IsSuccess());
            Assert::IsTrue(stateMachine.Pause().IsSuccess());
            Assert::AreEqual(
                static_cast<int>(PipelineState::Paused),
                static_cast<int>(stateMachine.State()));

            const OperationResult secondPause = stateMachine.Pause();
            Assert::IsTrue(secondPause.IsFailure());
            Assert::AreEqual("Pause", secondPause.diagnostic->operation.c_str());
        }

        TEST_METHOD(Resume_IsValidOnlyFromPaused)
        {
            PipelineStateMachine stateMachine;

            Assert::IsFalse(stateMachine.Resume().IsSuccess());
            Assert::IsTrue(stateMachine.Start().IsSuccess());
            Assert::IsTrue(stateMachine.Pause().IsSuccess());
            Assert::IsTrue(stateMachine.Resume().IsSuccess());
            Assert::AreEqual(
                static_cast<int>(PipelineState::Recording),
                static_cast<int>(stateMachine.State()));
        }

        TEST_METHOD(SetAudioMuted_IsValidFromRecordingAndPaused)
        {
            PipelineStateMachine stateMachine;

            Assert::IsTrue(stateMachine.SetAudioMuted().IsFailure());
            Assert::IsTrue(stateMachine.Start().IsSuccess());
            Assert::IsTrue(stateMachine.SetAudioMuted().IsSuccess());
            Assert::IsTrue(stateMachine.Pause().IsSuccess());
            Assert::IsTrue(stateMachine.SetAudioMuted().IsSuccess());
        }

        TEST_METHOD(SetAudioGain_IsValidFromRecordingAndPaused)
        {
            PipelineStateMachine stateMachine;

            Assert::IsTrue(stateMachine.SetAudioGain().IsFailure());
            Assert::IsTrue(stateMachine.Start().IsSuccess());
            Assert::IsTrue(stateMachine.SetAudioGain().IsSuccess());
            Assert::IsTrue(stateMachine.Pause().IsSuccess());
            Assert::IsTrue(stateMachine.SetAudioGain().IsSuccess());
        }

        TEST_METHOD(Stop_IsValidFromRecordingAndPaused)
        {
            PipelineStateMachine recordingStateMachine;
            Assert::IsTrue(recordingStateMachine.Start().IsSuccess());
            Assert::IsTrue(recordingStateMachine.Stop().IsSuccess());
            Assert::AreEqual(
                static_cast<int>(PipelineState::Finalized),
                static_cast<int>(recordingStateMachine.State()));
            Assert::IsTrue(recordingStateMachine.IsTerminal());

            PipelineStateMachine pausedStateMachine;
            Assert::IsTrue(pausedStateMachine.Start().IsSuccess());
            Assert::IsTrue(pausedStateMachine.Pause().IsSuccess());
            Assert::IsTrue(pausedStateMachine.Stop().IsSuccess());
            Assert::AreEqual(
                static_cast<int>(PipelineState::Finalized),
                static_cast<int>(pausedStateMachine.State()));
        }

        TEST_METHOD(Stop_BeforeStart_ReturnsInvalidState)
        {
            PipelineStateMachine stateMachine;

            const OperationResult result = stateMachine.Stop();

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::InvalidState),
                static_cast<uint32_t>(result.code));
            Assert::AreEqual("PipelineStateMachine", result.diagnostic->component.c_str());
            Assert::AreEqual("Stop", result.diagnostic->operation.c_str());
        }

        TEST_METHOD(Fail_IsValidBeforeTerminalState)
        {
            PipelineStateMachine stateMachine;

            Assert::IsTrue(stateMachine.Fail().IsSuccess());
            Assert::AreEqual(
                static_cast<int>(PipelineState::Failed),
                static_cast<int>(stateMachine.State()));
            Assert::IsTrue(stateMachine.IsTerminal());
            Assert::IsTrue(stateMachine.Fail().IsFailure());
        }

        TEST_METHOD(FinalizedState_RejectsRuntimeCommands)
        {
            PipelineStateMachine stateMachine;
            Assert::IsTrue(stateMachine.Start().IsSuccess());
            Assert::IsTrue(stateMachine.Stop().IsSuccess());

            Assert::IsTrue(stateMachine.Start().IsFailure());
            Assert::IsTrue(stateMachine.Pause().IsFailure());
            Assert::IsTrue(stateMachine.Resume().IsFailure());
            Assert::IsTrue(stateMachine.Stop().IsFailure());
            Assert::IsTrue(stateMachine.SetAudioMuted().IsFailure());
            Assert::IsTrue(stateMachine.SetAudioGain().IsFailure());
        }

        TEST_METHOD(OperationNames_AreStable)
        {
            Assert::AreEqual("Start", PipelineStateMachine::OperationName(PipelineOperation::Start));
            Assert::AreEqual("Pause", PipelineStateMachine::OperationName(PipelineOperation::Pause));
            Assert::AreEqual("Resume", PipelineStateMachine::OperationName(PipelineOperation::Resume));
            Assert::AreEqual("Stop", PipelineStateMachine::OperationName(PipelineOperation::Stop));
            Assert::AreEqual("SetAudioMuted", PipelineStateMachine::OperationName(PipelineOperation::SetAudioMuted));
            Assert::AreEqual("SetAudioGain", PipelineStateMachine::OperationName(PipelineOperation::SetAudioGain));
        }
    };
}

#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Core/RecordingClock.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;

namespace
{
    class ManualTimeProvider final : public IClockTimeProvider
    {
    public:
        [[nodiscard]] MediaTime Now() const noexcept override
        {
            return m_now;
        }

        void Set(MediaTime time) noexcept
        {
            m_now = time;
        }

        void Advance(MediaDuration duration) noexcept
        {
            m_now = m_now + duration;
        }

    private:
        MediaTime m_now;
    };
}

namespace CaptureInteropTests
{
    TEST_CLASS(V2CoreRecordingClockTests)
    {
    public:
        TEST_METHOD(CurrentTime_BeforeStart_IsZero)
        {
            ManualTimeProvider provider;
            provider.Set(MediaTime::FromTicks(5'000'000));
            const RecordingClock clock(provider);

            Assert::AreEqual(0LL, clock.CurrentTime().ticks100ns);
            Assert::IsFalse(clock.IsStarted());
            Assert::IsFalse(clock.IsPaused());
        }

        TEST_METHOD(Start_BeginsRecordingTimeAtZero)
        {
            ManualTimeProvider provider;
            provider.Set(MediaTime::FromTicks(5'000'000));
            RecordingClock clock(provider);

            const OperationResult result = clock.Start();

            Assert::IsTrue(result.IsSuccess());
            Assert::IsTrue(clock.IsStarted());
            Assert::AreEqual(0LL, clock.CurrentTime().ticks100ns);
        }

        TEST_METHOD(CurrentTime_AfterStart_TracksElapsedWallTime)
        {
            ManualTimeProvider provider;
            RecordingClock clock(provider);
            Assert::IsTrue(clock.Start().IsSuccess());

            provider.Advance(MediaDuration::FromSeconds(1));

            Assert::AreEqual(10'000'000LL, clock.CurrentTime().ticks100ns);
        }

        TEST_METHOD(Pause_FreezesRecordingTime)
        {
            ManualTimeProvider provider;
            RecordingClock clock(provider);
            Assert::IsTrue(clock.Start().IsSuccess());

            provider.Advance(MediaDuration::FromSeconds(1));
            Assert::IsTrue(clock.Pause().IsSuccess());
            provider.Advance(MediaDuration::FromSeconds(5));

            Assert::IsTrue(clock.IsPaused());
            Assert::AreEqual(10'000'000LL, clock.CurrentTime().ticks100ns);
        }

        TEST_METHOD(Resume_ExcludesPausedWallClockDuration)
        {
            ManualTimeProvider provider;
            RecordingClock clock(provider);
            Assert::IsTrue(clock.Start().IsSuccess());

            provider.Advance(MediaDuration::FromSeconds(1));
            Assert::IsTrue(clock.Pause().IsSuccess());
            provider.Advance(MediaDuration::FromSeconds(5));
            Assert::IsTrue(clock.Resume().IsSuccess());
            provider.Advance(MediaDuration::FromSeconds(2));

            Assert::IsFalse(clock.IsPaused());
            Assert::AreEqual(30'000'000LL, clock.CurrentTime().ticks100ns);
        }

        TEST_METHOD(MultiplePauses_AccumulateExcludedDuration)
        {
            ManualTimeProvider provider;
            RecordingClock clock(provider);
            Assert::IsTrue(clock.Start().IsSuccess());

            provider.Advance(MediaDuration::FromSeconds(1));
            Assert::IsTrue(clock.Pause().IsSuccess());
            provider.Advance(MediaDuration::FromSeconds(10));
            Assert::IsTrue(clock.Resume().IsSuccess());

            provider.Advance(MediaDuration::FromSeconds(2));
            Assert::IsTrue(clock.Pause().IsSuccess());
            provider.Advance(MediaDuration::FromSeconds(20));
            Assert::IsTrue(clock.Resume().IsSuccess());

            provider.Advance(MediaDuration::FromSeconds(3));

            Assert::AreEqual(60'000'000LL, clock.CurrentTime().ticks100ns);
        }

        TEST_METHOD(CurrentTime_IsMonotonicWhenProviderMovesBackward)
        {
            ManualTimeProvider provider;
            RecordingClock clock(provider);
            Assert::IsTrue(clock.Start().IsSuccess());

            provider.Advance(MediaDuration::FromSeconds(5));
            const MediaTime later = clock.CurrentTime();

            provider.Set(MediaTime::FromTicks(MediaTicksPerSecond));
            const MediaTime clamped = clock.CurrentTime();

            Assert::AreEqual(later.ticks100ns, clamped.ticks100ns);
        }

        TEST_METHOD(Start_WhenAlreadyStarted_ReturnsInvalidState)
        {
            ManualTimeProvider provider;
            RecordingClock clock(provider);
            Assert::IsTrue(clock.Start().IsSuccess());

            const OperationResult result = clock.Start();

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::InvalidState),
                static_cast<uint32_t>(result.code));
            Assert::AreEqual("Start", result.diagnostic->operation.c_str());
        }

        TEST_METHOD(Pause_WhenAlreadyPaused_ReturnsInvalidState)
        {
            ManualTimeProvider provider;
            RecordingClock clock(provider);
            Assert::IsTrue(clock.Start().IsSuccess());
            Assert::IsTrue(clock.Pause().IsSuccess());

            const OperationResult result = clock.Pause();

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual("Recording clock is already paused", result.diagnostic->message.c_str());
        }

        TEST_METHOD(Pause_BeforeStart_ReturnsInvalidState)
        {
            ManualTimeProvider provider;
            RecordingClock clock(provider);

            const OperationResult result = clock.Pause();

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual("Recording clock has not started", result.diagnostic->message.c_str());
        }

        TEST_METHOD(Resume_WhenNotPaused_ReturnsInvalidState)
        {
            ManualTimeProvider provider;
            RecordingClock clock(provider);
            Assert::IsTrue(clock.Start().IsSuccess());

            const OperationResult result = clock.Resume();

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual("Recording clock is not paused", result.diagnostic->message.c_str());
        }

        TEST_METHOD(Resume_BeforeStart_ReturnsInvalidState)
        {
            ManualTimeProvider provider;
            RecordingClock clock(provider);

            const OperationResult result = clock.Resume();

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual("Recording clock has not started", result.diagnostic->message.c_str());
        }
    };
}

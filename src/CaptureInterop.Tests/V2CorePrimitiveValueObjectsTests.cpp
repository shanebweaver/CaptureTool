#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Core/MediaPrimitives.h"

#include <type_traits>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;

namespace CaptureInteropTests
{
    TEST_CLASS(V2CorePrimitiveValueObjectsTests)
    {
    public:
        TEST_METHOD(SourceId_Default_IsInvalid)
        {
            SourceId id;

            Assert::AreEqual(0u, id.value);
            Assert::IsFalse(id.IsValid());
            Assert::IsFalse(SourceId::Invalid().IsValid());
        }

        TEST_METHOD(SourceId_Equality_IsValueBased)
        {
            const SourceId first = SourceId::FromValue(7);
            const SourceId same = SourceId::FromValue(7);
            const SourceId different = SourceId::FromValue(8);

            Assert::IsTrue(first.IsValid());
            Assert::IsTrue(first == same);
            Assert::IsFalse(first == different);
        }

        TEST_METHOD(StreamId_Default_IsInvalid)
        {
            StreamId id;

            Assert::AreEqual(0u, id.value);
            Assert::IsFalse(id.IsValid());
            Assert::IsFalse(StreamId::Invalid().IsValid());
        }

        TEST_METHOD(StreamId_Equality_IsValueBased)
        {
            const StreamId first = StreamId::FromValue(3);
            const StreamId same = StreamId::FromValue(3);
            const StreamId different = StreamId::FromValue(4);

            Assert::IsTrue(first.IsValid());
            Assert::IsTrue(first == same);
            Assert::IsFalse(first == different);
        }

        TEST_METHOD(PrimitiveIds_AreStronglyTyped)
        {
            static_assert(!std::is_convertible_v<uint32_t, SourceId>);
            static_assert(!std::is_convertible_v<SourceId, uint32_t>);
            static_assert(!std::is_convertible_v<uint32_t, StreamId>);
            static_assert(!std::is_convertible_v<StreamId, uint32_t>);
            static_assert(!std::is_same_v<SourceId, StreamId>);

            Assert::IsTrue(true);
        }

        TEST_METHOD(MediaAndSourceKinds_DefaultToUnknown)
        {
            MediaKind mediaKind{};
            SourceKind sourceKind{};

            Assert::AreEqual(
                static_cast<int>(MediaKind::Unknown),
                static_cast<int>(mediaKind));
            Assert::AreEqual(
                static_cast<int>(SourceKind::Unknown),
                static_cast<int>(sourceKind));
        }

        TEST_METHOD(Rational_Default_IsInvalid)
        {
            Rational rational;

            Assert::AreEqual(0u, rational.numerator);
            Assert::AreEqual(0u, rational.denominator);
            Assert::IsFalse(rational.IsValid());
        }

        TEST_METHOD(Rational_RequiresPositiveNumeratorAndDenominator)
        {
            Assert::IsTrue(Rational::From(60, 1).IsValid());
            Assert::IsFalse(Rational::From(0, 1).IsValid());
            Assert::IsFalse(Rational::From(60, 0).IsValid());
        }

        TEST_METHOD(Rational_Equality_IsValueBased)
        {
            const Rational first = Rational::From(30000, 1001);
            const Rational same = Rational::From(30000, 1001);
            const Rational different = Rational::From(60, 1);

            Assert::IsTrue(first == same);
            Assert::IsFalse(first == different);
        }

        TEST_METHOD(MediaDuration_Default_IsZero)
        {
            MediaDuration duration;

            Assert::AreEqual(0LL, duration.ticks100ns);
            Assert::IsTrue(duration.IsZero());
            Assert::IsFalse(duration.IsPositive());
            Assert::IsFalse(duration.IsNegative());
        }

        TEST_METHOD(MediaDuration_Uses100NanosecondTicks)
        {
            Assert::AreEqual(10'000'000LL, MediaDuration::FromSeconds(1).ticks100ns);
            Assert::AreEqual(1'000'000LL, MediaDuration::FromMilliseconds(100).ticks100ns);
            Assert::AreEqual(123LL, MediaDuration::FromTicks(123).ticks100ns);
        }

        TEST_METHOD(MediaDuration_Arithmetic_IsTickBased)
        {
            const MediaDuration oneSecond = MediaDuration::FromSeconds(1);
            const MediaDuration quarterSecond = MediaDuration::FromMilliseconds(250);

            Assert::AreEqual(12'500'000LL, (oneSecond + quarterSecond).ticks100ns);
            Assert::AreEqual(7'500'000LL, (oneSecond - quarterSecond).ticks100ns);
            Assert::AreEqual(-2'500'000LL, (-quarterSecond).ticks100ns);
            Assert::IsTrue(quarterSecond < oneSecond);
        }

        TEST_METHOD(MediaTime_Default_IsZero)
        {
            MediaTime time;

            Assert::AreEqual(0LL, time.ticks100ns);
            Assert::IsTrue(time.IsZero());
            Assert::IsFalse(time.IsNegative());
        }

        TEST_METHOD(MediaTime_Arithmetic_UsesDurations)
        {
            const MediaTime start = MediaTime::FromTicks(5'000'000);
            const MediaDuration offset = MediaDuration::FromMilliseconds(250);
            const MediaTime end = start + offset;

            Assert::AreEqual(7'500'000LL, end.ticks100ns);
            Assert::AreEqual(5'000'000LL, (end - offset).ticks100ns);
            Assert::AreEqual(2'500'000LL, (end - start).ticks100ns);
            Assert::IsTrue(start < end);
        }

        TEST_METHOD(PrimitiveTypes_AreCheapToCopy)
        {
            static_assert(std::is_trivially_copyable_v<SourceId>);
            static_assert(std::is_trivially_copyable_v<StreamId>);
            static_assert(std::is_trivially_copyable_v<Rational>);
            static_assert(std::is_trivially_copyable_v<MediaTime>);
            static_assert(std::is_trivially_copyable_v<MediaDuration>);

            Assert::IsTrue(true);
        }
    };
}

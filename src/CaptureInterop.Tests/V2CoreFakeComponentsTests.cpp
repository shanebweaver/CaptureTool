#include "pch.h"
#include "CppUnitTest.h"
#include "V2CoreTestComponents.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;
using namespace CaptureInterop::V2::Testing;

namespace CaptureInteropTests
{
    TEST_CLASS(V2CoreFakeComponentsTests)
    {
    public:
        TEST_METHOD(SampleBuilder_CreatesDeterministicVideoSamples)
        {
            const VideoSample sample = SampleBuilder::Video(MediaTime::FromTicks(123), MediaDuration::FromMilliseconds(16));

            Assert::AreEqual(1u, sample.sourceId.value);
            Assert::AreEqual(1u, sample.streamId.value);
            Assert::AreEqual(123LL, sample.timestamp.ticks100ns);
            Assert::AreEqual(MediaDuration::FromMilliseconds(16).ticks100ns, sample.duration.ticks100ns);
            Assert::AreEqual(static_cast<size_t>(4), sample.pixelData.size());
            Assert::IsTrue(sample.mediaType.IsValid());
        }

        TEST_METHOD(SampleBuilder_CreatesDeterministicAudioSamples)
        {
            const AudioSample sample = SampleBuilder::Audio(MediaTime::FromTicks(456), MediaDuration::FromMilliseconds(10));

            Assert::AreEqual(2u, sample.sourceId.value);
            Assert::AreEqual(2u, sample.streamId.value);
            Assert::AreEqual(456LL, sample.timestamp.ticks100ns);
            Assert::AreEqual(MediaDuration::FromMilliseconds(10).ticks100ns, sample.duration.ticks100ns);
            Assert::AreEqual(static_cast<size_t>(4), sample.pcmData.size());
            Assert::IsTrue(sample.mediaType.IsValid());
        }

        TEST_METHOD(FakeVideoSource_CanStartStopAndEmitSamples)
        {
            FakeVideoSource source;
            int receivedCount = 0;
            MediaTime receivedTimestamp;

            CallbackRegistrationToken token = source.RegisterFrameArrivedHandler(
                [&](const VideoSample& sample)
                {
                    receivedCount++;
                    receivedTimestamp = sample.timestamp;
                });

            Assert::IsTrue(source.Start().IsSuccess());
            Assert::IsTrue(source.Emit(SampleBuilder::Video(MediaTime::FromTicks(100))).IsSuccess());
            Assert::IsTrue(source.Stop().IsSuccess());

            Assert::IsTrue(source.Started());
            Assert::IsTrue(source.Stopped());
            Assert::AreEqual(1, receivedCount);
            Assert::AreEqual(100LL, receivedTimestamp.ticks100ns);
            Assert::IsNotNull(token.get());
        }

        TEST_METHOD(FakeVideoSource_CallbackTokenUnregistersHandler)
        {
            FakeVideoSource source;
            int receivedCount = 0;

            CallbackRegistrationToken token = source.RegisterFrameArrivedHandler(
                [&](const VideoSample&)
                {
                    receivedCount++;
                });

            Assert::IsTrue(source.Emit(SampleBuilder::Video()).IsSuccess());
            token.reset();
            Assert::IsTrue(source.Emit(SampleBuilder::Video()).IsSuccess());

            Assert::AreEqual(1, receivedCount);
        }

        TEST_METHOD(FakeVideoSource_CanSimulateStartFailure)
        {
            FakeVideoSource source;
            source.SetStartResult(OperationResult::Failure(
                CoreResultCode::NativeFailure,
                "FakeVideoSource",
                "Start",
                "Injected start failure"));

            const OperationResult result = source.Start();

            Assert::IsTrue(result.IsFailure());
            Assert::IsFalse(source.Started());
            Assert::AreEqual("Injected start failure", result.diagnostic->message.c_str());
        }

        TEST_METHOD(FakeAudioSource_CanStartStopAndEmitSamples)
        {
            FakeAudioSource source;
            int receivedCount = 0;
            MediaDuration receivedDuration;

            CallbackRegistrationToken token = source.RegisterSampleArrivedHandler(
                [&](const AudioSample& sample)
                {
                    receivedCount++;
                    receivedDuration = sample.duration;
                });

            Assert::IsTrue(source.Start().IsSuccess());
            Assert::IsTrue(source.Emit(SampleBuilder::Audio(MediaTime::Zero(), MediaDuration::FromMilliseconds(20))).IsSuccess());
            Assert::IsTrue(source.Stop().IsSuccess());

            Assert::IsTrue(source.Started());
            Assert::IsTrue(source.Stopped());
            Assert::AreEqual(1, receivedCount);
            Assert::AreEqual(MediaDuration::FromMilliseconds(20).ticks100ns, receivedDuration.ticks100ns);
            Assert::IsNotNull(token.get());
        }

        TEST_METHOD(PassThroughProcessor_ForwardsSamplesToOutputHandler)
        {
            PassThroughProcessor processor(MediaKind::Video);
            int outputCount = 0;
            MediaKind outputKind = MediaKind::Unknown;

            CallbackRegistrationToken token = processor.RegisterOutputHandler(
                [&](const MediaSample& sample)
                {
                    outputCount++;
                    outputKind = sample.Kind();
                });

            const VideoMediaType mediaType = SampleBuilder::VideoType();
            Assert::IsTrue(processor.Configure(mediaType, mediaType).IsSuccess());
            Assert::IsTrue(processor.Process(MediaSample{ SampleBuilder::Video() }).IsSuccess());

            Assert::IsNotNull(token.get());
            Assert::IsTrue(processor.Configured());
            Assert::AreEqual(static_cast<size_t>(1), processor.ReceivedSamples().size());
            Assert::AreEqual(1, outputCount);
            Assert::AreEqual(static_cast<int>(MediaKind::Video), static_cast<int>(outputKind));
        }

        TEST_METHOD(PassThroughProcessor_CanSimulateProcessFailure)
        {
            PassThroughProcessor processor(MediaKind::Audio);
            processor.SetProcessResult(OperationResult::Failure(
                CoreResultCode::NativeFailure,
                "PassThroughProcessor",
                "Process",
                "Injected process failure"));

            const OperationResult result = processor.Process(MediaSample{ SampleBuilder::Audio() });

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(static_cast<size_t>(0), processor.ReceivedSamples().size());
        }

        TEST_METHOD(NullOutputSink_RecordsReceivedSamples)
        {
            NullOutputSink sink;
            OutputPlan plan;
            plan.container = ContainerFormat::Mp4;
            plan.outputPath = L"C:\\Temp\\fake.mp4";

            Assert::IsTrue(sink.Open(plan).IsSuccess());
            Assert::IsTrue(sink.WriteSample(MediaSample{ SampleBuilder::Video() }).IsSuccess());
            Assert::IsTrue(sink.WriteSample(MediaSample{ SampleBuilder::Audio() }).IsSuccess());
            Assert::IsTrue(sink.Finalize().IsSuccess());

            Assert::IsTrue(sink.Opened());
            Assert::IsTrue(sink.Finalized());
            Assert::IsTrue(sink.Plan().has_value());
            Assert::AreEqual(static_cast<size_t>(2), sink.ReceivedSamples().size());
            Assert::AreEqual(static_cast<int>(MediaKind::Video), static_cast<int>(sink.ReceivedSamples()[0].Kind()));
            Assert::AreEqual(static_cast<int>(MediaKind::Audio), static_cast<int>(sink.ReceivedSamples()[1].Kind()));
        }

        TEST_METHOD(NullOutputSink_CanSimulateWriteFailure)
        {
            NullOutputSink sink;
            sink.SetWriteResult(OperationResult::Failure(
                CoreResultCode::NativeFailure,
                "NullOutputSink",
                "WriteSample",
                "Injected write failure"));

            const OperationResult result = sink.WriteSample(MediaSample{ SampleBuilder::Audio() });

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(static_cast<size_t>(0), sink.ReceivedSamples().size());
        }
    };
}

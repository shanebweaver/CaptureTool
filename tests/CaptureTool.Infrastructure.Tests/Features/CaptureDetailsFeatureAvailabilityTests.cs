using CaptureTool.Domain.Analysis;
using CaptureTool.FeatureManagement;
using CaptureTool.Infrastructure.Features;

namespace CaptureTool.Infrastructure.Tests.Features;

[TestClass]
public sealed class CaptureDetailsFeatureAvailabilityTests
{
    [TestMethod]
    public void ReleaseDefaults_KeepImageTextAndHideRecordingTextAndSpeech()
    {
        var availability = new CaptureDetailsFeatureAvailability(new MicrosoftFeatureManager());

        Assert.IsTrue(availability.IsTextTabEnabled(AnalysisMediaKind.Image));
        Assert.IsFalse(availability.IsSpeechTabEnabled(AnalysisMediaKind.Image));
        Assert.IsFalse(availability.IsTextTabEnabled(AnalysisMediaKind.Video));
        Assert.IsFalse(availability.IsSpeechTabEnabled(AnalysisMediaKind.Video));
        Assert.IsFalse(availability.IsTextTabEnabled(AnalysisMediaKind.Audio));
        Assert.IsFalse(availability.IsSpeechTabEnabled(AnalysisMediaKind.Audio));
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(false, false, true)]
    [DataRow(true, true, false)]
    [DataRow(true, false, true)]
    [DataRow(false, true, true)]
    [DataRow(true, true, true)]
    public void RecordingTabs_CanBeEnabledIndependently(bool videoText, bool videoSpeech, bool audioSpeech)
    {
        var flags = new Dictionary<FeatureFlag, bool>
        {
            [AppFeatures.Feature_CaptureDetails_VideoTextTab] = videoText,
            [AppFeatures.Feature_CaptureDetails_VideoSpeechTab] = videoSpeech,
            [AppFeatures.Feature_CaptureDetails_AudioSpeechTab] = audioSpeech
        };
        var availability = new CaptureDetailsFeatureAvailability(new TestFeatureManager(flags));

        Assert.AreEqual(videoText, availability.IsTextTabEnabled(AnalysisMediaKind.Video));
        Assert.AreEqual(videoSpeech, availability.IsSpeechTabEnabled(AnalysisMediaKind.Video));
        Assert.AreEqual(audioSpeech, availability.IsSpeechTabEnabled(AnalysisMediaKind.Audio));
        Assert.IsTrue(availability.IsTextTabEnabled(AnalysisMediaKind.Image));
        Assert.IsFalse(availability.IsSpeechTabEnabled(AnalysisMediaKind.Image));
        Assert.IsFalse(availability.IsTextTabEnabled(AnalysisMediaKind.Audio));
    }

    private sealed class TestFeatureManager(IReadOnlyDictionary<FeatureFlag, bool> flags) : IFeatureManager
    {
        public bool IsEnabled(FeatureFlag featureFlag) => flags[featureFlag];
    }
}

using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Media;
using CaptureTool.Application.Abstractions.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Infrastructure.Analysis.FoundryLocal.DependencyInjection;

public static class FoundryLocalAnalysisServiceCollectionExtensions
{
    public static IServiceCollection AddFoundryLocalAnalysisProvider(this IServiceCollection services)
    {
        services.AddSingleton<IFoundryLocalSdkClient, FoundryLocalSdkClient>();
        services.AddSingleton<IFoundryLocalModelProvenanceStore,
            FoundryLocalModelProvenanceStore>();
        services.AddSingleton<IFoundryLocalSpeechLanguagePolicy>(provider =>
            new FoundryLocalSpeechLanguagePolicy(
                provider.GetRequiredService<ILocalizationService>()));
        services.AddSingleton<FoundryLocalNemotronSpeechTranscriptionService>();
        services.AddSingleton<FoundryLocalWhisperSpeechTranscriptionService>();
        services.AddSingleton<IFoundryLocalSpeechTranscriptionService>(provider =>
            provider.GetRequiredService<FoundryLocalNemotronSpeechTranscriptionService>());
        services.AddSingleton<IFoundryLocalSpeechTranscriptionService>(provider =>
            provider.GetRequiredService<FoundryLocalWhisperSpeechTranscriptionService>());
        services.AddSingleton<ICaptureAnalyzer>(provider =>
            new FoundryLocalSpeechTranscriptAnalyzer(
                provider.GetRequiredService<FoundryLocalWhisperSpeechTranscriptionService>(),
                provider.GetService<IVideoAudioExtractionService>(),
                FoundryLocalSpeechModelConfiguration.Whisper));
        services.AddSingleton<ICaptureAnalyzer>(provider =>
            new FoundryLocalSpeechTranscriptAnalyzer(
                provider.GetRequiredService<FoundryLocalNemotronSpeechTranscriptionService>(),
                provider.GetService<IVideoAudioExtractionService>(),
                FoundryLocalSpeechModelConfiguration.NemotronMultilingual));
        return services;
    }
}

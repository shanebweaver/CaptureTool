using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Infrastructure.Analysis.FoundryLocal.DependencyInjection;

public static class FoundryLocalAnalysisServiceCollectionExtensions
{
    public static IServiceCollection AddFoundryLocalAnalysisProvider(this IServiceCollection services)
    {
        services.AddSingleton<IFoundryLocalSpeechTranscriptionService,
            FoundryLocalSpeechTranscriptionService>();
        services.AddSingleton<ICaptureAnalyzer, FoundryLocalSpeechTranscriptAnalyzer>();
        return services;
    }
}

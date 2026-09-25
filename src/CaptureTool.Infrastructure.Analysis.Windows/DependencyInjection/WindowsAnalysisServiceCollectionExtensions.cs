using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;
using CaptureTool.Infrastructure.Analysis.Windows.Foundry;
using CaptureTool.Infrastructure.Analysis.Windows.Media;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Infrastructure.Analysis.Windows.DependencyInjection;

public static class WindowsAnalysisServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsAnalysisProviders(this IServiceCollection services)
    {
        services.AddSingleton<WindowsAnalysisMedia>();
        services.AddSingleton<FoundryRuntime>();
        services.AddSingleton<IMediaAnalyzer, FileDetailsAnalyzer>();
        services.AddSingleton<IMediaAnalyzer>(provider => new QrCodeAnalyzer("zxing-image-qr", AnalysisMediaKind.Image,
            provider.GetRequiredService<WindowsAnalysisMedia>()));
        services.AddSingleton<IMediaAnalyzer>(provider => new QrCodeAnalyzer("zxing-video-frame-qr", AnalysisMediaKind.Video,
            provider.GetRequiredService<WindowsAnalysisMedia>()));
        AddImage("windows-ai-ocr-document", WindowsImageModel.Ocr, AnalysisMediaKind.Image);
        AddImage("windows-ocr-document", WindowsImageModel.LegacyOcr, AnalysisMediaKind.Image);
        AddImage("windows-image-description", WindowsImageModel.Description, AnalysisMediaKind.Image);
        AddImage("windows-ai-video-frame-ocr", WindowsImageModel.Ocr, AnalysisMediaKind.Video);
        AddImage("windows-video-frame-ocr", WindowsImageModel.LegacyOcr, AnalysisMediaKind.Video);
        AddImage("windows-video-frame-description", WindowsImageModel.Description, AnalysisMediaKind.Video);
        services.AddSingleton<IMediaAnalyzer>(provider => new FoundryImageDescriptionAnalyzer("foundry-local-image-description",
            "qwen3.5-0.8b", provider.GetRequiredService<FoundryRuntime>(), provider.GetRequiredService<WindowsAnalysisMedia>()));
        services.AddSingleton<IMediaAnalyzer>(provider => new FoundrySpeechAnalyzer("foundry-local-nemotron-multilingual-speech-transcript",
            "nemotron-3.5-asr-streaming-0.6b", true, provider.GetRequiredService<FoundryRuntime>(), provider.GetRequiredService<WindowsAnalysisMedia>()));
        services.AddSingleton<IMediaAnalyzer>(provider => new FoundrySpeechAnalyzer("foundry-local-speech-transcript",
            "whisper-tiny", false, provider.GetRequiredService<FoundryRuntime>(), provider.GetRequiredService<WindowsAnalysisMedia>()));
        return services;

        void AddImage(string id, WindowsImageModel model, AnalysisMediaKind kind) =>
            services.AddSingleton<IMediaAnalyzer>(provider => new WindowsImageAnalyzer(id, model, kind, provider.GetRequiredService<WindowsAnalysisMedia>()));
    }
}

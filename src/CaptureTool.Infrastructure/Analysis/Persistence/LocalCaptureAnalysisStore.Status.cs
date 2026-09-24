using CaptureTool.Application.Abstractions.Analysis;
using System.Security.Cryptography;

namespace CaptureTool.Infrastructure.Analysis.Persistence;

internal sealed partial class LocalCaptureAnalysisStore
{
    public async Task<AnalysisStorageStatus> GetStorageStatusAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        bool? hasData = null;
        try
        {
            string[] generations = _files.GetDirectories(_recordsRoot);
            bool records = generations.Any(path => _files.GetFiles(path).Length != 0);
            hasData = records || _files.GetFiles(_root).Contains(_controlPath, StringComparer.OrdinalIgnoreCase);
            var control = await ReadControlAsync(false, cancellationToken).ConfigureAwait(false);
            bool cleanup = control != null && generations.Any(path => !string.Equals(path, GenerationPath(control.Generation), StringComparison.OrdinalIgnoreCase));
            return new(records || cleanup, true, cleanup);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            return new(hasData, false);
        }
        finally { _gate.Release(); }
    }
}

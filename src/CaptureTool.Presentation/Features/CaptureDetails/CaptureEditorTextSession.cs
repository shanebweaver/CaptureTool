using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;

namespace CaptureTool.Presentation.Features.CaptureDetails;

/// <summary>Text from the current editor image. Its lifetime is the edit session, not the details pane.</summary>
public sealed class CaptureEditorTextSession(Func<Task> extract, Action cancel)
{
    public event Action? Changed;
    public RecognizedTextDocument? Document { get; private set; }
    public bool HasChanges { get; private set; }
    public bool CanExtract { get; private set; }
    public bool IsRunning { get; private set; }
    public string Status { get; private set; } = string.Empty;

    public void SetAvailability(bool available)
    {
        if (CanExtract == available) return;
        CanExtract = available;
        Changed?.Invoke();
    }

    public void SetStatus(string status)
    {
        if (Status == status) return;
        Status = status;
        Changed?.Invoke();
    }

    public void SetDocument(RecognizedTextDocument document)
    {
        Document = document;
        Changed?.Invoke();
    }

    public void Invalidate(bool hasChanges)
    {
        Cancel();
        HasChanges = hasChanges;
        Document = null;
        Status = string.Empty;
        Changed?.Invoke();
    }

    public async Task ExtractAsync()
    {
        if (!CanExtract || IsRunning) return;
        IsRunning = true;
        Status = string.Empty;
        Changed?.Invoke();
        try { await extract(); }
        finally { IsRunning = false; Changed?.Invoke(); }
    }

    public void Cancel() => cancel();
}

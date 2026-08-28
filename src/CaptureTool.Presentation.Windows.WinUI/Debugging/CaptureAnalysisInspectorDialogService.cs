#if DEBUG
using CaptureTool.Application.Abstractions.Analysis.Queries;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Windowing;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Capture;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace CaptureTool.Presentation.Windows.WinUI.Debugging;

internal sealed class CaptureAnalysisInspectorDialogService
{
    private readonly SemaphoreSlim _dialogGate = new(1, 1);
    private readonly ICaptureAnalysisQueryService _queries;
    private readonly ICaptureAssetCatalog _captureAssets;
    private readonly IWindowHandleProvider _windowHandleProvider;

    public CaptureAnalysisInspectorDialogService(
        ICaptureAnalysisQueryService queries,
        ICaptureAssetCatalog captureAssets,
        IWindowHandleProvider windowHandleProvider)
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(captureAssets);
        ArgumentNullException.ThrowIfNull(windowHandleProvider);
        _queries = queries;
        _captureAssets = captureAssets;
        _windowHandleProvider = windowHandleProvider;
    }

    public async Task ShowAsync(XamlRoot xamlRoot)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        if (!await _dialogGate.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            List<InspectionEntry> entries = await LoadEntriesAsync().ConfigureAwait(true);
            var capturePicker = new ComboBox
            {
                Header = "Capture",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEnabled = entries.Count > 0,
            };
            foreach (InspectionEntry entry in entries)
            {
                capturePicker.Items.Add(entry.Label);
            }

            var sourcePath = new TextBox
            {
                Header = "Current source path",
                IsReadOnly = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            var json = new TextBox
            {
                Header = "Normalized analysis metadata",
                AcceptsReturn = true,
                IsReadOnly = true,
                Height = 430,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                TextWrapping = TextWrapping.NoWrap,
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(json, ScrollBarVisibility.Auto);
            ScrollViewer.SetVerticalScrollBarVisibility(json, ScrollBarVisibility.Auto);
            var status = new InfoBar
            {
                IsOpen = false,
                IsClosable = true,
            };
            var copyButton = new Button
            {
                Content = "Copy JSON",
                IsEnabled = entries.Count > 0,
            };
            var exportButton = new Button
            {
                Content = "Export JSON…",
                IsEnabled = entries.Count > 0,
            };
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
            };
            actions.Children.Add(copyButton);
            actions.Children.Add(exportButton);

            void SelectEntry(int index)
            {
                if (index < 0 || index >= entries.Count)
                {
                    sourcePath.Text = string.Empty;
                    json.Text = string.Empty;
                    return;
                }

                InspectionEntry entry = entries[index];
                sourcePath.Text = entry.SourcePath ?? "The capture asset is no longer available.";
                json.Text = CaptureAnalysisInspectorJsonSerializer.Serialize(
                    entry.Record,
                    entry.Asset);
                status.IsOpen = false;
            }

            capturePicker.SelectionChanged += (_, _) => SelectEntry(capturePicker.SelectedIndex);
            copyButton.Click += (_, _) =>
            {
                try
                {
                    var package = new DataPackage();
                    package.SetText(json.Text);
                    Clipboard.SetContent(package);
                    Clipboard.Flush();
                    ShowStatus(status, "Copied", "The selected analysis JSON is on the clipboard.",
                        InfoBarSeverity.Success);
                }
                catch
                {
                    ShowStatus(status, "Copy failed", "Capture Tool could not access the clipboard.",
                        InfoBarSeverity.Error);
                }
            };
            exportButton.Click += async (_, _) =>
            {
                if (capturePicker.SelectedIndex < 0 || capturePicker.SelectedIndex >= entries.Count)
                {
                    return;
                }

                try
                {
                    StorageFile? file = await PickExportFileAsync(entries[capturePicker.SelectedIndex]);
                    if (file == null)
                    {
                        return;
                    }

                    await FileIO.WriteTextAsync(file, json.Text, UnicodeEncoding.Utf8);
                    ShowStatus(status, "Exported", file.Path, InfoBarSeverity.Success);
                }
                catch
                {
                    ShowStatus(status, "Export failed", "Capture Tool could not write the JSON file.",
                        InfoBarSeverity.Error);
                }
            };

            if (entries.Count > 0)
            {
                capturePicker.SelectedIndex = 0;
            }
            else
            {
                json.Text = "No protected capture-analysis records are available yet.";
            }

            var content = new StackPanel
            {
                Width = 860,
                Spacing = 10,
            };
            content.Children.Add(new TextBlock
            {
                Text = "This is a readable projection of the encrypted canonical metadata. " +
                    "It includes extracted text, timestamps, analyzer/model provenance, and outcomes. " +
                    "Copying or exporting creates plaintext that may contain private capture content.",
                TextWrapping = TextWrapping.WrapWholeWords,
            });
            content.Children.Add(capturePicker);
            content.Children.Add(sourcePath);
            content.Children.Add(actions);
            content.Children.Add(status);
            content.Children.Add(json);

            var dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = "Capture Analysis Inspector",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close,
                Style = Microsoft.UI.Xaml.Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                Content = content,
            };
            await dialog.ShowAsync();
        }
        finally
        {
            _dialogGate.Release();
        }
    }

    private async Task<List<InspectionEntry>> LoadEntriesAsync()
    {
        var entries = new List<InspectionEntry>();
        await foreach (CaptureAnalysisRecord record in _queries.ReadAllAsync(CancellationToken.None))
        {
            CaptureAsset? asset = _captureAssets.Get(record.CaptureId);
            string? sourcePath = asset?.PreferredOpenPath ?? asset?.RetainedSourcePath;
            string fileName = sourcePath == null
                ? record.CaptureId.ToString()
                : Path.GetFileName(sourcePath);
            string resultSummary = record.Analyses.Count == 1
                ? "1 capability"
                : $"{record.Analyses.Count} capabilities";
            entries.Add(new(
                record,
                asset,
                sourcePath,
                $"{fileName} — {record.MediaKind} — {record.CapturedAtUtc.ToLocalTime():g} — {resultSummary}"));
        }

        return entries
            .OrderByDescending(entry => entry.Record.CapturedAtUtc)
            .ThenBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<StorageFile?> PickExportFileAsync(InspectionEntry entry)
    {
        string baseName = entry.SourcePath == null
            ? entry.Record.CaptureId.ToString()
            : Path.GetFileNameWithoutExtension(entry.SourcePath);
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = SanitizeFileName(baseName) + ".capturetool-analysis",
        };
        unsafe
        {
#pragma warning disable IDE0028 // WinRT projected collections require a concrete mutable list.
            picker.FileTypeChoices.Add("JSON", new List<string>() { ".json" });
#pragma warning restore IDE0028
        }

        WinRT.Interop.InitializeWithWindow.Initialize(
            picker,
            _windowHandleProvider.GetMainWindowHandle());
        return await picker.PickSaveFileAsync();
    }

    private static string SanitizeFileName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(character => invalid.Contains(character) ? '_' : character));
    }

    private static void ShowStatus(
        InfoBar status,
        string title,
        string message,
        InfoBarSeverity severity)
    {
        status.Title = title;
        status.Message = message;
        status.Severity = severity;
        status.IsOpen = true;
    }

    private sealed record InspectionEntry(
        CaptureAnalysisRecord Record,
        CaptureAsset? Asset,
        string? SourcePath,
        string Label);
}
#endif

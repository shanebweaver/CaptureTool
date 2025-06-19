namespace CaptureTool.UI.Windows.Xaml.Controls.ImageCanvas.Commands;

internal interface IImageCanvasCommand
{
    void Execute();

    void Undo();
}

namespace CaptureTool.UI.Xaml.Controls.ImageCanvas.Commands;

internal interface IImageCanvasCommand
{
    void Execute();

    void Undo();
}

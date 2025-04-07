namespace CaptureTool.ViewModels;

public abstract partial class CanvasItemViewModel : ViewModelBase
{
    private int _left;
    public int Left
    {
        get => _left;
        set => Set(ref _left, value);
    }

    private int _top;
    public int Top
    {
        get => _top;
        set => Set(ref _top, value);
    }

    public override void Unload()
    {
        Left = 0;
        Top = 0;
        base.Unload();
    }
}

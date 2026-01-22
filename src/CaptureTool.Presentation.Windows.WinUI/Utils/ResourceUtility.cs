namespace CaptureTool.Presentation.Windows.WinUI.Utils;

internal static partial class ResourceUtility
{
    public static string GetStaticResourceString(string resourceName)
    {
        App.Current.Resources.TryGetValue(resourceName, out object resource);
        return (string)resource;
    }
}

namespace CaptureTool.UI.Windows.Utils;

internal static partial class ResourceUtility
{
    public static string GetStaticResourceString(string resourceName)
    {
        App.Current.Resources.TryGetValue(resourceName, out object resource);
        return (string)resource;
    }
}

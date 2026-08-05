using Microsoft.Windows.AppLifecycle;

namespace CaptureTool.Presentation.Windows.WinUI.Activation;

internal static class ActivationMaterializer
{
    public static ActivationMaterializationResult Materialize(AppActivationArguments args)
    {
        try
        {
            ExtendedActivationKind kind = args.Kind;
            Uri? protocolUri = null;

            if (kind == ExtendedActivationKind.Protocol)
            {
                if (args.Data is not global::Windows.ApplicationModel.Activation.IProtocolActivatedEventArgs protocolArgs)
                {
                    return ActivationMaterializationResult.Failed(
                        "Protocol activation data is not of expected type.");
                }

                protocolUri = new Uri(protocolArgs.Uri.AbsoluteUri);
            }

            return ActivationMaterializationResult.Succeeded(
                new MaterializedActivation(kind, protocolUri));
        }
        catch (Exception exception)
        {
            return ActivationMaterializationResult.Failed(
                "Failed to read activation data.",
                exception);
        }
    }
}

internal readonly record struct MaterializedActivation(
    ExtendedActivationKind Kind,
    Uri? ProtocolUri);

internal readonly record struct ActivationMaterializationResult(
    MaterializedActivation? Activation,
    string? FailureMessage,
    Exception? FailureException)
{
    public static ActivationMaterializationResult Succeeded(MaterializedActivation activation)
    {
        return new(activation, null, null);
    }

    public static ActivationMaterializationResult Failed(string message, Exception? exception = null)
    {
        return new(null, message, exception);
    }
}

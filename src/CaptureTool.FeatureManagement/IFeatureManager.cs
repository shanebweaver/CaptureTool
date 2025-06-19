using System.Threading.Tasks;

namespace CaptureTool.FeatureManagement
{
    public interface IFeatureManager
    {
        bool IsEnabled(FeatureFlag featureFlag);
    }
}
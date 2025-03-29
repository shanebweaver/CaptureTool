using System.Threading.Tasks;

namespace CaptureTool.FeatureManagement
{
    public interface IFeatureManager
    {
        Task<bool> IsEnabledAsync(FeatureFlag featureFlag);
    }
}
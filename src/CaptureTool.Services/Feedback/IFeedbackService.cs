using System.Threading.Tasks;

namespace CaptureTool.Services.Feedback;

public interface IFeedbackService
{
    Task<bool> IsFeedbackSupportedAsync();
    Task ShowFeedbackUIAsync();
}
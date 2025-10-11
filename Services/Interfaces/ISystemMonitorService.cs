using WebApplication1.Models;

namespace WebApplication1.Services.Interfaces
{
    public interface ISystemMonitorService
    {
        Task<SystemResources> GetSystemResourcesAsync();
        Task<StreamStats> GetStreamStatsAsync();
        Task<List<SystemResources>> GetResourceHistoryAsync(TimeSpan duration);
    }
}

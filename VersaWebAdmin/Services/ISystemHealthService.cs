using VersaWebAdmin.Models;

namespace VersaWebAdmin.Services;

public interface ISystemHealthService
{
    Task<SystemHealthViewModel> GetSnapshotAsync(CancellationToken cancellationToken = default);
}

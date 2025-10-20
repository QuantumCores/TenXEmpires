using TenXEmpires.Server.Domain.Entities;

namespace TenXEmpires.Server.Domain.Repositories;

/// <summary>
/// Repository for Save entity with save-specific queries
/// </summary>
public interface ISaveRepository : IRepository<Save>
{
    /// <summary>
    /// Get all saves for a game
    /// </summary>
    Task<IEnumerable<Save>> GetGameSavesAsync(long gameId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get manual save by slot
    /// </summary>
    Task<Save?> GetManualSaveAsync(Guid userId, long gameId, int slot, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get latest autosave for a game
    /// </summary>
    Task<Save?> GetLatestAutosaveAsync(long gameId, CancellationToken cancellationToken = default);
}


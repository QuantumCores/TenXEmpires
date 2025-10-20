namespace TenXEmpires.Server.Domain.Repositories;

/// <summary>
/// Unit of Work pattern for coordinating multiple repositories in a single transaction
/// </summary>
public interface IUnitOfWork : IDisposable
{
    // Repository properties
    IGameRepository Games { get; }
    IMapRepository Maps { get; }
    IUnitRepository Units { get; }
    ICityRepository Cities { get; }
    IParticipantRepository Participants { get; }
    ISaveRepository Saves { get; }
    ITurnRepository Turns { get; }
    
    /// <summary>
    /// Save all changes made in this unit of work
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Begin a new database transaction
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Commit the current transaction
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Rollback the current transaction
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}


using System.Linq.Expressions;

namespace TenXEmpires.Server.Domain.Repositories;

/// <summary>
/// Generic repository interface for basic CRUD operations
/// </summary>
/// <typeparam name="TEntity">Entity type</typeparam>
public interface IRepository<TEntity> where TEntity : class
{
    /// <summary>
    /// Get entity by ID
    /// </summary>
    Task<TEntity?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all entities
    /// </summary>
    Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Find entities matching predicate
    /// </summary>
    Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add new entity
    /// </summary>
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add multiple entities
    /// </summary>
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update existing entity
    /// </summary>
    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete entity
    /// </summary>
    Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete multiple entities
    /// </summary>
    Task DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if entity with ID exists
    /// </summary>
    Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Count total entities
    /// </summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}


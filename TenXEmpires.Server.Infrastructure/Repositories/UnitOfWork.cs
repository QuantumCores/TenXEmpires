using Microsoft.EntityFrameworkCore.Storage;
using TenXEmpires.Server.Domain.Repositories;
using TenXEmpires.Server.Infrastructure.Data;

namespace TenXEmpires.Server.Infrastructure.Repositories;

/// <summary>
/// Unit of Work implementation coordinating multiple repositories in a single transaction
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly TenXDbContext _context;
    private IDbContextTransaction? _transaction;

    // Lazy initialization of repositories
    private IGameRepository? _games;
    private IMapRepository? _maps;
    private IUnitRepository? _units;
    private ICityRepository? _cities;
    private IParticipantRepository? _participants;
    private ISaveRepository? _saves;
    private ITurnRepository? _turns;

    public UnitOfWork(TenXDbContext context)
    {
        _context = context;
    }

    // Repository properties with lazy initialization
    public IGameRepository Games =>
        _games ??= new GameRepository(_context);

    public IMapRepository Maps =>
        _maps ??= new MapRepository(_context);

    public IUnitRepository Units =>
        _units ??= new UnitRepository(_context);

    public ICityRepository Cities =>
        _cities ??= new CityRepository(_context);

    public IParticipantRepository Participants =>
        _participants ??= new ParticipantRepository(_context);

    public ISaveRepository Saves =>
        _saves ??= new SaveRepository(_context);

    public ITurnRepository Turns =>
        _turns ??= new TurnRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
            
            if (_transaction != null)
            {
                await _transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}


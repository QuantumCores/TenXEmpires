using Microsoft.EntityFrameworkCore;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Domain.Repositories;
using TenXEmpires.Server.Infrastructure.Data;

namespace TenXEmpires.Server.Infrastructure.Repositories;

public class UnitRepository : RepositoryBase<Unit>, IUnitRepository
{
    public UnitRepository(TenXDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Unit>> GetGameUnitsAsync(long gameId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(u => u.Type)
            .Include(u => u.Participant)
            .Where(u => u.GameId == gameId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Unit>> GetParticipantUnitsAsync(long participantId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(u => u.Type)
            .Include(u => u.Tile)
            .Where(u => u.ParticipantId == participantId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Unit?> GetUnitOnTileAsync(long gameId, long tileId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(u => u.Type)
            .Include(u => u.Participant)
            .FirstOrDefaultAsync(u => u.GameId == gameId && u.TileId == tileId, cancellationToken);
    }

    public async Task<IEnumerable<Unit>> GetUnitsReadyToActAsync(long participantId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(u => u.Type)
            .Include(u => u.Tile)
            .Where(u => u.ParticipantId == participantId && !u.HasActed)
            .ToListAsync(cancellationToken);
    }
}


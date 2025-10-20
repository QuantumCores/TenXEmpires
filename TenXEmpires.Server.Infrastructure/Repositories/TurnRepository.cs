using Microsoft.EntityFrameworkCore;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Domain.Repositories;
using TenXEmpires.Server.Infrastructure.Data;

namespace TenXEmpires.Server.Infrastructure.Repositories;

public class TurnRepository : RepositoryBase<Turn>, ITurnRepository
{
    public TurnRepository(TenXDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Turn>> GetGameTurnsAsync(long gameId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(t => t.Participant)
            .Where(t => t.GameId == gameId)
            .OrderBy(t => t.TurnNo)
            .ToListAsync(cancellationToken);
    }

    public async Task<Turn?> GetLatestTurnAsync(long gameId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(t => t.Participant)
            .Where(t => t.GameId == gameId)
            .OrderByDescending(t => t.TurnNo)
            .FirstOrDefaultAsync(cancellationToken);
    }
}


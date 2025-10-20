using Microsoft.EntityFrameworkCore;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Domain.Repositories;
using TenXEmpires.Server.Infrastructure.Data;

namespace TenXEmpires.Server.Infrastructure.Repositories;

public class GameRepository : RepositoryBase<Game>, IGameRepository
{
    public GameRepository(TenXDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Game>> GetUserGamesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(g => g.UserId == userId)
            .OrderByDescending(g => g.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Game?> GetGameWithDetailsAsync(long gameId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(g => g.Map)
            .Include(g => g.Participants)
            .Include(g => g.Units)
                .ThenInclude(u => u.Type)
            .Include(g => g.Cities)
                .ThenInclude(c => c.CityResources)
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);
    }

    public async Task<IEnumerable<Game>> GetActiveUserGamesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(g => g.UserId == userId && g.Status == "active")
            .OrderByDescending(g => g.LastTurnAt ?? g.StartedAt)
            .ToListAsync(cancellationToken);
    }
}


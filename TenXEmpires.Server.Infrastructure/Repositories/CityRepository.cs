using Microsoft.EntityFrameworkCore;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Domain.Repositories;
using TenXEmpires.Server.Infrastructure.Data;

namespace TenXEmpires.Server.Infrastructure.Repositories;

public class CityRepository : RepositoryBase<City>, ICityRepository
{
    public CityRepository(TenXDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<City>> GetGameCitiesAsync(long gameId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Participant)
            .Include(c => c.Tile)
            .Where(c => c.GameId == gameId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<City>> GetParticipantCitiesAsync(long participantId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Tile)
            .Include(c => c.CityResources)
            .Where(c => c.ParticipantId == participantId)
            .ToListAsync(cancellationToken);
    }

    public async Task<City?> GetCityWithDetailsAsync(long cityId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Participant)
            .Include(c => c.Tile)
            .Include(c => c.CityResources)
            .Include(c => c.CityTiles)
                .ThenInclude(ct => ct.Tile)
            .FirstOrDefaultAsync(c => c.Id == cityId, cancellationToken);
    }
}


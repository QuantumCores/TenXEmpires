using Microsoft.EntityFrameworkCore;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Domain.Repositories;
using TenXEmpires.Server.Infrastructure.Data;

namespace TenXEmpires.Server.Infrastructure.Repositories;

public class MapRepository : RepositoryBase<Map>, IMapRepository
{
    public MapRepository(TenXDbContext context) : base(context)
    {
    }

    public async Task<Map?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(m => m.Code == code, cancellationToken);
    }

    public async Task<Map?> GetMapWithTilesAsync(long mapId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(m => m.MapTiles)
            .FirstOrDefaultAsync(m => m.Id == mapId, cancellationToken);
    }
}


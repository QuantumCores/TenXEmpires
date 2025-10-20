using Microsoft.EntityFrameworkCore;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Domain.Repositories;
using TenXEmpires.Server.Infrastructure.Data;

namespace TenXEmpires.Server.Infrastructure.Repositories;

public class SaveRepository : RepositoryBase<Save>, ISaveRepository
{
    public SaveRepository(TenXDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Save>> GetGameSavesAsync(long gameId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(s => s.GameId == gameId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Save?> GetManualSaveAsync(Guid userId, long gameId, int slot, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(s => s.UserId == userId && s.GameId == gameId && s.Kind == "manual" && s.Slot == slot, cancellationToken);
    }

    public async Task<Save?> GetLatestAutosaveAsync(long gameId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(s => s.GameId == gameId && s.Kind == "autosave")
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}


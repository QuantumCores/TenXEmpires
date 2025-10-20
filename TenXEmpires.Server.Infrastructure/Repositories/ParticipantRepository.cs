using Microsoft.EntityFrameworkCore;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Domain.Repositories;
using TenXEmpires.Server.Infrastructure.Data;

namespace TenXEmpires.Server.Infrastructure.Repositories;

public class ParticipantRepository : RepositoryBase<Participant>, IParticipantRepository
{
    public ParticipantRepository(TenXDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Participant>> GetGameParticipantsAsync(long gameId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(p => p.GameId == gameId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Participant?> GetUserParticipantAsync(long gameId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == userId && p.Kind == "human", cancellationToken);
    }
}


using Microsoft.EntityFrameworkCore;
using TenXEmpires.Server.Domain.Constants;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Infrastructure.Data;

namespace TenXEmpires.Server.Infrastructure.Services;

internal static class CityCaptureHelper
{
    public static async Task CaptureCityAsync(
        TenXDbContext context,
        City city,
        long newOwnerParticipantId,
        CancellationToken cancellationToken = default)
    {
        if (city.Hp > 0) return; // only capture when defeated

        var oldOwnerId = city.ParticipantId;
        if (oldOwnerId == newOwnerParticipantId) return;

        // Transfer ownership and set minimal HP to keep city alive
        city.ParticipantId = newOwnerParticipantId;
        if (city.Hp <= 0) city.Hp = 1;

        // Persist transfer so subsequent queries reflect new ownership
        await context.SaveChangesAsync(cancellationToken);

        // Mark old owner eliminated if they have no other cities
        var oldOwnerHasCities = await context.Cities
            .Where(c => c.GameId == city.GameId && c.ParticipantId == oldOwnerId)
            .AnyAsync(cancellationToken);

        if (!oldOwnerHasCities)
        {
            var oldOwner = await context.Participants.FirstOrDefaultAsync(p => p.Id == oldOwnerId, cancellationToken);
            if (oldOwner != null)
            {
                oldOwner.IsEliminated = true;
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        // Finish game if there are no enemy cities left
        var anyEnemyCity = await context.Cities
            .Where(c => c.GameId == city.GameId && c.ParticipantId != newOwnerParticipantId)
            .AnyAsync(cancellationToken);

        if (!anyEnemyCity)
        {
            var game = await context.Games.FirstOrDefaultAsync(g => g.Id == city.GameId, cancellationToken);
            if (game != null)
            {
                game.Status = GameStatus.Finished;
                game.FinishedAt = DateTimeOffset.UtcNow;
                game.ActiveParticipantId = null;
                await context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}

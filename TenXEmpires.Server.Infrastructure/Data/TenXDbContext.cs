using Microsoft.EntityFrameworkCore;
using TenXEmpires.Server.Domain.Entities;

namespace TenXEmpires.Server.Infrastructure.Data;

/// <summary>
/// Entity Framework DbContext for TenX Empires game data (app schema)
/// </summary>
public class TenXDbContext : DbContext
{
    public TenXDbContext(DbContextOptions<TenXDbContext> options) : base(options)
    {
    }

    // DbSets for all entities
    public DbSet<Map> Maps => Set<Map>();
    public DbSet<MapTile> MapTiles => Set<MapTile>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<UnitDefinition> UnitDefinitions => Set<UnitDefinition>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<CityTile> CityTiles => Set<CityTile>();
    public DbSet<CityResource> CityResources => Set<CityResource>();
    public DbSet<Save> Saves => Set<Save>();
    public DbSet<Turn> Turns => Set<Turn>();
    public DbSet<AnalyticsEvent> AnalyticsEvents => Set<AnalyticsEvent>();
    public DbSet<Setting> Settings => Set<Setting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Set default schema to 'app'
        modelBuilder.HasDefaultSchema("app");

        // Configure entities
        ConfigureMap(modelBuilder);
        ConfigureMapTile(modelBuilder);
        ConfigureGame(modelBuilder);
        ConfigureParticipant(modelBuilder);
        ConfigureUnitDefinition(modelBuilder);
        ConfigureUnit(modelBuilder);
        ConfigureCity(modelBuilder);
        ConfigureCityTile(modelBuilder);
        ConfigureCityResource(modelBuilder);
        ConfigureSave(modelBuilder);
        ConfigureTurn(modelBuilder);
        ConfigureAnalyticsEvent(modelBuilder);
        ConfigureSetting(modelBuilder);
    }

    private static void ConfigureMap(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Map>(entity =>
        {
            entity.ToTable("maps");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Code).HasColumnName("code").IsRequired();
            entity.Property(e => e.SchemaVersion).HasColumnName("schema_version");
            entity.Property(e => e.Width).HasColumnName("width");
            entity.Property(e => e.Height).HasColumnName("height");
        });
    }

    private static void ConfigureMapTile(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MapTile>(entity =>
        {
            entity.ToTable("map_tiles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.MapId).HasColumnName("map_id");
            entity.Property(e => e.Row).HasColumnName("row");
            entity.Property(e => e.Col).HasColumnName("col");
            entity.Property(e => e.Terrain).HasColumnName("terrain").IsRequired();
            entity.Property(e => e.ResourceType).HasColumnName("resource_type");
            entity.Property(e => e.ResourceAmount).HasColumnName("resource_amount");

            entity.HasOne(e => e.Map)
                .WithMany(m => m.MapTiles)
                .HasForeignKey(e => e.MapId);
        });
    }

    private static void ConfigureGame(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Game>(entity =>
        {
            entity.ToTable("games");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.MapId).HasColumnName("map_id");
            entity.Property(e => e.MapSchemaVersion).HasColumnName("map_schema_version");
            entity.Property(e => e.TurnNo).HasColumnName("turn_no");
            entity.Property(e => e.ActiveParticipantId).HasColumnName("active_participant_id");
            entity.Property(e => e.TurnInProgress).HasColumnName("turn_in_progress");
            entity.Property(e => e.RngSeed).HasColumnName("rng_seed");
            entity.Property(e => e.RngVersion).HasColumnName("rng_version").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.FinishedAt).HasColumnName("finished_at");
            entity.Property(e => e.LastTurnAt).HasColumnName("last_turn_at");
            entity.Property(e => e.Settings).HasColumnName("settings").HasColumnType("jsonb");

            entity.HasOne(e => e.Map)
                .WithMany(m => m.Games)
                .HasForeignKey(e => e.MapId);

            entity.HasOne(e => e.ActiveParticipant)
                .WithMany()
                .HasForeignKey(e => e.ActiveParticipantId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureParticipant(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Participant>(entity =>
        {
            entity.ToTable("participants");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.GameId).HasColumnName("game_id");
            entity.Property(e => e.Kind).HasColumnName("kind").IsRequired();
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.DisplayName).HasColumnName("display_name").IsRequired();
            entity.Property(e => e.IsEliminated).HasColumnName("is_eliminated");

            entity.HasOne(e => e.Game)
                .WithMany(g => g.Participants)
                .HasForeignKey(e => e.GameId);
        });
    }

    private static void ConfigureUnitDefinition(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UnitDefinition>(entity =>
        {
            entity.ToTable("unit_definitions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Code).HasColumnName("code").IsRequired();
            entity.Property(e => e.IsRanged).HasColumnName("is_ranged");
            entity.Property(e => e.Attack).HasColumnName("attack");
            entity.Property(e => e.Defence).HasColumnName("defence");
            entity.Property(e => e.RangeMin).HasColumnName("range_min");
            entity.Property(e => e.RangeMax).HasColumnName("range_max");
            entity.Property(e => e.MovePoints).HasColumnName("move_points");
            entity.Property(e => e.Health).HasColumnName("health");
        });
    }

    private static void ConfigureUnit(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Unit>(entity =>
        {
            entity.ToTable("units");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.GameId).HasColumnName("game_id");
            entity.Property(e => e.ParticipantId).HasColumnName("participant_id");
            entity.Property(e => e.TypeId).HasColumnName("type_id");
            entity.Property(e => e.TileId).HasColumnName("tile_id");
            entity.Property(e => e.Hp).HasColumnName("hp");
            entity.Property(e => e.HasActed).HasColumnName("has_acted");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Game)
                .WithMany(g => g.Units)
                .HasForeignKey(e => e.GameId);

            entity.HasOne(e => e.Participant)
                .WithMany(p => p.Units)
                .HasForeignKey(e => e.ParticipantId);

            entity.HasOne(e => e.Type)
                .WithMany(t => t.Units)
                .HasForeignKey(e => e.TypeId);

            entity.HasOne(e => e.Tile)
                .WithMany(t => t.Units)
                .HasForeignKey(e => e.TileId);
        });
    }

    private static void ConfigureCity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<City>(entity =>
        {
            entity.ToTable("cities");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.GameId).HasColumnName("game_id");
            entity.Property(e => e.ParticipantId).HasColumnName("participant_id");
            entity.Property(e => e.TileId).HasColumnName("tile_id");
            entity.Property(e => e.Hp).HasColumnName("hp");
            entity.Property(e => e.MaxHp).HasColumnName("max_hp");

            entity.HasOne(e => e.Game)
                .WithMany(g => g.Cities)
                .HasForeignKey(e => e.GameId);

            entity.HasOne(e => e.Participant)
                .WithMany(p => p.Cities)
                .HasForeignKey(e => e.ParticipantId);

            entity.HasOne(e => e.Tile)
                .WithMany(t => t.Cities)
                .HasForeignKey(e => e.TileId);
        });
    }

    private static void ConfigureCityTile(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CityTile>(entity =>
        {
            entity.ToTable("city_tiles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.GameId).HasColumnName("game_id");
            entity.Property(e => e.CityId).HasColumnName("city_id");
            entity.Property(e => e.TileId).HasColumnName("tile_id");

            entity.HasOne(e => e.Game)
                .WithMany(g => g.CityTiles)
                .HasForeignKey(e => e.GameId);

            entity.HasOne(e => e.City)
                .WithMany(c => c.CityTiles)
                .HasForeignKey(e => e.CityId);

            entity.HasOne(e => e.Tile)
                .WithMany(t => t.CityTiles)
                .HasForeignKey(e => e.TileId);
        });
    }

    private static void ConfigureCityResource(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CityResource>(entity =>
        {
            entity.ToTable("city_resources");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CityId).HasColumnName("city_id");
            entity.Property(e => e.ResourceType).HasColumnName("resource_type").IsRequired();
            entity.Property(e => e.Amount).HasColumnName("amount");

            entity.HasOne(e => e.City)
                .WithMany(c => c.CityResources)
                .HasForeignKey(e => e.CityId);
        });
    }

    private static void ConfigureSave(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Save>(entity =>
        {
            entity.ToTable("saves");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.GameId).HasColumnName("game_id");
            entity.Property(e => e.Kind).HasColumnName("kind").IsRequired();
            entity.Property(e => e.Slot).HasColumnName("slot");
            entity.Property(e => e.TurnNo).HasColumnName("turn_no");
            entity.Property(e => e.ActiveParticipantId).HasColumnName("active_participant_id");
            entity.Property(e => e.SchemaVersion).HasColumnName("schema_version");
            entity.Property(e => e.MapCode).HasColumnName("map_code").IsRequired();
            entity.Property(e => e.State).HasColumnName("state").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.Game)
                .WithMany(g => g.Saves)
                .HasForeignKey(e => e.GameId);
        });
    }

    private static void ConfigureTurn(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Turn>(entity =>
        {
            entity.ToTable("turns");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.GameId).HasColumnName("game_id");
            entity.Property(e => e.TurnNo).HasColumnName("turn_no");
            entity.Property(e => e.ParticipantId).HasColumnName("participant_id");
            entity.Property(e => e.CommittedAt).HasColumnName("committed_at");
            entity.Property(e => e.DurationMs).HasColumnName("duration_ms");
            entity.Property(e => e.Summary).HasColumnName("summary").HasColumnType("jsonb");

            entity.HasOne(e => e.Game)
                .WithMany(g => g.Turns)
                .HasForeignKey(e => e.GameId);

            entity.HasOne(e => e.Participant)
                .WithMany(p => p.Turns)
                .HasForeignKey(e => e.ParticipantId);
        });
    }

    private static void ConfigureAnalyticsEvent(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnalyticsEvent>(entity =>
        {
            entity.ToTable("analytics_events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.EventType).HasColumnName("event_type").IsRequired();
            entity.Property(e => e.OccurredAt).HasColumnName("occurred_at");
            entity.Property(e => e.GameKey).HasColumnName("game_key");
            entity.Property(e => e.UserKey).HasColumnName("user_key").IsRequired().HasMaxLength(64).IsFixedLength();
            entity.Property(e => e.SaltVersion).HasColumnName("salt_version");
            entity.Property(e => e.TurnNo).HasColumnName("turn_no");
            entity.Property(e => e.MapCode).HasColumnName("map_code");
            entity.Property(e => e.MapSchemaVersion).HasColumnName("map_schema_version");
            entity.Property(e => e.RngSeed).HasColumnName("rng_seed");
            entity.Property(e => e.GameStartedAt).HasColumnName("game_started_at");
            entity.Property(e => e.GameFinishedAt).HasColumnName("game_finished_at");
            entity.Property(e => e.ParticipantCount).HasColumnName("participant_count");
            entity.Property(e => e.ClientRequestId).HasColumnName("client_request_id");
        });
    }

    private static void ConfigureSetting(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Setting>(entity =>
        {
            entity.ToTable("settings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AnalyticsSalt).HasColumnName("analytics_salt").IsRequired();
            entity.Property(e => e.SaltVersion).HasColumnName("salt_version");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });
    }
}


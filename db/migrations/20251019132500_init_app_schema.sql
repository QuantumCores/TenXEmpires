-- ============================================================================
-- Migration: 20251019132500_init_app_schema.sql
-- Purpose : Initialize schemas, roles, grants, core app tables, constraints,
--           indexes, helper functions, and Row-Level Security (RLS) policies
--           for the TenX Empires MVP.
-- Scope   : Creates app (game data) and references auth (Identity) schema.
--           Identity tables are NOT created here; FKs to auth.users are added
--           as NOT VALID to allow independent Identity migrations.
-- Notes   :
--   - All timestamps use timestamptz with DEFAULT now() unless noted.
--   - Primary keys are bigint identity except auth.users.id (uuid v7).
--   - RLS is ownership-based using app.games.user_id and a session GUC
--     (SET LOCAL app.user_id = '<uuid>').
--   - Public/shared tables (maps, map_tiles, unit_definitions, analytics_events,
--     settings) have RLS disabled per design; maps and unit_definitions are
--     read-only to app_user; analytics_events is write-only to app_user.
--   - Destructive commands are not used in this migration.
-- ============================================================================

-- Ensure schemas exist
create schema if not exists auth;
create schema if not exists app;

-- --------------------------------------------------------------------------
-- Note: Roles (app_user, app_admin, app_migrator) are created in the earlier
-- migration (20251019130000_create_identity_schema.sql).
-- --------------------------------------------------------------------------

-- Lock down schemas; explicit grants follow
revoke all on schema app from public;
revoke all on schema auth from public;

grant usage on schema app to app_user, app_admin, app_migrator;
grant usage on schema auth to app_user, app_admin, app_migrator;

-- --------------------------------------------------------------------------
-- Helper function: read current user id from session GUC (Grand Unified Config)
-- The backend sets: SET LOCAL app.user_id = '<uuid>' per transaction/request.
-- --------------------------------------------------------------------------
create or replace function app.current_user_id()
returns uuid
language sql
stable
as $$
  select current_setting('app.user_id', true)::uuid
$$;

-- ============================================================================
-- Tables
-- ============================================================================

-- Reference content: maps
create table if not exists app.maps (
  id               bigint generated always as identity primary key,
  code             text not null unique,
  schema_version   int  not null,
  width            int  not null,
  height           int  not null
);

-- Reference content: map tiles (odd-r coordinates)
create table if not exists app.map_tiles (
  id               bigint generated always as identity primary key,
  map_id           bigint not null references app.maps(id),
  row              int    not null,
  col              int    not null,
  terrain          text   not null,
  resource_type    text   null,
  resource_amount  int    not null default 0,
  constraint ux_map_tiles_unique unique (map_id, row, col)
);

-- Games (per-user top-level ownership)
create table if not exists app.games (
  id                     bigint generated always as identity primary key,
  user_id                uuid not null,
  map_id                 bigint not null references app.maps(id),
  map_schema_version     int    not null,
  turn_no                int    not null default 1,
  active_participant_id  bigint null,
  turn_in_progress       boolean not null default false,
  rng_seed               bigint not null,
  rng_version            text   not null default 'v1',
  status                 text   not null default 'active',
  started_at             timestamptz not null default now(),
  finished_at            timestamptz null,
  last_turn_at           timestamptz null,
  settings               jsonb not null default '{}'
);

-- Participants (owners of units/cities)
create table if not exists app.participants (
  id             bigint generated always as identity primary key,
  game_id        bigint not null,
  kind           text   not null,          -- 'human' | 'ai' (validated in app)
  user_id        uuid   null,              -- set for human; null for AI
  display_name   text   not null,
  is_eliminated  boolean not null default false,
  constraint fk_participants_games foreign key (game_id)
    references app.games(id) deferrable initially deferred
);

-- Unit definitions (static stats)
create table if not exists app.unit_definitions (
  id          bigint generated always as identity primary key,
  code        text    not null unique, -- e.g., 'warrior', 'slinger'
  is_ranged   boolean not null,
  attack      int     not null,
  defence     int     not null,
  range_min   int     not null,
  range_max   int     not null,
  move_points int     not null,
  health      int     not null         -- max HP
);

-- Units (runtime state)
create table if not exists app.units (
  id              bigint generated always as identity primary key,
  game_id         bigint not null,
  participant_id  bigint not null,
  type_id         bigint not null,
  tile_id         bigint not null,
  hp              int    not null,
  has_acted       boolean not null default false,
  updated_at      timestamptz not null default now(),
  constraint fk_units_games foreign key (game_id)
    references app.games(id) deferrable initially deferred,
  constraint fk_units_participants foreign key (participant_id)
    references app.participants(id),
  constraint fk_units_type foreign key (type_id)
    references app.unit_definitions(id),
  constraint fk_units_tile foreign key (tile_id)
    references app.map_tiles(id),
  constraint ux_units_1upt unique (game_id, tile_id) -- enforce 1UPT at rest
);

-- Cities (runtime state)
create table if not exists app.cities (
  id              bigint generated always as identity primary key,
  game_id         bigint not null,
  participant_id  bigint not null,
  tile_id         bigint not null,
  hp              int    not null,
  max_hp          int    not null,
  constraint fk_cities_games foreign key (game_id)
    references app.games(id) deferrable initially deferred,
  constraint fk_cities_participants foreign key (participant_id)
    references app.participants(id),
  constraint fk_cities_tile foreign key (tile_id)
    references app.map_tiles(id)
);

-- City tiles (precomputed membership including the city tile)
create table if not exists app.city_tiles (
  id        bigint generated always as identity primary key,
  game_id   bigint not null,
  city_id   bigint not null,
  tile_id   bigint not null,
  constraint fk_citytiles_games foreign key (game_id)
    references app.games(id) deferrable initially deferred,
  constraint fk_citytiles_city foreign key (city_id)
    references app.cities(id),
  constraint fk_citytiles_tile foreign key (tile_id)
    references app.map_tiles(id),
  constraint ux_citytiles unique (game_id, city_id, tile_id)
);

-- City resources (normalized 1 row per resource type)
create table if not exists app.city_resources (
  id             bigint generated always as identity primary key,
  city_id        bigint not null references app.cities(id),
  resource_type  text   not null,
  amount         int    not null default 0,
  constraint ux_city_resources unique (city_id, resource_type)
);

-- Saves (snapshots derived from normalized tables)
create table if not exists app.saves (
  id                     bigint generated always as identity primary key,
  user_id                uuid   not null,
  game_id                bigint not null,
  kind                   text   not null, -- 'manual' | 'autosave'
  slot                   int    null,     -- 1..3 for manual; null for autosave
  name                   text   not null, -- display name for manual saves; autogenerated for autosave
  turn_no                int    not null,
  active_participant_id  bigint not null,
  schema_version         int    not null,
  map_code               text   not null,
  state                  jsonb  not null,
  created_at             timestamptz not null default now(),
  constraint fk_saves_games foreign key (game_id)
    references app.games(id) deferrable initially deferred,
  constraint ck_saves_manual_slot check (kind <> 'manual' or (slot between 1 and 3))
);

-- Turns ledger (append-only, one per committed turn)
create table if not exists app.turns (
  id              bigint generated always as identity primary key,
  game_id         bigint not null,
  turn_no         int    not null,
  participant_id  bigint not null,
  committed_at    timestamptz not null default now(),
  duration_ms     int    null,
  summary         jsonb  null,
  constraint fk_turns_games foreign key (game_id)
    references app.games(id) deferrable initially deferred,
  constraint fk_turns_participant foreign key (participant_id)
    references app.participants(id),
  constraint ux_turns unique (game_id, turn_no)
);

-- Analytics (outside RLS by design; retained after account deletion)
create table if not exists app.analytics_events (
  id                 bigint generated always as identity primary key,
  event_type         text        not null,
  occurred_at        timestamptz not null default now(),
  game_key           bigint      not null,      -- copy of games.id; no FK
  user_key           char(64)    not null,      -- salted hash of user_id
  salt_version       int         not null,
  turn_no            int         null,
  map_code           text        null,
  map_schema_version int         null,
  rng_seed           bigint      null,
  game_started_at    timestamptz null,
  game_finished_at   timestamptz null,
  participant_count  int         null,
  client_request_id  uuid        null
);

-- Settings (e.g., analytics salt)
create table if not exists app.settings (
  id              bigint generated always as identity primary key,
  analytics_salt  bytea not null,
  salt_version    int   not null,
  updated_at      timestamptz not null default now()
);

-- --------------------------------------------------------------------------
-- Foreign keys added post-creation (where needed) for special behaviors
-- - auth.users FK: added NOT VALID to decouple from Identity migrations
-- - games.active_participant_id: FK to participants, DEFERRABLE
-- --------------------------------------------------------------------------

-- FK to auth.users (NOT VALID; validated after Identity tables exist)
do $$ begin
  if not exists (
    select 1 from pg_constraint c
    join pg_class t on t.oid = c.conrelid
    join pg_namespace n on n.oid = t.relnamespace
    where n.nspname = 'app' and t.relname = 'games' and c.conname = 'fk_games_users'
  ) then
    alter table app.games
      add constraint fk_games_users
      foreign key (user_id)
      references auth."AspNetUsers"("Id")
      on delete cascade
      not valid;
  end if;
end $$;

-- FK games.active_participant_id → participants(id) (DEFERRABLE)
do $$ begin
  if not exists (
    select 1 from pg_constraint c
    join pg_class t on t.oid = c.conrelid
    join pg_namespace n on n.oid = t.relnamespace
    where n.nspname = 'app' and t.relname = 'games' and c.conname = 'fk_games_active_participant'
  ) then
    alter table app.games
      add constraint fk_games_active_participant
      foreign key (active_participant_id)
      references app.participants(id)
      deferrable initially deferred;
  end if;
end $$;

-- ============================================================================
-- Indexes (beyond PKs)
-- ============================================================================

-- Saves: manual slot uniqueness per user/game
create unique index if not exists ux_saves_manual_slot
  on app.saves(user_id, game_id, slot)
  where kind = 'manual';

-- Analytics: idempotency on client_request_id when present
create unique index if not exists ux_analytics_client_req
  on app.analytics_events(client_request_id)
  where client_request_id is not null;

-- ============================================================================
-- Row-Level Security (RLS)
-- Design: single ownership-based policy per table (FOR ALL) using BOTH USING
-- and WITH CHECK, tied to app.games.user_id and the app.current_user_id() GUC.
-- We do NOT define per-role or per-action policies; roles are handled by GRANTs.
-- RLS is ENABLED and FORCED on protected tables. Public/shared tables keep RLS
-- disabled per design (maps, map_tiles, unit_definitions, analytics_events, settings).
-- ============================================================================

-- Enable and force RLS on protected tables
alter table app.games            enable row level security; alter table app.games            force row level security;
alter table app.participants     enable row level security; alter table app.participants     force row level security;
alter table app.units            enable row level security; alter table app.units            force row level security;
alter table app.cities           enable row level security; alter table app.cities           force row level security;
alter table app.city_tiles       enable row level security; alter table app.city_tiles       force row level security;
alter table app.city_resources   enable row level security; alter table app.city_resources   force row level security;
alter table app.saves            enable row level security; alter table app.saves            force row level security;
alter table app.turns            enable row level security; alter table app.turns            force row level security;

-- Games: owner-only visibility and mutation
do $$ begin
  if not exists (
    select 1 from pg_policies p
    where p.schemaname = 'app' and p.tablename = 'games' and p.policyname = 'games_owner_isolation'
  ) then
    create policy games_owner_isolation on app.games
      for all
      using (user_id = app.current_user_id())
      with check (user_id = app.current_user_id());
  end if;
end $$;

-- Child tables: access allowed only when row’s game belongs to current user
do $$ begin
  if not exists (
    select 1 from pg_policies p
    where p.schemaname = 'app' and p.tablename = 'participants' and p.policyname = 'by_game_ownership'
  ) then
    create policy by_game_ownership on app.participants
      for all
      using (exists (
        select 1 from app.games g
        where g.id = participants.game_id
          and g.user_id = app.current_user_id()
      ))
      with check (exists (
        select 1 from app.games g
        where g.id = participants.game_id
          and g.user_id = app.current_user_id()
      ));
  end if;
end $$;

do $$ begin
  if not exists (
    select 1 from pg_policies p
    where p.schemaname = 'app' and p.tablename = 'units' and p.policyname = 'by_game_ownership'
  ) then
    create policy by_game_ownership on app.units
      for all
      using (exists (
        select 1 from app.games g
        where g.id = units.game_id
          and g.user_id = app.current_user_id()
      ))
      with check (exists (
        select 1 from app.games g
        where g.id = units.game_id
          and g.user_id = app.current_user_id()
      ));
  end if;
end $$;

do $$ begin
  if not exists (
    select 1 from pg_policies p
    where p.schemaname = 'app' and p.tablename = 'cities' and p.policyname = 'by_game_ownership'
  ) then
    create policy by_game_ownership on app.cities
      for all
      using (exists (
        select 1 from app.games g
        where g.id = cities.game_id
          and g.user_id = app.current_user_id()
      ))
      with check (exists (
        select 1 from app.games g
        where g.id = cities.game_id
          and g.user_id = app.current_user_id()
      ));
  end if;
end $$;

do $$ begin
  if not exists (
    select 1 from pg_policies p
    where p.schemaname = 'app' and p.tablename = 'city_tiles' and p.policyname = 'by_game_ownership'
  ) then
    create policy by_game_ownership on app.city_tiles
      for all
      using (exists (
        select 1 from app.games g
        where g.id = city_tiles.game_id
          and g.user_id = app.current_user_id()
      ))
      with check (exists (
        select 1 from app.games g
        where g.id = city_tiles.game_id
          and g.user_id = app.current_user_id()
      ));
  end if;
end $$;

do $$ begin
  if not exists (
    select 1 from pg_policies p
    where p.schemaname = 'app' and p.tablename = 'city_resources' and p.policyname = 'by_game_ownership'
  ) then
    create policy by_game_ownership on app.city_resources
      for all
      using (exists (
        select 1 from app.games g
        join app.cities c on c.id = city_resources.city_id and c.game_id = g.id
        where g.user_id = app.current_user_id()
      ))
      with check (exists (
        select 1 from app.games g
        join app.cities c on c.id = city_resources.city_id and c.game_id = g.id
        where g.user_id = app.current_user_id()
      ));
  end if;
end $$;

do $$ begin
  if not exists (
    select 1 from pg_policies p
    where p.schemaname = 'app' and p.tablename = 'saves' and p.policyname = 'by_game_ownership'
  ) then
    create policy by_game_ownership on app.saves
      for all
      using (exists (
        select 1 from app.games g
        where g.id = saves.game_id
          and g.user_id = app.current_user_id()
      ))
      with check (exists (
        select 1 from app.games g
        where g.id = saves.game_id
          and g.user_id = app.current_user_id()
      ));
  end if;
end $$;

do $$ begin
  if not exists (
    select 1 from pg_policies p
    where p.schemaname = 'app' and p.tablename = 'turns' and p.policyname = 'by_game_ownership'
  ) then
    create policy by_game_ownership on app.turns
      for all
      using (exists (
        select 1 from app.games g
        where g.id = turns.game_id
          and g.user_id = app.current_user_id()
      ))
      with check (exists (
        select 1 from app.games g
        where g.id = turns.game_id
          and g.user_id = app.current_user_id()
      ));
  end if;
end $$;

-- ============================================================================
-- Grants (post-RLS):
-- - app_user: full DML on protected tables (RLS restricts rows), read-only on
--   maps/map_tiles/unit_definitions, insert-only on analytics_events.
-- - app_admin: broad access (bypasses RLS), used only by maintenance procs.
-- - app_migrator: DDL/seeding; typically run outside of application runtime.
-- ============================================================================

-- Read-only reference data for app_user
grant select on app.maps, app.map_tiles, app.unit_definitions to app_user;

-- Protected tables: allow DML; RLS enforces row visibility
grant select, insert, update, delete on
  app.games,
  app.participants,
  app.units,
  app.cities,
  app.city_tiles,
  app.city_resources,
  app.saves,
  app.turns
to app_user;

-- Analytics: app_user writes only (no select to minimize data exfiltration)
grant insert on app.analytics_events to app_user;

-- Settings typically admin-only
grant select on app.settings to app_admin;

-- Sequence usage for identity columns
grant usage, select on all sequences in schema app to app_user;
grant usage, select on all sequences in schema app to app_admin;

-- Admin read over analytics
grant select, insert, update, delete on app.analytics_events to app_admin;

-- Migrator broad DDL/DML in app schema
grant select, insert, update, delete on all tables in schema app to app_migrator;
grant usage, select on all sequences in schema app to app_migrator;

-- ============================================================================
-- End of migration
-- ============================================================================


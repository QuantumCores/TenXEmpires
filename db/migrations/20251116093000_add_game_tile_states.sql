-- ============================================================================
-- Migration: 20251116093000_add_game_tile_states.sql
-- Purpose : Track mutable per-game tile resource state instead of mutating the
--           shared map_tiles template data.
-- Notes   :
--   - Adds app.game_tile_states with RLS/grants mirroring other game-owned tables.
--   - Backfills existing games by cloning the template map tile resource amounts.
-- ============================================================================

create table if not exists app.game_tile_states (
  id               bigint generated always as identity primary key,
  game_id          bigint not null references app.games(id) on delete cascade,
  tile_id          bigint not null references app.map_tiles(id) on delete cascade,
  resource_amount  int    not null default 0,
  constraint ux_game_tile_states unique (game_id, tile_id)
);

create index if not exists ix_game_tile_states_game on app.game_tile_states(game_id);

-- Backfill existing games by copying template map tile amounts (only tiles with resources)
insert into app.game_tile_states (game_id, tile_id, resource_amount)
select g.id, t.id, t.resource_amount
from app.games g
join app.map_tiles t on t.map_id = g.map_id
where (t.resource_type is not null or t.resource_amount > 0)
on conflict do nothing;

-- Enable RLS for ownership-based access control
alter table app.game_tile_states enable row level security;
alter table app.game_tile_states force row level security;

do $$ begin
  if not exists (
    select 1 from pg_policies
    where schemaname = 'app'
      and tablename = 'game_tile_states'
      and policyname = 'by_game_ownership'
  ) then
    create policy by_game_ownership on app.game_tile_states
      for all
      using (exists (
        select 1 from app.games g
        where g.id = game_tile_states.game_id
          and g.user_id = app.current_user_id()
      ))
      with check (exists (
        select 1 from app.games g
        where g.id = game_tile_states.game_id
          and g.user_id = app.current_user_id()
      ));
  end if;
end $$;

-- Grants
grant select, insert, update, delete on app.game_tile_states to app_user;
grant select, insert, update, delete on app.game_tile_states to app_admin;
grant select, insert, update, delete on app.game_tile_states to app_migrator;

grant usage, select on sequence app.game_tile_states_id_seq to app_user;
grant usage, select on sequence app.game_tile_states_id_seq to app_admin;
grant usage, select on sequence app.game_tile_states_id_seq to app_migrator;

-- ============================================================================
-- End of migration
-- ============================================================================

-- RLS Smoke Test Script
-- Purpose: Validate ownership-based Row-Level Security (RLS) policies using psql.
-- Usage:
--   psql "$TENX_DB_CONNECTION" -v ON_ERROR_STOP=1 -f db/testing/rls-smoke.sql
-- Prereqs:
--   - Run after the initial migration is applied.
--   - Connect as a login that is a member of role app_user (e.g., tenx_app).
--   - The script seeds a reference map if missing.

\echo '== Seeding minimal reference data (map) as current user =='
insert into app.maps(code, schema_version, width, height)
values ('fixed-20x15', 1, 20, 15)
on conflict (code) do nothing;

select id as map_id from app.maps where code = 'fixed-20x15';
\gset

-- Define two test user IDs (no need to exist in auth.users for this test)
\set user_a '00000000-0000-0000-0000-0000000000a1'
\set user_b '00000000-0000-0000-0000-0000000000a2'

\echo '== Create a game as user A (should succeed) =='
begin;
  set local app.user_id = :'user_a';
  insert into app.games(user_id, map_id, map_schema_version, rng_seed)
  values (:'user_a', :map_id, 1, 42)
  returning id as game_id_a;
\gset
commit;

\echo '== Create a game as user B (should succeed) =='
begin;
  set local app.user_id = :'user_b';
  insert into app.games(user_id, map_id, map_schema_version, rng_seed)
  values (:'user_b', :map_id, 1, 84)
  returning id as game_id_b;
\gset
commit;

\echo '== Verify user A can only see their own game ==' 
begin;
  set local app.user_id = :'user_a';
  select id, user_id from app.games order by id;  -- expect only :game_id_a
commit;

\echo '== Attempt to UPDATE user B\'s game as user A (should affect 0 rows) =='
begin;
  set local app.user_id = :'user_a';
  update app.games set status = 'won' where id = :game_id_b;
  \echo 'rows updated: ' :ROWCOUNT  -- expect 0
commit;

\echo '== Attempt to INSERT a save for user B\'s game as user A (should ERROR) =='
-- This should be rejected by the WITH CHECK policy. Expect an error.
begin;
  set local app.user_id = :'user_a';
  insert into app.saves(user_id, game_id, kind, slot, turn_no, active_participant_id, schema_version, map_code, state)
  values (:'user_a', :game_id_b, 'manual', 1, 1, 0, 1, 'fixed-20x15', '{}'::jsonb);
rollback;  -- rollback in case the above was not executed due to error

\echo '== RLS smoke test completed ==' 


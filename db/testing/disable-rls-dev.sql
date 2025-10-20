-- ============================================================================
-- Development helper: Disable RLS for easier debugging
-- WARNING: Only use in development! Never in production!
-- ============================================================================

-- Disable RLS on all protected tables
alter table app.games            disable row level security;
alter table app.participants     disable row level security;
alter table app.units            disable row level security;
alter table app.cities           disable row level security;
alter table app.city_tiles       disable row level security;
alter table app.city_resources   disable row level security;
alter table app.saves            disable row level security;
alter table app.turns            disable row level security;

-- Note: This only disables RLS for non-superusers who are not subject to FORCE RLS
-- To fully bypass, use a role with BYPASSRLS or connect as superuser


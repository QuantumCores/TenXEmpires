-- ============================================================================
-- Development helper: Re-enable RLS after debugging
-- ============================================================================

-- Re-enable RLS on all protected tables
alter table app.games            enable row level security;
alter table app.participants     enable row level security;
alter table app.units            enable row level security;
alter table app.cities           enable row level security;
alter table app.city_tiles       enable row level security;
alter table app.city_resources   enable row level security;
alter table app.saves            enable row level security;
alter table app.turns            enable row level security;

-- Re-force RLS (even for table owners)
alter table app.games            force row level security;
alter table app.participants     force row level security;
alter table app.units            force row level security;
alter table app.cities           force row level security;
alter table app.city_tiles       force row level security;
alter table app.city_resources   force row level security;
alter table app.saves            force row level security;
alter table app.turns            force row level security;


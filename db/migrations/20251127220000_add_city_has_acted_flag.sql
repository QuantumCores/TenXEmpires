-- ============================================================================
-- Migration: 20251127220000_add_city_has_acted_flag.sql
-- Purpose  : Add per-city action flag for manual production limits
-- ============================================================================

ALTER TABLE app.cities
    ADD COLUMN IF NOT EXISTS has_acted_this_turn BOOLEAN NOT NULL DEFAULT FALSE;

-- ============================================================================
-- End of migration
-- ============================================================================

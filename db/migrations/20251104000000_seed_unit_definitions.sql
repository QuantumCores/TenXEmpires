-- ============================================================================
-- Migration: 20251104000000_seed_unit_definitions.sql
-- Purpose : Seed initial unit definitions for MVP gameplay
-- Notes   :
--   - warrior: Melee unit with balanced stats
--   - slinger: Ranged unit with lower defense
--   - All units have 2 move points for MVP simplicity
--   - Health values represent maximum HP
-- ============================================================================

-- Insert unit definitions (idempotent with ON CONFLICT DO NOTHING)
INSERT INTO app.unit_definitions (code, is_ranged, attack, defence, range_min, range_max, move_points, health)
VALUES
    ('warrior', false, 20, 10, 0, 0, 2, 100),
    ('slinger', true,  15,  8, 1, 2, 2,  80)
ON CONFLICT (code) DO NOTHING;

-- Verify insertion
DO $$
DECLARE
    v_count int;
BEGIN
    SELECT count(*) INTO v_count FROM app.unit_definitions WHERE code IN ('warrior', 'slinger');
    
    IF v_count = 2 THEN
        RAISE NOTICE 'Successfully seeded % unit definitions', v_count;
    ELSE
        RAISE WARNING 'Expected 2 unit definitions, but found %', v_count;
    END IF;
END $$;

-- ============================================================================
-- End of migration
-- ============================================================================


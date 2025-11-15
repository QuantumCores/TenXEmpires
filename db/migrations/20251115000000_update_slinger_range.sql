-- ============================================================================
-- Migration: 20251115000000_update_slinger_range.sql
-- Purpose : Allow slingers to attack adjacent targets by lowering range_min
-- Notes   :
--   - Slinger should be able to fire at distance 1 or 2.
-- ============================================================================

UPDATE app.unit_definitions
SET range_min = 1,
    range_max = 2
WHERE code = 'slinger';

-- Echo result so we know the update ran
DO $$
DECLARE
    v_range_min int;
    v_range_max int;
BEGIN
    SELECT range_min, range_max
    INTO v_range_min, v_range_max
    FROM app.unit_definitions
    WHERE code = 'slinger';

    RAISE NOTICE 'Slinger range updated to min %, max %', v_range_min, v_range_max;
END $$;

-- ============================================================================
-- End of migration
-- ============================================================================

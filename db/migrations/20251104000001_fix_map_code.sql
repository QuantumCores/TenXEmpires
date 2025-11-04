-- ============================================================================
-- Migration: 20251104000001_fix_map_code.sql
-- Purpose : Fix map code from 'initial' to 'standard_15x20' to match app config
-- ============================================================================

-- Update the map code
UPDATE app.maps 
SET code = 'standard_15x20' 
WHERE code = 'initial';

-- Verify the update
DO $$
DECLARE
    v_count int;
BEGIN
    SELECT count(*) INTO v_count FROM app.maps WHERE code = 'standard_15x20';
    
    IF v_count > 0 THEN
        RAISE NOTICE 'Successfully updated map code to "standard_15x20"';
    ELSE
        RAISE WARNING 'Map code update may have failed';
    END IF;
END $$;

-- ============================================================================
-- End of migration
-- ============================================================================


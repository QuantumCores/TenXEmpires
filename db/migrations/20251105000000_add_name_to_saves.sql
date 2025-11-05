-- ============================================================================
-- Migration: 20251105000000_add_name_to_saves.sql
-- Purpose : Add missing 'name' column to app.saves table
-- ============================================================================

-- Check if the column already exists before adding it
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'app' 
        AND table_name = 'saves' 
        AND column_name = 'name'
    ) THEN
        -- Add the name column
        ALTER TABLE app.saves 
        ADD COLUMN name text NOT NULL DEFAULT '';
        
        RAISE NOTICE 'Added name column to app.saves';
    ELSE
        RAISE NOTICE 'Column name already exists in app.saves';
    END IF;
END $$;

-- ============================================================================
-- End of migration
-- ============================================================================


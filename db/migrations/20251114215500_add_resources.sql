-- ============================================================================
-- Migration: 20251104000001_fix_map_code.sql
-- Purpose : Fix map missing resources
-- ============================================================================

-- Change water tiles into grassland
UPDATE app.map_tiles
	SET terrain = 'grassland'
	WHERE terrain = 'water';

-- Add grassland around cities for more space for resources
UPDATE APP.MAP_TILES
	SET
	TERRAIN = 'grassland'
	WHERE
	ROW = 4	AND COL = 1
	OR ROW = 3	AND COL = 2
	OR ROW = 12	AND COL = 18
	OR ROW = 13	AND COL = 16;

-- Add wheat resources
UPDATE app.map_tiles
	SET resource_type = 'wheat', resource_amount = 100
	WHERE ROW = 4	AND COL = 1
	OR ROW = 3	AND COL = 2
	OR ROW = 12	AND COL = 18
	OR ROW = 13	AND COL = 16;

-- Add stone resources
UPDATE app.map_tiles
	SET resource_type = 'stone', resource_amount = 100
	WHERE ROW = 5	AND COL = 2
	OR ROW = 12	AND COL = 16;

-- Add iron resources
UPDATE app.map_tiles
	SET resource_type = 'iron', resource_amount = 100
	WHERE ROW = 6	AND COL = 1
	OR ROW = 10	AND COL = 18;
	
-- ============================================================================
-- End of migration
-- ============================================================================

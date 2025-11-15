-- ============================================================================
-- Migration: 20251031000000_seed_initial_map.sql
-- Purpose : Seed initial map (20x15 hexagonal, odd-r) with terrain distribution
-- Notes   :
--   - Map code: 'standard_15x20', schema_version: 1
--   - Ocean tiles distributed in outer 3 layers with probability:
--     * Layer 0 (outermost): 90% ocean
--     * Layer 1 (second):    50% ocean
--     * Layer 2 (third):     25% ocean
--     * Inner layers:        0% ocean
--   - Layer calculation: min(row, height-1-row, col, width-1-col)
--   - Non-ocean terrains: desert, grassland, tropical, tundra, water
--   - Resources left NULL for game-time generation
--   - Uses seeded random for deterministic terrain generation
-- ============================================================================

DO $$
DECLARE
    v_map_id bigint;
    v_row int;
    v_col int;
    v_layer int;
    v_terrain text;
    v_rand float;
    v_width constant int := 20;
    v_height constant int := 15;
    v_terrains constant text[] := ARRAY['desert', 'grassland', 'tropical', 'tundra'];
BEGIN
    -- Set seed for reproducible terrain generation
    PERFORM setseed(0.42);
    
    -- Insert map entry
    INSERT INTO app.maps (code, schema_version, width, height)
    VALUES ('standard_15x20', 1, v_width, v_height)
    ON CONFLICT (code) DO NOTHING
    RETURNING id INTO v_map_id;
    
    -- If map already exists, retrieve its id
    IF v_map_id IS NULL THEN
        SELECT id INTO v_map_id FROM app.maps WHERE code = 'standard_15x20';
        RAISE NOTICE 'Map "standard_15x20" already exists with id %', v_map_id;
        RETURN;
    END IF;
    
    RAISE NOTICE 'Creating map tiles for map id % (standard_15x20)', v_map_id;
    
    -- Generate tiles for 20x15 map (columns 0-19, rows 0-14)
    FOR v_row IN 0..(v_height - 1) LOOP
        FOR v_col IN 0..(v_width - 1) LOOP
            -- Calculate layer (distance from nearest edge)
            v_layer := LEAST(
                v_row,                    -- distance from top
                v_height - 1 - v_row,     -- distance from bottom
                v_col,                    -- distance from left
                v_width - 1 - v_col       -- distance from right
            );
            
            -- Generate random value for ocean probability check
            v_rand := random();
            
            -- Determine terrain based on layer and ocean probability
            IF v_layer = 0 THEN
                -- Outermost layer: 90% ocean, 10% other terrains
                IF v_rand < 0.9 THEN
                    v_terrain := 'ocean';
                ELSE
                    v_terrain := v_terrains[1 + floor(random() * cardinality(v_terrains))::int];
                END IF;
            ELSIF v_layer = 1 THEN
                -- Second layer: 50% ocean, 50% other terrains
                IF v_rand < 0.5 THEN
                    v_terrain := 'ocean';
                ELSE
                    v_terrain := v_terrains[1 + floor(random() * cardinality(v_terrains))::int];
                END IF;
            ELSIF v_layer = 2 THEN
                -- Third layer: 25% ocean, 75% other terrains
                IF v_rand < 0.25 THEN
                    v_terrain := 'ocean';
                ELSE
                    v_terrain := v_terrains[1 + floor(random() * cardinality(v_terrains))::int];
                END IF;
            ELSE
                -- Inner layers: no ocean, only land terrains
                v_terrain := v_terrains[1 + floor(random() * cardinality(v_terrains))::int];
            END IF;
            
            -- Insert tile (resources left NULL for game-time generation)
            INSERT INTO app.map_tiles (map_id, row, col, terrain, resource_type, resource_amount)
            VALUES (v_map_id, v_row, v_col, v_terrain, NULL, 0);
        END LOOP;
    END LOOP;
    
    RAISE NOTICE 'Successfully created % tiles for map "standard_15x20"', v_width * v_height;
END $$;

-- ============================================================================
-- End of migration
-- ============================================================================


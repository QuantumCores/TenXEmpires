# PNG Image Integration for Game Map

## Summary

The rendering code is **now fully ready** for PNG images! The implementation includes:

✅ **Image Loading System** - Automatic preloading and caching  
✅ **Fallback Rendering** - Works with or without PNG files  
✅ **Layer Support** - Terrain, features, cities can all use PNGs  
✅ **Performance Optimized** - Images cached after first load  

## What You Need to Do

### 1. Create Image Directories

Create these folders in your project:

```
tenxempires.client/public/images/game/
├── terrain/
├── feature/
├── city/
└── unit/
```

### 2. Add Your PNG Files

Place your PNG files in the appropriate directories with these names:

#### Terrain Tiles (Layer 1)
```
terrain/grassland.png    # For grassland hexes
terrain/plains.png       # For plains hexes
terrain/desert.png       # For desert hexes
terrain/tundra.png       # For tundra hexes
terrain/ocean.png        # For ocean hexes
terrain/coast.png        # For coast hexes
terrain/mountain.png     # For mountain hexes
terrain/hill.png         # For hill hexes
```

#### Features (Layer 3)
```
feature/forest.png       # For forest features
feature/jungle.png       # For jungle features
feature/marsh.png        # For marsh features
feature/oasis.png        # For oasis features
feature/ice.png          # For ice features
```

#### Cities (Layer 3)
```
city/city.png            # Prehistoric era settlement - Default
city/city-ancient.png    # (Optional) Ancient era cities
city/city-medieval.png   # (Optional) Medieval era cities
city/city-modern.png     # (Optional) Modern era cities
```

### 3. Image Specifications

**Terrain Hexes:**
- Size: ~64x56 pixels (or 128x112 for retina displays)
- Format: PNG (transparency optional)
- Shape: Should cover a pointy-top hexagon
- The image will be centered and scaled to fit the hex

**Features & Cities:**
- Size: ~40x40 pixels (or 80x80 for retina)
- Format: PNG with transparency recommended
- The image will be centered on the hex

### 4. Update the Manifest (if adding new types)

If you add new terrain types or features, update `src/features/game/imageLoader.ts`:

```typescript
export const DEFAULT_MANIFEST: ImageManifest = {
  terrain: [
    'grassland',
    'plains',
    // ... add your new types here
  ],
  feature: [
    'forest',
    'jungle',
    // ... add your new features here
  ],
  // ...
}
```

## How It Works

### Rendering Flow

1. **Preload**: When game map loads, all images are preloaded asynchronously
2. **Render**: For each tile/feature:
   - Try to load PNG image
   - If PNG exists: draw the PNG
   - If PNG missing: draw canvas-based fallback (colored hexagons/shapes)
3. **Cache**: Once loaded, images are cached for the entire session

### Example Code Flow

```typescript
// In renderTiles():
const terrainImage = imageLoader.getImage('terrain', tile.terrain.toLowerCase())

if (terrainImage) {
  // Draw your beautiful PNG
  ctx.drawImage(terrainImage, -imageSize, -imageSize, imageSize * 2, imageSize * 2)
} else {
  // Draw fallback colored hexagon
  // (this is what currently displays)
}
```

## Current Layer Stack

**Your rendering layers (in order):**

1. **TileLayer** - Terrain hex backgrounds (PNG support ✅)
2. **GridLayer** - Hex grid lines (canvas-drawn, no PNG needed)
3. **FeatureLayer** - Cities, forests, etc. (PNG support ✅)
4. **UnitLayer** - Units (canvas-drawn currently, PNG support can be added)
5. **OverlayLayer** - Selection highlights, paths (canvas-drawn, no PNG needed)

## Testing

**Without PNGs:**
- Game works normally with colored hexagons (current behavior)
- No errors or warnings

**With PNGs:**
- Drop PNG files into the folders
- Refresh the page
- Images will automatically display

**Mixed Mode:**
- Can have some PNGs and not others
- Missing PNGs fall back to canvas drawing
- No need to have all images ready at once

## Performance

- **Initial Load**: Images preload asynchronously (non-blocking)
- **Rendering**: PNG images are faster than canvas drawing
- **Memory**: Images cached once, reused for all tiles of that type
- **No Penalty**: Missing images don't slow anything down

## Future Enhancements

If needed, you can extend the system to:
- Add unit PNG sprites (just update `renderUnits` function)
- Support animated GIFs or sprite sheets
- Add different city sprites based on era/civilization
- Implement terrain features (forests, resources) as separate PNGs

## File References

**Implementation Files:**
- `src/features/game/imageLoader.ts` - Image loading system
- `src/components/game/MapCanvasStack.tsx` - Rendering with PNG support
- `public/images/game/README.md` - Detailed image specifications

**To Test:**
1. Create `public/images/game/terrain/grassland.png`
2. Start the app
3. Any grassland hexes will now show your PNG instead of green rectangles!


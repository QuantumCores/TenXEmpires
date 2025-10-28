# Game Image Assets

This directory contains PNG images for game rendering on the canvas.

## Directory Structure

```
public/images/game/
├── terrain/          # Terrain hex tiles (bottom layer)
│   ├── grassland.png
│   ├── plains.png
│   ├── desert.png
│   ├── tundra.png
│   ├── ocean.png
│   ├── coast.png
│   ├── mountain.png
│   └── hill.png
├── feature/          # Terrain features (third layer)
│   ├── forest.png
│   ├── jungle.png
│   ├── marsh.png
│   ├── oasis.png
│   └── ice.png
├── city/             # Cities (third layer)
│   ├── city.png
│   ├── city-ancient.png
│   ├── city-medieval.png
│   └── city-modern.png
└── unit/             # Units (fourth layer)
    └── (optional unit sprites)
```

## Image Specifications

### Terrain Tiles
- **Size**: Approximately 64x56 pixels (to match HEX_SIZE * 2)
- **Format**: PNG with transparency (if needed)
- **Shape**: Should cover a pointy-top hexagon
- **Naming**: Must match terrain type from backend (lowercase)
  - Examples: `grassland.png`, `plains.png`, `desert.png`

### Features
- **Size**: Similar to terrain (64x56 pixels)
- **Format**: PNG with transparency
- **Rendering**: Drawn on top of terrain tiles
- **Examples**: forests, jungles, resources

### Cities
- **Size**: Approximately 40x40 pixels
- **Format**: PNG with transparency
- **Centered**: Image will be centered on the hex

### Units (Optional)
- **Size**: Approximately 40x40 pixels  
- **Format**: PNG with transparency
- **Centered**: Image will be centered on the hex

## How It Works

1. **Automatic Loading**: Images are preloaded when the game map loads
2. **Fallback Rendering**: If a PNG is not found, the system falls back to canvas-drawn shapes
3. **Performance**: Images are cached after first load for optimal performance
4. **Layers**: 
   - Layer 1 (Tiles): Terrain PNG images
   - Layer 2 (Grid): Canvas-drawn grid (no images)
   - Layer 3 (Features): City/feature PNG images
   - Layer 4 (Units): Unit sprites (drawn or PNG)
   - Layer 5 (Overlays): Selection/preview (canvas-drawn)

## Adding New Images

1. Add your PNG file to the appropriate directory
2. Update the manifest in `src/features/game/imageLoader.ts`:

```typescript
export const DEFAULT_MANIFEST: ImageManifest = {
  terrain: [
    'grassland',
    'plains',
    'desert',
    'tundra',
    'ocean',
    'coast',
    'mountain',
    'hill',
    'your-new-terrain', // Add here
  ],
  feature: [
    'forest',
    'jungle',
    'your-new-feature', // Add here
  ],
  // ... etc
}
```

3. The image will be automatically loaded and used

## Image Guidelines

- **Transparent Backgrounds**: Use PNG transparency for features, cities, and units
- **Hex Shape**: Terrain tiles should cover the entire hexagon
- **Consistent Style**: Maintain visual consistency across all tiles
- **File Size**: Keep files optimized (recommend < 50KB per image)
- **Resolution**: Use 2x resolution for retina displays (128x112 for terrain)

## Current Behavior

- **Terrain**: PNG images if available, otherwise solid colored hexagons
- **Grid**: Always canvas-drawn (no PNG support needed)
- **Cities**: PNG images if available, otherwise canvas-drawn circles
- **Units**: Currently canvas-drawn (can be enhanced with PNGs)
- **Overlays**: Always canvas-drawn (selection highlights, paths, etc.)

## Performance Notes

- Images are preloaded on game map mount
- Loaded images are cached for the entire session
- Fallback to canvas drawing ensures game always works
- No performance impact from missing images


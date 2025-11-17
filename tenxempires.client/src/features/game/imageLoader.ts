/**
 * Image loader for hex tiles and features.
 * Preloads and caches PNG images for rendering on canvas.
 */

export type ImageCategory = 'terrain' | 'feature' | 'unit' | 'city' | 'resources'

interface ImageManifest {
  terrain: string[]
  feature: string[]
  unit: string[]
  city: string[]
  resources: string[]
}

class ImageLoader {
  private images = new Map<string, HTMLImageElement>()
  private loading = new Map<string, Promise<HTMLImageElement>>()
  private basePath: string

  constructor(basePath: string = '/images/game') {
    this.basePath = basePath
  }

  /**
   * Gets the full path for an image
   */
  private getImagePath(category: ImageCategory, name: string): string {
    return `${this.basePath}/${category}/${name}.png`
  }

  /**
   * Generates a cache key for an image
   */
  private getCacheKey(category: ImageCategory, name: string): string {
    return `${category}:${name}`
  }

  /**
   * Loads a single image
   */
  private async loadImage(category: ImageCategory, name: string): Promise<HTMLImageElement> {
    const cacheKey = this.getCacheKey(category, name)

    // Return cached image if available
    if (this.images.has(cacheKey)) {
      return this.images.get(cacheKey)!
    }

    // Return existing promise if already loading
    if (this.loading.has(cacheKey)) {
      return this.loading.get(cacheKey)!
    }

    // Start loading
    const promise = new Promise<HTMLImageElement>((resolve, reject) => {
      const img = new Image()
      const path = this.getImagePath(category, name)

      img.onload = () => {
        this.images.set(cacheKey, img)
        this.loading.delete(cacheKey)
        resolve(img)
      }

      img.onerror = () => {
        this.loading.delete(cacheKey)
        reject(new Error(`Failed to load image: ${path}`))
      }

      img.src = path
    })

    this.loading.set(cacheKey, promise)
    return promise
  }

  /**
   * Loads multiple images for a category
   */
  async loadCategory(category: ImageCategory, names: string[]): Promise<void> {
    const promises = names.map(async (name) => {
      try {
        return await this.loadImage(category, name)
      } catch (error) {
        const path = this.getImagePath(category, name)
        console.warn(`Failed to load image: ${path}`, error)
        throw error
      }
    })
    await Promise.allSettled(promises)
  }

  /**
   * Preloads all images from manifest
   */
  async preloadManifest(manifest: ImageManifest): Promise<void> {
    const promises: Promise<void>[] = []

    for (const category of Object.keys(manifest) as ImageCategory[]) {
      promises.push(this.loadCategory(category, manifest[category]))
    }

    await Promise.allSettled(promises)
  }

  /**
   * Gets a loaded image, returns null if not loaded
   */
  getImage(category: ImageCategory, name: string): HTMLImageElement | null {
    const cacheKey = this.getCacheKey(category, name)
    const image = this.images.get(cacheKey) || null
    // Debug: log if image not found in development
    if (process.env.NODE_ENV === 'development' && !image) {
      const path = this.getImagePath(category, name)
      console.debug(`Image not in cache: ${path} (key: ${cacheKey})`)
    }
    return image
  }

  /**
   * Checks if an image is loaded
   */
  isLoaded(category: ImageCategory, name: string): boolean {
    const cacheKey = this.getCacheKey(category, name)
    return this.images.has(cacheKey)
  }

  /**
   * Gets loading progress (0-1)
   */
  getProgress(): number {
    const total = this.images.size + this.loading.size
    if (total === 0) return 1
    return this.images.size / total
  }

  /**
   * Clears all cached images
   */
  clear(): void {
    this.images.clear()
    this.loading.clear()
  }
}

// Global image loader instance
let globalImageLoader: ImageLoader | null = null

export function getGlobalImageLoader(): ImageLoader {
  if (!globalImageLoader) {
    globalImageLoader = new ImageLoader()
  }
  return globalImageLoader
}

/**
 * Default image manifest for the game
 * Add your terrain types, features, etc. here
 */
export const DEFAULT_MANIFEST: ImageManifest = {
  terrain: [
    'desert',
    'grassland',
    'ocean',
    'tropical',
    'tundra',
    'water',
  ],
  feature: [
    // Features will use fallback sprites for now
  ],
  unit: [
    'slinger',
    'warrior', // Note: filename has typo (should be "warrior")
  ],
  city: [
    'human',
    'enemy',
  ],
  resources: [
    'wood',
    'stone',
    'wheat',
    'iron',
  ],
}


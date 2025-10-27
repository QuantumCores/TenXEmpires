# View Implementation Plan Gallery

Reference: See [01 - routing-and-modal-framework-implementation-plan.md](./01 - routing-and-modal-framework-implementation-plan.md) for shared routing and modal framework implementation details.

## 1. Overview
Public gallery page showing screenshots/art with captions, accessible without authentication.

## 2. View Routing
- Path: `/gallery`

## 3. Component Structure
- `GalleryPage`
  - `GalleryHeader`
  - `ImageGrid`
    - `ImageCard` × N
  - `Lightbox` (optional)
  - `FooterLinks`

## 4. Component Details
### GalleryPage
- Description: Static page with grid of images.
- Main elements: Grid container, cards, optional lightbox overlay.
- Handled interactions: Click to open lightbox; keyboard navigation in lightbox.
- Handled validation: N/A.
- Types: `GalleryItem` (UI type).
- Props: None.

### ImageGrid / ImageCard
- Description: Responsive grid; each card shows an image with alt text and caption.
- Main elements: `<img>` with `alt`, caption.
- Interactions: Click to open in lightbox.
- Validation: Ensure alt text present.
- Types: `GalleryItem` `{ src: string; alt: string; caption?: string }`.
- Props: `{ items: GalleryItem[] }`.

### Lightbox (optional)
- Description: Fullscreen overlay to view image.
- Main elements: Overlay container, image, caption, close button.
- Interactions: Click outside/Esc to close; arrows to navigate.
- Validation: Focus management.
- Types: `GalleryItem`.
- Props: `{ current: GalleryItem; onClose() }`.

## 5. Types
- `GalleryItem` UI-only type as above.

## 6. State Management
- Local component state for lightbox visibility and current item index.

## 7. API Integration
- None (static assets bundled or hosted; no API requirement in PRD).

## 8. User Interactions
- Click image to open lightbox; navigate; close.

## 9. Conditions and Validation
- Ensure keyboard accessibility for lightbox; alt text required.

## 10. Error Handling
- Broken image handling with fallback placeholder.

## 11. Implementation Steps
1. Scaffold page with responsive grid.
2. Add ImageCard with alt/caption.
3. Implement Lightbox with focus trap and keyboard support.
4. Content outline: Include 4–8 images showing (a) map rendering at 1080p with grid off, (b) unit selection and path preview, (c) city reach overlay (radius-2), (d) end-turn UI with autosave toast, (e) Saves modal. Provide meaningful alt text; use lazy loading and appropriate sizes.

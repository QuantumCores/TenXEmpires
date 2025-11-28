# US-101: Resource Harvest and Cap - Implementation Plan

## Overview

This document provides a step-by-step implementation guide for the "Resource Harvest and Cap" user story. The feature ensures that resources (Wheat, Wood, Stone, Iron) are harvested from controlled tiles at the end of each turn, with a storage cap of 100 per resource type.

### User Story
> As a player, I want to accumulate resources up to a limit so I can save for actions.

### Acceptance Criteria
- Resources are harvested from all controlled tiles at the end of the turn
- Storage is capped at 100 per resource type
- Resources are not harvested if the cap is reached

---

## 1. Client-Side Step-by-Step Implementation Plan

The client-side UI for displaying resources is already implemented in `CityModal.tsx`. The current implementation correctly shows resource amounts, storage bars, and a "FULL" indicator when at cap. No additional client-side changes are required for this user story.

### Existing Implementation Verification

1. **Review `tenxempires.client/src/components/modals/CityModal.tsx`**
   - Constant `STORAGE_CAP = 100` is already defined
   - `ResourceCard` component already shows:
     - Current resource amount
     - Storage bar with fill percentage
     - "FULL" indicator when `amount >= maxAmount`

2. **Verify Type Definitions** in `tenxempires.client/src/types/game.ts`
   - `CityResourceDto` interface is defined with `cityId`, `resourceType`, and `amount`
   - `GameStateDto` includes `cityResources: CityResourceDto[]`

### Optional Client Enhancement (Post-MVP)

Consider adding a visual indicator showing overflow (resources lost due to cap) in the turn summary display. This would require:
- Adding `overflow` field to turn summary parsing
- Displaying overflow warning toast or indicator

---

## 2. Server-Side Step-by-Step Implementation Plan

### Step 2.1: Add Storage Cap Configuration

**File:** `TenXEmpires.Server.Domain/Configuration/GameSettings.cs`

Add a new configuration property for the resource storage cap:

```csharp
/// <summary>
/// Maximum resource storage per type per city.
/// </summary>
public int ResourceStorageCap { get; set; } = 100;
```

**File:** `TenXEmpires.Server/appsettings.json`

Add the setting under `GameSettings`:

```json
"GameSettings": {
    // ... existing settings ...
    "ResourceStorageCap": 100
}
```

### Step 2.2: Add Resource Cap Constant

**File:** `TenXEmpires.Server.Domain/Constants/ResourceTypes.cs`

Add a constant for the default cap:

```csharp
/// <summary>
/// Default storage cap per resource type per city.
/// </summary>
public const int DefaultStorageCap = 100;
```

### Step 2.3: Modify HarvestCityResources Method

**File:** `TenXEmpires.Server.Infrastructure/Services/TurnService.cs`

Update the `HarvestCityResources` method to enforce the storage cap and track overflow:

```csharp
private void HarvestCityResources(
    City city,
    List<Unit> allUnits,
    Dictionary<string, int> harvestedTotals,
    Dictionary<string, int> overflowTotals,  // NEW parameter
    IReadOnlyDictionary<long, GameTileState> tileStates,
    int storageCap)  // NEW parameter
{
    foreach (var link in city.CityTiles)
    {
        var tile = link.Tile;
        if (tile.ResourceType is null)
            continue;

        if (!tileStates.TryGetValue(tile.Id, out var tileState))
        {
            _logger.LogWarning(
                "Missing tile state for tile {TileId} in city {CityId}; skipping harvest",
                tile.Id,
                city.Id);
            continue;
        }

        if (tileState.ResourceAmount <= 0)
            continue;

        // Skip if enemy unit is on tile
        var blockingUnit = allUnits.FirstOrDefault(u => 
            u.ParticipantId != city.ParticipantId && u.TileId == tile.Id);
        if (blockingUnit is not null)
        {
            _logger.LogDebug(
                "City {CityId} skipped harvesting tile {TileId} due to enemy unit {UnitId}",
                city.Id,
                tile.Id,
                blockingUnit.Id);
            continue;
        }

        var cr = city.CityResources.FirstOrDefault(r => r.ResourceType == tile.ResourceType);
        if (cr is null)
        {
            cr = new CityResource { CityId = city.Id, ResourceType = tile.ResourceType, Amount = 0 };
            city.CityResources.Add(cr);
            _context.CityResources.Add(cr);
        }

        // NEW: Check storage cap before harvesting
        if (cr.Amount >= storageCap)
        {
            // Resource at cap - track overflow, don't consume tile resource
            if (overflowTotals.ContainsKey(tile.ResourceType))
                overflowTotals[tile.ResourceType] += 1;
            else
                overflowTotals[tile.ResourceType] = 1;
                
            _logger.LogDebug(
                "City {CityId} at storage cap for {ResourceType}; skipping harvest from tile {TileId}",
                city.Id,
                tile.ResourceType,
                tile.Id);
            continue;
        }

        // Calculate how much can be harvested (partial harvest at cap boundary)
        int potentialHarvest = 1; // Base harvest per tile
        int availableStorage = storageCap - cr.Amount;
        int actualHarvest = Math.Min(potentialHarvest, availableStorage);
        int overflow = potentialHarvest - actualHarvest;

        // Apply harvest
        cr.Amount += actualHarvest;
        tileState.ResourceAmount -= 1; // Always consume from tile

        // Track totals
        if (harvestedTotals.ContainsKey(tile.ResourceType))
            harvestedTotals[tile.ResourceType] += actualHarvest;
        else
            harvestedTotals[tile.ResourceType] = actualHarvest;

        if (overflow > 0)
        {
            if (overflowTotals.ContainsKey(tile.ResourceType))
                overflowTotals[tile.ResourceType] += overflow;
            else
                overflowTotals[tile.ResourceType] = overflow;
        }
    }
}
```

### Step 2.4: Update EndTurnAsync Method

**File:** `TenXEmpires.Server.Infrastructure/Services/TurnService.cs`

Update the `EndTurnAsync` method to pass the storage cap and track overflow:

```csharp
// In EndTurnAsync, before the foreach (var city in cities) loop:
var overflowTotals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
{
    { ResourceTypes.Wood, 0 },
    { ResourceTypes.Stone, 0 },
    { ResourceTypes.Wheat, 0 },
    { ResourceTypes.Iron, 0 }
};

var storageCap = _gameSettings?.ResourceStorageCap ?? ResourceTypes.DefaultStorageCap;

// Update the HarvestCityResources call:
HarvestCityResources(city, allUnits, harvestedTotals, overflowTotals, tileStates, storageCap);

// Update the turn summary to include overflow:
var summaryObj = new
{
    regenAppliedCities = regenApplied,
    harvested = harvestedTotals,
    overflow = overflowTotals,  // NEW
    producedUnits = producedUnitCodes,
    productionDelayed,
    aiExecuted = false
};
```

### Step 2.5: Update AI Turn Processing

**File:** `TenXEmpires.Server.Infrastructure/Services/TurnService.cs`

Update `TryExecuteAiTurnsAsync` to also enforce the cap for AI cities:

```csharp
// In TryExecuteAiTurnsAsync, add overflow tracking:
var aiOverflow = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
{
    { ResourceTypes.Wood, 0 },
    { ResourceTypes.Stone, 0 },
    { ResourceTypes.Wheat, 0 },
    { ResourceTypes.Iron, 0 }
};

var storageCap = _gameSettings?.ResourceStorageCap ?? ResourceTypes.DefaultStorageCap;

// Update HarvestCityResources call:
HarvestCityResources(city, allUnits, aiHarvested, aiOverflow, aiTileStates, storageCap);
```

---

## 3. Database Changes

No database schema changes are required for this user story. The existing tables support the implementation:

- **`app.city_resources`**: Stores resource amounts per city (Amount column has no cap constraint - enforced in application)
- **`app.game_tile_states`**: Tracks per-game tile resource availability

### Data Integrity Considerations

The application-level enforcement of the 100 cap is intentional to allow for:
1. Future configurability (different caps per game mode)
2. Flexibility in adjusting caps without migrations
3. Avoiding database constraint violations during migrations

---

## 4. Error Handling

### Error Scenarios and Handling

| # | Error Scenario | Detection | Handling | HTTP Status |
|---|---------------|-----------|----------|-------------|
| E1 | City not found during harvesting | City query returns null | Log warning, skip city | N/A (internal) |
| E2 | Invalid resource type in tile data | ResourceType not in valid list | Log warning, skip tile | N/A (internal) |
| E3 | Negative resource amount | Amount < 0 | Log error, clamp to 0 | N/A (internal) |
| E4 | Missing GameTileState record | TileState lookup fails | Log warning, skip tile | N/A (internal) |
| E5 | Database save failure | SaveChangesAsync throws | Transaction rollback, rethrow | 500 |
| E6 | Concurrent modification | Serializable transaction conflict | Transaction rollback, retry hint | 409 |
| E7 | Game not found | Game query returns null | UnauthorizedAccessException | 404 |
| E8 | Not player's turn | Active participant check fails | InvalidOperationException | 409 |

### Error Response Format

All errors follow the existing API error format:

```json
{
    "code": "ERROR_CODE",
    "message": "Human-readable error message"
}
```

### Logging Strategy

- **Debug**: Resource cap checks, individual tile harvest skips
- **Information**: Turn completion with harvest totals
- **Warning**: Missing tile states, skipped tiles due to enemy units
- **Error**: Data integrity issues (negative amounts, invalid types)

---

## 5. Security Considerations

### Authentication & Authorization

1. **Turn Ownership Validation**: The `EndTurnAsync` method already verifies:
   - Game belongs to authenticated user (`game.UserId == userId`)
   - Active participant is human player
   - User is the active participant

2. **RLS Enforcement**: PostgreSQL Row-Level Security policies ensure:
   - Users can only access their own games
   - City and resource data is scoped to game ownership

### Data Integrity

1. **Server-Authoritative**: All resource calculations happen server-side
   - Client cannot directly modify resource amounts
   - Storage cap is enforced in `TurnService`

2. **Transaction Isolation**: 
   - `EndTurnAsync` uses `IsolationLevel.Serializable` transaction
   - Prevents race conditions during concurrent requests

3. **Idempotency**: 
   - End-turn operations support idempotency keys
   - Duplicate requests return cached results

### Input Validation

The harvest operation doesn't accept user input directly - it operates on server-side data only. However:
- Resource types are validated against `ResourceTypes.ValidTypes`
- Amounts are validated to be non-negative

---

## 6. Backend Unit Test Scenarios

**File:** `TenXEmpires.Server.Tests/Services/TurnServiceHarvestTests.cs`

### Test Cases

```csharp
public class TurnServiceHarvestTests
{
    // === Happy Path Tests ===
    
    [Fact]
    public async Task HarvestCityResources_BelowCap_HarvestsNormally()
    // Given: City has 50 wheat, tile has wheat resource
    // When: End turn is processed
    // Then: City wheat increases to 51
    
    [Fact]
    public async Task HarvestCityResources_AtCap_DoesNotHarvest()
    // Given: City has 100 wheat (at cap), tile has wheat resource
    // When: End turn is processed
    // Then: City wheat remains 100, overflow is tracked
    
    [Fact]
    public async Task HarvestCityResources_NearCap_PartialHarvest()
    // Given: City has 99 wheat, tile has wheat resource
    // When: End turn is processed
    // Then: City wheat becomes 100, no overflow
    
    [Fact]
    public async Task HarvestCityResources_MultipleResources_IndependentCaps()
    // Given: City has 100 wheat, 50 wood, tiles have both
    // When: End turn is processed
    // Then: Wheat unchanged (at cap), wood increases to 51
    
    // === Edge Cases ===
    
    [Fact]
    public async Task HarvestCityResources_MultipleTiles_CapsAtTotal()
    // Given: City has 98 wheat, 3 tiles with wheat
    // When: End turn is processed
    // Then: City wheat becomes 100, 1 overflow
    
    [Fact]
    public async Task HarvestCityResources_EnemyOnTile_SkipsHarvest()
    // Given: City has 50 wheat, enemy unit on wheat tile
    // When: End turn is processed
    // Then: City wheat unchanged
    
    [Fact]
    public async Task HarvestCityResources_DepletedTile_SkipsHarvest()
    // Given: Tile has ResourceAmount = 0
    // When: End turn is processed
    // Then: No harvest from that tile
    
    [Fact]
    public async Task HarvestCityResources_NoResourceTile_SkipsHarvest()
    // Given: Tile has ResourceType = null
    // When: End turn is processed
    // Then: No harvest from that tile
    
    // === Turn Summary Tests ===
    
    [Fact]
    public async Task EndTurn_TurnSummary_IncludesHarvestTotals()
    // Given: City harvests 3 wheat, 2 wood
    // When: End turn response received
    // Then: TurnSummary.harvested contains correct totals
    
    [Fact]
    public async Task EndTurn_TurnSummary_IncludesOverflowTotals()
    // Given: City at cap, would overflow 2 wheat
    // When: End turn response received
    // Then: TurnSummary.overflow contains correct totals
    
    // === Configuration Tests ===
    
    [Fact]
    public async Task HarvestCityResources_UsesConfiguredCap()
    // Given: GameSettings.ResourceStorageCap = 50
    // When: City has 50 wheat and harvests
    // Then: Wheat does not increase, overflow tracked
    
    [Fact]
    public async Task HarvestCityResources_DefaultCapWhenNotConfigured()
    // Given: GameSettings is null
    // When: Harvest runs
    // Then: Uses ResourceTypes.DefaultStorageCap (100)
}
```

### Test Implementation Details

```csharp
[Fact]
public async Task HarvestCityResources_AtCap_DoesNotHarvest()
{
    // Arrange
    var city = CreateTestCity();
    city.CityResources.Add(new CityResource 
    { 
        CityId = city.Id, 
        ResourceType = ResourceTypes.Wheat, 
        Amount = 100 
    });
    
    var tile = CreateTestTile(ResourceType: ResourceTypes.Wheat, ResourceAmount: 50);
    city.CityTiles.Add(new CityTile { CityId = city.Id, TileId = tile.Id, Tile = tile });
    
    var tileStates = new Dictionary<long, GameTileState>
    {
        { tile.Id, new GameTileState { TileId = tile.Id, ResourceAmount = 50 } }
    };
    
    var harvestedTotals = CreateEmptyTotals();
    var overflowTotals = CreateEmptyTotals();
    
    // Act
    _service.HarvestCityResources(
        city, 
        new List<Unit>(), 
        harvestedTotals, 
        overflowTotals, 
        tileStates, 
        storageCap: 100);
    
    // Assert
    city.CityResources.First(r => r.ResourceType == ResourceTypes.Wheat)
        .Amount.Should().Be(100);
    harvestedTotals[ResourceTypes.Wheat].Should().Be(0);
    overflowTotals[ResourceTypes.Wheat].Should().Be(1);
}
```

---

## 7. Frontend Unit Test Scenarios

**File:** `tenxempires.client/src/components/modals/CityModal.test.tsx`

### Test Cases

```typescript
describe('CityModal Resource Display', () => {
  // === Resource Amount Display ===
  
  it('displays correct resource amounts from gameState', () => {
    // Given: gameState with cityResources for city
    // When: CityModal rendered
    // Then: Each resource shows correct amount
  });
  
  it('displays zero for resources not in cityResources', () => {
    // Given: cityResources missing iron entry
    // When: CityModal rendered
    // Then: Iron shows 0
  });
  
  // === Storage Cap Indicators ===
  
  it('shows FULL indicator when resource at cap', () => {
    // Given: cityResources has wheat = 100
    // When: CityModal rendered
    // Then: Wheat card has "FULL" badge
  });
  
  it('does not show FULL indicator when below cap', () => {
    // Given: cityResources has wheat = 99
    // When: CityModal rendered
    // Then: Wheat card has no "FULL" badge
  });
  
  // === Storage Bar ===
  
  it('shows storage bar at correct fill percentage', () => {
    // Given: cityResources has wheat = 75
    // When: CityModal rendered
    // Then: Wheat storage bar is 75% filled
  });
  
  it('shows storage bar at 100% when at cap', () => {
    // Given: cityResources has wheat = 100
    // When: CityModal rendered
    // Then: Wheat storage bar is 100% filled
  });
  
  // === Styling ===
  
  it('applies amber styling when resource at cap', () => {
    // Given: cityResources has wheat = 100
    // When: CityModal rendered
    // Then: Wheat card has amber border/background
  });
  
  it('applies default styling when below cap', () => {
    // Given: cityResources has wheat = 50
    // When: CityModal rendered
    // Then: Wheat card has default styling
  });
});
```

### Test Implementation Example

```typescript
import { render, screen } from '@testing-library/react';
import { CityModal } from './CityModal';
import { createMockGameState } from '../../test/mocks';

describe('CityModal Resource Display', () => {
  it('shows FULL indicator when resource at cap', () => {
    // Arrange
    const gameState = createMockGameState({
      cityResources: [
        { cityId: 1, resourceType: 'wheat', amount: 100 },
        { cityId: 1, resourceType: 'wood', amount: 50 },
      ],
    });

    // Act
    render(
      <CityModal
        onRequestClose={jest.fn()}
        gameState={gameState}
        cityId={1}
      />
    );

    // Assert
    const wheatCard = screen.getByText('Wheat').closest('div');
    expect(wheatCard).toHaveTextContent('FULL');
    
    const woodCard = screen.getByText('Wood').closest('div');
    expect(woodCard).not.toHaveTextContent('FULL');
  });
});
```

---

## 8. E2E Test Scenarios

**File:** `tenxempires.client/e2e/resource-harvest.spec.ts`

### Test Cases

```typescript
import { test, expect } from '@playwright/test';
import { GamePage } from './pages/GamePage';
import { LoginPage } from './pages/LoginPage';

test.describe('Resource Harvest and Cap', () => {
  let gamePage: GamePage;
  
  test.beforeEach(async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.loginAsTestUser();
    gamePage = new GamePage(page);
    await gamePage.createNewGame();
  });
  
  test('resources accumulate after ending turn', async () => {
    // Get initial resource values
    await gamePage.openCityModal();
    const initialWheat = await gamePage.getResourceAmount('wheat');
    await gamePage.closeCityModal();
    
    // End turn
    await gamePage.endTurn();
    
    // Check resources increased
    await gamePage.openCityModal();
    const newWheat = await gamePage.getResourceAmount('wheat');
    expect(newWheat).toBeGreaterThan(initialWheat);
  });
  
  test('resources do not exceed cap of 100', async ({ page }) => {
    // Play multiple turns to accumulate resources
    for (let i = 0; i < 50; i++) {
      await gamePage.endTurn();
      await page.waitForTimeout(100); // Brief wait for state update
    }
    
    // Open city modal and check resources
    await gamePage.openCityModal();
    
    const wheat = await gamePage.getResourceAmount('wheat');
    const wood = await gamePage.getResourceAmount('wood');
    const stone = await gamePage.getResourceAmount('stone');
    const iron = await gamePage.getResourceAmount('iron');
    
    expect(wheat).toBeLessThanOrEqual(100);
    expect(wood).toBeLessThanOrEqual(100);
    expect(stone).toBeLessThanOrEqual(100);
    expect(iron).toBeLessThanOrEqual(100);
  });
  
  test('FULL indicator appears when at cap', async ({ page }) => {
    // Mock or setup game state with resources at cap
    // (may need test fixture or many turns)
    
    for (let i = 0; i < 100; i++) {
      await gamePage.endTurn();
    }
    
    await gamePage.openCityModal();
    
    // Check for FULL indicator on at least one resource
    const fullIndicators = page.locator('text=FULL');
    await expect(fullIndicators.first()).toBeVisible();
  });
  
  test('turn summary shows harvested resources', async ({ page }) => {
    // End turn and check turn log/summary
    await gamePage.endTurn();
    
    // The turn summary should be accessible in game state
    const gameState = await gamePage.getGameState();
    expect(gameState.turnSummary).toBeDefined();
    expect(gameState.turnSummary.harvested).toBeDefined();
  });
  
  test('enemy unit on tile blocks harvest', async ({ page }) => {
    // This requires a more complex game state setup
    // with enemy units positioned on resource tiles
    
    // Setup: Position AI unit on player's wheat tile
    // Action: End turn
    // Verify: Wheat did not increase from that tile
    
    // Implementation depends on test fixtures/game manipulation
    test.skip(); // Mark as future implementation
  });
});
```

### Page Object Helper Methods

```typescript
// e2e/pages/GamePage.ts

export class GamePage {
  constructor(private page: Page) {}
  
  async openCityModal() {
    // Click on player's city tile (may need double-click if unit present)
    await this.page.click('[data-testid="game-map-canvas"]', {
      position: { x: 100, y: 100 } // Adjust based on city position
    });
    await this.page.waitForSelector('[aria-labelledby="resources-heading"]');
  }
  
  async closeCityModal() {
    await this.page.click('button[aria-label="Close city modal"]');
  }
  
  async getResourceAmount(resourceType: string): Promise<number> {
    const resourceCard = this.page.locator(`text=${resourceType}`).first();
    const amountText = await resourceCard.locator('xpath=..').locator('.text-lg').textContent();
    return parseInt(amountText || '0', 10);
  }
  
  async endTurn() {
    await this.page.click('[data-testid="end-turn-button"]');
    await this.page.waitForResponse(response => 
      response.url().includes('/end-turn') && response.status() === 200
    );
  }
  
  async getGameState(): Promise<GameStateDto> {
    return await this.page.evaluate(() => {
      // Access game state from React context or store
      return window.__gameState__;
    });
  }
}
```

---

## Implementation Checklist

- [ ] **Server**
  - [ ] Add `ResourceStorageCap` to `GameSettings.cs`
  - [ ] Add `DefaultStorageCap` constant to `ResourceTypes.cs`
  - [ ] Update `appsettings.json` with new setting
  - [ ] Modify `HarvestCityResources` method signature
  - [ ] Implement cap enforcement in `HarvestCityResources`
  - [ ] Add overflow tracking
  - [ ] Update `EndTurnAsync` to pass new parameters
  - [ ] Update `TryExecuteAiTurnsAsync` for AI cities
  - [ ] Update turn summary JSON structure

- [ ] **Tests**
  - [ ] Create `TurnServiceHarvestTests.cs`
  - [ ] Implement all backend unit tests
  - [ ] Create/update `CityModal.test.tsx`
  - [ ] Implement frontend unit tests
  - [ ] Create `resource-harvest.spec.ts` E2E tests

- [ ] **Documentation**
  - [ ] Update API documentation if turn summary structure changes
  - [ ] Update CHANGELOG.md

---

## Dependencies

This user story has no blocking dependencies. It extends existing functionality in:
- `TurnService.cs` - End-of-turn processing
- `CityModal.tsx` - Resource display (already complete)

Future stories that depend on this:
- **US-102**: Manual Unit Spawn (requires resource spending)
- **US-103**: Manual Territory Expansion (requires wheat spending)
- **US-104**: Build Wooden Walls (requires wood spending)



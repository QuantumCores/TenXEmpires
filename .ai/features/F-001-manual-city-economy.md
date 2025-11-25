# Feature – Manual City Economy & Expansion

## 1. Feature Overview

This feature shifts the City management mechanic from an automated, passive system to an active, manual economy. Players now accumulate four distinct resources (Wheat, Wood, Stone, Iron) with a storage cap and must strategically choose how to spend them. Instead of units spawning automatically, players manually produce units, build defensive structures (Wooden Walls), or expand their territory tile-by-tile. This adds strategic depth by introducing opportunity cost: a city is limited to exactly one production action per turn (Spawn OR Build OR Expand).

## 2. Functional Requirements

2.1 City Interaction & UI
- Clicking a city tile opens a "City Modal" displaying current resource stocks and production options.
- Interaction Priority: If a friendly unit occupies the city tile, the first click selects the unit. A second click on the same tile selects the city and opens the modal.
- The "End Turn" shortcut (E) is disabled while the City Modal is open to prevent accidental skips.

2.2 Resource System
- Resources: Wheat, Wood, Iron, Stone.
- Storage: Resources accumulate per city.
- Cap: Each resource type is capped at 100.
- Overflow: Any resources that reaches the cap is not harvested.
- Harvest: Occurs automatically at the end of the turn for all tiles within the city's current territory unless resource cap is reached.

2.3 Action Economy
- Limit: A city may perform only ONE "production" action per turn.
- Actions: Spawn Unit, Build Building, Expand Territory.
- UI Feedback: Once an action is taken, other options are disabled/grayed out for the remainder of the turn.
- Backend validation: Action taken is stored on the city entity as boolean flag and is used to validate if action can be performed.

2.4 Manual Spawning
- Interface: Players select a unit type from the City Modal. Selection enables `Confirm` button. Clicking on `Confirm` button spawn unit.
- Costs: Warrior requires 10 Iron; Slinger requires 10 Stone.
- Placement: Unit spawns immediately on the city tile.

2.5 Manual Expansion
- Interface: Clicking "Expand" in the modal enters "Expansion Mode".
- Mechanics: The modal closes, and valid target tiles are highlighted with strong blue border.
- Valid Targets: Any adjacent tile that is NOT owned by an enemy city and NOT occupied by an enemy unit.
- Cost: Uses Wheat. Formula: Cost = BaseCost + ((ControlledTilesCount - InitialTilesCount) * 10).
- BaseCost is configurable in appSettings (default 20).
- Cancellation: Right-click or ESC cancels Expansion Mode without spending resources and returns to city modal.

2.6 Buildings
- Interface: Players select a building type from the City Modal. Selection enables `Confirm` button. Clicking on `Confirm` button builds selected building.
- Cost: 50 Wood.
- Effect: Instantly adds +10 Defence to the City's stats.
- Visualization: City image changes to the one with wooden wall `human-wooden-wall.png`.

## 3. Feature Boundaries

In Scope
- City Modal UI implementation.
- Resource storage, capping, and harvest logic (backend).
- Manual spawning logic and validation.
- "Expansion Mode" interaction state and validation.
- Wooden Walls building logic and visual update.
- Interaction priority system (Unit vs City).
- Database updates to store City Resources, Action taken flag and Buildings.

Out of Scope
- Resource trading or transfer between cities.
- AI usage of this manual system (AI remains on asymmetric auto-rules).

## 4. User Stories

US-100: City Modal and Resources
Description
As a player, I want to view my city's resources and options so I can make strategic decisions.
Acceptance Criteria
- Clicking a city tile opens the City Modal.
- If a unit is on the city, the first click selects the unit; the second click opens the modal.
- Modal displays current counts for Wheat, Wood, Iron, and Stone with their icons (`tenxempires.client\public\images\game\resources`). 
- Modal displays current buildings with their icons (`tenxempires.client\public\images\game\buildings`).
- The "End Turn" hotkey (E) does not function while the modal is open.

US-101: Resource Harvest and Cap
Description
As a player, I want to accumulate resources up to a limit so I can save for actions.
Acceptance Criteria
- Resources are harvested from all controlled tiles at the end of the turn.
- Storage is capped at 100 per resource type.
- Resources are not harvested if the cap is reached.

US-102: Manual Unit Spawn
Description
As a player, I want to spend resources to spawn units manually.
Acceptance Criteria
- I can select Warrior (cost 10 Iron) or Slinger (cost 10 Stone) from the modal.
- Selection enables `Confirm` button. Clicking on `Confirm` button spawn unit.
- The unit spawns immediately on the nearest free adjacent tile.
- This counts as the city's single action for the turn; further actions are disabled.

US-103: Manual Territory Expansion
Description
As a player, I want to expand my territory using Wheat.
Acceptance Criteria
- Clicking "Expand" closes the modal and enters "Expansion Mode".
- Valid adjacent tiles (not enemy owned/occupied) are highlighted with strong blue border.
- Clicking a valid tile deducts Wheat and claims the tile immediately.
- Cost follows the formula: Base + (Increase * Count).
- Expansion counts as the city's single action for the turn.
- Pressing ESC cancels the mode without spending resources.

US-104: Build Wooden Walls
Description
As a player, I want to build walls to improve my city's defence.
Acceptance Criteria
- I can select Wooden walls (cost 50 wood) from the modal.
- Selection enables `Confirm` button. Clicking on `Confirm` button builds building.
- Building a building disables this building selection permanently.
- The city's Defence stat increases by +10 immediately.
- A visual indicator (city with wooden wall image) appears on the map.
- This counts as the city's single action for the turn.

US-105: City Action Limit
Description
As a player, I want to be restricted to one significant city action per turn to force strategic choices.
Acceptance Criteria
- Performing a Spawn, Build, or Expand action disables all production options for that city until the next turn.
- The UI clearly indicates that the city has acted.

US-106: Interaction Priority
Description
As a player, I want to easily select units stationed on cities without accidentally opening the city menu.
Acceptance Criteria
- Clicking a tile with both a City and a Friendly Unit selects the Unit first.
- Clicking the same tile again (while the unit is selected) opens the City Modal.
- Clicking a City with no unit opens the Modal immediately.

US-107: Secure Action Validation
Description
As a developer, I want to ensure players cannot cheat the economy.
Acceptance Criteria
- All spend requests (Spawn, Build, Expand) are validated server-side against current resource stocks.
- The "one action per turn" limit is enforced server-side.
- Invalid requests return an error and do not change game state.

## 5. Success Metrics

- Resource Engagement: Average resource balance at end of game (are players spending or hoarding?).
- Overflow Rate: Percentage of total harvested resources lost to the 100 cap (target < 20% indicates good spending flow).
- Expansion Depth: Average number of manual expansions performed per city per game.
- Turn Efficiency: Time spent with City Modal open vs total turn time.


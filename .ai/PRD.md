# Product Requirements Document (PRD) – TenX Empires MVP

## 1. Product Overview

TenX Empires is a browser-based, single-player, turn-based strategy prototype. The MVP delivers a simple but fully playable loop: authenticate, start a fixed 20×15 hex map, move and attack with basic units, contest cities, play against a deterministic AI in strict alternating turns, and save/load progress from the backend (3 manual slots plus 5 autosaves). The experience targets 1080p and emphasizes responsiveness, clarity, and deterministic outcomes without animations or audio.

Goals

- Demonstrate an end-to-end, server-authoritative, single-player gameplay loop.
- Provide reliable save/load with manual slots and autosaves.
- Keep turns fast (AI ≤ 500 ms) and the JS bundle small (≤ 300 KB gzip).
- Capture essential analytics events to evaluate playthrough funnels and turn depth.

Audience

- Strategy players evaluating a prototype without onboarding friction.
- Internal dev/product stakeholders measuring playthrough and performance.

Scope Summary

- Authentication: ASP.NET Identity (email/password). No guest play; public About/Gallery only.
- Frontend: React 19 + TailwindCSS 4 (PostCSS) + Vite + TypeScript 5; CSS + SVG UI.
- Backend: .NET 8 MVC Web API; PostgreSQL persistence; server authoritative.
- Board: Fixed 20×15 hex grid, pointy-top, odd-r offset; full visibility.
- Units: Warrior (melee), Slinger (ranged); one unit per tile (1UPT); move or attack per turn.
- Cities: Harvest resources in radius 2; auto-produce units when thresholds met.
- AI: Deterministic priority list; completes turn < 500 ms.
- Saves: 3 manual slots + 5 autosaves (ring buffer on end-turn); retention 3 months.
- Analytics: Minimal event set with per-turn batching.
- Deployment: Github actions, Dockerized, CI/CD to DigitalOcean (sizing and ingress TBD).

Constraints and Non-functional Targets

- JS bundle ≤ 300 KB (gzip). AI turn ≤ 500 ms. Session idle timeout 30 min.
- API rate limit 60 req/min. Strict CORS and anti-forgery. Analytics batched 1 call/turn.
- Assets owned; include Privacy and Cookie pages; ASSETS.md in repo.

Assumptions

- Single AI opponent; fixed spawns; handcrafted map embedded as a resource.
- All in-reach city tiles are considered worked by default; no tile assignment UI in MVP.

Outcomes

- A playable slice validating core mechanics, data model, and performance targets.
- Actionable analytics on funnels (start, turns taken, finish) and save behavior.

## 2. User Problem

Players need a quick, responsive way to try a new TBS concept in the browser without learning complex systems. The MVP should let them log in, start a match instantly on a fixed map, make clear tactical choices each turn, and see an AI respond quickly. Progress must be reliable across sessions via simple save/load. Product needs telemetry to understand engagement and completion.

Pain Points Addressed

- Slow or unclear turn resolution: AI must be fast and deterministic.
- Ambiguous movement/combat: preview paths, deterministic combat, clear rules (1UPT).
- Lost progress: server-side saves with retention and slot management.
- Complex onboarding: fixed content, full visibility, simple two-unit roster.
- Privacy and control: delete account purges saves/metrics; data retention is limited.

## 3. Functional Requirements

3.1 Authentication and Access

- Email/password via ASP.NET Identity only; no third-party auth in MVP.
- No guest play; unauthenticated users may view About and Gallery.
- Session idle timeout of 30 minutes; enforced client- and server-side.
- Rate limit 60 requests/min per client identity.
- Delete account purges user profile, save slots, and analytics events.
- Minimal profile data; retention 3 months from last end-turn.

3.2 Game Loop and Turn System

- Strict order: player turn, then AI turn, repeat until win condition.
- End turn via UI button or keyboard key E.
- No undo; no move confirmations.
- End-turn summary toast lists pending actions.

3.3 Map and Movement

- Map: fixed 20×15, pointy-top hexes, odd-r offset coordinates.
- Distance: offset→cube conversion; cube metric.
- Pathfinding: A* with uniform cost (1 per tile).
- Full map visibility (no fog of war).
- Selecting a unit shows path preview; second click executes move.
- 1UPT: cannot end on occupied tile; pass-through friendlies allowed.

3.4 Units and Combat

- Units: Warrior (melee), Slinger (ranged) with server-side stats.
- Actions: a unit may move or attack once per turn.
- Damage formula: DMG = Attack × (1 + (Attack − Defence)/Defence) × 0.5; round half up; min 1.
- Order: attacker hits first; if defender survives and is eligible, it counterattacks.
- Ranged units never receive counterattacks (adjacent or not).
- Ties go to the attacker.

3.5 Cities and Economy

- City stats: HP=50, Defence=10; regenerate +4 HP/turn; +2 HP/turn under siege.
- Capture: city HP ≤ 0 and a melee unit ends its turn on the city tile.
- On final capture, remaining enemy units despawn.
- City reach: radius 2; always displayed as SVG border overlay.
- Auto-harvest: all resource tiles in reach per turn; resource types wheat, wood, stone, iron.
- Auto-production: Warrior (10 iron), Slinger (10 stone); 1 unit/city/turn.
- Under siege: only non-occupied tiles contribute to harvesting/production.
- Spawn: nearest free adjacent tile; otherwise production is delayed.

3.6 AI Behavior

- Deterministic priority list: defend city; attack if single-round win chance > 60%; explore; expand reach/work tiles (MVP: all in-reach tiles worked by default); produce units.
- City defense: keep 1 unit on city tile if enemies within 3 tiles; maintain defender strength ≥ nearby enemy if possible.
- AI completes turn in ≤ 500 ms on reference hardware.

3.7 Save/Load and Persistence

- Server authoritative validation of all actions and state transitions.
- PostgreSQL storage; 3 manual save slots and 5 autosaves (ring buffer per end-turn).
- Manual save to a chosen slot; delete/overwrite with confirmation.
- Manual load and autosave load restore exact state.
- Retention: saves and analytics kept for 3 months from last end-turn.
- Map JSON is a versioned resource with server-side schema gate; incompatible loads blocked.

3.8 Analytics and Telemetry

- Events: game_start, turn_end, autosave, manual_save, manual_load, game_finish.
- Properties: user_id hash, game_id, turn_no (if applicable), timestamp.
- Batched to at most 1 network call per turn.
- Stored in DB; queries are manual in MVP.

3.9 UX and Display

- Target 1080p; hex width ~48–56 px.
- Pan/zoom: 75–150% wheel zoom; drag pan.
- No animations or sound; reuse SVG symbols; PNG assets ≤ 2× display resolution.
- End-turn summary toast surfaces pending actions.

3.10 Security, Privacy, Compliance

- Strict CORS; anti-forgery; role scoping for admin-only endpoints if any.
- API rate limit 60 req/min.
- Delete-account purges saves and analytics.
- Privacy and cookie notices accessible.

3.11 Non-functional Requirements

- AI turn ≤ 500 ms; input smooth at 60 FPS on reference hardware.
- Initial JS bundle ≤ 300 KB (gzip) at build time; CI gate.
- Server authoritative validation and error logging with actionable messages.
- Analytics events persisted reliably.

3.12 Deployment and DevOps

- Dockerized application; CI/CD to DigitalOcean.
- Droplet size, managed Postgres tier, ingress/TLS are TBD.

3.13 Data Schema and Compatibility

- map.json includes schemaVersion; server validates against allowed versions.
- Unit tests verify schema compatibility and gating behavior.
- On mismatch, loads are rejected with clear UI messaging.
## 4. Product Boundaries

In Scope

- Email/password authentication; About/Gallery public.
- Fixed 20×15 map; full visibility; hex pathfinding; unit movement and combat.
- Two unit types; 1UPT; pass-through friendlies; move or attack per turn.
- One AI opponent with deterministic priorities and sub-500 ms turns.
- Cities with radius-2 harvest, auto-production, capture, and regen rules.
- Saves: 3 manual slots and 5 autosaves with ring buffer; server persistence.
- Minimal analytics event set with batching to 1 call/turn.
- Privacy page, cookie notice, ASSETS.md.
- Dockerized deploy with CI/CD.

Out of Scope (MVP)

- Multiplayer; spectators; guest play.
- Fog of war; line of sight; zones of control; unit promotions; tech trees.
- Animations, audio, advanced VFX; accessibility beyond basic keyboard/mouse.
- Procedural map generation; multiple maps; dynamic map editing.
- Complex economy or tile assignment UI.
- Modding; localization; controller support.
- Analytics dashboards and automated reporting.

Dependencies and Assumptions

- PostgreSQL availability and migrations.
- CI/CD secrets management.
- Reference hardware definition for AI timing.

Open Questions / TBD

- DigitalOcean sizing (compute, DB plan), ingress/TLS approach.
- Final map.json schema fields and validator specifics.
- AI expand/work tiles simplified in MVP: treat all in-reach tiles as worked.
- Save/autosave UI layout/copy and confirmations.
- Error handling copy for save/load/network failures.
## 5. User Stories

US-001: Register with email/password \
Description \
As a new user, I want to register with my email and a password so I can access the game and saves. \
Acceptance Criteria

- Valid email and password create an account and sign me in.
- Invalid inputs show specific errors; no account is created.
- Endpoint adheres to 60 req/min rate limit.

US-002: Log in 
Description \
As a returning user, I want to log in with my email and password so I can continue my game. \
Acceptance Criteria

- Correct credentials authenticate and navigate to the hub.
- Incorrect credentials show an error; remain unauthenticated.
- Idle timeout set to 30 minutes from last activity.

US-003: Log out \
Description \
As an authenticated user, I want to log out so my session ends. \
Acceptance Criteria

- Logging out invalidates session and redirects to public area.
- Authenticated endpoints are rejected until I log in again.

US-004: Session idle timeout \
Description \
As a user, I want idle sessions to expire after 30 minutes to protect my account. \
Acceptance Criteria

- After 30 minutes of inactivity, next authenticated request is rejected.
- A visible message indicates session expiration.

US-005: Public About/Gallery access

Description

As a visitor, I want to browse About and Gallery without logging in.

Acceptance Criteria

- About and Gallery are accessible unauthenticated.
- Gameplay routes redirect to login when unauthenticated.

US-006: Privacy and Cookie notices

Description

As a visitor, I want to view privacy and cookie information.

Acceptance Criteria

- Privacy and Cookie pages are accessible without auth.
- Pages state retention and delete-account policies.

US-007: Delete my account

Description

As a user, I want to permanently delete my account and data.

Acceptance Criteria

- Confirmation flow states irreversibility.
- Profile, saves, and analytics for my user are purged; I am logged out.

US-008: Rate limit feedback

Description

As a user, I want clear feedback when exceeding API rate limits.

Acceptance Criteria

- Exceeding 60 req/min returns clear status and message.
- UI shows non-blocking notice; retries idempotent calls when reasonable.

US-010: Start a new game

Description

As a player, I want to start a new game on the fixed map.

Acceptance Criteria

- New Game loads the 20×15 map with starting city, Warrior, Slinger.
- AI spawns opposite; game_start event recorded.

US-011: View saves and autosaves

Description

As a player, I want to view my 3 manual slots and 5 most recent autosaves.

Acceptance Criteria

- Manual slots show slot number, label, timestamp; empty if unused.
- Autosaves show timestamped entries limited to 5.

US-012: Manual save

Description

As a player, I want to save to one of 3 slots.

Acceptance Criteria

- Choosing a slot saves state; manual_save event recorded.
- Overwrite requires explicit confirmation.

US-013: Delete manual save

Description

As a player, I want to delete a manual save.

Acceptance Criteria

- Delete requires confirmation and clears the slot.
- UI updates to show empty slot.

US-015: Autosave on end-turn with ring buffer

Description

As a player, I want autosaves after end-turn with the latest 5 preserved.

Acceptance Criteria

- End-turn triggers autosave and logs autosave event.
- Oldest of 6 is dropped; list reflects 5 most recent entries.

US-016: Load manual save

Description

As a player, I want to load a manual save.

Acceptance Criteria

- Selecting a manual save restores state; manual_load recorded.
- Load failures leave the current game unchanged.

US-017: Load autosave

Description

As a player, I want to load an autosave.

Acceptance Criteria

- Selecting an autosave restores state; manual_load recorded.
- The autosave list retains order and timestamps.

US-018: Schema gate for incompatible loads

Description

As a player, I want incompatible saves blocked with a clear message.

Acceptance Criteria

- schemaVersion validated; incompatible loads rejected with explanation.
- No partial or corrupt state is loaded.

US-019: Retention purge

Description

As a system, I want to purge saves and analytics older than 3 months from last end-turn.

Acceptance Criteria

- Data older than policy window is eligible for purge.
- Purge jobs remove targeted records and log actions.

US-020: Render fixed map 20×15

Description

As a player, I want the fixed hex map to render clearly at 1080p.

Acceptance Criteria

- Pointy-top, odd-r layout renders; tiles are selectable.
- Hex width ~48–56 px at default zoom.

US-021: Pan and zoom (75–150%)

Description

As a player, I want to zoom and pan the map for comfort.

Acceptance Criteria

- Wheel zoom between 75–150%; drag to pan.
- Controls do not interfere with basic interactions.

US-022: Select and preview path

Description

As a player, I want to see a path preview before moving.

Acceptance Criteria

- Selecting a unit shows reachable tiles and a path preview.
- Preview uses A* (uniform cost) and respects 1UPT.

US-023: Move via second click

Description

As a player, I want a second click to execute the move.

Acceptance Criteria

- Second click on valid destination executes the move.
- Invalid destinations are ignored with a brief hint.

US-024: Enforce 1UPT and no swaps

Description

As a player, I want one unit per tile with no swaps.

Acceptance Criteria

- Ending on occupied tiles is blocked.
- Swapping unit positions is disallowed.

US-025: Pass-through friendlies

Description

As a player, I want to pass through friendly units.

Acceptance Criteria

- Pathfinding allows passing through friendlies.
- Destination still cannot be occupied.
US-026: Move or attack once per turn

Description

As a player, I want clear action limits for units.

Acceptance Criteria

- A unit may move or attack once per turn.
- Actions reset at the start of the owner’s next turn.

US-030: Melee attack (Warrior)

Description

As a player, I want melee combat to be deterministic.

Acceptance Criteria

- Damage uses the specified formula with round half up; min 1.
- If defender survives, it counterattacks.

US-031: Ranged attack (Slinger) with no counterattack

Description

As a player, I want ranged attacks that never receive counterattacks.

Acceptance Criteria

- Ranged can target within allowed range including adjacent.
- Defenders never counterattack ranged attackers.

US-032: Damage rounding and minimum

Description

As a player, I want rounding and minimum damage enforced.

Acceptance Criteria

- Round half up; values below 1 are set to 1.
- Server tests cover edge cases.

US-033: Counterattack eligibility

Description

As a player, I want counterattacks only when the defender survives.

Acceptance Criteria

- If defender HP ≤ 0 after the attack, no counterattack occurs.
- Ranged defenders never counterattack.

US-034: Ties go to attacker

Description

As a player, I want ties to resolve predictably.

Acceptance Criteria

- When outcomes tie, the attacker wins.
- Rule enforced server-side.

US-040: City regeneration and siege

Description

As a player, I want city regen to reflect siege status.

Acceptance Criteria

- +4 HP/turn normally; +2 HP/turn when enemies occupy any tile in reach.
- Regen capped at max HP.

US-041: City capture

Description

As a player, I want to capture cities clearly.

Acceptance Criteria

- After HP ≤ 0, a melee unit ending turn on the city captures it.
- Capturing the last enemy city ends the game; game_finish recorded.

US-042: City reach visualization

Description

As a player, I want a visible radius-2 border.

Acceptance Criteria

- City reach is always shown as an SVG overlay.
- Updates immediately on ownership change.

US-043: Auto-harvest resources

Description

As a player, I want cities to auto-harvest within reach.

Acceptance Criteria

- Server adds resources each turn from in-reach tiles.
- Under siege, only non-occupied tiles contribute.

US-044: Auto-produce units when thresholds met

Description

As a player, I want cities to auto-produce Warrior/Slinger.

Acceptance Criteria

- Warrior costs 10 iron; Slinger 10 stone; max 1 unit/city/turn.
- Resources deducted server-side.

US-045: Spawn nearest free or delay

Description

As a player, I want deterministic spawning or delay if blocked.

Acceptance Criteria

- Nearest free adjacent tile chosen with stable tie-breaker.
- If none free, production delayed and noted in summary.

US-046: Under-siege production limits

Description

As a player, I want production limited when enemies occupy reach.

Acceptance Criteria

- Only non-occupied tiles count for harvesting/production under siege.
- Rule enforced server- and client-side UI notifications.

US-047: Starting setup fixed spawns

Description

As a player, I want predictable starting forces and positions.

Acceptance Criteria

- Player and AI each start with 1 city, 1 Warrior, 1 Slinger in fixed positions.
- Server validates starting setup.

US-050: AI turn time ≤ 500 ms

Description

As a player, I want quick AI turns.

Acceptance Criteria

- AI decisions and actions complete ≤ 500 ms on reference hardware.
- Timing measured and logged for profiling.

US-051: AI defend city

Description

As a player, I want the AI to defend its city when threatened.

Acceptance Criteria

- If enemies within 3 tiles, AI keeps 1 unit on city if possible.
- AI maintains defender strength ≥ nearby enemies when feasible.

US-052: AI attack if favorable (>60%)

Description

As a player, I want the AI to attack when advantageous.

Acceptance Criteria

- AI attacks when single-round evaluator estimates > 60% chance.
- Target choice follows deterministic priority ordering.

US-053: AI explore

Description

As a player, I want the AI to explore when idle.

Acceptance Criteria

- Idle units move toward strategic tiles when not defending or attacking.
- Movements are deterministic.

US-054: AI produce units

Description

As a player, I want the AI to produce units when resources suffice.

Acceptance Criteria

- AI triggers production per thresholds and 1/turn limit.
- Spawn follows same rules as player.

US-055: Deterministic AI behavior

Description

As a developer, I want reproducible AI outcomes.

Acceptance Criteria

- Same initial state yields same AI actions.
- Any randomness is seeded from game state.
US-060: Record analytics events

Description

As a developer, I want essential analytics for funnels.

Acceptance Criteria

- Record game_start, turn_end, autosave, manual_save, manual_load, game_finish with user_id hash, game_id, turn_no, timestamp.
- Events persist in DB.

US-061: Batch analytics to 1 call/turn

Description

As a developer, I want to batch analytics for efficiency.

Acceptance Criteria

- Multiple events per turn are sent as one payload.
- On failure, payload is retried next turn.

US-070: Server authoritative validation

Description

As a developer, I want the server to validate all client actions.

Acceptance Criteria

- Illegal moves, 1UPT violations, and invalid combat are rejected.
- Responses include specific error codes/messages.

US-071: Strict CORS and anti-forgery

Description

As a developer, I want secure cross-origin and CSRF protections.

Acceptance Criteria

- Only configured origins allowed; state-changing calls protected.
- Verified in integration tests.

US-072: Bundle size budget ≤ 300 KB

Description

As a developer, I want fast initial load times.

Acceptance Criteria

- Initial JS bundle ≤ 300 KB (gzip) at build; CI fails if exceeded.
- Reported in build artifacts.

US-080: Save/load error handling

Description

As a player, I want clear errors for save/load failures.

Acceptance Criteria

- Transient errors show retry; non-retriable errors show guidance.
- No partial loads or duplicate saves occur on retries.

US-081: Schema mismatch messaging

Description

As a player, I want a clear explanation when a save cannot load.

Acceptance Criteria

- Load screen shows concise reason and suggests starting a new game.
- Game state remains unchanged if load fails.

US-082: Invalid move feedback

Description

As a player, I want quick feedback on invalid moves.

Acceptance Criteria

- Rejected moves show a brief hint; no server call for obviously invalid actions.
- The selection state remains stable.

US-090: End turn via button and E key

Description

As a player, I want to end my turn quickly.

Acceptance Criteria

- Clicking the button or pressing E ends turn immediately; no confirmation.
- Autosave and turn_end event occur at end-turn.

US-091: Keyboard focus behavior

Description

As a player, I want shortcuts to work only when the game has focus.

Acceptance Criteria

- E key does not trigger when typing in inputs.
- Focus behavior is consistent across supported browsers.

US-092: Deterministic tie-breaking

Description

As a developer, I want deterministic tie-breaking for paths and spawns.

Acceptance Criteria

- Equal-cost paths choose the same route consistently.
- Spawn tile selection is stable with a documented order.
## 6. Success Metrics

Player Funnel and Engagement

- Percent starting games (game_start per active user).
- Median turns per game.
- Percent reaching ≥ 5 turns: target 90%.
- Percent finishing (all enemy cities captured): target 75% of starters.

Performance and Technical

- AI turn compute time: p95 ≤ 500 ms on reference hardware.
- Initial JS bundle size: ≤ 300 KB gzip; CI-gated.
- Analytics network calls: ≤ 1 per turn on average.

Reliability and Data

- Save/load success rate ≥ 99% for valid requests.
- Schema gate blocks 100% of incompatible loads; zero corrupt loads.
- Delete-account and retention purges complete within SLA and remove targets.

Security and Compliance

- Session idle timeout enforced at 30 minutes.
- API rate limiting effective at 60 req/min with clear errors.
- Strict CORS and anti-forgery verified in integration tests.

Quality Gates and Definition of Done

- All user stories have passing acceptance tests.
- Bundle, AI timing, and analytics batching budgets met.
- Privacy, Cookie, and ASSETS.md present and correct.

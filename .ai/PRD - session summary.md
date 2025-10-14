<conversation_summary>
  <decisions>

1. Tech stack: **.NET 8 MVC Web API + React** in a single solution; **TailwindCSS via PostCSS** with **Vite** bundling; static assets served by ASP.NET; UI built with **CSS + SVG**.
2. Authentication: **ASP.NET Identity (email/password only)** for MVP; **no guests** (browse-only for About/Gallery); session **idle timeout 30 min**; API **rate limit 60 req/min**; minimal profile data retained **3 months** from last end-turn; “Delete my account” purges saves/metrics.
3. Data & persistence: **PostgreSQL** for game state and analytics; **server authoritative** rules; 3 manual save slots per user; **5 autosaves** (ring buffer, after end-turn); retention **3 months**.
4. Map: Fixed **20×15 hex** board; **pointy-top, odd-r offset**; distance via **offset→cube** conversion and cube metric; **A* pathfinding**, uniform cost (1/tile); **full map visible** (no fog of war).
5. Units: Two unit types (Warrior, Slinger) with defined stats; **1UPT** (one unit per tile); **pass-through friendlies allowed** (cannot end on occupied tile); **move OR attack once per turn**; **ranged never receives counterattacks** (adjacent or not).
6. Combat: Deterministic damage formula `DMG = Attack * (1 + (Attack - Defence)/Defence) * 0.5`; **round half up**, **min 1**; attack resolves first, defender counterattacks if alive; ties go to attacker.
7. Turn loop & input: Strict **player → AI → player**; AI turn **< 500 ms**; keyboard **E** ends turn; **no undo** and **no move confirmations**; selecting a tile shows **path preview**, second click executes move; end-turn **summary toast** lists pending actions.
8. Cities: **HP=50, Defense=10**; **capture** when a melee unit ends turn on the city tile after city HP ≤ 0; on capture, remaining enemy units despawn. **Regeneration:** city +4 HP/turn, or +2 HP/turn under siege. **Auto-production**: each turn, cities harvest resources within **2-hex radius** (always visible as SVG border); units produced automatically when resources meet costs (**Warrior 10 iron**, **Slinger 10 stone**, max 1 unit/turn). Production under siege only from tiles **not occupied by enemies**; spawn new unit on nearest free adjacent tile (or delay).
9. AI: **Deterministic priority list** — defend city; attack if chance > 60% (single-round evaluator); explore; expand city reach; work resource tiles; produce units. City defense: keep **1 unit** on city tile if any enemy within 3 tiles; maintain defender strength ≥ enemy nearby if possible.
10. Analytics: Store events in DB: **game_start, turn_end, autosave, manual_save, manual_load, game_finish** with properties (user_id hash, game_id, turn_no, timestamp). Finish = **all enemy cities captured**.
11. Non-functional: Initial **JS bundle ≤ 300 KB (gzip)**; PNG sprites ≤2× display resolution; reuse SVG symbols; **AI ≤ 500 ms**; batch analytics to **1 call per turn**.
12. UX/Display: Target **1080p single-screen fit** (~48–56 px hex width); **pan/zoom** (75–150% wheel zoom, drag pan); **no animations or sound**.
13. Assets & legal: All PNG assets **owned**; include Privacy page, cookie notice, **ASSETS.md** (ownership/attribution), and account deletion.
14. Deployment: **Dockerized**; platform **DigitalOcean** with GitHub CI/CD; exact droplet/DB sizing and ingress/TLS **to be decided later**.
15. Map content: **Handcrafted JSON** embedded as a resource; **versioned schema** with `"schemaVersion"` and **server-side schema gate**; unit tests to verify compatibility.
16. Starting setup: **1 AI opponent**; fixed spawns (opposite corners); each side starts with **1 city + Warrior + Slinger**.

</decisions>

<matched_recommendations>

1. Use React + Tailwind via PostCSS and Vite inside ASP.NET solution — **Accepted** (build pipeline documented in repo).
2. Fixed handcrafted map resource + versioned JSON schema with validation — **Accepted** (schema gating and unit tests planned).
3. Pointy-top hexes with **odd-r** offset, A* pathfinding, uniform costs — **Accepted**.
4. 1UPT, no zone-of-control, pass-through friendlies, no swaps — **Accepted** (pass-through allowed; swaps disallowed).
5. Deterministic combat, clear UI feedback, ranged no counterattack — **Accepted**.
6. Strict player→AI loop, AI compute budget ≤ 500 ms — **Accepted**.
7. Saves: 3 manual + autosave ring buffer (5) with timestamps — **Accepted**.
8. Analytics: minimal event set for starts/turns/saves/loads/finishes — **Accepted** (DB storage; manual querying in MVP).
9. Non-functional targets: load/perf budgets, bundle ≤ 300 KB, batched analytics — **Accepted**.
10. Security: server-authoritative validation, anti-forgery, strict CORS, role scoping — **Accepted**.
11. UI affordances: visible city/unit reach via SVG overlays; end-turn shortcut **E**; pan/zoom — **Accepted**.
12. Deployment via CI/CD to DigitalOcean, Dockerized — **Partially accepted** (infrastructure sizing deferred).
13. Privacy/data handling: retention 3 months; delete-account flow; cookie notice — **Accepted**.
14. Starting configuration: 1 AI, fixed spawns, starting units — **Accepted**.
</matched_recommendations>

<prd_planning_summary>
<a>Main functional requirements</a>

* **Authentication & access**: ASP.NET Identity (email/password). No guest play; public About/Gallery.
* **Game loop**: Browser-based, single-player, turn-based (player→AI). AI ≤ 500 ms/turn.
* **Map & movement**: 20×15 hex, pointy-top odd-r; A* pathfinding with uniform costs; full visibility (no fog); 1UPT; pass-through friendlies; move OR attack per turn; pan/zoom; path preview on selection.
* **Combat**: Deterministic damage formula with round-half-up, min 1; attacker first; ranged immune to counters; tie → attacker.
* **Cities & economy**: City reach radius 2 (always outlined); auto-harvest resources per tile each turn; auto-produce units when resource thresholds met (Warrior 10 iron, Slinger 10 stone, 1 unit/turn); city HP 50, Defense 10; regen +4 HP/turn (+2 under siege); capture via melee occupying city after HP ≤ 0; despawn enemy units on final capture.
* **AI**: Deterministic priorities (defend city rules, attack if >60% win chance via single-round evaluator, explore, expand reach, work tiles, produce).
* **Saves & persistence**: 3 manual saves; 5 autosaves (ring buffer at end-turn); retention 3 months; PostgreSQL storage; server authoritative validation.
* **Telemetry**: Events game_start, turn_end, autosave, manual_save, manual_load, game_finish with core properties; stored in DB; manual querying in MVP.
* **UI & assets**: React + Tailwind + Vite; CSS/SVG UI; owned PNG assets; no audio/animations; 1080p fit with zoom/pan; end-turn summary toast; shortcut **E**.
* **Non-functional**: JS bundle ≤ 300 KB gzip; AI ≤ 500 ms; analytics batched (1 call/turn); session timeout 30 min; API 60 req/min; strict CORS & anti-forgery.

<b>Key user stories & flows</b>

1. **Sign in**: User registers/logs in with email/password → session established (30-min idle).
2. **Start game**: User clicks “New Game” → loads fixed 20×15 map with starting city/units.
3. **Play turn**: Select unit → path preview → second click to move; or select target within range to attack (ranged immune to counters); end turn via button or **E**; AI resolves in <500 ms; summary toast shows pending actions.
4. **City overview**: City reach border always visible; resources auto-harvested; unit auto-produced when resources meet costs; spawn near city if free tile exists.
5. **Saving**: Manual save to 1 of 3 slots; autosave after end-turn; access autosave list (timestamps) and manual slots; restore or delete as needed.
6. **Win condition**: Capture all enemy cities; game finish event logged; show result screen.
7. **Account management**: Privacy page, cookie notice; Delete account → purge saves/metrics.

<c>Success criteria & measurement</c>

* From original goals: runs fully in browser; player can move units, end turn, see AI respond; turn loop functional; **save/load (max 3 slots)**; **75%** of players finish their game; **90%** complete **≥5 turns**.
* **Instrumentation**: Use DB events to derive funnels — % starting games, median turns, % reaching ≥5 turns, % finishing (all enemy cities captured), save/load usage.
* **Performance**: AI turn duration tracked; bundle size enforced in build; per-turn analytics batched.

<d>Unresolved issues / clarifications needed</d>

* **Deployment sizing & ingress**: Exact DigitalOcean droplet size, managed Postgres tier, TLS/ingress (NGINX/App Platform) not yet selected.
* **Map JSON schema**: Will be finalized later (after DB schemas); versioning approach defined, but concrete fields/validators pending.
* **AI “expand city reach / work tiles”**: With auto-harvest in MVP, clarify whether AI actively changes worked tiles or if all in-reach tiles are considered “worked” by default.
* **UI save/autosave screens**: Exact layout and copy (naming, sorting, restore/delete confirmations) to be specified in wireframes.
* **Error handling UX**: Exact messages and retry logic for failed saves/loads and connectivity issues (alerts defined; copy and states TBD).
</prd_planning_summary>

<unresolved_issues>

* DigitalOcean deployment details (compute, DB plan, TLS/ingress) — **TBD**.
* Final **map.json** schema fields and validator — **TBD** (versioning and gating agreed).
* Clarify AI behavior for “expand city reach / work tiles” given auto-harvest — **TBD**.
* Detailed save/autosave UI/UX (slot management, confirmations) — **TBD**.
* Error state UX copy and flows for save/load/network failures — **TBD**.
</unresolved_issues>
</conversation_summary>

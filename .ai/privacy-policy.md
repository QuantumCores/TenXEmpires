# Privacy Policy

Effective date: YYYY-MM-DD

This Privacy Policy explains how TenX Empires ("we", "us") collects, uses, and retains information when you use the TenX Empires MVP.

## Summary
- We use email/password authentication (ASP.NET Identity).
- We store game saves and autosaves; saves are retained for 3 months since your last end-turn.
- We record a minimal set of analytics events to understand funnels and reliability.
- When you delete your account, we delete your profile and game saves. We retain analytics in pseudonymous form using a salted hash ("user_key").
- We do not use third‑party trackers; analytics are first‑party and stored in our database.

## Data We Process
### Account
- Email address
- Password hash (never the raw password)
- Authentication/session metadata (e.g., last sign‑in timestamps)

### Gameplay
- Games and participants (server‑authoritative state)
- Cities, units, map references (normalized game state)
- Saves and autosaves (snapshots derived from normalized state)

### Analytics (First‑party)
- Event types: `game_start`, `turn_end`, `autosave`, `manual_save`, `manual_load`, `game_finish`
- Properties: `user_key` (salted hash of your user_id), `game_key` (copy of game id), `turn_no` (if applicable), timestamps, and minimal context (map code/schema version, rng seed/version, participant count)
- Batching: at most one network call per turn from the client

## Purposes and Legal Bases
- Provide and secure the service (authentication, session management)
- Persist and restore game state (saves/autosaves)
- Measure funnels, reliability, and performance (first‑party analytics)

## Retention
- Saves: retained for up to 3 months from your last end‑turn; older saves are purged.
- Account deletion: your account profile and saves are deleted.
- Analytics: retained in pseudonymous form (salted `user_key`) and not deleted when your account is removed, to preserve historical trends. The salt is stored server‑side and not shared externally.

## Your Choices
- You can delete your account at any time from within the app; this removes your profile and game saves.
- You can manage cookies through your browser settings. Essential cookies are required for sign‑in and security.

## Security
- We use server‑authoritative validation, rate limiting, anti‑forgery protections, and role‑scoped access.
- Data access is restricted by role, and row‑level security (RLS) applies to game data.

## Sharing
- We do not sell your data.
- We do not share analytics with third parties; analytics are stored in our own database.

## Contact
If you have questions or requests (access, deletion, questions about this policy), contact us at: privacy@yourdomain.example

We may update this policy to reflect changes to the service. We will indicate the latest effective date above.


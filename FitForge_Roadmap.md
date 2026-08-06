# FitForge — Roadmap

*This file did not exist before this session — recreated fresh, reflecting the real, verified state of the project as of this handover. Keep it updated as the running source of truth going forward.*

---

## Phase 0 — Foundation (shipped)

Auth (login/register, BCrypt, lockout, generic error message), core layered architecture (Controllers → BL → DL → MySQL), PWA shell (installable, service worker, offline page), "Street Set" design system, dashboard, programs (presets + custom builder + sandbox mode), active workout logging core loop, skill tree, AI coach (chat, program proposals, injury report/resolve), onboarding tours.

Security fixes shipped: IDOR on `LogSet`'s session ownership, `BuildActiveWorkoutVM` session-id validation, generic login error.

Reliability: global `unhandledrejection` handler + `fetchJSON()` wrapper so failed requests surface a toast instead of a stuck "Saving" button.

## Phase 1 — Quick wins (shipped)

1. Chart colors read from CSS custom properties at runtime instead of hardcoded hex
2. Accent color synced server-side (`accent_color` column) instead of localStorage-only
3. Native `confirm()` replaced everywhere with the app's own `ffConfirm()` modal
4. Bottom nav labels matched to real page titles
5. BMI card removed from Dashboard (Profile-only now)
6. Test Your Max hub (`/Skills/MaxTest`) — skills + gym lifts, full history table, feeds the real PR pipeline

## Phase 2 — Real fixes (shipped this session)

All five verified against the actual code (not assumed from memory), fixed for real behavior (not cosmetic), and validated — JS via `node --check` after every change, C# via brace/paren balance checks.

1. **Session timer** — now anchored to the session's real server-side `StartedAt`, with client/server clock-drift correction. Survives a page refresh instead of restarting from `Date.now()`.
2. **Edit/reopen a logged set** — any set-dot can now be tapped to reopen and correct that exact set, pre-filled with what was actually recorded, without double-counting session progress. (Sets never round-trip to the server individually — this fix works entirely on the client-side `loggedSets` array, which already gets batch-written at Finish.)
3. **Tempo notation** — new `tempo` column (migration: `FitForge_Migration_TempoField.sql`), full stack wired model → DL → BL → capture UI (4 fields: eccentric/pause/concentric/pause, shown only when the Tempo pill is active) → session history now shows the recorded notation next to the Tempo badge.
4. **Weekly Schedule two-step picker** — program dropdown first, then a day dropdown scoped to just that program. Existing schedules reverse-map correctly on load. Zero backend contract changes — the old flat `<select>` still exists (hidden) as the field of record `saveSchedule()` reads.
5. **Dashboard week-row real states** — derives `rest` / `done` / `upcoming` / `today` / `missed` per day by cross-referencing `ActiveSchedule.Slots` against `RecentSessions`, replacing the old binary done/not-done. Added a subtle "today" ring independent of state color so today stays locatable even on a rest day.

Also bumped the service worker cache version (`fitforge-v20`) since `app.css` changed this session.

**Known pre-existing issue flagged, not yet fixed:** `FitForge_Migration_SetTypesPlateMath.sql` still hardcodes `TABLE_SCHEMA='fitforgedb'` / `USE fitforgedb` — the exact bug a past session's handover doc said was fixed, but that fix apparently only landed in a combined `FitForge_Migration_ALL.sql` that isn't actually in this project. Needs patching to `TABLE_SCHEMA=DATABASE()` like the other migrations.

## Phase 3 — Not started

Ideas raised but deliberately not built yet:

- **Missed-day affordance** — right now a "missed" day on the dashboard is just a visual flag with no action attached. Could offer a late-log or explicit "mark skipped" action from the dot.
- Push/pull-balance checking in the AI coach's program generation, using the already-seeded but unused `movement_pattern` / `is_compound` exercise classification columns
- Exercise ordering logic using the same classification data

**Explicitly deprioritized (not bugs, don't re-flag as urgent):**
- Forgot password / email verification UI (DB columns + `EmailService` exist, no controller/UI wiring — deliberate)
- OAuth (Google/Apple sign-in)
- Deload / periodization programming logic

---

## Standing constraints (carry forward)

- No dotnet SDK or MySQL in the AI sandbox — all validation is static (brace/paren balance for C#, `node --check` with proper Razor-expression neutralization for JS — never brace-counting alone).
- Never hardcode a schema name in migrations — actual schema is `defaultdb`, not `fitforgedb`. Use `TABLE_SCHEMA=DATABASE()`.
- SQL string literals: single-quotes only (host has `ANSI_QUOTES` behavior).
- Every migration idempotent (check `information_schema` before `ALTER`/`CREATE`).
- Never use partial views — this codebase inlines everything per-page by convention.
- Bump the service worker `CACHE` constant in `wwwroot/sw.js` any time `app.js` or `app.css` changes — not needed for `.cshtml`-only or pure C#/SQL changes.
- Delete the stray `{Controllers,BL,DL,Models,Views...}` directory if it reappears in an uploaded zip (an old brace-expansion `mkdir` artifact from upstream tooling).

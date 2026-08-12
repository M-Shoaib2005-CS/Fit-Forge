# FitForge — Roadmap to "Best Calisthenics App"

**Current state rating: 7.5/10** *(was 7/10 — see "Honest re-rate" below for why it only moved a little)*

Strong engineering fundamentals (parameterized SQL, BCrypt + lockout, real production hardening, offline-safe draft recovery) held back by a thin exercise library, an unverified core logging-speed metric, and zero progress yet on the AI coach's biggest whitespace opportunity (recovery-awareness). The one real security bug from the original assessment is fixed. This session closed five more real functional gaps in the core logging loop — none of them differentiators, all of them things that would have quietly eroded trust in daily use.

**What already sets FitForge apart from Hevy / Strong / Fitbod / Jefit:**
- Calisthenics-first skill tree with requirement-gated unlocks (none of the big four have this)
- Injury-aware exercise flagging + automatic alternative suggestion
- A real conversational AI coach (Gemini-backed), not a black-box algorithm or a paid bolt-on
- Adaptive per-exercise targets that update after every session
- PWA with push notifications and offline draft recovery for in-progress workouts

**Honest re-rate — why only +0.5, not more:** everything shipped this session was debt-paying, not moat-building. A timer that lied on refresh, no way to fix a fat-fingered rep count, a Tempo pill that captured no data, a schedule dropdown that became unusable past one program, a dashboard signal that couldn't tell "rest day" from "missed workout" — these are exactly the kind of rough edges that make a real user (your sister, or anyone else) quietly lose a bit of trust in the app, even if they can't articulate why. Fixing them was worth doing and worth doing carefully. But none of it touches what would actually separate FitForge from Hevy/Strong: the exercise library is still thin, the skill tree still hasn't grown past its original seed, the core "how fast can you log a set" metric is still unmeasured, and the recovery-awareness gap — the single biggest whitespace in the whole category per every competitor review — hasn't been started. The score can't move meaningfully until Phase 2 gets real attention.

---

## Phase 0 — Fix before anything else ✅ done
- [x] Close the `LogSet` IDOR: verify `session_id` belongs to the requesting `uid` server-side
- [x] Fix `BuildActiveWorkoutVM` ignoring the `sessionId` route param
- [x] Generic login error message
- [x] Delete the stray `{Controllers,BL,DL,Models,Views...}` leftover directory (recurs occasionally in re-uploaded zips — delete on sight)
- [x] Shared `fetchJSON` helper + global unhandled-rejection safety net

## Phase 1 — Nail the core loop (this decides whether people stick around)
- [ ] **Still not done — the most important unchecked item on this whole roadmap.** Time and map the exact tap sequence to log a single set in the Active workout view, benchmarked directly against Hevy/Strong. Every fix this session made the loop more *correct*; none of them measured or improved raw *speed*.
- [x] Rest timer — automatic, already good, confirmed by reading the real code
- [x] Plate/weight math helper
- [x] Warm-up set / AMRAP / drop-set types — **and, as of this session, Tempo sets now actually capture data** (eccentric-pause-concentric-pause notation), closing the last gap in this line. Previously the Tempo pill existed but recorded nothing.
- [ ] Reusable templates/routines — confirm current Programs/Days model covers "start this exact workout again" with minimal taps

## Phase 2 — Go deeper on what already makes you different
- [ ] Expand the skill tree past its current 8-skill seed — this is the real moat, still not invested in
- [x] **Exercise library grown 45 → 116** — sourced from the [hasaneyldrm/exercises-dataset](https://github.com/hasaneyldrm/exercises-dataset) (data is MIT; GIF media is © Gym visual, redistributed there "with permission" that doesn't extend further — flagged to the user; data-only was brought in now, media licensing is a separate decision to make before wider distribution). Started from 1324 candidates, filtered to genuinely well-known, non-redundant movements (no "v.2" duplicates, no gimmick hybrid moves, no obscure single-use machine variants) — 71 net-new after removing exact/near duplicates of the existing 45. Landed at 116 total, inside the roadmap's own "past 45, up to ~100-150" target zone. Migration: `FitForge_Migration_ExerciseLibraryExpansion.sql`, using explicit `exercise_id` values 46-116 (confirmed with the user nothing had been added to the table since the original 45, so this range was safe to fix directly) — idempotent, checked by both id and name. GIFs for all 71 are already placed at `wwwroot/images/exercises/46.gif` through `116.gif`, matching those same fixed IDs — no separate script or manual mapping step needed.
- [x] **AI coach exercise catalog is now live, not hardcoded** — the actual blocker to "just add more exercises." It used to be a hand-maintained `const string` listing exactly the original 45 seed exercises; anything added via the builder was silently invisible to the coach forever, no matter how large the library grew. Now built from `ExerciseDL.GetAll()` at request time, with optional muscle-group/equipment filtering already wired in (currently unused — full catalog is still cheap at this size — but ready to flip on with a one-line change at the call site once the library grows past ~100-150 exercises, where a long flat list measurably hurts accuracy, not just cost).
- [x] **Recovery-aware volume coaching** — built this session (see below). The biggest whitespace in the category is no longer fully unstarted.
- [ ] Injury system: track recovery timelines, auto-suggest reintroduction

## Phase 3 — Breadth and retention — untouched this session
- [ ] Decide deliberately on social/sharing scope
- [ ] Deeper analytics/graphing
- [ ] Wearable/HRV integration if recovery-aware coaching proves valuable

---

## Recently shipped

### Recovery-aware volume coaching (this session)
The biggest single item on the whole roadmap — designed collaboratively over many rounds of spec refinement before any code, then built as a fully deterministic backend (Gemini never computes numbers or picks exercises, only phrases the result — keeps this to 0-1 model calls per suggestion, protecting the free-tier message budget).

**Detection logic:**
- Every finished session logs a real volume ratio (sets actually completed ÷ sets scheduled for that day, excluding warm-ups and skipped sets) to an append-only `day_volume_log` table — nothing is inferred from stale running counters.
- **Per-day anchor detection:** the first day-type whose last 2 occurrences (within a 20-day recency window) are both low (<50%) or both high (>200%) becomes the "anchor." Any other day-type that also had a matching low/high hit somewhere between the anchor's two occurrences rides along as a bundled option.
- **Whole-program detection:** 3 consecutive sessions overall (any day types) all low or all high → whole-program suggestion, takes priority over any per-day one at that moment.
- Declining, or accepting only part of a bundled group, resets **all** involved days and applies a 14-day cooldown per day (the cooldown timestamp doubles as the reset — history before it is ignored).

**What gets suggested:**
- Low volume → cut the isolation exercise (using the real, previously-unused `is_compound` column) carrying the most sets by one set, or drop it entirely if already at the floor. Compound exercises are only touched if the day has no isolation work at all.
- High volume → suggest adding an exercise, with 3-4 candidate pills pulled from muscle groups already present on that day, excluding what's already scheduled.

**UX flow:** coach icon gets a small "1" badge (in-app only — not a push notification) → opening chat shows the suggestion as a natural message with Fix/Fix+Bundled/Leave-it pills → any "add exercise" day prompts a follow-up candidate-pill pick → a preview card shows exactly what will change → explicit Apply/Cancel. A "See coach suggestion" pill also appears on the Workouts tab, deep-linking straight into chat. Same badge-check call drives both surfaces — no duplicate network requests.

New tables: `day_volume_log`, `day_suggestion_cooldown`, `coach_suggestions`. New `RecoveryCoachingDL`/`RecoveryCoachingBL`, wired into `FinishSession` right after the existing per-exercise adaptive-target logic. Every table/column name was cross-checked against the actual schema, not assumed. **Not yet build-verified** — no dotnet SDK available in the sandbox, so this was validated statically (brace/paren balance, `node --check` on all JS) but should be the first thing checked on your next local build, since it's structurally the most novel C# added this project (new DI classes, JSON payload serialization).

### Real-fixes round
All five verified against the actual code — not assumed from memory — fixed for real behavior, not cosmetically, and validated: JS via `node --check` with proper Razor-expression neutralization after every change, C# via brace/paren balance checks on every touched file.

1. **Session timer** — now anchored to the session's real server-side `StartedAt`, with client/server clock-drift correction. Survives a page refresh instead of resetting to 0:00.
2. **Edit/reopen a logged set** — any set-dot can be tapped to reopen and correct that exact set, pre-filled with what was actually recorded, without double-counting session progress. Pure client-side fix — sets never round-trip to the server individually; they batch-write at Finish, so the `loggedSets` array was the right place to fix this, not a new endpoint.
3. **Tempo notation captured** — new `tempo` DB column (idempotent migration, `TABLE_SCHEMA=DATABASE()`), wired through model → DL → BL → capture UI (4 fields, shown only when the Tempo pill is active) → session history now shows the recorded notation next to the badge.
4. **Weekly Schedule two-step picker** — program dropdown first, then a day dropdown scoped to that program. Zero backend contract changes — `saveSchedule()` still reads the same hidden field of record.
5. **Dashboard week-row real states** — `rest` / `done` / `upcoming` / `today` / `missed`, derived from `ActiveSchedule.Slots` cross-referenced against `RecentSessions`, replacing a binary done/not-done that couldn't tell a rest day from a missed one.

Also: bumped the service worker cache (`fitforge-v20`, since `app.css` changed this session).

**Known pre-existing bug flagged, not yet fixed:** `FitForge_Migration_SetTypesPlateMath.sql` still hardcodes `TABLE_SCHEMA='fitforgedb'` / `USE fitforgedb` — the exact bug a past round's notes claimed was fixed, but that fix apparently only ever landed in a combined `FitForge_Migration_ALL.sql` that isn't actually in this project. Needs patching to match the other migrations.

**Idea raised, not built:** a missed-day dot on the Dashboard currently has no action attached to it — could offer a late-log or explicit "mark skipped." Parked until UI/feel pass.

**Also parked:** badge/achievement art and styling — 25 badges exist (see catalogue below), currently single-emoji icons, feel "meh." User is making custom images; badge UI/logic redo to follow once art exists.

### Sister's-feedback round
- [x] Customizable coach name (DB field + Profile UI + fed into the AI system prompt)
- [x] "Replay Tutorials" popup on Profile
- [x] Multi-tour engine — Dashboard/Programs/Skills/Workouts tracked independently
- [x] Programs tour → sandbox handoff (practice program-building, nothing persisted)
- [x] Skills and Workouts/PR page tours added
- [x] Service worker cache version bumped alongside JS changes (v9 → v10)

---

## Badge/achievement catalogue (for reference — art in progress)

25 badges seeded in `FitForge_Schema.sql`, single-emoji icons only, no custom art yet:

**Workout milestones:** First Rep · Getting Serious (10) · Half Century (50) · Century Club (100) · Grind Never Stops (250) · Early Bird (before 7 AM) · Night Owl (after 10 PM) · Volume King (10,000kg single session)

**Streaks:** Three-Peat (3d) · Week Warrior (7d) · Month Master (30d) · Unstoppable (100d) · Perfect Week

**Personal records:** Record Breaker (1st PR) · PR Machine (5 PRs) · Elite Performer (25 PRs)

**Skills:** Skill Seeker (1st unlock) · Skill Master (master any skill) · Renaissance Body (master all)

**Health tracking:** Body Tracker (10 weigh-ins) · Hydration Hero (7-day streak) · First Measurement

**Collector tier** (per-exercise Bronze/Silver/Gold/Diamond/Legend rating): Bronze Collector · Silver Collector · Gold Collector · Diamond Miner · Legend Born

---

## Working notes
- Competitive landscape research done: July 2026 (Hevy, Strong, Fitbod, Jefit — verify again before launch)
- Project builds successfully as of last local test.

## Standing constraints (carry forward every session)
- No dotnet SDK or MySQL in the AI sandbox — validation is static only: brace/paren balance for C#, `node --check` (with proper Razor-expression neutralization, never brace-counting alone) for JS.
- Never hardcode a schema name in migrations — real schema is `defaultdb`, not `fitforgedb`. Use `TABLE_SCHEMA=DATABASE()`.
- SQL string literals: single-quotes only (host has `ANSI_QUOTES` behavior).
- Every migration idempotent (check `information_schema` before `ALTER`/`CREATE`).
- Never use partial views — this codebase inlines everything per-page by convention.
- Bump the service worker `CACHE` constant in `wwwroot/sw.js` any time `app.js` or `app.css` changes.
- Delete the stray `{Controllers,BL,DL,Models,Views...}` directory if it reappears in an uploaded zip.

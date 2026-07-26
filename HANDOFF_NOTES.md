# FitForge — Session Handoff Notes

**Read this first.** This project has had a long back-and-forth debugging/feature session.
This file summarizes everything so a new Claude session (or the next person) has full
context without re-discovering it from scratch.

## What FitForge is
- ASP.NET Core 8 MVC (C#), Razor views, MySQL (hosted on **Aiven**), deployed on **Render**
  (free tier) at `https://fit-forge-o082.onrender.com`
- Also a PWA (installable, service worker, offline shell)
- Has an AI coach feature powered by **Gemini** (model configurable, was using
  `gemini-3.5-flash-lite`)
- Owner/developer: a CS student, building this independently (started as a WPF prototype,
  then a university lab project "FitnessASP", now this ASP.NET Core MVC version)

## Architecture pattern (for anyone editing code)
Controllers → BL (business logic) → DL (data layer, raw SQL via a small `DB` helper wrapper
around MySqlConnector, no ORM). Models all live in one file: `Models/AllModels.cs`.
`BaseController(UserDL)` gives `Uid` (nullable int, from session) to every controller.

---

## Everything fixed/built this session, in order

### 1. AI Coach reliability (`GeminiService.cs`)
- **Bug:** Gemini's JSON schema for the coach's program-building output required `days` to
  *exist* but not to have anything *in* it — so Gemini could satisfy the schema with an
  empty `days: []`, passing validation but producing a useless "proposal," which showed the
  user a generic "didn't come through" message regardless of prompt complexity.
- **Fix:** added `minItems: 1` on the days array, added `propertyOrdering` throughout (Gemini
  defaults to alphabetical property ordering, which hurts weaker/lite models' coherence), and
  added logging of the raw model text whenever a `proposal` comes back without a usable
  program — previously that failure mode was completely silent.

### 2. Program editing (previously impossible)
- Programs (own-built, coach-built, or cloned from a preset) could be created and deleted,
  but never **edited** — a real gap, not just polish.
- Added `ProgramsController.Update`, `ProgramBL.UpdateProgram`, DL methods
  (`CanEdit`, `UpdateProgramMeta`, `ClearDayDependents`, `DeleteDaysOnly`).
- Ownership rule: same as delete — `user_id` must match and must not be `1` (presets are
  owned by reserved `user_id=1`, never editable directly).
- **Known trade-off:** editing recreates a program's days under the hood (simpler/safer than
  diffing), so day_ids change. This means logged session history tied to those specific old
  days gets cleared on edit — same behavior as the existing Delete flow, not a new risk.
  The user's **weekly schedule assignment is preserved** across an edit by remapping weekday
  → new day_id by day-order position (was NOT preserved before this was added — editing used
  to silently blow away your schedule).
- Frontend: Edit button on own-program cards reuses the existing Build Program form,
  pre-filled.

### 3. UX/bug fixes
- **Dashboard "still shows today's workout after finishing" bug:** `BuildDashboard` only
  checked for an *open* session, never checked whether one had already been *finished*
  today. Added `WorkoutDL.GetCompletedToday` + a real "done for today" state on the
  dashboard (shows finish time/duration/sets, with a secondary "log another session" option).
- **Blank white flash between page navigations** (this is a multi-page app, not an SPA):
  fixed via inline critical CSS in `_Layout.cshtml` setting background color before any
  external stylesheet loads.
- **Onboarding tour "circles wrong box" bug:** the tour's spotlight positioning used
  `scrollIntoView({behavior:'smooth'})` + a fixed 220ms timeout before measuring the target's
  position — on any scroll longer than that, it measured mid-scroll. Replaced with a
  rect-stability check (poll `getBoundingClientRect()` across animation frames until it
  stops changing).
- **Delete confirmation:** was already using native browser `confirm()` (functional, but
  visually jarring in a themed dark app). Replaced with a styled in-app modal (`ffConfirm()`
  in `app.js`, markup in `_Layout.cshtml`) — reusable anywhere else that needs a confirm step.
- Razor gotcha fixed: `<option selected="@(x ? "selected" : null)">` is the correct pattern —
  a bare `@(...)` expression floating in a tag-helper-bound element's attribute area throws
  **RZ1031**. If you see that error again anywhere else, this is the fix pattern.

### 4. Notification system (the big addition — built from scratch)
Three notification types: **workout reminders** (+ rest-day messages), **injury check-ins**
(every 3 days while an injury is Active), **coach encouragement** (if 2+ days since a
finished session). User controls this via Settings → Notifications: All / Workout-only / Off,
plus a **per-user reminder time** and **auto-captured timezone**.

**Architecture — IMPORTANT, this changed mid-session:**
- Originally built as an in-process `BackgroundService` ticking every 30 min.
- **This was replaced** because Render's free tier suspends the whole app process after
  ~15 min of no HTTP traffic, silently killing any in-process timer with it.
- Current design: `NotificationsController.Tick(string key)` — a secured GET endpoint,
  protected by `Notifications:CronSecret`, meant to be hit periodically by an **external**
  scheduler (the user set up **cron-job.org**, hitting it every few minutes). This works
  regardless of hosting tier. **`NotificationBackgroundService.cs` was deleted** — if you see
  any reference to it, that's stale/from an earlier point in the session, ignore it.
- Time-of-day matching happens in **C#** (`TimeZoneInfo`), not SQL — MySQL/Aiven doesn't
  reliably have IANA timezone conversion tables loaded, .NET on Linux handles IANA ids
  natively. `NotificationDL.GetWorkoutReminderCandidates` returns all still-eligible
  candidates for today's weekday; `NotificationBL` filters by whether each user's own local
  clock has passed their `reminder_time`.
- Timezone is captured automatically client-side (`Intl.DateTimeFormat().resolvedOptions().timeZone`)
  the moment someone subscribes to push — no manual input needed.
- Push delivery: `WebPush` NuGet package (**pinned to v1.0.13** — v1.0.16 doesn't exist on
  nuget.org, that was a real mistake caught during a build), VAPID keys.

**DB additions** (`FitForge_Migration_Notifications.sql`):
  - `users.notification_pref` (varchar, All/WorkoutOnly/Off)
  - `users.reminder_time` (TIME)
  - `users.timezone` (varchar, IANA id, default UTC)
  - `push_subscriptions` table (one row per device/browser)
  - `notification_log` table (dedupe — prevents double-sends per day/N-days per type)
  - **Gotcha hit and fixed:** Aiven's MySQL parses **double-quoted strings as identifiers**
    (ANSI_QUOTES-like behavior), so the original migration's `IF(@col=0, "ALTER TABLE...",
    'SELECT 1')` pattern failed with "Unknown column" errors. Fixed by switching to
    single-quote delimiters with escaped inner quotes (`''All''`). If writing any more
    dynamic SQL for this DB, **use single quotes, not double quotes**, for string literals.

**Known limitations, stated honestly (not silently glossed over):**
  - The weekday used to pick "today's" schedule slot is based on the **server's** clock, not
    each user's local day — near midnight, a user in a very different timezone from the
    server could theoretically get evaluated against the wrong day. Minor edge case, not
    fixed (would require a bigger per-user-weekday rewrite).
  - No per-user custom time for injury check-ins / encouragement — those are governed purely
    by their dedupe windows (3 days / 2 days), not tied to the reminder_time setting.

### 5. `pwa.js` was orphaned — real bug, not something I broke
- Discovered mid-session: `wwwroot/js/pwa.js` (where all push-subscribe logic lives, and
  where pre-existing "Install PWA" prompt logic already lived) was **never referenced by any
  `<script>` tag anywhere in the app** — not in `_Layout.cshtml`, not in any view. It's
  unclear if this predates this session or if it was always silently broken.
- **Fixed** by adding `<script src="~/js/pwa.js">` to `_Layout.cshtml`, right after `app.js`.
- Also hardened `enablePushNotifications()` to log each step to console and surface the
  *actual* browser error in the toast instead of one generic "Could not enable notifications"
  message for every possible failure — this is what made the *next* bug (see below)
  diagnosable at all.

### 6. Real subscribe failure found and fixed
- After all the above, enabling notifications still failed with the generic message.
- Root cause: the `timezone` column migration step had silently not been applied (Aiven
  quoting issue above meant the script errored out partway through and Workbench kept
  running past the error without completing every statement). `UserDL.UpdateTimezone` was
  throwing on missing column, which killed the entire `/Notifications/Subscribe` request with
  a 500 — so even though the actual push subscription succeeded fine, the whole request
  failed and the user saw "could not enable."
- Fixed two ways: (1) had the user run the missing `ALTER TABLE... timezone` statement
  directly, (2) **hardened `UserDL.UpdateTimezone` to swallow DB errors** so a missing/broken
  timezone column can never again take down the core subscribe flow — timezone accuracy is a
  nice-to-have, not something that should be able to block notifications working at all.

---

## Current state as of end of session

- **A real push notification was successfully received on the user's Android phone** —
  confirmed via screenshot. Full pipeline (Vapid keys → subscribe → DB → cron-job.org →
  `/Notifications/Tick` → send → phone) is working end to end.
- **In progress, not yet resolved:** the notification's *appearance* doesn't match the
  in-chat mockup — no FitForge icon showing (generic circle instead), no "Start workout"
  action button. Two separate causes identified so far:
  1. `icon-192.png` was confirmed via Pillow to be **mode RGB, no alpha channel**. Android
     requires the `badge` (small/status-bar icon) to be a transparent-background monochrome
     image — it derives a white silhouette from the alpha channel. An opaque RGB PNG can't be
     used that way, which is the likely reason a generic fallback icon is showing instead.
     **Not yet fixed** — next step is either (a) generate a proper alpha-channel monochrome
     badge asset, or (b) simplify by dropping the `badge` field entirely and only setting
     `icon` (letting Android supply its own default small icon), whichever the next session
     decides is worth the effort.
  2. The **"Start workout" action button was never actually wired into the real service
     worker push handler** (`sw.js`) — it only ever existed in the illustrative in-chat
     mockup shown earlier in the conversation. `sw.js`'s `push` event handler currently only
     sets `body`/`icon`/`badge`/`data.url`, no `actions` array. To add it: the server payload
     (`PushNotificationService.SendToUserAsync`) would need a `type` field so `sw.js` can
     decide whether to include an actions array (only relevant for workout-reminder type, not
     rest-day/coach/injury messages), and `notificationclick` would need a
     `e.action === 'start-workout'` branch.

## Deployment/environment state (as of last known check-in)
- Render env vars set: `Gemini__ApiKey`, `Vapid__PublicKey`, `Vapid__PrivateKey`,
  `Vapid__Subject`, `Notifications__CronSecret`, `DOTNET_USE_POLLING_FILE_WATCHER=true`
  (fixed an earlier inotify-limit crash on Render's container).
- cron-job.org is configured hitting `/Notifications/Tick?key=...` on a schedule.
- Aiven MySQL migration has been run (with the quoting fix + the manually-added `timezone`
  column) — as of last check, `DESCRIBE users` showed `notification_pref`, `reminder_time`
  present; `timezone` was added via a manual one-line `ALTER TABLE` after being caught missing.
  **Worth double-checking `DESCRIBE users` again** in a new session before assuming this is
  fully settled, since it went through a couple of rounds of partial failures.
- `WebPush` NuGet package pinned to `1.0.13` in `FitForge.csproj` (1.0.16 doesn't exist,
  caught during a local build attempt).

## If picking this up fresh
1. Confirm the DB migration state first (`DESCRIBE users`, check the two new tables exist)
   before assuming anything about notifications works.
2. The icon/badge issue above is the most recent open thread — start there if continuing
   notification polish.
3. Everything else in this document is considered **done and working** as of this handoff,
   confirmed either by the user directly or by balance-checked code review (no live build
   environment was available during this session to run an actual `dotnet build` — all
   verification was manual code review + brace/paren balance checks + the user's own local/
   Render build output when they shared it).

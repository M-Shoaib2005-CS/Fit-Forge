# FitForge v5 — Polish & Fixes

## What was fixed

### Critical bugs
- **Dashboard skills** now show active unlocked skills (was always empty)
- **Program progression style** is respected when finishing workouts (was hardcoded to Adaptive)
- **Target reps** are saved correctly to the database (was always 0)
- **Calendar** no longer crashes on days with multiple sessions
- **Skill achievements** fire on unlock and mastery
- **Broken auto-advance** during workouts disabled (was advancing steps using unlock requirements)

### Features wired up
- **PR toasts** show exercise names instead of IDs
- **Water goal editor** + delete individual entries in dashboard modal
- **Recent sessions** and **avg session time** shown on dashboard
- **Muscle group filter** in program builder now works correctly
- **RPE** backfills to all logged sets when selected

### Mobile & UX
- Removed double-tap/click bug on mobile buttons
- Added `touch-action: manipulation` for faster taps
- Improved water tracker UI with settings button

### Security
- Database password moved to `appsettings.Development.json` only (not in production config)

## Setup

1. Install .NET 8 SDK and MySQL 8
2. Import `FitForge_Schema.sql` into MySQL
3. Set your MySQL password in `appsettings.Development.json`
4. Run: `dotnet run`
5. Open http://localhost:5000

## Package

Run `build-and-zip.ps1` to build and create a distributable zip.

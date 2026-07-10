# FitForge — Complete Fitness App

## Setup
1. Create MySQL database: `CREATE DATABASE fitforgedb;`
2. Run `FitForge_Schema.sql` — creates all tables and seed data
3. Set your DB password in `appsettings.Development.json`
4. `dotnet run`

## Architecture
```
Controllers (thin — read request, call BL, return view/JSON)
    ↓
BL/ (business logic — validation, rules, orchestration)
    ↓
DL/ (SQL only — no logic, just data)
    ↓
MySQL (fitforgedb)
```

## Key Features
- **Adaptive rep engine** — targets adjust after every session based on performance + progression style
- **Program builder** — build named programs with custom days and exercises
- **Weekly schedule** — assign program days to Mon–Sun calendar
- **Smart injury system** — pick body part + injury type → exercises flagged automatically with alternatives
- **Skills progression** — step-by-step calisthenics skill unlocks with requirement checks
- **Personal records** — auto-detected for max reps, max weight, max volume per exercise
- **Weight tracking** — log body weight history over time
- **Streaks** — consecutive workout day tracking
- **Theme persistence** — dark/light saved server-side, follows user across devices

## Image Assets Needed
See IMAGE_ASSETS.md

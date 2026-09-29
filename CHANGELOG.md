# Changelog

## [1.0.1] - 2026-09-29
- Package now ships `Documentation~` (full guide + screenshots) and `Samples~/Demo` (LeagueDemo). They were missing from 1.0.0 because a global `*~` gitignore rule excluded them; `.gitignore` now re-includes them.
- No code changes.

## [1.0.0] - 2026-09-28
First release.
- Weekly league: 10 leagues (Bronze → Diamond), groups of 30, top 7 promote, bottom 5 demote, week starts Monday 00:00 UTC (all configurable).
- Three modes, switchable at runtime: **Simulated** (offline, deterministic simulated players with personalities and live scores),
  **Online** (real players), **Hybrid** (real players + simulated fill-ins for empty seats).
- Firebase backend (Firestore + Anonymous Auth, free Spark plan): group assignment by transaction, debounced and capped XP uploads,
  offline tolerance, security rules. Compiled only with the `LEAGUE_FIREBASE` define.
- Backend interface + registry for PlayFab / custom servers.
- UI: league board (badges, countdown, medals, promotion/demotion zones, highlighted player row, auto-scroll, live refresh)
  and end-of-week result popup; portrait, landscape and auto layouts; full theming (fonts, colours, UI-kit sprites, ribbon, border).
- `LeagueDemo` sample, full documentation.

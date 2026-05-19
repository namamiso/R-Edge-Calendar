# EdgeCalendar (Win11 Right-Edge Calendar)

Lightweight Windows 11 resident calendar panel.

## Features (MVP)
- Right-edge hover (2–6 px, dwell 100 ms) opens a no-activate panel.
- Google Calendar sync (single account), aggregated calendars with per-calendar toggle.
- Create / edit / delete events inside the panel.
- Sync window: past 31 days to next 31 days.
- Fullscreen suppression for hover-open.
- No WebView2 / No Electron.

## Dev prerequisites
- .NET SDK 8.x
- Windows 11

## Build
- dotnet build
- Release builds that include Google sign-in require an OAuth Client ID injected at publish time; see docs/OAUTH.md and docs/RELEASE.md.

## Docs
- docs/PRD.md (requirements)
- docs/UI-SPEC.md (panel behavior)
- docs/SYNC-SPEC.md (sync rules)
- docs/OAUTH.md (Google OAuth setup)
- docs/ADR/ (architecture decisions)
- docs/RELEASE.md (release steps)

## Security
- docs/SECURITY.md

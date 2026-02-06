# SYNC-SPEC — Google Calendar sync (MVP)

## 1. Sync window
- Fixed window: [today-31 days, today+31 days]
- "today" uses local date.
- Start = 00:00:00 local of today-31
- End = 23:59:59 local of today+31

## 2. Sync triggers
- Startup: initial sync (throttled)
- Panel open: lightweight sync (cooldown, e.g., 30–60 sec)
- Periodic: every 10 minutes (configurable later)

## 3. Conflict policy
- Use ETag and If-Match for updates.
- On conflict:
  - Fetch server latest
  - Save local draft JSON to ConflictLog
  - Notify user

## 4. Offline edit
- Not supported in MVP. If network fails, show error state and keep local cache.

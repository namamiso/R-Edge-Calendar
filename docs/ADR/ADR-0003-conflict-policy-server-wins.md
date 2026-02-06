# ADR-0003: Conflict policy = server wins + local draft is saved

## Status
Accepted

## Context
User edits may collide with edits from other devices. Silent overwrites are unacceptable.

## Decision
Use If-Match with ETag.
On conflict:
- Fetch server latest
- Save local draft JSON to ConflictLog
- Notify user

## Consequences
- Positive: prevents silent data loss
- Negative: requires conflict UX later (viewer/resolve)

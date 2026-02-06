# API-MAPPING — Event mapping

## Google event fields used (MVP)
- id -> Events.EventId
- summary -> Events.Title
- start.date / start.dateTime -> Events.StartUtc + Events.IsAllDay
- end.date / end.dateTime -> Events.EndUtc + Events.IsAllDay
- location -> Events.Location
- description -> Events.Notes
- updated -> Events.UpdatedAtUtc
- etag -> Events.ETag
- status -> Events.Status
- htmlLink -> Events.HtmlLink

## Notes
- All-day:
  - start.date/end.date are local-date semantics. Store normalized UTC boundaries or store as local date boundaries + flag.
- Time zones:
  - Display in local time. Convert carefully when storing and when sending PATCH/PUT.

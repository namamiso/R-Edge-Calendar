# Privacy Policy

Effective date: 2026-05-19

EdgeCalendar is a lightweight Windows desktop calendar app that connects to Google Calendar only to display and manage calendar events inside the app.

## Data We Access

When you connect a Google Account, EdgeCalendar requests access to:

- Your Google Calendar event data, so the app can display, create, edit, and delete events.
- Your Google Calendar list, so the app can show multiple calendars and let you choose which calendars to display.

EdgeCalendar does not request access to Gmail, Drive, Contacts, or other Google services.

## How We Use Data

Google Calendar data is used only for calendar features inside EdgeCalendar:

- Displaying events in the calendar panel.
- Creating new events.
- Editing existing events.
- Deleting events.
- Listing calendars for per-calendar display selection.

EdgeCalendar does not sell, rent, or share your Google user data with third parties.

## Local Storage

EdgeCalendar stores app data locally on your Windows device under:

```text
%LOCALAPPDATA%\EdgeCalendar
```

This may include:

- Calendar cache data in SQLite.
- OAuth tokens used to keep you signed in.
- Conflict logs when an event update conflict occurs.

OAuth tokens are protected using Windows user-level data protection APIs. Tokens are not committed to the repository and are not included in release packages.

## Network Transmission

EdgeCalendar sends calendar requests only to Google Calendar API endpoints required for the app's calendar features.

The app does not intentionally transmit your calendar data to any non-Google third-party service.

## Logs

EdgeCalendar must not log OAuth access tokens, refresh tokens, authorization codes, or raw authorization headers.

If diagnostic logs are added in the future, they should avoid storing sensitive calendar details unless explicitly required for troubleshooting.

## Data Deletion

You can remove local EdgeCalendar data by deleting:

```text
%LOCALAPPDATA%\EdgeCalendar
```

You can also revoke the app's Google access from your Google Account security settings:

```text
Google Account > Security > Third-party apps and services
```

## Children

EdgeCalendar is not directed to children and does not knowingly collect data from children.

## Changes

This policy may be updated as the app evolves. Material changes should be documented in the repository.

## Contact

For privacy questions, contact the maintainer through the support email shown on the Google OAuth consent screen or through the project's GitHub repository.

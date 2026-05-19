# OAuth

## Goal

End users should connect Google Calendar by clicking the Google sign-in flow. They should not create a Google Cloud project or enter a client secret.

## Client ID Policy

The official Google Desktop OAuth Client ID is not stored in the public repository.

Supported configuration sources, in priority order:

1. `EDGE_CALENDAR_GOOGLE_CLIENT_ID` environment variable for local development.
2. `GoogleOAuthClientId` MSBuild property for release builds.
3. DPAPI-protected local fallback saved by the development settings dialog.

Example release build injection:

```powershell
dotnet publish src\EdgeCalendar.App\EdgeCalendar.App.csproj -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -p:GoogleOAuthClientId="YOUR_DESKTOP_CLIENT_ID"
```

Do not commit real OAuth client IDs, client secrets, downloaded Google credential JSON, or signing material.

## Flow

- OAuth uses the system browser and a loopback callback.
- Authorization uses PKCE.
- The normal app flow does not require a client secret.
- A client secret may be accepted only as a local development or legacy local fallback when one is already present through `EDGE_CALENDAR_GOOGLE_CLIENT_SECRET` or DPAPI-protected local settings.
- Release builds must not depend on a client secret.

## Scopes

The current minimized MVP scope set is:

- `https://www.googleapis.com/auth/calendar.events`
- `https://www.googleapis.com/auth/calendar.calendarlist.readonly`

These scopes support event CRUD plus calendar list discovery without requesting full calendar account access. If an endpoint requires broader access, record the reason before expanding scopes.

## Public distribution requirements

Minimized scopes do not remove Google OAuth publication requirements.

- The official Google Cloud project still needs an OAuth consent screen configured for the app.
- Public distribution may require Google OAuth verification for the requested Calendar scopes.
- Until consent screen verification and test-user restrictions are resolved, `0.1.0-alpha` should be treated as a limited alpha, not unrestricted public distribution.
- Keep the consent screen, privacy policy, README, and release notes consistent with the exact scopes requested by the app.

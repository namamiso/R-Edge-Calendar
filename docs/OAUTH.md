# OAuth

## Goal

End users should connect Google Calendar through the system browser without creating a Google Cloud project or entering a client secret.

## Client ID Policy

The official Google Desktop OAuth Client ID is not stored in the public repository.

Supported Client ID configuration sources, in priority order:

1. `EDGE_CALENDAR_GOOGLE_CLIENT_ID` environment variable for local development.
2. `GoogleOAuthClientId` MSBuild property for release builds.
3. DPAPI-protected local fallback saved by the development settings dialog.

Supported Client Secret configuration sources:

1. `EDGE_CALENDAR_GOOGLE_CLIENT_SECRET` environment variable for local development.
2. `GoogleOAuthClientSecret` MSBuild property for release builds when the chosen Google Desktop OAuth client requires it.
3. DPAPI-protected legacy local fallback when already present.

Example release build injection:

```powershell
dotnet publish src\EdgeCalendar.App\EdgeCalendar.App.csproj -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -p:GoogleOAuthClientId="YOUR_DESKTOP_CLIENT_ID" -p:GoogleOAuthClientSecret="YOUR_DESKTOP_CLIENT_SECRET"
```

Do not commit real OAuth client IDs, client secrets, downloaded Google credential JSON, or signing material.

## Flow

- OAuth uses the system browser and a loopback callback.
- Authorization uses PKCE.
- On startup, the app checks Google authentication. If no usable token is stored, the browser opens automatically for Google sign-in and consent.
- Startup authentication does not sync calendar data by itself; sync runs when the panel opens or the user requests refresh.
- The normal app flow does not require a client secret.
- Some Google Desktop OAuth clients may still require a client secret during token exchange. If so, inject it at build time and never commit it to the repository.

## Scopes

The current minimized MVP scope set is:

- `https://www.googleapis.com/auth/calendar.events`
- `https://www.googleapis.com/auth/calendar.calendarlist.readonly`

These scopes support event CRUD plus calendar list discovery without requesting full calendar account access. If an endpoint requires broader access, record the reason before expanding scopes.

## Public distribution requirements

Minimized scopes do not remove Google OAuth publication requirements.

- The official Google Cloud project still needs an OAuth consent screen configured for the app.
- The OAuth consent screen should link to the repository privacy policy: `PRIVACY.md`.
- Public distribution may require Google OAuth verification for the requested Calendar scopes.
- Until consent screen verification and test-user restrictions are resolved, `0.1.0-alpha` should be treated as a limited alpha, not unrestricted public distribution.
- Keep the consent screen, privacy policy, README, and release notes consistent with the exact scopes requested by the app.

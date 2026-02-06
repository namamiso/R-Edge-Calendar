# SECURITY

## Secrets
- Do not store tokens in plain text files.
- Store OAuth tokens in Windows Credential Manager (DPAPI-backed).
- Never log:
  - access_token, refresh_token, authorization code
  - raw Authorization headers

## Logging
- Logs under %LOCALAPPDATA%\EdgeCalendar\Logs
- Redact request/response bodies if they might contain PII.

## OAuth
- Use Authorization Code + PKCE.
- Use system browser and loopback callback.
- Handle token refresh safely and silently.

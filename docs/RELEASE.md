# Release (OSS zip)

## Build pipeline
- CI builds the solution with `dotnet build` on Windows.
- Release artifacts are produced manually via `dotnet publish`.

## Portable release (recommended)
1) Build a self-contained single-file executable:
   ```powershell
   dotnet publish src\EdgeCalendar.App\EdgeCalendar.App.csproj -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -p:GoogleOAuthClientId="YOUR_DESKTOP_CLIENT_ID"
   ```
2) Package the publish output as a zip:
   - `src\EdgeCalendar.App\bin\Release\net8.0-windows\win-x64\publish\`
3) Upload the zip to GitHub Releases.

## OAuth client ID
- The official Google Desktop OAuth Client ID must be injected at release build time with `-p:GoogleOAuthClientId="..."`.
- Do not commit the real Client ID, Client Secret, downloaded Google credential JSON, or signing material to the public repository.
- If the Client ID is not injected, the app falls back to the development-only Client ID entry dialog and is not suitable for public release.

## Update policy
- Version with SemVer: `MAJOR.MINOR.PATCH`.
- Document breaking changes in `docs/ADR/` and `CHANGELOG.md` when added.

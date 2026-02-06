# CONTRIBUTING

## Branch strategy
- main: release-ready
- develop: integration (optional)
- feature/*: short-lived

## PR checklist
- [ ] PRD/ADR needed? (spec change must update docs first)
- [ ] Build passes: dotnet build
- [ ] No secrets in logs/config
- [ ] Performance not worse than baseline
- [ ] Only 1 layer touched (Shell/UI/Sync) unless explicitly planned

## Code style
- Nullable enabled
- Async for I/O
- Keep dependencies minimal
- Avoid heavy MVVM frameworks

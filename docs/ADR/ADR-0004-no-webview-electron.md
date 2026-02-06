# ADR-0004: No WebView2 / Electron

## Status
Accepted

## Context
Resident app must remain lightweight and fast. Web runtimes increase memory footprint and background overhead.

## Decision
Native WPF only. REST calls with HttpClient.

## Consequences
- Positive: smaller idle footprint, predictable performance
- Negative: must implement native UI for everything

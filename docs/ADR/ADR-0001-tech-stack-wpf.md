# ADR-0001: Choose WPF + .NET 8 for lightweight shell

## Status
Accepted

## Context
We need:
- NoActivate, edge-docked transparent window, fast animations
- Small dependency footprint
- Avoid WebView2/Electron

## Decision
Use WPF on .NET 8, minimal MVVM, minimal packages.

## Consequences
- Positive: good control of window styles, mature, lightweight for this use case
- Negative: WinUI 3 style parity is not the goal; styling is custom work

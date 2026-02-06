# ADR-0002: Fix sync window to ±31 days (62 days total) for MVP

## Status
Accepted

## Context
We prioritize speed and low resource usage. Large history pulls increase initial sync time and DB size.

## Decision
Cache and sync only [today-31d, today+31d] for MVP.

## Consequences
- Positive: fast startup, small DB, predictable behavior
- Negative: browsing far months shows "out of range" unless we extend later
- Follow-up: optional future enhancement to slide window based on viewed month

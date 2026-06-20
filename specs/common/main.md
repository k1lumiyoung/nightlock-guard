# NightLock Guard

## Project intent

NightLock Guard is a small Windows-only parental-control utility for one job: prevent late-night computer use by locking the active Windows session from 23:30 until 08:00 local time.

The first target user is a younger sibling using the same family computer. The parent/older sibling who controls the tool may still run the Windows account as an administrator in the first version, so protection is intentionally best-effort rather than tamper-proof.

## Canonical references

- [PROP-000-base-rules.md](./PROP-000-base-rules.md) - base project rules.
- [structure.md](./structure.md) - spec-space and source map.
- [PROP-001-product-boundaries.md](../modules/core/PROP-001-product-boundaries.md) - product actors, limits, and safety boundaries.

## Current modules

- `core` - schedule policy, lock behavior, parent override, Windows runtime.

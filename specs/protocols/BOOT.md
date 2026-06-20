# BOOT

0. Read `specs/.me` for the current `@handle`.
1. Read `specs/WAL.md` and only edit active sections owned by the current `@handle`.
2. Read `specs/BOARD.md` and keep work item state consistent with `WAL`.
3. For implementation work, open the governing `FEAT` or `INFRA` spec before touching code.
4. For spec edits, follow `specs/protocols/SPEC-PROTOCOL.md`; for new specs, follow `specs/protocols/SPEC-AUTHORING-PROTOCOL.md`.
5. For new or substantially changed spec-owned code, add `@spec spec://...#anchor` markers at major responsibility points.
6. If an accepted limitation remains outside the current scope, record it in `specs/TECHDEBT.md`.
7. Work atomically: one meaningful work item at a time.

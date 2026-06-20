# NightLock Guard agent entrypoint

This project follows a spec-driven workflow. The specs live in `specs/` and are the source of truth.

Before changing code or specs:
- read `specs/.me`;
- read `specs/protocols/BOOT.md`;
- use `specs/BOARD.md` and `specs/WAL.md` as the operational state;
- treat `specs/common/*` and `specs/modules/*` as canonical behavior.

Code must follow the governing `spec://...#anchor` references and should add `@spec` markers on major ownership points.

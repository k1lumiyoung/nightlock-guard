# BOARD-PROTOCOL

`BOARD.md` is the operational source of truth for work item status.

One row is one `FEAT` or `INFRA` work item. A work item can be in only one state:
- `Backlog`;
- `In Progress`;
- `Blocked`;
- `Done`.

Move an item to `In Progress` only when work has actually started and a matching active section exists in `WAL.md`.

Move an item to `Done` only when the scope and acceptance of the governing spec are satisfied.

# SPEC-PROTOCOL

## Priority

Human -> spec -> tests -> code.

Specs define canonical behavior. Tests verify specs. Code follows tests and specs.

## Spec Requirements

Every `PROP`, `FEAT`, and `INFRA` spec must:
- start with a plain-language section;
- have a stable `#root` anchor;
- use stable human-readable anchors;
- define scope and out-of-scope explicitly;
- define acceptance criteria that can guide implementation;
- include `Document Notes`.

## Addressing

Canonical addresses use:
- `spec://common/PROP-000-base-rules#root`
- `spec://modules/core/FEAT-001-night-lock-window#behavior.lock-window`
- `spec://modules/core/INFRA-001-windows-runtime-baseline#service`

## Changing Specs

Edit a spec directly for wording, clarification, or acceptance detail that does not change canonical behavior.

Create a change spec with `.A`, `.B`, etc. when an already accepted behavior changes in a way that must remain historically separate.

Use `<!-- REVIEW: ... -->` when implementation can proceed but the spec contains a risk or unresolved judgment call.

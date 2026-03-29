# Testing

## Rule
- Tests protect behavior, stable output contracts, invariants, and fixed regressions.
- If a test would fail after a harmless restyle, copy edit, layout tweak, or refactor, do not add it in that form.

## Prefer
- User workflows and interaction flows.
- State transitions across services, view models, and controls.
- Stable user-visible outputs such as formatted values, normalized paths, URLs, and persisted settings.
- Algorithmic invariants such as containment, non-overlap, ordering, monotonicity, and tree-treemap synchronization.
- Focused regression tests for real bugs.

## Avoid
- Exact colors, brushes, gradients, font sizes, margins, paddings, radii, coordinates, and pixel offsets when they are only current styling choices.
- Exact UI copy for placeholders, button labels, empty states, progress text, titles, or about text unless the wording itself is the contract.
- Internal helper constants and implementation details when the same intent can be asserted through observable behavior.
- Visual text lookup when a stable name or explicit test hook exists.
- Mock call counts or call order unless preventing duplicate work or side effects is the behavior under test.

## Assertion Style
- Prefer observable behavior over implementation detail.
- Prefer invariants over exact values.
- For UI layout, assert clipping, containment, ordering, or non-overlap instead of pixel-perfect geometry.
- For styling, assert that a required resource resolves or that a visual distinction exists; do not lock tests to exact theme values unless that exact value is the contract.
- For text, use exact equality only for stable data output, not mutable product copy.

## Agent Check
- What user-visible regression would this test catch?
- Would a reasonable refactor or restyle break it without breaking the product?
- Can the same intent be asserted through state, output, or invariants instead?

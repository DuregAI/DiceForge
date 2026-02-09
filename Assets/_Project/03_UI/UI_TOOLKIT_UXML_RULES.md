# UI Toolkit UXML Rules (Unity 6.3)

- **Do NOT use `id="..."` in UXML.** UI Toolkit UXML does not support HTML-style `id` attributes.
- Use `name="..."` when an element must be queried from C# (`root.Q("name")`) or targeted with a USS `#selector`.
- Use `class="..."` for styling and reusable visual variants.
- If you need styling-only hooks, prefer class selectors over name selectors.

## Quick checks

```bash
rg 'id="' Assets -g '*.uxml'
rg '^\s*#' Assets -g '*.uss'
```

The first command must always return zero matches.

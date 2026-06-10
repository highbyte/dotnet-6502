<!--
Per-system feature documentation for the Avalonia apps (Browser + Desktop), included into the
per-system pages under docs/host-apps/avalonia/<system>.md via pymdownx.snippets
(`--8<-- "avalonia/<system>-features.md"`).

Files in this folder are NOT rendered as standalone pages — includes/ is the snippets base_path
(see mkdocs.yml), not part of docs/.

Why
---
The Avalonia Browser and Desktop apps share almost all code (including UI), so most system-specific
features are identical between the two runtimes. Keeping them in one shared fragment avoids
duplicating the common ~90%; the small Browser-vs-Desktop differences are expressed inline with
`pymdownx.tabbed` (`=== "Browser"` / `=== "Desktop"`) or `admonition` (`!!! note "Browser only"`).
See decisions/2026-06-10-avalonia-system-docs-structure.md.

Naming convention
-----------------
  <system>-features.md   — e.g. `c64-features.md`, future `vic20-features.md`

Heading levels
--------------
The consumer page (docs/host-apps/avalonia/<system>.md) supplies the `#` title; each fragment
starts its sections at `##`.

Relative links
--------------
Links resolve relative to the CONSUMING page (docs/host-apps/avalonia/), e.g. the system pages are
`../../systems/<system>/...` and library pages are `../../libraries/...`. Nested `--8<--` snippet
includes are resolved against the snippets base_path (includes/), not the page.

Adding a new system
-------------------
1. Create `includes/avalonia/<system>-features.md` (sections at `##`).
2. Create `docs/host-apps/avalonia/<system>.md` that includes it.
3. Add one nav line under Avalonia in mkdocs.yml.
There is no glob include in MkDocs/snippets, so the include line is the one manual step per system.
-->

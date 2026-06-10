<!--
Startup-parameter documentation fragments, included into the app pages via
pymdownx.snippets (`--8<-- "startup-params/<file>.md"`).

Files in this folder are NOT rendered as standalone pages — `includes/` is the
snippets base_path (see mkdocs.yml), not part of `docs/`.

Naming convention
-----------------
  {frontend}-{scope}.md

  frontend:
    cli      — shared by the Avalonia Desktop and Headless apps (command-line flags)
    browser  — the Avalonia Browser app (URL query parameters)

  scope:
    intro    — lead-in prose + cross-cutting rules (no parameter tables)
    general  — system-agnostic parameters (one "### General parameters" group)
    <system> — one file per system, e.g. `c64`, future `vic20`
               (one "### <System> parameters" group)

Each consumer page (docs/host-apps/avalonia/desktop.md, .../headless.md,
docs/host-apps/avalonia/browser.md) is a thin assembler that includes the intro,
the general file, then one `--8<--` line per system file.

Heading levels
--------------
The parent heading on each page is `##` (h2). Each fragment therefore starts its
group at `###` (h3: "General parameters" / "C64 parameters") with sub-sections
at `####` (h4). Keep this so the page TOC stays two levels deep.

Adding a new system
-------------------
1. Create `cli-<system>.md` and/or `browser-<system>.md` following the same
   structure (a `### <System> parameters` group with `####` sub-sections).
2. Add one `--8<-- "startup-params/<file>.md"` line to each relevant page,
   after the existing system files.
There is no glob include in MkDocs/snippets, so the include line is the one
manual step per system.
-->

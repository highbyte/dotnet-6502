#!/usr/bin/env python3
"""Shared path validation for the swiftlink smoke-test helper scripts.

Sonar rule pythonsecurity:S8707 flags filesystem paths that are built from
CLI arguments and then used as write targets without validation. These
helpers only ever need to write artifacts into the system temp directory
(the run-*.sh smoke runners create their working dir with `mktemp` under
``$TMPDIR``/``/tmp``) or into the current working directory.

``safe_path`` resolves the candidate and confirms it stays within one of
those allowed roots before any filesystem access, rejecting path-traversal
escapes from faulty/hostile CLI arguments.
"""
import pathlib
import tempfile


def safe_path(candidate: str) -> pathlib.Path:
    """Resolve ``candidate`` and ensure it stays within an allowed root.

    Allowed roots are the system temp directory and the current working
    directory. Raises ``ValueError`` if the resolved path escapes them.
    """
    resolved = pathlib.Path(candidate).resolve()
    allowed_roots = (
        pathlib.Path(tempfile.gettempdir()).resolve(),
        pathlib.Path.cwd().resolve(),
    )
    if any(resolved == root or root in resolved.parents for root in allowed_roots):
        return resolved
    raise ValueError(
        f"refusing to access path outside allowed temp/working directories: {candidate}"
    )

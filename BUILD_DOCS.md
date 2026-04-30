# Building & serving the documentation site

The documentation site uses [MkDocs](https://www.mkdocs.org/) with the
[Material](https://squidfunk.github.io/mkdocs-material/) theme. Source files live
in [`docs/`](docs/) and the configuration is [`mkdocs.yml`](mkdocs.yml).

The only runtime dependency is **Python 3.9+**. All other dependencies are
installed via pip into a local virtual environment.

---

## 1. Install Python (one-time)

| OS | Recommended install |
| --- | --- |
| **macOS** | `brew install python` (or use the pre-installed `python3`) |
| **Windows** | Download from [python.org](https://www.python.org/downloads/windows/) and tick *"Add Python to PATH"* during install |
| **Linux** | Use the distro package — e.g. `sudo apt install python3 python3-venv python3-pip` (Debian/Ubuntu) or `sudo dnf install python3 python3-pip` (Fedora) |

Verify:

```bash
python3 --version    # macOS/Linux
py --version         # Windows
```

---

## 2. Create a virtual environment & install MkDocs

Run from the repository root.

### macOS / Linux

```bash
python3 -m venv .venv-docs
source .venv-docs/bin/activate
pip install --upgrade pip
pip install -r requirements-docs.txt
```

### Windows (PowerShell)

```powershell
py -m venv .venv-docs
.\.venv-docs\Scripts\Activate.ps1
pip install --upgrade pip
pip install -r requirements-docs.txt
```

### Windows (cmd.exe)

```bat
py -m venv .venv-docs
.venv-docs\Scripts\activate.bat
pip install --upgrade pip
pip install -r requirements-docs.txt
```

> **Note for PowerShell users:** if activation fails with an *"execution policy"*
> error, run once:
> `Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned`

---

## 3. Serve the site locally with live reload

With the virtual environment activated:

```bash
mkdocs serve
```

Open <http://127.0.0.1:8000> in a browser. Edits to any file under `docs/` or
`mkdocs.yml` will trigger an automatic reload.

To bind to a different port or host:

```bash
mkdocs serve -a 0.0.0.0:9000
```

---

## 4. Build a static site

```bash
mkdocs build
```

The generated HTML is written to `site/` (which is gitignored). Deploy that
directory to any static host.

---

## 5. Re-activate the environment in later sessions

You only run `python -m venv` once. On subsequent sessions just activate:

```bash
source .venv-docs/bin/activate          # macOS/Linux
.\.venv-docs\Scripts\Activate.ps1       # Windows PowerShell
```

Then run `mkdocs serve` or `mkdocs build` as above.

---

## Alternative: install without a virtual environment

If you prefer not to manage a venv, install user-wide:

```bash
pip install --user mkdocs-material
```

You may need to add the user `bin`/`Scripts` directory to `PATH` so that the
`mkdocs` command is found.

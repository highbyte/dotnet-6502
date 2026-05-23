# Publishing the VSCode Extension to the Marketplace

## One-time setup

### 1. Create a Visual Studio Marketplace publisher account

- Go to https://marketplace.visualstudio.com/manage
- Sign in with your Microsoft account
- Create a publisher with ID `highbyte`

Optional but recommended:
- Add your GitHub profile (https://github.com/highbyte) as the Company Web Site
- Verify your domain to get a verified publisher badge

### 2. Generate an Azure DevOps Personal Access Token (PAT)

- Go to https://dev.azure.com and sign in with the **same** Microsoft account
- User Settings → Personal Access Tokens → New Token
- Set scope: **Marketplace → Manage**
- Copy the generated token (it is only shown once)

### 3. Add the PAT as a GitHub Actions secret

- Go to the GitHub repo → Settings → Secrets and variables → Actions
- Create a new repository secret:
  - Name: `VSCE_PAT`
  - Value: the PAT from the previous step

> The PAT has an expiry date. When it expires, generate a new one and update the `VSCE_PAT` secret.

---

## Publishing a new release

### 1. Make and commit your changes

Make your code changes and commit them to the repo as normal.

From here, you have two tracks:

- **Track A — Manual.** Do the changelog edit + tag creation by hand (steps below under "Track A").
- **Track B — Automated.** Run `tools/vscode-extension/release.sh`, which walks you through the same steps interactively (section "Track B" below).

Either track produces the same end state: a `vscode-v*` tag pushed to GitHub, a GitHub Release published, and the corresponding marketplace publish workflow triggered.

---

## Track A — Manual

### 2. Update CHANGELOG.md

Update `tools/vscode-extension/CHANGELOG.md` — move items from `[Unreleased]` to a new version section:

```markdown
## [1.1.0] - 2026-04-01

### Added
- ...

### Fixed
- ...
```

Commit the changelog update.

### 3. Create a version tag

Tag format: `vscode-v<major>.<minor>.<patch>` — any `-*` suffix marks it as a pre-release (e.g. `vscode-v1.0.0-beta`).

**Option A — via GitHub Releases page (recommended)**

Go to https://github.com/highbyte/dotnet-6502/releases → **Draft a new release**:
- In the **Choose a tag** field, type the new tag (e.g. `vscode-v1.0.0`) and select **Create new tag on publish**
- Set the release title to the same `vscode-v1.0.0`
- In release notes use a link to the changelog in the Extension Marketplace: `**Extension Changelog**: https://marketplace.visualstudio.com/items/highbyte.dotnet-6502-debugger/changelog`
- For a pre-release, check **Set as a pre-release**
- Click **Publish release**

**Option B — via git command line**

```bash
git tag vscode-v1.0.0
git push origin vscode-v1.0.0
```

Either option triggers the GitHub Actions workflow (`.github/workflows/release-vscode-extension.yml`) which:
- Strips any pre-release suffix from the version set in `package.json` (the Marketplace only accepts `major.minor.patch`)
- Publishes to the VSCode Marketplace, passing `--pre-release` if a suffix was present
- Creates a GitHub Release for the tag

The extension is live on the Marketplace within a few minutes.

---

## Track B — Automated (via `release.sh`)

The `tools/vscode-extension/release.sh` script automates the steps in Track A. It is meant to be run **after** step 1 (your code changes are committed and pushed to `master`).

### Run it

```bash
tools/vscode-extension/release.sh
```

Useful flags:

| Flag | Effect |
|---|---|
| `--dry-run` | Walk through the whole flow without modifying files, committing, pushing, or calling `gh`. Prints what each step would do. Recommended for the first run. |
| `--force` | Skip the safety pre-checks (must be on `master`, clean working tree, in sync with `origin/master`). Use only when you know what you're doing. |
| `--help` / `-h` | Print usage. |

### What it does, in order

1. **Pre-checks.** Verifies you are on `master`, the working tree is clean, and local `master` matches `origin/master`. Bails out otherwise (override with `--force`).
2. **Suggests a new version.** Looks at the most recent `vscode-v*` GitHub release, bumps the patch number by 1, and preserves any pre-release suffix. For example, latest `vscode-v0.2.4-alpha` → suggested `vscode-v0.2.5-alpha`.
3. **Prompts for the tag.** Shows the suggested tag and lets you press Enter to accept it, type a different tag to override, or Ctrl-C to cancel.
4. **Validates the tag.** Checks the format, confirms no GitHub release / local tag / remote tag of the same name already exists, and refuses if the new version is not strictly greater than the highest existing `vscode-v*` version.
5. **Migrates the changelog.** If `tools/vscode-extension/CHANGELOG.md` has a non-empty `## [Unreleased]` section and no section for the new version yet, prints the `[Unreleased]` contents and prompts (default Y) to move them into a new `## [<version>] - <today's date>` section.
6. **Sets the pre-release flag.** Marks the release as pre-release if the base version is below `1.0.0` (e.g. `0.2.5`) **or** the tag carries a `-suffix` (e.g. `-alpha`, `-beta`).
7. **Shows the planned release for final confirmation.** Tag, title, pre-release flag, release notes, and whether a changelog commit will be pushed.
8. **Commits and pushes the changelog change** (if you accepted the migration in step 5), using commit message `"Release <tag>"`. This way the GitHub release tag points at a commit that already contains the updated changelog.
9. **Creates the GitHub release** via `gh release create`, with:
   - Title equal to the tag.
   - Notes: `**Extension Changelog**: https://marketplace.visualstudio.com/items/highbyte.dotnet-6502-debugger/changelog`.
   - `--prerelease` if applicable.

The rest of the flow (workflow trigger, marketplace publish) is identical to Track A — the GitHub Actions workflow handles `package.json` version stripping and `vsce publish` automatically.

### Cancellation

You can cancel at any prompt with **Ctrl-C** or by answering `n` to the final confirmation. If the script had already rewritten `CHANGELOG.md` locally but you decline the final confirmation, it reverts the changelog change so your working tree is back to clean.

### Requirements

- `gh` CLI installed and authenticated (`gh auth status`).
- `git` and `python3` on `PATH` (Python is used by the script for portable file rewriting; macOS bash 3.2 lacks the built-in `read -e -i` for prefilled input editing).
- Push access to `origin master` for the changelog commit (only if you accept the migration).

---

## Version number conventions

| Tag | Marketplace version | Pre-release? |
|---|---|---|
| `vscode-v1.0.0` | `1.0.0` | No |
| `vscode-v1.0.0-beta` | `1.0.0` | Yes |
| `vscode-v1.0.0-alpha` | `1.0.0` | Yes |

Any suffix after `-` is treated as a pre-release indicator. Users in VS Code can opt in to pre-release versions via the **Switch to Pre-Release Version** button on the extension's Marketplace page.

---

## Manual publishing (without GitHub Actions)

If needed, you can publish manually from the command line:

```bash
cd tools/vscode-extension
npm install
npx vsce login highbyte    # prompts for PAT, one-time per machine
npm run publish            # stable
npm run publish -- --pre-release  # pre-release
```

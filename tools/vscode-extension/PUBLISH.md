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
- Set the release title to e.g. `VSCode Extension v1.0.0`
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

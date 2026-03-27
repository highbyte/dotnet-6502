# Build and package the VSCode extension as a .vsix file
# Requires Node.js and npm to be installed
# Usage: .\publish.ps1

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$OutputDir = Join-Path $ScriptDir "publish"

Write-Host "Building VSCode extension..."
Write-Host "  Output: $OutputDir"
Write-Host ""

# Install dependencies if node_modules is missing
if (-not (Test-Path (Join-Path $ScriptDir "node_modules"))) {
    Write-Host "Installing npm dependencies..."
    & npm install --prefix $ScriptDir
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "❌ npm install failed" -ForegroundColor Red
        exit 1
    }
    Write-Host ""
}

# Compile TypeScript
Write-Host "Compiling TypeScript..."
& npm run compile --prefix $ScriptDir
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "❌ TypeScript compilation failed" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Create output directory
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# Package the extension
Write-Host "Packaging extension..."
& npx --prefix $ScriptDir vsce package --out $OutputDir
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "❌ Packaging failed" -ForegroundColor Red
    exit 1
}

Write-Host ""
$VsixFile = Get-ChildItem -Path $OutputDir -Filter "*.vsix" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host "✅ Packaged successfully: $($VsixFile.FullName)" -ForegroundColor Green
Write-Host ""
Write-Host "To install manually in VS Code:"
Write-Host "  code --install-extension `"$($VsixFile.FullName)`""
Write-Host ""
Write-Host "To uninstall:"
Write-Host "  code --uninstall-extension highbyte.dotnet-6502-debugger"
Write-Host ""
Write-Host "Note: if 'code' command is not found, install it from within VS Code:"
Write-Host "  Ctrl+Shift+P -> 'Shell Command: Install code command in PATH'"
Write-Host "  Then restart your terminal."

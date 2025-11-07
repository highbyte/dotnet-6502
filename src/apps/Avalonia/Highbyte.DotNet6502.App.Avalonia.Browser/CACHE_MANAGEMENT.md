# Cache Management for Avalonia Browser App

This document describes the cache invalidation and version management system implemented for the Avalonia Browser WebAssembly application.

## Problem

When deploying a WebAssembly application to a static hosting service like GitHub Pages, browsers may cache static resources (HTML, CSS, JS, WASM files) for extended periods. This can cause users to continue using old versions of the application even after new deployments.

## Solutions Implemented

### 1. Version-Based Cache Busting

**Files Modified:**
- `wwwroot/index.html` - Added version placeholders and cache-busting query parameters
- GitHub Actions workflows - Replace version placeholders during build

**How it works:**
- Static resources are loaded with version query parameters (e.g., `main.js?v=1.2.3`)
- Version is injected during the GitHub Actions build process
- Forces browsers to fetch new resources when version changes

### 2. Intelligent Update Notification

**Files Modified:**
- `wwwroot/index.html` - Added JavaScript for version detection and update notifications

**Features:**
- Compares current app version with stored version in localStorage
- Shows a user-friendly notification when a new version is available
- Provides "Update Now" button to force refresh and clear caches
- Allows users to dismiss the notification

## Deployment Configuration

### GitHub Actions

Two workflows are configured:

1. **Production**: `dotnet-avalonia-browser-app-publish-to-gh-pages.yml`
   - Triggered on version tags (v*.*.*)
   - Deploys to `/app` path in gh-pages branch

2. **Test/Beta**: `dotnet-blazor-app-publish-to-gh-pages-beta.yml`
   - Manually triggered
   - Deploys to `/app-test` path in gh-pages branch

Both workflows:
- Replace `{{APP_VERSION}}` placeholders with actual version numbers
- Handle version injection in HTML and JavaScript files

## User Experience

### Normal Operation
- Users load the app normally
- Resources are cached appropriately for performance
- Updates happen transparently

### When Update Available
1. User visits the site
2. JavaScript detects version mismatch
3. Friendly notification appears at top of page:
   ```
   🔄 A new version is available! [Update Now] [✕]
   ```
4. User can:
   - Click "Update Now" to immediately refresh with new version
   - Click "✕" to dismiss and continue with current version
   - Ignore the notification

### Force Refresh Process
When user clicks "Update Now":
1. Clears all browser caches (Cache API)
2. Clears localStorage version info
3. Forces hard reload bypassing all caches
4. User gets the latest version immediately

## Testing

### Test Version Detection
1. Deploy a version with version number X
2. Manually change localStorage: `localStorage.setItem('app-version', 'old-version')`
3. Refresh page - should see update notification

## Maintenance

### Adding New Static Resources
1. Test that new resources are properly versioned in URLs
2. Verify version detection continues to work

### Updating Cache Strategy
1. Modify version detection logic if needed
2. Test cross-browser compatibility

### Troubleshooting Cache Issues
1. Check browser DevTools → Application → Storage
2. Clear all storage and test fresh load
3. Verify version replacement in deployed files

## Files Overview

```
wwwroot/
├── index.html          # Main HTML with version detection
├── main.js            # Main application script (versioned)
└── app.css            # Styles with update notification CSS
```
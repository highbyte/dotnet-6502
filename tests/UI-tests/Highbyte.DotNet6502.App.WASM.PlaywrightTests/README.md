# Playwright UI Tests for Highbyte.DotNet6502.App.WASM

This folder contains Playwright-based UI tests for the WASM application.

## Getting Started

1. Install dependencies:
   - Node.js (https://nodejs.org/)
   - Playwright: `npm install --save-dev @playwright/test`
   - (Optional) VS Code extension: Playwright Test for VSCode

2. To initialize Playwright in this folder, run:
   ```sh
   npx playwright install
   ```

3. To run tests:
   ```sh
   npx playwright test
   ```

   ```sh
   npx playwright test --headed
   ```

   Run individual tests (auto-opens report in browser afterwards):
   ```sh
   npx playwright test tests/00-homepage.spec.ts
   ```

4. Show report:
   ```sh
   npx playwright show-report 
    ```


## Folder Structure
- `tests/` - Place your Playwright test files here (e.g., `example.spec.ts`).
- `playwright.config.ts` - Playwright configuration file.

For more details, see [Playwright documentation](https://playwright.dev/).

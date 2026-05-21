import { test, expect, type ConsoleMessage } from '@playwright/test';

const fatalPatterns: RegExp[] = [
  /TypeLoadException/i,
  /MissingMethodException/i,
  /MissingFieldException/i,
  /FileNotFoundException/i,
  /Could not load file or assembly/i,
  /Unable to load/i,
  /trimm(ed|er)/i,
];

test('Avalonia Browser AOT publish boots past splash and renders canvas', async ({ page }) => {
  const consoleErrors: string[] = [];
  const recordError = (text: string) => {
    if (fatalPatterns.some(p => p.test(text))) consoleErrors.push(text);
  };
  page.on('console', (msg: ConsoleMessage) => {
    if (msg.type() === 'error') recordError(msg.text());
  });
  page.on('pageerror', err => recordError(`${err.name}: ${err.message}`));

  await page.goto('/');

  // Splash is in static markup — its absence would mean index.html itself failed.
  await expect(page.locator('.avalonia-splash')).toBeVisible();

  // Avalonia replaces #out with its rendered Skia canvas once the runtime
  // boots and the app's Program.cs finishes building. Slow on CI under AOT.
  await expect(page.locator('#out canvas')).toBeVisible({ timeout: 4 * 60 * 1000 });

  // Desktop-Safari guard in index.html redirects elsewhere — we run Chromium,
  // so the redirect must not have fired.
  expect(page.url()).not.toContain('safari-notice.html');

  expect(consoleErrors, `Fatal console errors:\n${consoleErrors.join('\n---\n')}`).toEqual([]);
});

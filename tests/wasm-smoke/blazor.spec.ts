import { test, expect, type ConsoleMessage } from '@playwright/test';

// Failure patterns that indicate trimming or AOT damaged the published build:
// a missing trim root, an assembly that didn't make it into the bundle, or a
// type/method the AOT compiler couldn't resolve. Generic warnings are tolerated.
const fatalPatterns: RegExp[] = [
  /TypeLoadException/i,
  /MissingMethodException/i,
  /MissingFieldException/i,
  /FileNotFoundException/i,
  /Could not load file or assembly/i,
  /Unable to load/i,
  /trimm(ed|er)/i,
];

test('Blazor WASM AOT publish boots, plug-ins discovered, no AOT/trim errors', async ({ page }) => {
  const consoleErrors: string[] = [];
  const recordError = (text: string) => {
    if (fatalPatterns.some(p => p.test(text))) consoleErrors.push(text);
  };
  page.on('console', (msg: ConsoleMessage) => {
    if (msg.type() === 'error') recordError(msg.text());
  });
  page.on('pageerror', err => recordError(`${err.name}: ${err.message}`));

  await page.goto('/');

  // Index.razor renders #system-selector once Initialized == true. Wait for that
  // rather than the loading-progress overlay so we know the .NET runtime + DI +
  // plug-in discovery all finished without hitting the StartupError path.
  await expect(page.locator('#system-selector')).toBeVisible({ timeout: 4 * 60 * 1000 });

  // The system <select> is populated from discovered plug-ins. An empty list
  // would mean the per-system plug-in assemblies didn't survive trim/AOT.
  const systemOptions = page.locator('#system-selector select').first().locator('option');
  await expect.poll(async () => systemOptions.count()).toBeGreaterThan(0);

  await expect(page.getByRole('button', { name: 'Start' })).toBeVisible();
  await expect(page.locator('.startup-error')).toHaveCount(0);

  expect(consoleErrors, `Fatal console errors:\n${consoleErrors.join('\n---\n')}`).toEqual([]);
});

import { test, expect } from '@playwright/test';
import { writeFileSync } from 'node:fs';

// Headless emulated-speed readout for the Blazor WASM (Mono AOT) build.
//
// Not a smoke test and not a gate: it starts the ROM-free Generic system, turns on the
// in-app stats panel (which is what enables per-frame instrumentation) and records the
// host frame rate and emulator time per frame that the app itself reports. The numbers
// land in `speed-readout.json` and `speed-readout.md` in this directory; the workflow
// appends the markdown to the job summary. Shared runners are noisy, so read trends and
// ratios, never single runs. Only one assertion is made: the emulator actually runs.
//
// The stats panel renders `<span class="header">Name</span>: <span class="value">V</span>`
// pairs joined by <br />; values are pre-formatted strings ("59.83", "3.21ms").

const SETTLE_MS = Number(process.env.WASM_SPEED_SETTLE_MS ?? 8000);

test('Blazor WASM emulated speed readout (Generic system)', async ({ page }) => {
  await page.goto('/?systemName=Generic&audioEnabled=false');
  await expect(page.locator('#system-selector')).toBeVisible({ timeout: 4 * 60 * 1000 });

  // exact: the page also has a "Load & start binary PRG file" button.
  await page.getByRole('button', { name: 'Start', exact: true }).click();
  // The Stats button is only enabled once a system is running.
  const statsButton = page.getByRole('button', { name: 'Stats', exact: true });
  await expect(statsButton).toBeEnabled({ timeout: 60 * 1000 });
  await statsButton.click();

  const statsPanel = page.locator('.statsStyle .infobox-output');
  await expect(statsPanel).toBeVisible({ timeout: 60 * 1000 });
  await page.waitForTimeout(SETTLE_MS);

  const stats: Record<string, string> = {};
  const headers = statsPanel.locator('span.header');
  const values = statsPanel.locator('span.value');
  const count = await headers.count();
  for (let i = 0; i < count; i++) {
    stats[(await headers.nth(i).innerText()).trim()] = (await values.nth(i).innerText()).trim();
  }

  const fpsKey = Object.keys(stats).find(k => k.endsWith('OnUpdateFPS'));
  const fps = fpsKey ? Number.parseFloat(stats[fpsKey]) : Number.NaN;
  const systemTimeKey = Object.keys(stats).find(k => k === 'SystemTime' || k.endsWith('-SystemTime'));
  const systemTimeMs = systemTimeKey ? Number.parseFloat(stats[systemTimeKey]) : Number.NaN;

  const readout = {
    app: 'blazor',
    system: 'Generic',
    userAgent: await page.evaluate(() => navigator.userAgent),
    settleMs: SETTLE_MS,
    hostFps: fps,
    emulatorMsPerFrame: systemTimeMs,
    stats,
  };
  writeFileSync('speed-readout.json', JSON.stringify(readout, null, 2));
  writeFileSync(
    'speed-readout.md',
    [
      '| Metric | Value |',
      '| --- | --- |',
      `| Host frame rate (OnUpdateFPS) | ${Number.isNaN(fps) ? 'n/a' : fps.toFixed(2)} |`,
      `| Emulator time per frame (SystemTime) | ${Number.isNaN(systemTimeMs) ? 'n/a' : systemTimeMs.toFixed(2) + ' ms'} |`,
      ...Object.entries(stats).map(([k, v]) => `| ${k} | ${v} |`),
      '',
    ].join('\n'),
  );

  expect(fpsKey, `No *OnUpdateFPS stat found. Stats seen: ${Object.keys(stats).join(', ')}`).toBeTruthy();
  expect(fps, 'Emulator is not producing frames').toBeGreaterThan(1);
});

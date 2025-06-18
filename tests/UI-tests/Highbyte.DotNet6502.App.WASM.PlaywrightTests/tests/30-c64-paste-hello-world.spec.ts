import { test, expect } from '@playwright/test';
import { navigateAndUploadC64Roms } from './helpers/c64-navigate-upload-roms';

const helloWorldBasic = `10 print "hello, world!"
20 goto 10
run
`;

test('Upload ROMs, start emulator, and paste C64 BASIC hello world from clipboard', async ({ page, context }) => {
  // Navigate, open config, and upload ROMs
  await navigateAndUploadC64Roms(page);

  // Click OK to close the config dialog
  await page.getByRole('button', { name: 'Ok' }).click();

  // Start the emulator
  await page.getByRole('button', { name: 'Start' }).click();
  await expect(page.getByText('Status: Running')).toBeVisible();

  // Wait for 5 seconds after starting the emulator
  await page.waitForTimeout(3000);

  // Set clipboard to the hello world BASIC program
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await page.evaluate(async (text) => {
    await navigator.clipboard.writeText(text);
  }, helloWorldBasic);

  // Click the Paste button in the emulator
  await page.getByRole('button', { name: 'Paste' }).click();

  // Wait for 10 seconds after pasting
  await page.waitForTimeout(5000);

  // Optionally, verify that the program appears in the emulator (if possible)
});

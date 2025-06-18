import { test, expect } from '@playwright/test';
import { navigateAndUploadC64Roms } from './helpers/c64-navigate-upload-roms';

test('Upload C64 ROMs and start emulator', async ({ page }) => {
  // Navigate, open config, and upload ROMs using the reusable module
  await navigateAndUploadC64Roms(page);

  // Confirm ROMs are loaded (look for correct sizes)
  await expect(page.getByText('kernal 8192 bytes')).toBeVisible();
  await expect(page.getByText('basic 8192 bytes')).toBeVisible();
  await expect(page.getByText('chargen 4096 bytes')).toBeVisible();

  // Click OK to close the config dialog
  await page.getByRole('button', { name: 'Ok' }).click();

  // Start the emulator
  await page.getByRole('button', { name: 'Start' }).click();

  // Verify the status changes to Running
  await expect(page.getByText('Status: Running')).toBeVisible();
});

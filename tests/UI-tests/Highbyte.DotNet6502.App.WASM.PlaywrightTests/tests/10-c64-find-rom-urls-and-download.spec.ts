import { test, expect } from '@playwright/test';
import * as setup from './test-setup';
import { downloadC64Roms } from './helpers/c64-download-roms';

test('Find C64 ROM file URLs in config dialog and download them', async ({ page }) => {
  // Go to the emulator page
  await page.goto(setup.BASE_URL);

  // Open the C64 Config dialog
  await page.getByRole('button', { name: 'C64 Config' }).click();

  // Try expanding all sections until the "Load ROMs" button is visible
  let loadRomsVisible = false;
  const sectionButtons = await page.locator('button').all();
  for (const btn of sectionButtons) {
    const text = await btn.textContent();
    if (text && text.trim() !== 'Ok' && text.trim() !== 'Cancel') {
      await btn.click();
      if (await page.getByRole('button', { name: 'Load ROMs', exact: true }).isVisible()) {
        loadRomsVisible = true;
        break;
      }
    }
  }

  // Ensure the Load ROMs button is visible
  await expect(page.getByRole('button', { name: 'Load ROMs', exact: true })).toBeVisible();

  // Find all links (anchor tags) in the dialog
  const romLinks = await page.locator('a').all();
  const romUrls: string[] = [];
  for (const link of romLinks) {
    const href = await link.getAttribute('href');
    if (href && (href.endsWith('.bin') || href.includes('rom'))) {
      romUrls.push(href);
    }
  }

  // Expect 3 ROM URLs to be found
  expect(romUrls.length).toBe(3);
  console.log('ROM URLs:', romUrls);

  // Download each ROM file to the artifacts folder using the shared module
  await downloadC64Roms(romUrls);
});

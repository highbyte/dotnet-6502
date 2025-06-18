import { Page } from '@playwright/test';
import * as setup from '../test-setup';

/**
 * Navigates from the app start, opens the C64 Config dialog, expands the ROMs section if needed,
 * and uploads the three C64 ROM files directly to the file input, avoiding the native dialog.
 * @param page Playwright Page object
 */
export async function navigateAndUploadC64Roms(page: Page) {
  // Go to the emulator page
  //await page.goto('/');
  await page.goto(setup.BASE_URL);

  // Open the C64 Config dialog
  await page.getByRole('button', { name: 'C64 Config' }).click();

  // Expand the ROMs section if needed
  const romsSection = await page.getByText('ROMs The C64 system requires', { exact: false });
  if (await romsSection.isVisible()) {
    await romsSection.click();
  }

  // Make the file input visible if it is hidden
  await page.evaluate(() => {
    const input = document.querySelector('input[type="file"]');
    if (input) {
      (input as HTMLElement).style.display = 'block';
      (input as HTMLElement).style.visibility = 'visible';
      (input as HTMLElement).style.opacity = '1';
    }
  });

  // Upload the three ROM files directly
  await page.setInputFiles('input[type="file"]', [
    `${setup.ARTIFACT_PATH}/kernal.901227-03.bin`,
    `${setup.ARTIFACT_PATH}/basic.901226-01.bin`,
    `${setup.ARTIFACT_PATH}/characters.901225-01.bin`,
  ]);
}

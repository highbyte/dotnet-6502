import { test, expect } from '@playwright/test';
import * as setup from './test-setup';

test('homepage loads', async ({ page }, testInfo) => {
 
  // console.log('BaseURL:', testInfo.project.use.baseURL); 
  // await page.goto('/');

  // Note: using page.goto("/") where baseURL set in playwright.config.ts does not seem work. It always opens the root path regardless if baseURL has a path specified.
  // Workaround: Use a global variable from test-setup.ts that sets the full base path and use that.
  console.log('BASE_URL:', setup.BASE_URL);  
  await page.goto(setup.BASE_URL);  
  
  // Take a screenshot for debugging
  await page.waitForTimeout(4000);
  await page.screenshot({ path: setup.ARTIFACT_PATH + '/screenshot.png' });

  // Verify expected page title
  await expect(page).toHaveTitle(/dotnet-6502|WebAssembly/i);
});

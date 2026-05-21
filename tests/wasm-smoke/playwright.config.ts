import { defineConfig, devices } from '@playwright/test';

const siteRoot = process.env.WASM_SITE_ROOT;
if (!siteRoot) {
  throw new Error(
    'WASM_SITE_ROOT must be set to an absolute path to the published wwwroot ' +
    'directory (e.g. build/blazor/wwwroot).'
  );
}

const port = Number(process.env.WASM_SITE_PORT ?? 8080);
const baseURL = `http://127.0.0.1:${port}`;

export default defineConfig({
  testDir: '.',
  // AOT-compiled WASM apps can take well over a minute to boot on a CI runner.
  timeout: 5 * 60 * 1000,
  expect: { timeout: 60 * 1000 },
  fullyParallel: false,
  retries: 0,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL,
    trace: 'retain-on-failure',
    video: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: {
    command: `npx http-server "${siteRoot}" -p ${port} -c-1 -s`,
    url: baseURL,
    reuseExistingServer: !process.env.CI,
    timeout: 60 * 1000,
    stdout: 'ignore',
    stderr: 'pipe',
  },
});

import { defineConfig, devices } from '@playwright/test';
import fs from 'fs';
import os from 'os';
import path from 'path';

const repoRoot = path.resolve(__dirname, '..');
const e2eRoot = path.join(os.tmpdir(), 'kelliphoto-e2e');
const gallery = path.join(e2eRoot, 'gallery');
const thumbs = path.join(e2eRoot, 'thumbnails');
const web = path.join(e2eRoot, 'web');
const assets = path.join(gallery, '.web');

for (const dir of [gallery, thumbs, web, assets]) {
  fs.mkdirSync(dir, { recursive: true });
}

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: process.env.CI ? [['github'], ['html'], ['list']] : 'list',
  timeout: 60_000,
  use: {
    baseURL: process.env.PLAYWRIGHT_BASE_URL ?? 'http://127.0.0.1:5050',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: {
    command:
      'dotnet run --project src/KelliPhoto.Web/KelliPhoto.Web.csproj --no-launch-profile',
    cwd: repoRoot,
    url: 'http://127.0.0.1:5050',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
    env: {
      ASPNETCORE_ENVIRONMENT: 'Testing',
      KELLIPHOTO_INTEGRATION_TEST: '1',
      ASPNETCORE_URLS: 'http://127.0.0.1:5050',
      GallerySettings__GalleryPath: gallery,
      GallerySettings__ThumbnailPath: thumbs,
      GallerySettings__WebImagePath: web,
      GallerySettings__WebAssetsPath: assets,
      WatermarkSettings__Enabled: 'false',
    },
  },
});

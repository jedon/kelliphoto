import { test, expect } from '@playwright/test';

test.describe('Gallery site', () => {
  test('home page loads with gallery layout', async ({ page }) => {
    await page.goto('/');
    await expect(page).toHaveTitle(/Kelli Thompson Photography/i);
    await expect(page.locator('.gallery-page')).toBeVisible();
    await expect(page.locator('header.site-header')).toBeVisible();
    await expect(page.getByRole('navigation', { name: '' }).first()).toBeVisible();
  });

  test('main navigation reaches About and Contact', async ({ page }) => {
    await page.goto('/');

    await page.getByRole('link', { name: 'About' }).first().click();
    await expect(page).toHaveURL(/\/about$/);
    await expect(page.locator('h1')).toHaveText('About');

    await page.getByRole('link', { name: 'Contact' }).first().click();
    await expect(page).toHaveURL(/\/contact$/);
    await expect(page.locator('h1')).toHaveText('Contact');
  });

  test('footer shows copyright', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('.copyright')).toContainText('Kelli Thompson');
  });
});

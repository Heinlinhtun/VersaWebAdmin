// Run with the bundled Playwright directory in NODE_PATH and Vite on localhost.
const assert = require('node:assert/strict');
const { mkdirSync, writeFileSync } = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');
const { QRCodeWriter, BarcodeFormat } = require('@zxing/library');

const baseURL = 'http://127.0.0.1:5173';
const output = path.resolve(__dirname, '../../node_modules/.cache/scanner-verification');
mkdirSync(output, { recursive: true });
const report = { screenshots: [], checks: [], interceptedApiRequests: 0, externalRequestsSent: 0 };
const services = [
  { ServiceId: 1, ServiceName: 'Priority SKU item', Sku: ' 001aB ', Price: 500 },
  { ServiceId: 2, ServiceName: 'Duplicate one', Sku: 'DUP', Price: 100 },
  { ServiceId: 3, ServiceName: 'Duplicate two', Sku: ' dup ', Price: 100 },
  { ServiceId: 4, ServiceName: 'Name fallback', Sku: 'NAME-4', Price: 100 },
];
const products = [
  { ProductId: 1, Name: '001AB', Sku: 'OTHER', StockQuantity: 10, SalePrice: 200, UnitId: 1, UnitName: 'Each' },
  { ProductId: 2, Name: 'Sold out sample', Sku: '0000', StockQuantity: 0, SalePrice: 200, UnitId: 1, UnitName: 'Each' },
];
async function setup(browser, viewport, locale = 'en') {
  const context = await browser.newContext({ viewport, serviceWorkers: 'block' });
  await context.route('**/*', route => {
    const url = new URL(route.request().url());
    if (url.origin === baseURL) return route.continue();
    if (url.pathname.toLowerCase().startsWith('/api/')) {
      report.interceptedApiRequests++;
      const endpoint = url.pathname.toLowerCase();
      let body = [];
      if (endpoint.includes('/sales/for-sale/')) body = products;
      else if (endpoint.endsWith('/services')) body = services;
      else if (endpoint.endsWith('/settings')) body = { ShopName: 'Scanner verification', UseDecimalQuantity: false };
      else if (endpoint.endsWith('/branches')) body = [{ BranchId: 1, BranchName: 'Synthetic branch' }];
      else if (endpoint.endsWith('/units')) body = [{ UnitId: 1, UnitName: 'Each' }];
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
    }
    return route.abort();
  });
  await context.addInitScript(({ locale }) => {
    localStorage.setItem('token', 'synthetic-test-token');
    localStorage.setItem('Role', 'Cashier');
    localStorage.setItem('BranchId', '1');
    localStorage.setItem('app_locale', locale);
    window.__cameraStreams = [];
    const original = navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices);
    navigator.mediaDevices.getUserMedia = async constraints => {
      window.__cameraRequests = (window.__cameraRequests || 0) + 1;
      const stream = await original(constraints);
      window.__cameraStreams.push(stream);
      return stream;
    };
  }, { locale });
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto(`${baseURL}/Sale`);
  await page.getByRole('button', { name: locale === 'en' ? 'Scan barcode or QR code' : 'ဘားကုဒ် သို့မဟုတ် QR ကုဒ် ဖတ်ရန်', exact: true }).waitFor();
  await page.getByText('Priority SKU item', { exact: true }).first().waitFor();
  return { context, page, errors };
}
async function screenshot(page, name) {
  const target = path.join(output, `${name}.png`);
  await page.screenshot({ path: target, fullPage: true });
  report.screenshots.push(target);
}
async function scan(page, value) {
  const input = page.getByPlaceholder('Search Name, SKU, Service or Bundle...');
  await input.fill(value);
  await input.press('Enter');
  return input;
}
async function cart(page) {
  return page.evaluate(() => {
    const input = document.querySelector('input[placeholder="Search Name, SKU, Service or Bundle..."]');
    return input.closest('.h-full').__vueParentComponent.setupState.cart.map(item => ({ id: item.ServiceId || item.ProductId, type: item.ItemType, quantity: item.Quantity }));
  });
}

(async () => {
  const browser = await chromium.launch({ channel: 'chrome', headless: true, args: ['--use-fake-device-for-media-stream'] });
  try {
    for (const [name, viewport, locale] of [
      ['desktop', { width: 1440, height: 1000 }, 'en'],
      ['mobile', { width: 390, height: 844 }, 'en'],
      ['mobile-mm', { width: 390, height: 844 }, 'mm'],
    ]) {
      const { context, page, errors } = await setup(browser, viewport, locale);
      try {
        await screenshot(page, `${name}-sale`);
        const button = page.getByRole('button', { name: locale === 'en' ? 'Scan barcode or QR code' : 'ဘားကုဒ် သို့မဟုတ် QR ကုဒ် ဖတ်ရန်', exact: true });
        const buttonBox = await button.boundingBox();
        const inputBox = await page.locator('input').first().boundingBox();
        assert.ok(buttonBox.x > inputBox.x + inputBox.width / 2);
        await button.click();
        const dialog = page.getByRole('dialog', { name: locale === 'en' ? 'Scan barcode / QR code' : 'ဘားကုဒ် / QR ကုဒ် ဖတ်ရန်' });
        await dialog.waitFor();
        const alert = dialog.getByRole('alert');
        await alert.waitFor();
        assert.match(await alert.textContent(), locale === 'en' ? /permission was denied/ : /ကင်မရာအသုံးပြုခွင့် မရပါ/);
        assert.equal(await page.evaluate(() => window.__cameraRequests), 1);
        const bounds = await dialog.locator('section').boundingBox();
        assert.ok(bounds.x >= 0 && bounds.y >= 0 && bounds.x + bounds.width <= viewport.width);
        const overflow = await alert.evaluate(element => element.scrollWidth > element.clientWidth);
        assert.equal(overflow, false);
        await screenshot(page, `${name}-camera-denied`);
        // Use the only dialog command so translated close labels do not affect the test.
        await dialog.locator('button').click();
        await dialog.waitFor({ state: 'detached' });
        assert.equal(await button.evaluate(element => document.activeElement === element), true);
        assert.deepEqual(errors, []);
        report.checks.push(`${name}: right-side scan button, denied camera dialog, readable error, focus restored, no page errors`);
        if (locale === 'en') {
          let input = await scan(page, ' 001AB ');
          await page.waitForFunction(() => document.querySelector('input[placeholder="Search Name, SKU, Service or Bundle..."]').value === '');
          assert.deepEqual(await cart(page), [{ id: 1, type: 'Service', quantity: 1 }]);
          await scan(page, '1AB');
          await page.getByText('No matching item found. The scanned code is kept in search.', { exact: true }).last().waitFor();
          assert.equal(await input.inputValue(), '1AB');
          await scan(page, 'DUP');
          await page.getByText('Multiple items match this code. Select the correct item from search.', { exact: true }).last().waitFor();
          assert.equal(await input.inputValue(), 'DUP');
          assert.equal((await cart(page)).length, 1);
          await screenshot(page, `${name}-ambiguous`);
          await scan(page, '0000');
          await page.getByText('"Sold out sample" Out of stock', { exact: true }).last().waitFor();
          assert.equal(await input.inputValue(), '0000');
          assert.equal((await cart(page)).length, 1);
          assert.equal(await page.getByText('Added Sold out sample', { exact: true }).count(), 0);
          assert.equal(await page.getByLabel('Show out of stock items').isChecked(), true);
          await screenshot(page, `${name}-stock-rejected`);
          report.checks.push(`${name}: SKU priority, leading zeros, ambiguous no-add, stock rejection and visibility`);
        }
      } finally { await context.close(); }
    }

    const { context, page, errors } = await setup(browser, { width: 1280, height: 900 });
    try {
      await context.grantPermissions(['camera'], { origin: baseURL });
      const button = page.getByRole('button', { name: 'Scan barcode or QR code', exact: true });
      for (const action of ['close', 'escape', 'unmount']) {
        await button.click();
        await page.getByText('Ready to scan', { exact: true }).waitFor();
        await page.evaluate(() => { window.__lastVideo = document.querySelector('[role="dialog"] video'); });
        await screenshot(page, `desktop-live-camera-${action}`);
        if (action === 'close') await page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }).click();
        else if (action === 'escape') await page.keyboard.press('Escape');
        else await page.evaluate(async () => { await document.querySelector('#app').__vue_app__.config.globalProperties.$router.push('/privacy-policy'); });
        await page.getByRole('dialog').waitFor({ state: 'detached' });
        assert.equal(await page.evaluate(() => window.__cameraStreams.every(stream => stream.getTracks().every(track => track.readyState === 'ended'))), true);
        assert.equal(await page.evaluate(() => window.__lastVideo.srcObject === null), true);
        report.checks.push(`real browser fake video: tracks ended and video detached after ${action}`);
      }
      assert.deepEqual(errors, []);
    } finally { await context.close(); }

    const qr = new QRCodeWriter().encode('001AB', BarcodeFormat.QR_CODE, 320, 320, new Map());
    const pixels = Array.from({ length: 320 * 320 }, (_, i) => qr.get(i % 320, Math.floor(i / 320)) ? 0 : 255);
    const decoding = await setup(browser, { width: 1280, height: 900 });
    try {
      await decoding.page.evaluate(pixels => {
        const canvas = document.createElement('canvas');
        canvas.width = canvas.height = 320;
        const ctx = canvas.getContext('2d');
        const data = ctx.createImageData(320, 320);
        for (let i = 0; i < pixels.length; i++) {
          data.data.set([pixels[i], pixels[i], pixels[i], 255], i * 4);
        }
        ctx.putImageData(data, 0, 0);
        navigator.mediaDevices.getUserMedia = async () => {
          const stream = canvas.captureStream(10);
          const timer = setInterval(() => {
            if (stream.getTracks().every(track => track.readyState === 'ended')) clearInterval(timer);
            else ctx.putImageData(data, 0, 0);
          }, 100);
          window.__cameraStreams.push(stream);
          return stream;
        };
      }, pixels);
      await decoding.page.getByRole('button', { name: 'Scan barcode or QR code', exact: true }).click();
      try {
        await decoding.page.getByText('Added Priority SKU item', { exact: true }).waitFor();
      } catch (error) {
        await screenshot(decoding.page, 'qr-debug');
        console.error(await decoding.page.evaluate(() => ({
          video: [...document.querySelectorAll('video')].map(video => ({ width: video.videoWidth, height: video.videoHeight, readyState: video.readyState, paused: video.paused })),
          alerts: [...document.querySelectorAll('[role="alert"]')].map(node => node.textContent),
        })));
        throw error;
      }
      assert.deepEqual(await cart(decoding.page), [{ id: 1, type: 'Service', quantity: 1 }]);
      assert.equal(await decoding.page.getByRole('dialog').count(), 0);
      assert.equal(await decoding.page.evaluate(() => window.__cameraStreams.every(stream => stream.getTracks().every(track => track.readyState === 'ended'))), true);
      await screenshot(decoding.page, 'desktop-qr-decoded');
      report.checks.push('ZXing decoded synthetic QR video through actual modal, selected unique SKU and stopped stream on result');
      assert.deepEqual(decoding.errors, []);
    } finally { await decoding.context.close(); }
    writeFileSync(path.join(output, 'report.json'), JSON.stringify(report, null, 2));
    console.log(JSON.stringify(report, null, 2));
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });

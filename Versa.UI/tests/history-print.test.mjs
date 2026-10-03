import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { runInNewContext } from 'node:vm';

const source = readFileSync(new URL('../src/views/HistoryCenter.vue', import.meta.url), 'utf8');
const printFunction = source.slice(source.indexOf('const printFromDetail ='), source.indexOf('const shareFromDetail ='));

for (const size of ['80mm', 'A4', 'A5']) {
  for (const viaAction of [false, true]) {
    test(`History ${viaAction ? 'action' : 'detail'} print opens dialog and offers manual controls (${size})`, async () => {
      let html = '';
      let documentClosed = false;
      let opened = 0;
      const popup = { document: {
        write: value => { html += value; },
        close: () => { documentClosed = true; },
      } };
      const dependencies = {
        selectedSale: { value: { sale: { SaleId: 248 } } },
        receiptSize: { value: size },
        supportsEpsonAssistant: false,
        printAndroidDetailImageIfPossible: async () => false,
        printWithAndroidIfPossible: () => false,
        nextTick: async () => {},
        shopInfo: { value: { LogoData: '', LogoSize: 'Large' } },
        window: { open: () => { opened++; return popup; } },
        document: { getElementById: id => {
          assert.equal(id, size === '80mm' ? 'print-area' : 'print-area-a4');
          return { innerHTML: '<div>Invoice 248</div>' };
        } },
      };
      const print = new Function(...Object.keys(dependencies), `${printFunction}\nreturn printFromDetail;`)(...Object.values(dependencies));
      await print(viaAction ? popup : null);
      assert.equal(opened, viaAction ? 0 : 1);
      assert.equal(documentClosed, true);
      assert.match(html, /@media print\s*\{ #history-print-controls \{ display: none !important; \} \}/);

      // Execute the generated popup script: Vue/Vite cannot catch syntax errors inside HTML strings.
      const script = html.match(/<script>([\s\S]*?)<\/script>/)?.[1];
      assert.ok(script);
      const handlers = {};
      const timers = new Map();
      let prints = 0;
      let closes = 0;
      const popupWindow = { focus() {}, print: () => { prints++; }, close: () => { closes++; } };
      runInNewContext(script, {
        window: popupWindow,
        document: { getElementById: id => {
          assert.ok(html.includes(`id="${id}"`));
          return { addEventListener: (event, handler) => { handlers[id] = handler; } };
        } },
        setTimeout: fn => { timers.set(1, fn); return 1; },
        clearTimeout: id => timers.delete(id),
      });
      assert.equal(prints, 0);
      popupWindow.onload();
      assert.equal(timers.size, 1);
      timers.get(1)();
      assert.equal(prints, 1);
      handlers['history-print-button']();
      assert.equal(prints, 2);
      handlers['history-close-button']();
      assert.equal(closes, 1);

      // A manual click before auto-print fires must not open two dialogs.
      popupWindow.onload();
      handlers['history-print-button']();
      assert.equal(timers.size, 0);
      assert.equal(prints, 3);
    });
  }
}

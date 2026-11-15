import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { runInNewContext } from 'node:vm';
import { buildNativePrintDocument } from '../src/utils/print.js';

const source = readFileSync(new URL('../src/views/HistoryCenter.vue', import.meta.url), 'utf8');
const printFunction = source.slice(source.indexOf('const printFromDetail ='), source.indexOf('const shareFromDetail ='));
const csvPayloadFunction = source.slice(source.indexOf('const csvValueOf ='), source.indexOf('const fetchFilteredExportData ='));

for (const size of ['80mm', '104mm', '104mm-slip', 'A4', 'A5']) {
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
        androidBridge: { isAndroid: () => false },
        isSlipReceiptSize: value => value === '80mm' || value === '104mm-slip',
        receiptPaper: value => ({
          '80mm': { pageSize: '80mm auto', bodyWidth: '76mm' },
          '104mm': { pageSize: '104mm auto', bodyWidth: '104mm' },
          '104mm-slip': { pageSize: '104mm auto', bodyWidth: '100mm' },
          A5: { pageSize: '148mm 210mm', bodyWidth: '148mm' },
          A4: { pageSize: '210mm 297mm', bodyWidth: '210mm' },
        })[value],
        printAndroidDetailImageIfPossible: async () => false,
        printWithAndroidIfPossible: () => false,
        nextTick: async () => {},
        shopInfo: { value: { LogoData: '', LogoSize: 'Large' } },
        window: { open: () => { opened++; return popup; } },
        document: { getElementById: id => {
          assert.equal(id, ['80mm', '104mm-slip'].includes(size) ? 'print-area' : 'print-area-a4');
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
      const windowHandlers = {};
      const timers = new Map();
      let prints = 0;
      let closes = 0;
      let openerFocuses = 0;
      const popupWindow = {
        focus() {},
        print: () => { prints++; },
        close: () => { closes++; },
        opener: { closed: false, focus: () => { openerFocuses++; } },
        addEventListener: (event, handler) => { windowHandlers[event] = handler; }
      };
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
      assert.equal(openerFocuses, 1);

      // Canceling the native preview fires afterprint; it must close the child and restore app focus.
      windowHandlers.afterprint();
      assert.equal(closes, 2);
      assert.equal(openerFocuses, 2);

      // A manual click before auto-print fires must not open two dialogs.
      popupWindow.onload();
      handlers['history-print-button']();
      assert.equal(timers.size, 0);
      assert.equal(prints, 3);
    });
  }
}

for (const size of ['104mm', '104mm-slip']) {
  test(`Android sale preview sends ${size} image directly to Bluetooth`, async () => {
    const saleSource = readFileSync(new URL('../src/views/Sale.vue', import.meta.url), 'utf8');
    const fn = saleSource.slice(saleSource.indexOf('const printFromPreview ='), saleSource.indexOf('const closeInvoicePreview ='));
    let printed;
    const deps = {
      supportsEpsonAssistant: false, receiptSize: { value: size }, saveInvoiceAsImage: { value: false },
      receiptPaper: () => ({ isSlip: size === '104mm-slip', pageSize: '104mm auto', bodyWidth: size === '104mm-slip' ? '100mm' : '104mm' }),
      shopInfo: { value: {} }, capturedPrintHtml: { value: '<div>KPay 1000</div>' }, lastSaleNo: { value: 'SA248' },
      buildNativePrintDocument,
      capturePreviewElement: async () => new Blob(['voucher']), blobToDataUrl: async () => 'data:image/png;base64,test',
      androidBridge: { isAndroid: () => true, supportsVoucherPrint: () => true,
        printVoucherImage: (image, paper) => { printed = { image, paper }; return { ok: true }; },
        printHtml: () => { throw new Error('Must not open Save as PDF'); } },
      printWithAndroidIfPossible: () => { throw new Error('Unexpected 80mm text printing'); },
      addToast() {}, t: key => key
    };
    await new Function(...Object.keys(deps), `${fn}; return printFromPreview;`)(...Object.values(deps))();
    assert.deepEqual(printed, { image: 'data:image/png;base64,test', paper: size });
  });
  test(`Android history sends ${size} image directly to Bluetooth`, async () => {
    let documentHtml;
    let closed = false;
    const deps = {
      selectedSale: { value: { sale: { SaleId: 248 } } }, receiptSize: { value: size },
      supportsEpsonAssistant: false,
      isSlipReceiptSize: value => value === '104mm-slip',
      receiptPaper: () => ({ pageSize: '104mm auto', bodyWidth: size === '104mm-slip' ? '100mm' : '104mm' }),
      buildNativePrintDocument,
      printWithAndroidIfPossible: () => { throw new Error('104mm must not use 80mm Bluetooth text'); },
      nextTick: async () => {}, shopInfo: { value: { LogoData: '' } },
      captureDetailInvoiceElement: async () => new Blob(['voucher']), blobToDataUrl: async () => 'data:image/png;base64,test',
      androidBridge: { isAndroid: () => true, supportsVoucherPrint: () => true,
        printVoucherImage: (image, paper) => { documentHtml = { image, paper }; return { ok: true }; },
        printHtml: () => { throw new Error('Must not open Save as PDF'); } },
      document: { getElementById: id => {
        assert.equal(id, size === '104mm-slip' ? 'print-area' : 'print-area-a4');
        return { innerHTML: '<div>Invoice 248: KPay 1000</div>' };
      } },
      window: { open: () => { throw new Error('Native print must not open popup'); } },
      addToast() {}, t: key => key
    };
    const print = new Function(...Object.keys(deps), `${printFunction}; return printFromDetail;`)(...Object.values(deps));
    await print({ close() { closed = true; } });
    assert.equal(closed, true);
    assert.deepEqual(documentHtml, { image: 'data:image/png;base64,test', paper: size });
  });
}

test('Return History CSV includes the matching sales or purchase voucher number', () => {
  const buildCsvPayload = new Function('t', 'formatDateTime', 'getSaleNetAmount', `${csvPayloadFunction}\nreturn buildCsvPayload;`)(
    key => key,
    value => value,
    () => 0
  );

  const { headers, rows } = buildCsvPayload('return', [
    { ReturnDate: '2026-10-04', ReturnType: 'Sale', SaleId: 42, ProductName: 'Coffee', Quantity: 2, RefundAmount: 5000, Reason: 'Damaged' },
    { returnDate: '2026-10-04', returnType: 'Purchase', poId: 9, productName: 'Tea', quantity: 1, refundAmount: 2000, reason: 'Wrong item' }
  ]);

  assert.deepEqual(headers, ['history.dateTime', 'history.voucherNo', 'history.category', 'history.productName', 'history.qty', 'history.refund', 'history.reason']);
  assert.equal(rows[0][1], 'SA-0000042');
  assert.equal(rows[1][1], 'PO-0000009');
});

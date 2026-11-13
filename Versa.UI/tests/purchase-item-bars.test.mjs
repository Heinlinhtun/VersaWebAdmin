import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { computed, ref } from 'vue';
import { parse } from '@vue/compiler-sfc';

const source = parse(readFileSync(new URL('../src/views/reports/PurchaseReport.vue', import.meta.url), 'utf8'))
  .descriptor.scriptSetup.content.replace(/^import .*;\r?\n/gm, '');

function createReport() {
  const exports = [];
  const deps = {
    ref, computed, onMounted() {}, watch() {},
    defineProps: () => ({ filters: {} }),
    useQuantitySettings: () => ({ formatQuantity: String }),
    useI18n: () => ({ t: key => key }),
    useCsvExport: () => ({ exportToCsv: (...args) => exports.push(args) }),
    api: {}
  };
  const vm = new Function(...Object.keys(deps), `${source}; return {
    purchaseItems, purchaseItemBars, toggleItemFilter, clearItemFilter,
    filteredPurchaseItems, currentPage, reportView, activeRows, handleExportCsv
  };`)(...Object.values(deps));
  return { ...vm, exports };
}

test('purchase bars combine batches while keeping different products and units separate', () => {
  const vm = createReport();
  vm.purchaseItems.value = [
    { ProductId: 1, UnitId: 1, ProductName: 'Coffee', UnitName: 'pcs', Quantity: 10, FOCQuantity: 2, LineTotal: 800 },
    { productId: 1, unitId: 1, productName: 'Coffee', unitName: 'pcs', quantity: 5, focQuantity: 1, lineTotal: 600 },
    { ProductId: 1, UnitId: 2, ProductName: 'Coffee', UnitName: 'box', Quantity: 2, LineTotal: 2000 },
    { ProductId: 2, UnitId: 1, ProductName: 'Coffee', UnitName: 'pcs', Quantity: 3, LineTotal: 300 },
    { ProductId: 3, UnitId: 1, ProductName: 'Gift', UnitName: 'pcs', Quantity: 0, FOCQuantity: 4, LineTotal: 0 }
  ];
  const bars = vm.purchaseItemBars.value;
  assert.equal(bars.length, 4);
  assert.equal(bars[0].unit, 'box');
  assert.equal(bars[0].percent, 100);
  const combined = bars.find(row => row.quantity === 15);
  assert.equal(combined.amount, 1400);
  assert.equal(combined.focQuantity, 3);
  assert.equal(combined.percent, 70);
  assert.equal(bars.at(-1).focQuantity, 4);

  vm.reportView.value = 'item';
  vm.currentPage.value = 3;
  vm.toggleItemFilter(combined);
  assert.equal(vm.currentPage.value, 1);
  assert.equal(vm.activeRows.value.length, 2);
  vm.handleExportCsv();
  assert.equal(vm.exports[0][2].length, 2);
  vm.toggleItemFilter(combined);
  assert.equal(vm.filteredPurchaseItems.value.length, 5);
  vm.toggleItemFilter(combined);
  vm.clearItemFilter();
  assert.equal(vm.filteredPurchaseItems.value.length, 5);
});

test('empty and zero-value purchases produce finite bar widths', () => {
  const vm = createReport();
  assert.deepEqual(vm.purchaseItemBars.value, []);
  vm.purchaseItems.value = [{ ProductId: 1, UnitId: 1, Quantity: 0, FOCQuantity: 2, LineTotal: 0 }];
  assert.equal(vm.purchaseItemBars.value[0].percent, 0);
});

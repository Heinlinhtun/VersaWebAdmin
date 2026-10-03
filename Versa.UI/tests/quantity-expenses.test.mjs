import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { ref, computed, nextTick } from 'vue';
import { parse, compileTemplate } from '@vue/compiler-sfc';

globalThis.localStorage = { getItem: () => null, setItem() {} };
globalThis.document = { documentElement: { classList: { toggle() {} } } };
const { useI18n } = await import('../src/composables/useI18n.js');
const { useQuantitySettings, applyQuantitySettings, normalizeQuantityDecimals } = await import('../src/composables/useQuantitySettings.js');
const { quantityInput } = await import('../src/utils/quantityInput.js');
const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');
const expenses = parse(read('../src/views/Expenses.vue')).descriptor;

test('quantity precision supports both setting casings, defaults and whole-number mode', () => {
  const settings = useQuantitySettings();
  for (const places of [1, 2, 3, 4, 5]) {
    applyQuantitySettings({ UseDecimalQuantity: true, QuantityDecimalPlaces: places });
    assert.equal(settings.quantityStep.value, 10 ** -places);
    assert.equal(settings.roundQuantity(1.123456), Number((1.123456).toFixed(places)));
  }
  applyQuantitySettings({ useDecimalQuantity: 1, quantityDecimalPlaces: 5 });
  assert.equal(settings.formatQuantity(0.00001), '0.00001');
  assert.equal(settings.floorQuantity(0.000019), 0.00001);
  assert.equal(settings.floorQuantity(1.234569), 1.23456);
  for (const invalid of [null, undefined, '', -1, 0, 6, 1.5, 'bad']) assert.equal(normalizeQuantityDecimals(invalid), 2);
  applyQuantitySettings({ UseDecimalQuantity: false, QuantityDecimalPlaces: 5 });
  assert.equal(settings.quantityStep.value, 1);
  assert.equal(settings.formatQuantity(1.23456), '1');
});

test('quantity input updates step/min live, rounds on change and respects available stock', async () => {
  const listeners = {}, dispatched = [];
  const element = {
    min: '1', max: '', value: '0.000019',
    addEventListener: (key, handler) => { listeners[key] = handler; },
    removeEventListener: key => { delete listeners[key]; },
    dispatchEvent: event => dispatched.push(event.type),
  };
  applyQuantitySettings({ UseDecimalQuantity: true, QuantityDecimalPlaces: 5 });
  quantityInput.mounted(element);
  assert.equal(element.step, '0.00001'); assert.equal(element.min, '0.00001');
  listeners.change(); assert.equal(element.value, '0.00002');
  element.max = '0.000019'; element.value = '1'; listeners.change();
  assert.equal(element.value, '0.00001'); assert.equal(dispatched.length, 2);
  applyQuantitySettings({ UseDecimalQuantity: true, QuantityDecimalPlaces: 2 });
  await nextTick(); assert.equal(element.step, '0.01'); assert.equal(element.min, '0.01');
  element.value = '1'; listeners.change(); assert.equal(element.value, '0');
  quantityInput.unmounted(element); assert.equal(listeners.change, undefined);
});

test('Live Inventory shows the selected decimal places including trailing zeros', () => {
  const { formatQuantity } = useQuantitySettings();
  const source = parse(read('../src/views/Inventory.vue')).descriptor.scriptSetup.content;
  const declaration = source.match(/const formatStockQuantity = [^\n]+/)[0];
  const formatStockQuantity = new Function('formatQuantity', `${declaration}; return formatStockQuantity;`)(formatQuantity);
  for (const places of [2, 4, 5]) {
    applyQuantitySettings({ UseDecimalQuantity: true, QuantityDecimalPlaces: places });
    assert.equal(formatStockQuantity(1), (1).toFixed(places));
    assert.equal(formatStockQuantity(0), (0).toFixed(places));
    assert.equal(formatStockQuantity(0.0001), (0.0001).toFixed(places));
    assert.equal(formatQuantity(1), '1');
  }
  applyQuantitySettings({ UseDecimalQuantity: false, QuantityDecimalPlaces: 5 });
  assert.equal(formatStockQuantity(1), '1');
});

function mountExpenses(api = {}) {
  const exports = [];
  const deps = { ref, computed, onMounted() {}, useI18n, useCsvExport: () => ({ exportToCsv: (...args) => exports.push(args) }), useToast: () => ({ addToast() {} }), api: { get: async () => ({ data: [] }), ...api } };
  const source = expenses.scriptSetup.content.replace(/^import .*;\r?\n/gm, '');
  const vm = new Function(...Object.keys(deps), `${source}; return { form, expenses, openCreateModal, openEditModal, saveExpense, filteredExpenses, paymentStatus, searchQuery, selectedCategory, fromDate, toDate, totalExpenseAmount, handleExportCsv, showModal };`)(...Object.values(deps));
  return { ...vm, exports };
}

test('expenses default paid; pending combines with category/search/date filters and CSV', () => {
  const vm = mountExpenses(); vm.openCreateModal(); assert.equal(vm.form.value.IsPaid, true);
  vm.expenses.value = [
    { Title: 'Rent', Category: 'Office', Amount: 10, ExpenseDate: '2026-09-17', IsPaid: false },
    { title: 'Rent', category: 'Office', amount: 20, expenseDate: '2026-09-17', isPaid: 0 },
    { Title: 'Rent', Category: 'Office', Amount: 30, ExpenseDate: '2026-09-17', IsPaid: true },
    { Title: 'Legacy', Amount: 40 },
  ];
  vm.paymentStatus.value = 'pending'; vm.searchQuery.value = 'rent'; vm.selectedCategory.value = 'Office';
  vm.fromDate.value = '2026-09-17'; vm.toDate.value = '2026-09-17';
  assert.equal(vm.filteredExpenses.value.length, 2); assert.equal(vm.totalExpenseAmount.value, 30);
  vm.handleExportCsv(); assert.equal(vm.exports[0][2].length, 2);
  assert.equal(vm.exports[0][2][0][5], useI18n().t('expenses.pending'));
  vm.paymentStatus.value = 'paid'; assert.equal(vm.filteredExpenses.value.length, 1);
  vm.openEditModal(vm.expenses.value[1]); assert.equal(vm.form.value.IsPaid, false);
  vm.openEditModal(vm.expenses.value[3]); assert.equal(vm.form.value.IsPaid, true);
});

test('pending expense saves false, retries without corrupting date, and can later be marked paid', async () => {
  const payloads = []; let fail = true;
  const vm = mountExpenses({
    post: async (_, payload) => { payloads.push({ ...payload }); if (fail) throw new Error('Offline'); },
    put: async (_, payload) => { payloads.push({ ...payload }); },
  });
  vm.openCreateModal(); vm.form.value.Title = 'Rent'; vm.form.value.IsPaid = false;
  vm.form.value.ExpenseDate = '2026-09-17';
  await vm.saveExpense(); assert.equal(vm.form.value.ExpenseDate, '2026-09-17'); assert.equal(vm.showModal.value, true);
  fail = false; await vm.saveExpense(); assert.equal(payloads[1].IsPaid, false); assert.equal(vm.showModal.value, false);
  assert.ok(!Number.isNaN(new Date(payloads[1].ExpenseDate).getTime()));
  vm.openEditModal({ ExpenseId: 2, IsPaid: false, ExpenseDate: '2026-09-17' });
  vm.form.value.IsPaid = true; await vm.saveExpense(); assert.equal(payloads.at(-1).IsPaid, true);
});

test('settings and expense labels are translated and templates compile', () => {
  const { locale, t } = useI18n();
  for (const key of ['settings.quantityDecimalPlaces', 'expenses.paid', 'expenses.pending', 'expenses.paymentStatus', 'expenses.allPaymentStatuses', 'expenses.paymentMethod']) {
    locale.value = 'en'; const english = t(key); locale.value = 'mm';
    assert.notEqual(t(key), english); assert.notEqual(t(key), key); assert.notEqual(english, key);
  }
  for (const descriptor of [expenses, parse(read('../src/views/Settings.vue')).descriptor]) {
    assert.deepEqual(compileTemplate({ source: descriptor.template.content, filename: 'test.vue', id: 'test' }).errors, []);
  }
});

test('bundle plus preserves tiny input, adds at 4/5 places and explains lower precision', () => {
  const source = parse(read('../src/views/Products.vue')).descriptor.scriptSetup.content;
  const handler = source.match(/const addItemToBundle = \(\) => \{[\s\S]*?\n\};/)[0];
  for (const places of [0, 2, 4, 5]) {
    applyQuantitySettings({ UseDecimalQuantity: places > 0, QuantityDecimalPlaces: places || 2 });
    const tempBundleItem = ref({ itemType: 'Product', productId: 1, unitId: 2, quantity: 0.0001 });
    const bundleForm = ref({ bundleItems: [] }), tempBundleUnits = ref([]), notices = [];
    const deps = { tempBundleItem, bundleForm, tempBundleUnits, ...useQuantitySettings(), t: useI18n().t, addToast: (...args) => notices.push(args) };
    const add = new Function(...Object.keys(deps), `${handler}; return addItemToBundle;`)(...Object.values(deps));
    const events = {};
    const element = {
      value: '0.0001', min: '', max: '',
      addEventListener: (key, handler) => { events[key] = handler; }, removeEventListener() {},
      dispatchEvent: () => { tempBundleItem.value.quantity = Number(element.value); },
    };
    quantityInput.mounted(element, { modifiers: { preserve: true } });
    events.change(); add();
    assert.equal(element.value, '0.0001');
    if (places >= 4) {
      assert.equal(bundleForm.value.bundleItems[0].quantity, 0.0001);
      assert.equal(notices.length, 0);
    } else {
      assert.equal(bundleForm.value.bundleItems.length, 0);
      assert.equal(tempBundleItem.value.quantity, 0.0001);
      assert.equal(notices.length, 1);
    }
    quantityInput.unmounted(element);
    if (places >= 4) {
      for (const item of [{ quantity: 0 }, { quantity: 1, itemType: 'Product', productId: 1 }, { quantity: 1, itemType: 'Product' }, { quantity: 1, itemType: 'Service' }]) {
        tempBundleItem.value = item;
        const before = notices.length;
        add(); assert.equal(notices.length, before + 1);
        assert.equal(bundleForm.value.bundleItems.length, 1);
      }
    }
  }
});

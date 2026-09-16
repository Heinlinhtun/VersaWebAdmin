import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { ref, computed, reactive, watch, effectScope, nextTick, markRaw } from 'vue';
import { parse, compileTemplate } from '@vue/compiler-sfc';

globalThis.localStorage = { getItem: () => null, setItem() {} };
globalThis.document = {
  documentElement: { classList: { toggle() {} } },
  body: { style: { overflow: 'auto' } },
};
const { useI18n } = await import('../src/composables/useI18n.js');
const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');
const partners = parse(read('../src/views/Partners.vue')).descriptor;
const modal = parse(read('../src/components/SupplierPaymentHistoryModal.vue')).descriptor;
const stripImports = source => source.replace(/^import .*;\r?\n/gm, '');

// Exercise the actual component setup with real Vue reactivity and translations.
function setup(t, source, dependencies, returned) {
  const scope = effectScope();
  t.after(() => scope.stop());
  return scope.run(() => new Function(...Object.keys(dependencies),
    `${stripImports(source)}\nreturn { ${returned} };`)(...Object.values(dependencies)));
}
function mountPartners(t) {
  const exports = [];
  const vm = setup(t, partners.scriptSetup.content, {
    ref, computed, reactive, watch, onMounted() {}, useI18n,
    useToast: () => ({ addToast() {} }),
    useCsvExport: () => ({ exportToCsv: (...args) => exports.push(args) }),
    usePermissions: () => ({ can: () => true, PERMISSIONS: {} }),
    useRouter: () => ({}), api: {},
  }, 'cust, supp, staff, exportPartnersCsv, activeTab, supplierHistoryTarget');
  return { ...vm, exports };
}
function mountHistory(t, get, supplier = { SupplierId: 7 }) {
  const props = reactive({ supplier });
  let mounted, unmount;
  const vm = setup(t, modal.scriptSetup.content, {
    ref, watch, useI18n, api: { get }, defineProps: () => props, defineEmits() {},
    onMounted: cb => { mounted = cb; }, onBeforeUnmount: cb => { unmount = cb; },
  }, 'payments, loading, error, loadHistory, displayValue, dialog, closeButton, trapFocus');
  t.after(() => unmount());
  return { ...vm, props, mounted, unmount };
}
const flush = async () => { await nextTick(); await Promise.resolve(); await Promise.resolve(); };
const deferred = () => { let resolve, reject; const promise = new Promise((a, b) => { resolve = a; reject = b; }); return { promise, resolve, reject }; };

test('exports all filtered rows across pages, accepts both API casings and preserves zero/false', t => {
  const vm = mountPartners(t);
  const { t: tr, locale } = useI18n();
  locale.value = 'en';
  for (const camel of [false, true]) {
    const casing = row => Object.fromEntries(Object.entries(row).map(([key, value]) => [camel ? key[0].toLowerCase() + key.slice(1) : key, value]));
    vm.cust.list = Array.from({ length: 12 }, (_, index) => casing({ Name: `Match ${index}`, Phone: '09123', Address: 'ရန်ကုန်, A', CurrentBalance: 0 }));
    vm.cust.list.push(casing({ Name: 'Excluded' }));
    vm.cust.searchQuery = 'match';
    vm.cust.currentPage = 2;
    vm.exportPartnersCsv('customers');
    const [filename, headers, rows] = vm.exports.at(-1);
    assert.match(filename, /^Partners_customers_\d{4}-\d{2}-\d{2}$/);
    assert.equal(headers.length, 5);
    assert.equal(rows.length, 12);
    assert.deepEqual(rows[0], [1, 'Match 0', '09123', 'ရန်ကုန်, A', 0]);
    vm.supp.list = [casing({ SupplierName: 'Match', ContactPerson: 'Contact', Phone: '09', Address: 'Town', CurrentDebt: 0 }), casing({ SupplierName: 'Excluded' })];
    vm.supp.searchQuery = 'match';
    vm.exportPartnersCsv('suppliers');
    assert.deepEqual(vm.exports.at(-1)[2], [[1, 'Match', 'Contact', '09', 'Town', 0]]);
    vm.staff.list = [casing({ Username: 'One', BranchName: 'Match', Role: 'Staff', IsActive: false }), casing({ Username: 'Match', BranchName: null, Role: 'Manager', IsActive: true }), casing({ Username: 'Excluded' })];
    vm.staff.searchQuery = 'match';
    vm.exportPartnersCsv('staff');
    assert.equal(vm.exports.at(-1)[1][1], tr('users.tableUser'));
    assert.deepEqual(vm.exports.at(-1)[2], [[1, 'One', 'Match', 'Staff', tr('users.statusInactive')], [2, 'Match', tr('users.unassigned'), 'Manager', tr('users.statusActive')]]);
  }
});

test('history uses PascalCase dynamic SQL fields and camelCase equivalents', async t => {
  const requests = [];
  const vm = mountHistory(t, async url => { requests.push(url); return { data: [
    { PaymentId: 1, PaymentDate: '2026-09-16T23:00:00Z', PaymentType: 'Purchase Payment', ReferenceNo: 'PU01', PaymentMethod: 'Cash', Note: 'မှတ်ချက်', Amount: 0 },
    { paymentId: 2, paymentDate: '2026-09-15', paymentType: 'Debt Payment', referenceNo: null, paymentMethod: 'KPay', note: 'Test', amount: '1250.5' },
  ] }; });
  assert.equal(vm.loading.value, true);
  await flush();
  assert.deepEqual(requests, ['/suppliers/7/payments']);
  assert.equal(vm.error.value, false);
  assert.deepEqual(vm.payments.value[0], { id: 1, date: '2026-09-16T23:00:00Z', type: 'Purchase Payment', reference: 'PU01', method: 'Cash', note: 'မှတ်ချက်', amount: 0 });
  const { locale, t: tr } = useI18n();
  for (const lang of ['en', 'mm']) {
    locale.value = lang;
    assert.equal(vm.displayValue(vm.payments.value[0], 'type'), tr('suppliers.purchasePayment'));
    assert.equal(vm.displayValue(vm.payments.value[1], 'type'), tr('suppliers.debtPayment'));
    assert.equal(vm.displayValue(vm.payments.value[0], 'date'), new Date(2026, 8, 16).toLocaleDateString(lang === 'mm' ? 'my-MM' : 'en-GB'));
    assert.equal(vm.displayValue(vm.payments.value[1], 'reference'), '—');
  }
  assert.equal(vm.payments.value[1].amount, 1250.5);
  assert.equal(vm.displayValue({ date: 'invalid' }, 'date'), '—');
  assert.equal(vm.displayValue({ amount: NaN }, 'amount'), '—');
});

test('history errors differ from empty success and retry recovers', async t => {
  for (const response of [new Error('Network'), new Error('403'), { data: {} }, { data: null }]) {
    let fail = true;
    const vm = mountHistory(t, async () => { if (fail) { if (response instanceof Error) throw response; return response; } return { data: [] }; });
    await flush();
    assert.equal(vm.error.value, true);
    assert.equal(vm.loading.value, false);
    fail = false;
    await vm.loadHistory();
    assert.equal(vm.error.value, false);
    assert.deepEqual(vm.payments.value, []);
  }
});

test('supplier changes, retries and closing cannot expose stale history', async t => {
  const first = deferred(), second = deferred();
  const vm = mountHistory(t, url => url.includes('/7/') ? first.promise : second.promise);
  vm.props.supplier = { supplierId: 8 };
  await nextTick();
  second.resolve({ data: [{ paymentId: 8, amount: 80 }] });
  await flush();
  first.reject(new Error('Late failure'));
  await flush();
  assert.equal(vm.payments.value[0].id, 8);
  assert.equal(vm.error.value, false);
  const last = deferred();
  const closing = mountHistory(t, () => last.promise);
  closing.unmount();
  last.resolve({ data: [{ PaymentId: 7 }] });
  await flush();
  assert.deepEqual(closing.payments.value, []);
});

test('dialog locks scrolling, traps focus, restores focus and closes on tab changes', async t => {
  const vm = mountHistory(t, async () => ({ data: [] }));
  const opener = { isConnected: true, focus() { document.activeElement = this; } };
  const first = markRaw({ focus() { document.activeElement = this; } });
  const last = markRaw({ focus() { document.activeElement = this; } });
  document.activeElement = opener;
  document.body.style.overflow = 'auto';
  vm.closeButton.value = first;
  vm.dialog.value = { querySelectorAll: () => [first, last] };
  vm.mounted();
  assert.equal(document.body.style.overflow, 'hidden');
  let prevented = 0;
  vm.trapFocus({ shiftKey: true, preventDefault() { prevented++; } });
  assert.equal(document.activeElement, last);
  vm.trapFocus({ shiftKey: false, preventDefault() { prevented++; } });
  assert.equal(document.activeElement, first);
  assert.equal(prevented, 2);
  vm.unmount();
  assert.equal(document.activeElement, opener);
  assert.equal(document.body.style.overflow, 'auto');
  const page = mountPartners(t);
  page.supplierHistoryTarget.value = { SupplierId: 7 };
  page.activeTab.value = 'staff';
  await nextTick();
  assert.equal(page.supplierHistoryTarget.value, null);
});

test('new UI, CSV headers and export notifications resolve in English and Myanmar', t => {
  const { locale, t: tr } = useI18n();
  const keys = [...modal.template.content.matchAll(/\bt\('([^']+)'\)/g)].map(match => match[1]);
  keys.push('suppliers.hDate', 'suppliers.hType', 'suppliers.hRef', 'suppliers.hChannel', 'suppliers.hNote', 'suppliers.hAmount');
  keys.push('suppliers.purchasePayment', 'suppliers.debtPayment', 'partners.exportCsv', 'common.noDataToExport', 'common.exportSuccess', 'common.exportFailed');
  for (const key of keys) {
    locale.value = 'en'; const english = tr(key);
    assert.notEqual(english, key);
    locale.value = 'mm'; assert.notEqual(tr(key), key); assert.notEqual(tr(key), english);
  }
  const vm = mountPartners(t);
  for (const tab of ['customers', 'suppliers', 'staff']) {
    locale.value = 'en'; vm.exportPartnersCsv(tab); const english = vm.exports.at(-1)[1];
    locale.value = 'mm'; vm.exportPartnersCsv(tab); const myanmar = vm.exports.at(-1)[1];
    english.forEach((header, index) => { assert.ok(header); assert.doesNotMatch(header, /^(users|customers|suppliers)\./); assert.notEqual(header, myanmar[index]); });
  }
  for (const descriptor of [partners, modal]) {
    assert.deepEqual(compileTemplate({ source: descriptor.template.content, filename: 'test.vue', id: 'test' }).errors, []);
  }
});

test('CSV download includes Myanmar BOM, escapes cells and reports empty/error states', async () => {
  const notices = [], blobs = [], links = [];
  const { t: tr, locale } = useI18n(); locale.value = 'en';
  const doc = { body: { appendChild() {}, removeChild() {} }, createElement: () => { const link = { style: {}, click() {} }; links.push(link); return link; } };
  const url = { createObjectURL: blob => { blobs.push(blob); return 'blob:test'; }, revokeObjectURL() {} };
  const source = stripImports(read('../src/composables/useCsvExport.js')).replace('export function', 'function');
  const { exportToCsv } = new Function('useToast', 'useI18n', 'window', 'document', 'setTimeout', `${source}; return useCsvExport();`)(
    () => ({ addToast: (...args) => notices.push(args) }), useI18n, { URL: url }, doc, cb => cb());
  exportToCsv('partners', ['Name', 'Note'], [['မြန်မာ', 'a,"b"\nc'], ['=SUM(1)', -3]]);
  assert.equal(links[0].download, 'partners.csv');
  const bytes = new Uint8Array(await blobs[0].arrayBuffer());
  assert.deepEqual([...bytes.slice(0, 3)], [239, 187, 191]);
  assert.equal(await blobs[0].text(), 'Name,Note\nမြန်မာ,"a,""b""\nc"\n\'=SUM(1),-3');
  assert.deepEqual(notices.at(-1), [tr('common.exportSuccess'), 'success']);
  exportToCsv('empty', [], []);
  assert.equal(blobs.length, 1);
  assert.deepEqual(notices.at(-1), [tr('common.noDataToExport'), 'error']);
  url.createObjectURL = () => { throw new Error('Download failed'); };
  const original = console.error;
  try { console.error = () => {}; exportToCsv('failed', ['Name'], [['Test']]); } finally { console.error = original; }
  assert.deepEqual(notices.at(-1), [tr('common.exportFailed'), 'error']);
});

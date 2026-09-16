import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { ref, computed, nextTick, watch, effectScope } from 'vue';
import { parse } from '@vue/compiler-sfc';
import { normalizeReceiptPayments, receiptUnitName } from '../src/utils/receiptData.js';

const storage = new Map();
globalThis.document = { documentElement: { classList: { toggle() {} } } };
globalThis.localStorage = {
  getItem: key => storage.get(key) ?? null,
  setItem: (key, value) => storage.set(key, value),
};
const { useI18n } = await import('../src/composables/useI18n.js');
const { descriptor } = parse(readFileSync(new URL('../src/views/Sale.vue', import.meta.url), 'utf8'));
// Execute the real setup with external effects stubbed; keep Vue reactivity and i18n.
const setup = descriptor.scriptSetup.content.replace(/^import .*;\r?\n/gm, '');
function mountSetup(t, post, { get, online = true } = {}) {
  const scope = effectScope();
  t.after(() => scope.stop());
  const notices = [];
  let cached;
  const dependencies = {
    ref, computed, nextTick, watch, normalizeReceiptPayments, receiptUnitName, useI18n,
    onMounted() {}, onUnmounted() {},
    api: { post, get },
    useToast: () => ({ addToast: (...args) => notices.push(args) }),
    isAppleMobile: () => false,
    isMobileBrowser: () => false,
    androidBridge: {
      isAvailable: () => true,
      isAndroid: () => true,
      isOnline: () => online,
      saveCatalog: value => { cached = value; },
    },
  };
  const instance = scope.run(() => new Function(...Object.keys(dependencies), `${setup}\nreturn {
    newCustomerForm, saveNewCustomer, customers, saleForm, customerSearch,
    showNewCustomerModal, savingNewCustomer, ensureCustomerForOrder,
    selectedCustomerObj, catalogView, currentPage, refreshCheckoutCustomerBalance
  };`)(...Object.values(dependencies)));
  return { ...instance, notices, get cached() { return cached; } };
}

test('checkout replaces credit from a returned voucher with the fresh customer balance', async t => {
  const vm = mountSetup(t, undefined, { get: async () => ({ data: [
    { CustomerId: 106, Name: 'Customer', CurrentBalance: 0 }
  ] }) });
  vm.customers.value = [{ customerId: 106, currentBalance: 968600 }];
  vm.saleForm.value.CustomerId = 106;
  assert.equal(await vm.refreshCheckoutCustomerBalance(), 0);
  assert.equal(vm.selectedCustomerObj.value.currentBalance, 0);
});

test('offline checkout keeps locally known credit and walk-in needs no request', async t => {
  const vm = mountSetup(t, undefined, { online: false, get: () => { throw new Error('Unexpected request'); } });
  vm.saleForm.value.CustomerId = null;
  assert.equal(await vm.refreshCheckoutCustomerBalance(), 0);
  vm.customers.value = [{ customerId: 106, currentBalance: 734600 }];
  vm.saleForm.value.CustomerId = 106;
  assert.equal(await vm.refreshCheckoutCustomerBalance(), 734600);
});

test('created customer is immediately selected and survives same-name checkout', async t => {
  for (const camelCase of [false, true]) {
    const vm = mountSetup(t, async () => ({ data: camelCase
      ? { customerId: 42, name: 'Same Name', phone: '09123' }
      : { CustomerId: 42, Name: 'Same Name', Phone: '09123' } }));
    vm.customers.value = [{ customerId: 1, name: 'Same Name' }];
    vm.newCustomerForm.value = { Name: 'Same Name', Phone: '09123', Address: '' };
    vm.showNewCustomerModal.value = true;
    await vm.saveNewCustomer();
    assert.equal(vm.saleForm.value.CustomerId, 42);
    assert.equal(vm.customerSearch.value, 'Same Name');
    assert.equal(vm.selectedCustomerObj.value.phone, '09123');
    assert.equal(vm.showNewCustomerModal.value, false);
    assert.equal(await vm.ensureCustomerForOrder({ requireCustomer: true }), 42);
    assert.equal(vm.cached.customers.at(-1).customerId, 42);
  }
});

test('pending save cannot submit twice; errors retain the form and selection', async t => {
  let rejectRequest;
  let requests = 0;
  const vm = mountSetup(t, () => {
    requests++;
    return new Promise((_, reject) => { rejectRequest = reject; });
  });
  vm.saleForm.value.CustomerId = 7;
  vm.newCustomerForm.value.Name = 'Retry customer';
  vm.showNewCustomerModal.value = true;
  const saving = vm.saveNewCustomer();
  await vm.saveNewCustomer();
  assert.equal(requests, 1);
  rejectRequest(new Error('Network failure'));
  await saving;
  assert.equal(vm.savingNewCustomer.value, false);
  assert.equal(vm.showNewCustomerModal.value, true);
  assert.equal(vm.newCustomerForm.value.Name, 'Retry customer');
  assert.equal(vm.saleForm.value.CustomerId, 7);
  vm.newCustomerForm.value.Name = '   ';
  await vm.saveNewCustomer();
  assert.equal(requests, 1);
});

test('catalog preference persists and resets pagination', async t => {
  const vm = mountSetup(t, async () => { throw new Error('Unexpected request'); });
  vm.currentPage.value = 3;
  vm.catalogView.value = 'list';
  await nextTick();
  assert.equal(storage.get('SaleCatalogView'), 'list');
  assert.equal(vm.currentPage.value, 1);
  assert.equal(mountSetup(t).catalogView.value, 'list');
});

test('customer and view controls resolve in English and Myanmar', () => {
  const { locale, t } = useI18n();
  const keys = [
    'sale.changeView', 'sale.gridView', 'sale.listView', 'sale.addNewCustomer',
    'sale.processing', 'common.close', 'customers.modalTitleAdd',
    'customers.labelName', 'customers.labelPhone', 'customers.labelAddress',
    'customers.placeholderName', 'customers.placeholderPhone', 'customers.placeholderAddress',
    'customers.btnSave', 'customers.identityRequired', 'customers.toastSuccess', 'customers.toastError',
  ];
  for (const key of keys) {
    locale.value = 'en';
    const english = t(key);
    assert.notEqual(english, key);
    locale.value = 'mm';
    assert.notEqual(t(key), english, `${key} must have its own Myanmar translation`);
    assert.match(t(key), /[\u1000-\u109f]/);
  }
  locale.value = 'en';
});

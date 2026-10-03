import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { ref, computed, watch } from 'vue';
import { parse, compileTemplate } from '@vue/compiler-sfc';

const source = readFileSync(new URL('../src/views/Products.vue', import.meta.url), 'utf8');
const component = parse(source).descriptor;
globalThis.localStorage = { getItem: () => null, setItem() {} };
globalThis.document = { documentElement: { classList: { toggle() {} } } };
const { useI18n } = await import('../src/composables/useI18n.js');
const { t, setLocale } = useI18n();
function mount(post) {
  const toasts = [], showModal = ref(true), currentTab = ref('bundles');
  const deps = {
    ref, computed, watch, t, showModal, currentTab, isEdit: ref(false),
    api: { post }, localStorage: { getItem: () => '3' },
    addToast: (...args) => toasts.push(args), tx: ref({ bundleSaveFailed: 'Failed' }), fetchAll() {},
  };
  const state = component.scriptSetup.content.split('const newBundleForm =')[1].split('const tempBundleItem =')[0];
  const save = component.scriptSetup.content.match(/const saveBundle = async \(\) => \{[\s\S]*?\n\};/)[0];
  const vm = new Function(...Object.keys(deps), `const newBundleForm = ${state}\n${save}
    return { bundleForm, productionMaximum, productionLoading, productionError, productionReady, refreshProductionAvailability, saveBundle };`)(...Object.values(deps));
  return { ...vm, toasts, showModal, currentTab };
}
const enable = vm => {
  vm.bundleForm.value.bundleItems = [{ productId: 1, unitId: 2, quantity: 10, itemType: 'Product' }];
  vm.bundleForm.value.productionUnitId = 2;
  vm.bundleForm.value.addAsProduct = true;
};

test('production availability accepts both response casings and gates valid whole quantities', async () => {
  for (const key of ['maximumAvailability', 'MaximumAvailability']) {
    const vm = mount(async (url, payload) => {
      assert.equal(url, '/bundles/production-availability');
      assert.equal(payload.branchId, 3);
      return { data: { [key]: 20 } };
    });
    enable(vm);
    assert.equal(vm.productionReady.value, false);
    await vm.refreshProductionAvailability();
    assert.equal(vm.productionMaximum.value, 20);
    for (const quantity of [0, -1, 1.5, 21, '2']) {
      vm.bundleForm.value.productionQuantity = quantity;
      assert.equal(vm.productionReady.value, false);
    }
    vm.bundleForm.value.productionQuantity = 20;
    assert.equal(vm.productionReady.value, true);
    vm.bundleForm.value.productionUnitId = null;
    assert.equal(vm.productionReady.value, false);
  }
});

test('component edits, errors and closing invalidate availability; late responses are ignored', async () => {
  const pending = [];
  const vm = mount(() => new Promise((resolve, reject) => pending.push({ resolve, reject })));
  enable(vm);
  const latest = vm.refreshProductionAvailability();
  pending[1].resolve({ data: { maximumAvailability: 5 } });
  await latest;
  pending[0].resolve({ data: { maximumAvailability: 20 } });
  await Promise.resolve();
  assert.equal(vm.productionMaximum.value, 5);
  vm.bundleForm.value.bundleItems[0].quantity = 11;
  assert.equal(vm.productionMaximum.value, null);
  pending[2].reject({ response: { data: { Code: 'stockChanged', Message: 'Stock unavailable' } } });
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(vm.productionError.value, 'products.production.stockChanged');
  const closing = vm.refreshProductionAvailability();
  vm.showModal.value = false;
  pending[3].resolve({ data: { maximumAvailability: 20 } });
  await closing;
  assert.equal(vm.productionMaximum.value, null);
  assert.equal(vm.productionReady.value, false);
});

test('production submits once, preserves the form on failure and opens Products on success', async () => {
  let calls = 0, resolveSave, rejectSave;
  const vm = mount((url, payload) => {
    if (url.endsWith('production-availability')) return Promise.resolve({ data: { maximumAvailability: 20 } });
    calls++;
    assert.equal(payload.addAsProduct, true);
    assert.equal(payload.productionQuantity, 4);
    assert.equal(payload.branchId, 3);
    return new Promise((resolve, reject) => { resolveSave = resolve; rejectSave = reject; });
  });
  enable(vm);
  vm.bundleForm.value.bundleName = 'Finished item';
  vm.bundleForm.value.productionQuantity = 4;
  await vm.refreshProductionAvailability();
  const failed = vm.saveBundle();
  await vm.saveBundle();
  assert.equal(calls, 1);
  rejectSave({ response: { data: { code: 'insufficientStock', message: 'Insufficient stock' } } });
  await failed;
  assert.equal(vm.showModal.value, true);
  assert.equal(vm.bundleForm.value.bundleName, 'Finished item');
  assert.equal(vm.toasts.at(-1)[0], t('products.production.insufficientStock'));
  const success = vm.saveBundle();
  resolveSave({ data: { ProductId: 99 } });
  await success;
  assert.equal(vm.showModal.value, false);
  assert.equal(vm.currentTab.value, 'products');
});

test('standard bundles still use the bundle endpoint and production template compiles', async () => {
  let payload;
  const vm = mount(async (url, body) => { assert.equal(url, '/bundles'); payload = body; return { data: {} }; });
  vm.bundleForm.value.bundleName = 'Standard bundle';
  await vm.saveBundle();
  assert.equal(payload.addAsProduct, false);
  assert.equal(vm.currentTab.value, 'bundles');
  assert.deepEqual(compileTemplate({ source: component.template.content, filename: 'Products.vue', id: 'production' }).errors, []);
});

test('production controls and every server validation code translate in English and Myanmar', () => {
  const controller = readFileSync(new URL('../../Versa.POS/Controllers/BundlesController.cs', import.meta.url), 'utf8');
  const calculation = readFileSync(new URL('../../Versa.Core/Services/BundleProductionCalculation.cs', import.meta.url), 'utf8');
  const codes = [...(controller + calculation).matchAll(/new BundleProductionException\("([^"]+)"/g)].map(match => match[1]);
  const keys = ['produce', 'badge', 'produceMore', 'recipeHint', 'recoveredRecipe', 'setupRecipe', 'recipeLoadFailed', 'label', 'warning', 'branchNote', 'unit', 'selectUnit', 'maximum', 'quantity', 'refresh',
    'calculating', 'unavailable', 'success', 'availabilityFailed', 'saveFailed', 'creationOnly', ...codes];
  try {
    for (const key of keys) {
      setLocale('en');
      const english = t(`products.production.${key}`);
      assert.notEqual(english, `products.production.${key}`);
      setLocale('mm');
      const myanmar = t(`products.production.${key}`);
      assert.notEqual(myanmar, english, key);
      assert.match(myanmar, /[\u1000-\u109f]/, key);
    }
    setLocale('en');
    assert.equal(t('products.production.warning'), 'Stock for the component items will be deducted in advance before the product is sold.');
  } finally { setLocale('en'); }
});

test('repeat production posts to the existing product and keeps its saved recipe', async () => {
  const calls = [];
  const vm = mount(async (url, body) => {
    calls.push({ url, body });
    return { data: url.endsWith('production-availability') ? { maximumAvailability: 12 } : { ProductId: 99 } };
  });
  enable(vm);
  vm.bundleForm.value.productionProductId = 99;
  vm.bundleForm.value.productionQuantity = 6;
  await vm.refreshProductionAvailability();
  await vm.saveBundle();
  const submitted = calls.filter(call => !call.url.endsWith('production-availability'));
  assert.equal(submitted.length, 1);
  assert.equal(submitted[0].url, '/bundles/production/99');
  assert.equal(submitted[0].body.productionQuantity, 6);
  assert.deepEqual(submitted[0].body.bundleItems, [{ itemType: 'Product', productId: 1, serviceId: null, unitId: 2, quantity: 10 }]);
  assert.equal(vm.currentTab.value, 'products');
});

test('focused production stays on Bundles after producing the selected product', async () => {
  const vm = mount(async (url, body) => {
    if (url.endsWith('production-availability')) return { data: { maximumAvailability: 8 } };
    assert.equal(url, '/bundles/production/42');
    assert.equal(body.productionQuantity, 3);
    return { data: { ProductId: 42 } };
  });
  enable(vm);
  Object.assign(vm.bundleForm.value, { productionOnly: true, productionProductId: 42, productionQuantity: 3 });
  await vm.refreshProductionAvailability();
  await vm.saveBundle();
  assert.equal(vm.currentTab.value, 'bundles');
  assert.equal(vm.showModal.value, false);
});

test('Bundles Produce action opens only production controls without saving product edits', async () => {
  const deps = { ref, t, quickPurchaseBusy: ref(false), saveProduct: () => assert.fail('Must not save product edits'),
    api: { get: async url => {
      assert.equal(url, '/bundles/production/42');
      return { data: { BundleName: 'Finished item', BundlePrice: 10, ProductionUnitId: 1,
        BundleItems: [{ ItemType: 'Product', ProductId: 2, UnitId: 1, Quantity: 5 }] } };
    } }, showModal: ref(false), isEdit: ref(false), bundleForm: ref({}), newBundleForm: () => ({ productionQuantity: 1 }),
    tempBundleItem: ref({}), tempBundleUnits: ref([]), currentTab: ref('bundles'), addToast() { assert.fail('Unexpected error'); } };
  const code = component.scriptSetup.content.split('const productionOpening =')[1].split('const editItem =')[0];
  await new Function(...Object.keys(deps), `const productionOpening = ${code}; return openProduction(42, true);`)(...Object.values(deps));
  assert.equal(deps.bundleForm.value.productionOnly, true);
  assert.equal(deps.bundleForm.value.productionProductId, 42);
  assert.equal(deps.bundleForm.value.bundleItems[0].quantity, 5);
  assert.equal(deps.showModal.value, true);
});

test('bundle recipe editor opens saved components in either API casing', async () => {
  for (const pascal of [true, false]) {
    const data = { BundleName: 'Finished goods', BundlePrice: 100, ProductionUnitId: 2, RecipeRecovered: false,
      BundleItems: [{ ItemType: 'Product', ProductId: 1, UnitId: 2, Quantity: 10 }] };
    const response = pascal ? data : Object.fromEntries(Object.entries(data).map(([key, value]) => [key[0].toLowerCase() + key.slice(1), value]));
    const deps = { ref, t, quickPurchaseBusy: ref(false), saveProduct: async () => 99,
      api: { get: async url => { assert.equal(url, '/bundles/production/99'); return { data: response }; } },
      showModal: ref(true), isEdit: ref(true), bundleForm: ref({}), newBundleForm: () => ({ productionQuantity: 1 }),
      tempBundleItem: ref({}), tempBundleUnits: ref([]), currentTab: ref('products'), addToast() { assert.fail('Unexpected error'); } };
    const code = component.scriptSetup.content.split('const productionOpening =')[1].split('const editItem =')[0];
    await new Function(...Object.keys(deps), `const productionOpening = ${code}; return openProduction(99);`)(...Object.values(deps));
    assert.equal(deps.bundleForm.value.productionProductId, 99);
    assert.equal(deps.bundleForm.value.addAsProduct, true);
    assert.equal(deps.bundleForm.value.bundleItems[0].quantity, 10);
    assert.equal(deps.showModal.value, true);
    assert.equal(deps.currentTab.value, 'bundles');
    assert.equal(deps.isEdit.value, false);
  }
});

test('production errors change language without losing form state and unknown errors use translated fallbacks', async () => {
  const vm = mount(async () => { throw { response: { data: { code: 'unitRequired' } } }; });
  enable(vm);
  await vm.refreshProductionAvailability();
  try {
    setLocale('en');
    assert.equal(t(vm.productionError.value), 'Select a unit for the produced product.');
    setLocale('mm');
    assert.match(t(vm.productionError.value), /[\u1000-\u109f]/);
    assert.equal(vm.bundleForm.value.productionUnitId, 2);
    assert.equal(vm.productionReady.value, false);
    const failed = mount(async () => { throw { response: { data: { code: 'Unknown', message: 'Raw server error' } } }; });
    enable(failed);
    await failed.refreshProductionAvailability();
    assert.equal(failed.productionError.value, 'products.production.availabilityFailed');
    assert.match(t(failed.productionError.value), /[\u1000-\u109f]/);
  } finally { setLocale('en'); }
});

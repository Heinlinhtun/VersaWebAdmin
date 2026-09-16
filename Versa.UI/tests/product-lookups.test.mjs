import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { ref, computed } from 'vue';
import { parse, compileTemplate } from '@vue/compiler-sfc';

globalThis.localStorage = { getItem: () => null, setItem() {} };
globalThis.document = { documentElement: { classList: { toggle() {} } } };
const { useI18n } = await import('../src/composables/useI18n.js');
const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');
const modal = parse(read('../src/components/ProductLookupCreateModal.vue')).descriptor;
const products = parse(read('../src/views/Products.vue')).descriptor;
const purchases = parse(read('../src/views/Purchases.vue')).descriptor;
function mount(kind, post) {
  const emitted = [];
  const deps = {
    ref, computed, onMounted() {}, onBeforeUnmount() {}, nextTick() {},
    defineProps: () => ({ kind }), defineEmits: () => (...args) => emitted.push(args),
    useI18n, useToast: () => ({ addToast() {} }), api: { post },
  };
  const source = modal.scriptSetup.content.replace(/^import .*;\r?\n/gm, '');
  return { ...new Function(...Object.keys(deps), `${source}; return {name, shortName, supplierDetails, saving, error, save, close};`)(...Object.values(deps)), emitted };
}

test('inline unit/category creation trims input, accepts API casing and selects returned ID', async () => {
  for (const kind of ['units', 'categories']) for (const pascal of [false, true]) {
    const unit = kind === 'units';
    const data = unit ? { unitId: 42, unitName: 'Box', shortName: 'bx' } : { categoryId: 43, categoryName: 'Parts' };
    const response = pascal ? Object.fromEntries(Object.entries(data).map(([k, v]) => [k[0].toUpperCase() + k.slice(1), v])) : data;
    const vm = mount(kind, async (url, payload) => {
      assert.equal(url, `/${kind}`);
      assert.deepEqual(payload, unit ? { unitName: 'Box', shortName: 'bx' } : { categoryName: 'Parts' });
      return { data: response };
    });
    vm.name.value = unit ? ' Box ' : ' Parts ';
    vm.shortName.value = ' bx ';
    await vm.save();
    assert.deepEqual(vm.emitted, [['created', data]]);
    const lookupCreateKind = ref(kind), units = ref([]), categories = ref([]);
    const form = ref({ name: 'Unsaved product', sku: 'ABC', unitId: 1, categoryId: 2 });
    const handler = products.scriptSetup.content.match(/const selectCreatedLookup = \(record\) => \{[\s\S]*?\n\};/)[0];
    new Function('lookupCreateKind', 'units', 'categories', 'form', `${handler}; selectCreatedLookup(arguments[4]);`)(lookupCreateKind, units, categories, form, data);
    assert.equal(form.value[unit ? 'unitId' : 'categoryId'], unit ? 42 : 43);
    assert.equal(form.value.name, 'Unsaved product');
    assert.equal(form.value.sku, 'ABC');
    assert.equal(lookupCreateKind.value, null);
    assert.deepEqual((unit ? units : categories).value, [data]);
  }
});

test('blank input, duplicate submissions and failures preserve the creation form', async () => {
  let calls = 0, reject;
  const vm = mount('units', () => { calls++; return new Promise((_, fail) => { reject = fail; }); });
  vm.name.value = '   ';
  await vm.save();
  assert.equal(calls, 0);
  assert.ok(vm.error.value);
  vm.name.value = 'Box';
  const pending = vm.save();
  await vm.save();
  vm.close();
  assert.equal(calls, 1);
  assert.deepEqual(vm.emitted, []);
  reject({ response: { data: { message: 'Name already exists' } } });
  await pending;
  assert.equal(vm.error.value, 'Name already exists');
  assert.equal(vm.name.value, 'Box');
  assert.equal(vm.saving.value, false);
  vm.close();
  assert.deepEqual(vm.emitted, [['close']]);
});

test('supplier creation selects the returned supplier in Quick Purchase and Purchases', async () => {
  for (const pascal of [false, true]) {
    const record = { SupplierId: 58, SupplierName: 'Parts supplier', ContactPerson: 'Contact', Phone: '09123', Address: 'Yangon' };
    const vm = mount('suppliers', async (url, payload) => {
      assert.equal(url, '/suppliers');
      assert.deepEqual(payload, { supplierName: 'Parts supplier', contactPerson: 'Contact', phone: '09123', address: 'Yangon' });
      return { data: pascal ? record : Object.fromEntries(Object.entries(record).map(([key, value]) => [key[0].toLowerCase() + key.slice(1), value])) };
    });
    vm.name.value = ' Parts supplier ';
    vm.supplierDetails.value = { contactPerson: ' Contact ', phone: ' 09123 ', address: ' Yangon ' };
    await vm.save();
    assert.deepEqual(vm.emitted, [['created', record]]);
    for (const descriptor of [products, purchases]) {
      const isQuick = descriptor === products;
      const form = ref({ SupplierId: 1, Items: [{ ProductId: 5, Quantity: 3 }] });
      const quickPurchase = ref({ supplierId: 1, quantity: 3 });
      const deps = {
        form, quickPurchase, suppliers: ref([]), supplierSearch: ref('Old supplier'),
        filteredSuppliers: ref([]), showSupplierResults: ref(true), supplierHighlightedIndex: ref(0),
        lookupCreateKind: ref('suppliers'), showSupplierCreate: ref(true),
      };
      const select = descriptor.scriptSetup.content.match(/const selectSupplier = \(supplier\) => \{[\s\S]*?\n\};/)[0];
      const handlerName = isQuick ? 'selectCreatedLookup' : 'selectCreatedSupplier';
      const handler = descriptor.scriptSetup.content.match(new RegExp(`const ${handlerName} = \\(record\\) => \\{[\\s\\S]*?\\n\\};`))[0];
      new Function(...Object.keys(deps), 'record', `${select}\n${handler}\n${handlerName}(record);`)(...Object.values(deps), record);
      assert.equal(isQuick ? quickPurchase.value.supplierId : form.value.SupplierId, 58);
      assert.equal(deps.supplierSearch.value, 'Parts supplier');
      assert.equal(deps.showSupplierResults.value, false);
      assert.deepEqual(deps.suppliers.value, [record]);
      assert.equal(quickPurchase.value.quantity, 3);
      assert.deepEqual(form.value.Items, [{ ProductId: 5, Quantity: 3 }]);
      assert.equal(isQuick ? deps.lookupCreateKind.value : deps.showSupplierCreate.value, isQuick ? null : false);
    }
  }
});

test('supplier errors retain contact details and retry succeeds', async () => {
  let fail = true;
  const vm = mount('suppliers', async () => {
    if (fail) throw new Error('Offline');
    return { data: { supplierId: 8, supplierName: 'Supplier' } };
  });
  vm.name.value = 'Supplier';
  vm.supplierDetails.value.phone = '09123';
  await vm.save();
  assert.ok(vm.error.value);
  assert.equal(vm.supplierDetails.value.phone, '09123');
  assert.deepEqual(vm.emitted, []);
  fail = false;
  await vm.save();
  assert.equal(vm.error.value, '');
  assert.equal(vm.emitted[0][1].SupplierId, 8);
});

test('new lookup labels translate and Vue templates compile', () => {
  const { locale, t } = useI18n();
  for (const key of ['products.addNewUnit', 'products.addNewCategory', 'products.lookupNameRequired', 'suppliers.addNewSupplier', 'suppliers.labelName', 'suppliers.placeholderName', 'suppliers.labelContact', 'suppliers.placeholderContact', 'suppliers.labelPhone', 'suppliers.placeholderPhone', 'suppliers.labelAddress', 'suppliers.placeholderAddress']) {
    locale.value = 'en'; const english = t(key);
    locale.value = 'mm'; assert.notEqual(t(key), english); assert.notEqual(english, key); assert.notEqual(t(key), key);
  }
  for (const descriptor of [modal, products, purchases]) assert.deepEqual(compileTemplate({ source: descriptor.template.content, filename: 'test.vue', id: 'test' }).errors, []);
});

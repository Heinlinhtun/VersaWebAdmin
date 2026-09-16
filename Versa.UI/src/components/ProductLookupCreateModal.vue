<template>
  <Teleport to="body">
    <div role="dialog" aria-modal="true" aria-labelledby="lookup-create-title"
      class="fixed inset-0 z-[150] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4"
      @keydown.esc.stop.prevent="close" @keydown.tab="trapFocus" ref="dialog">
      <form @submit.prevent="save" class="bg-white w-full max-w-sm max-h-full overflow-y-auto rounded-xl shadow-2xl">
        <header class="bg-[#3f3f46] p-5 text-white flex items-center justify-between gap-3">
          <h3 id="lookup-create-title">{{ isSupplier ? t('suppliers.addNewSupplier') : t(`products.addNew${isUnit ? 'Unit' : 'Category'}`) }}</h3>
          <button type="button" @click="close" :disabled="saving" :aria-label="t('common.close')" class="min-h-11 min-w-11 text-2xl disabled:opacity-40">×</button>
        </header>
        <div class="p-6 space-y-4">
          <div class="space-y-1.5">
            <label for="lookup-name" class="text-xs text-slate-500">{{ t(`${kind}.labelName`) }}</label>
            <input id="lookup-name" ref="nameInput" v-model="name" required :disabled="saving"
              :placeholder="t(`${kind}.placeholderName`)" class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg text-sm">
          </div>
          <div v-if="isUnit" class="space-y-1.5">
            <label for="lookup-short-name" class="text-xs text-slate-500">{{ t('units.shortName') }}</label>
            <input id="lookup-short-name" v-model="shortName" :disabled="saving"
              :placeholder="t('units.shortNamePlaceholder')" class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg text-sm">
          </div>
          <template v-if="isSupplier">
            <div v-for="field in supplierFields" :key="field.key" class="space-y-1.5">
              <label :for="`lookup-${field.key}`" class="text-xs text-slate-500">{{ t(`suppliers.label${field.label}`) }}</label>
              <input :id="`lookup-${field.key}`" v-model="supplierDetails[field.key]" :type="field.key === 'phone' ? 'tel' : 'text'"
                :disabled="saving" :placeholder="t(`suppliers.placeholder${field.label}`)"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg text-sm">
            </div>
          </template>
          <p v-if="error" role="alert" class="text-sm text-rose-600">{{ error }}</p>
          <div class="flex gap-3">
            <button type="button" @click="close" :disabled="saving" class="flex-1 min-h-11 rounded-lg bg-slate-100 text-slate-600 disabled:opacity-40">{{ t('common.cancel') }}</button>
            <button type="submit" :disabled="saving" class="flex-1 min-h-11 rounded-lg bg-blue-600 text-white disabled:opacity-40">{{ saving ? t('sale.processing') : t(`${kind}.btnSave`) }}</button>
          </div>
        </div>
      </form>
    </div>
  </Teleport>
</template>

<script setup>
import { ref, computed, onMounted, onBeforeUnmount, nextTick } from 'vue';
import api from '../services/api';
import { useI18n } from '../composables/useI18n';
import { useToast } from '../composables/useToast';

const props = defineProps({ kind: { type: String, required: true, validator: value => ['units', 'categories', 'suppliers'].includes(value) } });
const emit = defineEmits(['close', 'created']);
const { t } = useI18n();
const { addToast } = useToast();
const isUnit = computed(() => props.kind === 'units');
const isSupplier = computed(() => props.kind === 'suppliers');
const supplierDetails = ref({ contactPerson: '', phone: '', address: '' });
const supplierFields = [
  { key: 'contactPerson', label: 'Contact' },
  { key: 'phone', label: 'Phone' },
  { key: 'address', label: 'Address' },
];
const name = ref('');
const shortName = ref('');
const saving = ref(false);
const error = ref('');
const nameInput = ref(null);
const dialog = ref(null);
let previousFocus;
let disposed = false;

function close() { if (!saving.value) emit('close'); }
async function save() {
  if (saving.value) return;
  error.value = '';
  const trimmedName = name.value.trim();
  if (!trimmedName) { error.value = t('products.lookupNameRequired'); return; }
  saving.value = true;
  try {
    const payload = isUnit.value
      ? { unitName: trimmedName, shortName: shortName.value.trim() }
      : isSupplier.value
      ? { supplierName: trimmedName, ...Object.fromEntries(Object.entries(supplierDetails.value).map(([key, value]) => [key, value.trim()])) }
      : { categoryName: trimmedName };
    const { data } = await api.post(`/${props.kind}`, payload);
    if (disposed) return;
    const record = isUnit.value
      ? { unitId: data.unitId ?? data.UnitId, unitName: data.unitName ?? data.UnitName ?? trimmedName, shortName: data.shortName ?? data.ShortName ?? payload.shortName }
      : isSupplier.value
      ? { SupplierId: data.supplierId ?? data.SupplierId, SupplierName: data.supplierName ?? data.SupplierName ?? trimmedName,
        ContactPerson: data.contactPerson ?? data.ContactPerson ?? payload.contactPerson,
        Phone: data.phone ?? data.Phone ?? payload.phone, Address: data.address ?? data.Address ?? payload.address }
      : { categoryId: data.categoryId ?? data.CategoryId, categoryName: data.categoryName ?? data.CategoryName ?? trimmedName };
    if (!(Number(record.unitId ?? record.categoryId ?? record.SupplierId) > 0)) throw new Error('Missing created record ID');
    emit('created', record);
    addToast(t(`${props.kind}.toastSuccess`), 'success');
  } catch (err) {
    if (!disposed) error.value = err.response?.data?.message || err.response?.data?.Message || t(`${props.kind}.toastError`);
  } finally { saving.value = false; }
}
function trapFocus(event) {
  const controls = [...dialog.value.querySelectorAll('button:not(:disabled), input:not(:disabled)')];
  const first = controls[0], last = controls.at(-1);
  if (!first) { event.preventDefault(); return; }
  if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
  else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
}
onMounted(() => { previousFocus = document.activeElement; nameInput.value?.focus(); });
onBeforeUnmount(() => {
  disposed = true;
  nextTick(() => { if (previousFocus?.isConnected) previousFocus.focus(); });
});
</script>

<template>
  <Teleport to="body">
    <div ref="dialog" role="dialog" aria-modal="true" aria-labelledby="supplier-history-title"
      @keydown.esc.stop.prevent="$emit('close')" @keydown.tab="trapFocus"
      class="fixed inset-0 h-dvh z-[120] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-3 sm:p-4">
      <section class="bg-white w-full max-w-4xl max-h-full flex flex-col rounded-xl shadow-2xl overflow-hidden border border-slate-200">
        <header class="bg-[#3f3f46] p-4 sm:p-6 text-white flex justify-between items-start gap-3 shrink-0">
          <div class="min-w-0">
            <h3 id="supplier-history-title" class="text-lg">{{ t('suppliers.historyTitle') }}</h3>
            <p class="text-orange-300 text-sm mt-1 break-words">{{ supplier.SupplierName ?? supplier.supplierName }}</p>
          </div>
          <button ref="closeButton" type="button" @click="$emit('close')" :aria-label="t('common.close')"
            class="min-w-11 min-h-11 text-2xl rounded-lg hover:bg-white/10">×</button>
        </header>

        <div class="overflow-auto overscroll-contain min-h-0 flex-1 p-4 sm:p-6" :aria-busy="loading">
          <p v-if="loading" role="status" class="py-8 text-center text-slate-500">{{ t('suppliers.historyLoading') }}</p>
          <div v-else-if="error" role="alert" class="py-8 text-center space-y-4">
            <p class="text-rose-600">{{ t('suppliers.loadHistoryError') }}</p>
            <button type="button" @click="loadHistory" class="min-h-11 px-5 rounded-lg bg-orange-50 text-orange-700 border border-orange-200">
              {{ t('suppliers.historyRetry') }}
            </button>
          </div>
          <p v-else-if="!payments.length" class="py-8 text-center text-slate-500">{{ t('suppliers.noPaymentHistory') }}</p>
          <template v-else>
            <table class="hidden md:table w-full text-left text-sm">
              <thead class="text-xs text-slate-500 bg-slate-50">
                <tr>
                  <th v-for="column in columns" :key="column.key" class="p-3 font-normal" :class="{ 'text-right': column.key === 'amount' }">{{ t(column.label) }}</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100">
                <tr v-for="payment in payments" :key="payment.id">
                  <td v-for="column in columns" :key="column.key" class="p-3 align-top break-words max-w-48 whitespace-pre-line"
                    :class="column.key === 'amount' ? 'text-right tabular-nums text-emerald-700' : 'text-slate-600'">{{ displayValue(payment, column.key) }}</td>
                </tr>
              </tbody>
            </table>
            <div class="md:hidden space-y-3">
              <dl v-for="payment in payments" :key="payment.id" class="border border-slate-200 rounded-xl p-4 space-y-2 text-sm">
                <div v-for="column in columns" :key="column.key" class="grid grid-cols-2 gap-3">
                  <dt class="text-slate-500">{{ t(column.label) }}</dt>
                  <dd class="min-w-0 text-right break-words whitespace-pre-line" :class="column.key === 'amount' ? 'text-emerald-700 tabular-nums' : 'text-slate-800'">{{ displayValue(payment, column.key) }}</dd>
                </div>
              </dl>
            </div>
          </template>
        </div>
        <footer class="p-4 border-t border-slate-100 flex justify-end shrink-0">
          <button type="button" @click="$emit('close')" class="min-h-11 px-6 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50">{{ t('common.close') }}</button>
        </footer>
      </section>
    </div>
  </Teleport>
</template>

<script setup>
import { ref, watch, onMounted, onBeforeUnmount } from 'vue';
import api from '../services/api';
import { useI18n } from '../composables/useI18n';

const props = defineProps({ supplier: { type: Object, required: true } });
defineEmits(['close']);
const { t, locale } = useI18n();
const payments = ref([]);
const loading = ref(false);
const error = ref(false);
const dialog = ref(null);
const closeButton = ref(null);
let requestId = 0;
let previousFocus;
let previousOverflow;
const columns = [
  { key: 'date', label: 'suppliers.hDate' },
  { key: 'type', label: 'suppliers.hType' },
  { key: 'reference', label: 'suppliers.hRef' },
  { key: 'method', label: 'suppliers.hChannel' },
  { key: 'note', label: 'suppliers.hNote' },
  { key: 'amount', label: 'suppliers.hAmount' },
];

async function loadHistory() {
  const currentRequest = ++requestId;
  payments.value = [];
  error.value = false;
  loading.value = true;
  try {
    const id = props.supplier.SupplierId ?? props.supplier.supplierId;
    if (id == null) throw new Error('Missing supplier ID');
    const { data } = await api.get(`/suppliers/${id}/payments`);
    if (currentRequest !== requestId) return;
    if (!Array.isArray(data)) throw new Error('Invalid payment history');
    payments.value = data.map(payment => ({
      id: payment.PaymentId ?? payment.paymentId,
      date: payment.PaymentDate ?? payment.paymentDate,
      type: payment.PaymentType ?? payment.paymentType,
      reference: payment.ReferenceNo ?? payment.referenceNo,
      method: payment.PaymentMethod ?? payment.paymentMethod,
      note: payment.Note ?? payment.note,
      amount: Number(payment.Amount ?? payment.amount ?? 0),
    }));
  } catch {
    if (currentRequest === requestId) error.value = true;
  } finally {
    if (currentRequest === requestId) loading.value = false;
  }
}

function displayValue(payment, key) {
  const value = payment[key];
  if (key === 'amount') return Number.isFinite(value) ? `${value.toLocaleString(locale.value === 'mm' ? 'my-MM' : 'en-GB')} Ks` : '—';
  if (key === 'date') {
    // These are business dates; do not shift them across days via UTC conversion.
    const datePart = String(value ?? '').split('T')[0].split(' ')[0];
    const date = new Date(`${datePart}T00:00:00`);
    return Number.isNaN(date.getTime()) ? '—' : date.toLocaleDateString(locale.value === 'mm' ? 'my-MM' : 'en-GB');
  }
  if (key === 'type') {
    if (value === 'Purchase Payment') return t('suppliers.purchasePayment');
    if (value === 'Debt Payment') return t('suppliers.debtPayment');
  }
  return value || '—';
}

function trapFocus(event) {
  const controls = [...dialog.value.querySelectorAll('button')];
  const first = controls[0];
  const last = controls[controls.length - 1];
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault(); last.focus();
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault(); first.focus();
  }
}

watch(() => props.supplier.SupplierId ?? props.supplier.supplierId, loadHistory, { immediate: true });
onMounted(() => {
  previousFocus = document.activeElement;
  previousOverflow = document.body.style.overflow;
  document.body.style.overflow = 'hidden';
  closeButton.value?.focus();
});
onBeforeUnmount(() => {
  ++requestId;
  document.body.style.overflow = previousOverflow;
  if (previousFocus?.isConnected) previousFocus.focus();
});
</script>

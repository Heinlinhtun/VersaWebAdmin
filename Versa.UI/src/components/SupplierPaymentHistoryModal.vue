<template>
  <Teleport to="body">
    <div ref="dialog" role="dialog" aria-modal="true" aria-labelledby="supplier-history-title"
      @keydown.esc.stop.prevent="close" @keydown.tab="trapFocus"
      class="fixed inset-0 h-dvh z-[120] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-3 sm:p-4">
      <section class="bg-white w-full max-w-4xl max-h-full flex flex-col rounded-xl shadow-2xl overflow-hidden border border-slate-200">
        <header class="bg-[#3f3f46] p-4 sm:p-6 text-white flex justify-between items-start gap-3 shrink-0">
          <div class="min-w-0">
            <h3 id="supplier-history-title" class="text-lg">{{ t('suppliers.historyTitle') }}</h3>
            <p class="text-orange-300 text-sm mt-1 break-words">{{ supplier.SupplierName ?? supplier.supplierName }}</p>
          </div>
          <button ref="closeButton" type="button" @click="close" :disabled="reversing" :aria-label="t('common.close')"
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
                  <th v-for="column in columns" :key="column.key" class="p-3 font-normal" :class="{ 'text-right': column.key === 'amount' }">{{ column.key === 'action' ? labels.actions : t(column.label) }}</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100">
                <tr v-for="payment in payments" :key="payment.id">
                  <td v-for="column in columns" :key="column.key" class="p-3 align-top break-words max-w-48 whitespace-pre-line"
                    :class="column.key === 'amount' ? 'text-right tabular-nums text-emerald-700' : 'text-slate-600'">
                    <button v-if="column.key === 'action' && payment.canReverse" type="button" :disabled="reversing"
                      @click="selectReversal(payment)" class="min-h-11 px-3 rounded border border-rose-300 text-rose-700">{{ labels.reverse }}</button>
                    <button v-else-if="column.key === 'reference' && allowPurchaseDetails && payment.reference?.startsWith('PU')"
                      type="button" @click="emit('purchase', payment.reference)" class="text-blue-700 underline">{{ payment.reference }}</button>
                    <span v-else>{{ displayValue(payment, column.key) }}</span>
                  </td>
                </tr>
              </tbody>
            </table>
            <div class="md:hidden space-y-3">
              <dl v-for="payment in payments" :key="payment.id" class="border border-slate-200 rounded-xl p-4 space-y-2 text-sm">
                <div v-for="column in columns" :key="column.key" class="grid grid-cols-2 gap-3">
                  <dt class="text-slate-500">{{ column.key === 'action' ? labels.actions : t(column.label) }}</dt>
                  <dd class="min-w-0 text-right break-words whitespace-pre-line" :class="column.key === 'amount' ? 'text-emerald-700 tabular-nums' : 'text-slate-800'">
                    <button v-if="column.key === 'action' && payment.canReverse" type="button" :disabled="reversing"
                      @click="selectReversal(payment)" class="min-h-11 px-3 rounded border border-rose-300 text-rose-700">{{ labels.reverse }}</button>
                    <button v-else-if="column.key === 'reference' && allowPurchaseDetails && payment.reference?.startsWith('PU')"
                      type="button" @click="emit('purchase', payment.reference)" class="text-blue-700 underline">{{ payment.reference }}</button>
                    <span v-else>{{ displayValue(payment, column.key) }}</span>
                  </dd>
                </div>
              </dl>
            </div>
          </template>
          <form v-if="reversalTarget" @submit.prevent="reversePayment" class="mt-4 border border-rose-200 rounded-xl p-4 space-y-3">
            <p class="text-sm">{{ labels.confirm }} #{{ reversalTarget.id }} — {{ displayValue(reversalTarget, 'amount') }}</p>
            <p class="text-xs text-slate-600">{{ labels.explanation }}</p>
            <label class="block text-sm" for="supplier-reversal-reason">{{ labels.reason }}</label>
            <textarea id="supplier-reversal-reason" ref="reasonInput" v-model="reversalReason" required minlength="5" maxlength="500"
              :disabled="reversing" class="w-full border rounded p-2" />
            <div class="flex gap-3">
              <button type="submit" :disabled="reversing || reversalReason.trim().length < 5" class="min-h-11 px-4 bg-rose-700 text-white rounded disabled:opacity-50">
                {{ reversing ? labels.processing : labels.reverse }}</button>
              <button type="button" :disabled="reversing" @click="reversalTarget = null" class="min-h-11 px-4 border rounded">{{ labels.cancel }}</button>
            </div>
          </form>
          <p v-if="reversalError" role="alert" class="mt-3 text-sm text-rose-700">{{ reversalError }}</p>
          <p v-if="reversalSuccess" role="status" class="mt-3 text-sm text-emerald-700">{{ labels.success }}</p>
        </div>
        <footer class="p-4 border-t border-slate-100 flex justify-end shrink-0">
          <button type="button" @click="close" :disabled="reversing" class="min-h-11 px-6 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50">{{ t('common.close') }}</button>
        </footer>
      </section>
    </div>
  </Teleport>
</template>

<script setup>
import { computed, ref, watch, onMounted, onBeforeUnmount, nextTick } from 'vue';
import api from '../services/api';
import { useI18n } from '../composables/useI18n';
import { usePermissions } from '../composables/usePermissions';

const props = defineProps({ supplier: { type: Object, required: true }, allowPurchaseDetails: { type: Boolean, default: false } });
const emit = defineEmits(['close', 'changed', 'purchase']);
const { t, locale } = useI18n();
const { hasRole } = usePermissions();
const reversalTarget = ref(null);
const reversalReason = ref('');
const reversing = ref(false);
const reversalError = ref('');
const reversalSuccess = ref(false);
const reasonInput = ref(null);
const labels = computed(() => locale.value === 'mm' ? {
  actions: 'လုပ်ဆောင်ချက်များ', reverse: 'ငွေပေးချေမှု ပယ်ဖျက်ရန်', confirm: 'ပယ်ဖျက်မည့် ငွေပေးချေမှု',
  explanation: 'ဤလုပ်ဆောင်ချက်သည် စာရင်းမှတ်တမ်းကိုသာ ပြင်ဆင်ပြီး ဘဏ်မှ ငွေပြန်လွှဲခြင်း မပြုပါ။ နောက်ဆက်တွဲစာရင်း ပြောင်းလဲထားပါက ပယ်ဖျက်ခွင့်မပြုပါ။',
  reason: 'ပယ်ဖျက်ရသည့် အကြောင်းရင်း', processing: 'ဆောင်ရွက်နေသည်…', cancel: 'မလုပ်တော့ပါ',
  review: 'စာရင်းပြန်စစ်ရန် လိုအပ်သည်', reversed: 'ပယ်ဖျက်ပြီး', reversal: 'ပယ်ဖျက်စာရင်း', success: 'ငွေပေးချေမှုကို ပယ်ဖျက်ပြီးပါပြီ။'
} : {
  actions: 'Actions', reverse: 'Reverse payment', confirm: 'Reverse payment',
  explanation: 'This corrects the accounting record; it does not transfer money back from the supplier. Later activity may block reversal.',
  reason: 'Reason for reversal', processing: 'Processing…', cancel: 'Cancel',
  review: 'Reconciliation required', reversed: 'Reversed', reversal: 'Reversal entry', success: 'Payment reversed.'
});
const payments = ref([]);
const loading = ref(false);
const error = ref(false);
const dialog = ref(null);
const closeButton = ref(null);
let requestId = 0;
let previousFocus;
let previousOverflow;
const columns = computed(() => [
  { key: 'id', label: 'ID' },
  { key: 'date', label: 'suppliers.hDate' },
  { key: 'type', label: 'suppliers.hType' },
  { key: 'reference', label: 'suppliers.hRef' },
  { key: 'method', label: 'suppliers.hChannel' },
  { key: 'note', label: 'suppliers.hNote' },
  { key: 'amount', label: 'suppliers.hAmount' },
  ...(hasRole('Admin') ? [{ key: 'action' }] : []),
]);

function close() { if (!reversing.value) emit('close'); }
async function selectReversal(payment) {
  if (!hasRole('Admin') || !payment.canReverse || reversing.value) return;
  reversalTarget.value = payment;
  reversalReason.value = '';
  reversalError.value = '';
  reversalSuccess.value = false;
  await nextTick();
  reasonInput.value?.focus();
}
async function reversePayment() {
  if (reversing.value || !reversalTarget.value || !hasRole('Admin')) return;
  reversing.value = true;
  reversalError.value = '';
  try {
    const supplierId = props.supplier.SupplierId ?? props.supplier.supplierId;
    await api.post(`/suppliers/${supplierId}/payments/${reversalTarget.value.id}/reverse`, { Reason: reversalReason.value.trim() });
    reversalTarget.value = null;
    reversalSuccess.value = true;
    emit('changed');
    await loadHistory();
  } catch (err) {
    reversalError.value = err.response?.data?.message || 'Unable to confirm reversal. Retry the same payment; it can only be reversed once.';
  } finally { reversing.value = false; }
}

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
      canReverse: Boolean(Number(payment.CanRequestReversal ?? payment.canRequestReversal ?? 0)),
      reversed: payment.ReversalPaymentId ?? payment.reversalPaymentId,
      reverses: payment.ReversesPaymentId ?? payment.reversesPaymentId,
      reversalReason: payment.ReversalReason ?? payment.reversalReason,
      reversedBy: payment.ReversedBy ?? payment.reversedBy,
      reversedAt: payment.ReversedAt ?? payment.reversedAt,
    }));
  } catch {
    if (currentRequest === requestId) error.value = true;
  } finally {
    if (currentRequest === requestId) loading.value = false;
  }
}

function displayValue(payment, key) {
  const value = payment[key];
  if (key === 'action') return payment.reversed ? labels.value.reversed : payment.reverses ? labels.value.reversal : labels.value.review;
  if (key === 'note' && payment.reversed) return `${value || ''}\n${labels.value.reversed}: ${payment.reversalReason}\nUser #${payment.reversedBy} · ${payment.reversedAt} UTC`;
  if (key === 'amount') return Number.isFinite(value) ? `${value.toLocaleString(locale.value === 'mm' ? 'my-MM' : 'en-GB')} Ks` : '—';
  if (key === 'date') {
    // These are business dates; do not shift them across days via UTC conversion.
    const datePart = String(value ?? '').split('T')[0].split(' ')[0];
    const date = new Date(`${datePart}T00:00:00`);
    return Number.isNaN(date.getTime()) ? '—' : date.toLocaleDateString(locale.value === 'mm' ? 'my-MM' : 'en-GB');
  }
  if (key === 'type') {
    if (payment.reversed) return labels.value.reversed;
    if (payment.reverses) return `${labels.value.reversal} #${payment.reverses}`;
    if (value === 'Purchase Payment') return t('suppliers.purchasePayment');
    if (value === 'Debt Payment') return t('suppliers.debtPayment');
  }
  return value || '—';
}

function trapFocus(event) {
  const controls = [...dialog.value.querySelectorAll('button:not(:disabled), textarea:not(:disabled)')];
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

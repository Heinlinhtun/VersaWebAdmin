<template>
  <div class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
    <div class="bg-white w-full max-w-4xl max-h-[92vh] rounded-xl shadow-2xl overflow-y-auto border border-slate-200">
      <div class="sticky top-0 z-10 bg-[#3f3f46] px-6 py-5 text-white flex justify-between items-center">
        <h3 class="text-lg font-normal uppercase tracking-tight">{{ t('inventory.importModalTitle') }}</h3>
        <button @click="$emit('close')" class="text-white/60 hover:text-white text-2xl">&times;</button>
      </div>

      <div class="p-6 space-y-6">
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-5">
          <div class="bg-blue-50 border border-blue-100 rounded-xl p-5 flex items-center justify-between">
            <div>
              <p class="text-[11px] text-blue-400 uppercase tracking-widest mb-1">{{ t('inventory.stepOne') }}</p>
              <p class="text-sm text-blue-800">{{ t('inventory.downloadTemplate') }}</p>
            </div>
            <button @click="downloadTemplate" class="bg-white text-blue-600 border border-blue-200 px-4 py-2 rounded-lg text-[10px] uppercase hover:bg-blue-50 shadow-sm">
              {{ t('inventory.getTemplate') }}
            </button>
          </div>

          <div>
            <p class="text-[11px] text-slate-400 uppercase tracking-widest mb-2 ml-1">{{ t('inventory.stepTwo') }}: {{ t('inventory.uploadExcel') }}</p>
            <label class="group flex flex-col items-center justify-center w-full h-28 border-2 border-dashed border-slate-200 rounded-xl cursor-pointer hover:bg-slate-50 hover:border-blue-400 transition-all">
              <svg class="w-7 h-7 mb-2 text-slate-300 group-hover:text-blue-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" />
              </svg>
              <p class="text-xs text-slate-500">{{ selectedFile ? selectedFile.name : t('inventory.selectExcelFile') }}</p>
              <input ref="fileInput" type="file" @change="onFileChange" class="hidden" accept=".xlsx,.xls,.csv" />
            </label>
          </div>
        </div>

        <button @click="handleUpload" :disabled="!selectedFile || importing"
          class="w-full bg-blue-600 text-white py-3.5 rounded-xl uppercase text-xs tracking-widest shadow-lg shadow-blue-100 hover:bg-blue-700 active:scale-[.99] disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2">
          <span v-if="importing" class="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin"></span>
          {{ importing ? t('inventory.processingImport') : t('inventory.startImport') }}
        </button>

        <div v-if="importing" class="space-y-2">
          <div class="w-full bg-blue-100 rounded-full h-1.5 overflow-hidden"><div class="bg-blue-600 h-1.5 w-full animate-progress origin-left"></div></div>
          <p class="text-[10px] text-slate-500 text-center">{{ t('inventory.importWait') }}</p>
        </div>

        <div v-if="lastPurchaseOrders.length" class="rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3">
          <p class="text-[10px] uppercase tracking-widest text-emerald-600 mb-1">{{ t('inventory.createdPurchaseOrders') }}</p>
          <p class="text-sm font-medium text-emerald-800">{{ lastPurchaseOrders.join(', ') }}</p>
        </div>

        <section class="border-t border-slate-200 pt-5">
          <div class="flex items-center justify-between mb-3">
            <div>
              <h4 class="text-sm font-medium text-slate-800">{{ t('inventory.importHistory') }}</h4>
              <p class="text-[10px] text-slate-400 mt-0.5">{{ t('inventory.undoSafetyNote') }}</p>
            </div>
            <button @click="loadHistory" :disabled="historyLoading" class="text-[10px] uppercase text-blue-600 hover:underline disabled:opacity-50">{{ t('common.refresh') }}</button>
          </div>

          <div v-if="historyLoading" class="py-8 text-center text-xs text-slate-400">{{ t('inventory.loadingImportHistory') }}</div>
          <div v-else-if="!history.length" class="py-8 text-center text-xs text-slate-400 border border-dashed border-slate-200 rounded-xl">{{ t('inventory.noImportHistory') }}</div>
          <div v-else class="border border-slate-200 rounded-xl overflow-x-auto">
            <table class="w-full min-w-[700px] text-left">
              <thead class="bg-slate-50 text-[9px] uppercase tracking-wider text-slate-500">
                <tr>
                  <th class="px-4 py-3">{{ t('inventory.fileAndDate') }}</th>
                  <th class="px-4 py-3">{{ t('inventory.purchaseOrders') }}</th>
                  <th class="px-4 py-3 text-center">{{ t('inventory.importSummary') }}</th>
                  <th class="px-4 py-3 text-center">{{ t('inventory.importStatus') }}</th>
                  <th class="px-4 py-3 text-right">{{ t('inventory.action') }}</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100 text-xs">
                <tr v-for="batch in history" :key="field(batch, 'BatchId')">
                  <td class="px-4 py-3">
                    <p class="font-medium text-slate-700">{{ field(batch, 'FileName') }}</p>
                    <p class="text-[10px] text-slate-400 mt-1">{{ formatDate(field(batch, 'ImportedAt')) }} · {{ field(batch, 'ImportedByName') }}</p>
                  </td>
                  <td class="px-4 py-3 text-slate-600">{{ field(batch, 'PurchaseOrderNumbers') || '—' }}</td>
                  <td class="px-4 py-3 text-center text-slate-500">{{ field(batch, 'ItemCount') }} {{ t('inventory.rows') }} · {{ field(batch, 'CreatedProductCount') }} {{ t('inventory.created') }} · {{ field(batch, 'UpdatedProductCount') }} {{ t('inventory.updated') }}</td>
                  <td class="px-4 py-3 text-center">
                    <span :class="isCompleted(batch) ? 'bg-emerald-50 text-emerald-700 border-emerald-200' : 'bg-slate-100 text-slate-500 border-slate-200'" class="inline-flex px-2.5 py-1 rounded-full border text-[9px] uppercase tracking-wider">
                      {{ isCompleted(batch) ? t('inventory.completed') : t('inventory.rolledBack') }}
                    </span>
                  </td>
                  <td class="px-4 py-3 text-right">
                    <button v-if="isCompleted(batch)" @click="rollbackImport(batch)" :disabled="rollingBackId === field(batch, 'BatchId')" class="text-[10px] uppercase text-rose-600 border border-rose-200 rounded-lg px-3 py-1.5 hover:bg-rose-50 disabled:opacity-50">
                      {{ rollingBackId === field(batch, 'BatchId') ? t('inventory.undoing') : t('inventory.undoImport') }}
                    </button>
                    <span v-else class="text-slate-300">—</span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </div>
  </div>
</template>

<style scoped>
@keyframes indeterminate-progress {
  0% { transform: translateX(-100%) scaleX(0.2); }
  50% { transform: translateX(0%) scaleX(0.5); }
  100% { transform: translateX(100%) scaleX(0.2); }
}
.animate-progress { animation: indeterminate-progress 2s infinite linear; }
</style>

<script setup>
import { onMounted, ref } from 'vue';
import api from '../services/api';
import { useToast } from '../composables/useToast';
import { useI18n } from '../composables/useI18n';
import { useCsvExport } from '../composables/useCsvExport';
import { sanitizeError } from '../utils/sanitize';

const emit = defineEmits(['close', 'refresh']);
const { addToast } = useToast();
const { t } = useI18n();
const { exportToCsv } = useCsvExport();
const selectedFile = ref(null);
const fileInput = ref(null);
const importing = ref(false);
const historyLoading = ref(false);
const history = ref([]);
const rollingBackId = ref(null);
const lastPurchaseOrders = ref([]);

const field = (record, name) => record?.[name] ?? record?.[name.charAt(0).toLowerCase() + name.slice(1)];
const isCompleted = (batch) => String(field(batch, 'Status')).toLowerCase() === 'completed';
const formatDate = (value) => value ? new Date(value).toLocaleString() : '—';
const apiError = (err, fallback) => sanitizeError(err.response?.data?.Message || err.response?.data?.message || err.response?.data || fallback);

const loadHistory = async () => {
  historyLoading.value = true;
  try {
    const res = await api.get('/ExcelImport/history');
    history.value = Array.isArray(res.data) ? res.data : [];
  } catch (err) {
    addToast(apiError(err, t('inventory.historyLoadError')), 'error');
  } finally {
    historyLoading.value = false;
  }
};

const onFileChange = (event) => { selectedFile.value = event.target.files?.[0] || null; };

const downloadTemplate = () => {
  const headers = ['Branch Name', 'Item Name', 'SKU/Barcode', 'Category', 'Basic Unit', 'Qty', 'Purchase Price', 'Selling Price', 'Expiry Date', 'Warning Day count', 'Supplier Name'];
  const exampleRow = ['Main Branch', 'Example Item', 'SKU123', 'General', 'pcs', '100', '1500', '2000', '2026-12-31', '30', 'Main Supplier'];
  exportToCsv('Inventory_Import_Template', headers, [exampleRow]);
};

const handleUpload = async () => {
  if (!selectedFile.value) return;
  const formData = new FormData();
  formData.append('file', selectedFile.value);
  importing.value = true;
  try {
    const res = await api.post('/ExcelImport/import', formData, { headers: { 'Content-Type': 'multipart/form-data' } });
    lastPurchaseOrders.value = res.data?.PurchaseOrderNumbers || res.data?.purchaseOrderNumbers || [];
    addToast(res.data?.Message || res.data?.message || t('inventory.importSuccess'), 'success');
    selectedFile.value = null;
    if (fileInput.value) fileInput.value.value = '';
    emit('refresh');
    await loadHistory();
  } catch (err) {
    addToast(apiError(err, t('inventory.importError')), 'error');
  } finally {
    importing.value = false;
  }
};

const rollbackImport = async (batch) => {
  const batchId = field(batch, 'BatchId');
  if (!window.confirm(`${t('inventory.undoConfirm')}\n\n${field(batch, 'FileName')}`)) return;
  rollingBackId.value = batchId;
  try {
    const res = await api.post(`/ExcelImport/rollback/${batchId}`);
    addToast(res.data?.Message || res.data?.message || t('inventory.undoSuccess'), 'success');
    lastPurchaseOrders.value = [];
    emit('refresh');
    await loadHistory();
  } catch (err) {
    addToast(apiError(err, t('inventory.undoError')), 'error');
  } finally {
    rollingBackId.value = null;
  }
};

onMounted(loadHistory);
</script>

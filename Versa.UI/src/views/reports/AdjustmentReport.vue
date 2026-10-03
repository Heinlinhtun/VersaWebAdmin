<template>
  <div class="space-y-6 animate-in fade-in duration-500 text-left">
    <div
      class="bg-[#3f3f46] p-6 rounded-xl shadow-lg flex justify-between items-center text-white border-b-4 border-amber-500">
      <div>
        <h2 class="text-[10px] font-normal uppercase tracking-[0.2em] text-slate-400 mb-1">{{ tx.title }}</h2>
        <div class="text-3xl font-normal tracking-tight tabular-nums text-white">
          {{ items.length }} <span class="text-sm font-normal opacity-50 uppercase ml-1">{{ tx.quantity }}</span>
        </div>
      </div>
      <div class="text-right bg-white/5 p-3 rounded-lg border border-white/10">
        <div class="text-[9px] font-normal uppercase text-amber-400 tracking-widest mb-1">{{ tx.subtitle }}</div>
        <div class="text-xs font-normal font-mono text-amber-50">{{ tx.date }}</div>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
      <div class="px-6 py-4 border-b border-slate-100 flex justify-between items-center bg-slate-50/50">
        <h3 class="font-normal text-[10px] uppercase text-slate-500 tracking-[0.2em]">{{ tx.title }}</h3>
        <div class="flex items-center gap-4">
          <button @click="handleExportCsv" class="text-[10px] font-normal text-amber-600 uppercase hover:underline">EXPORT CSV</button>
          <div class="flex items-center gap-2">
            <span class="w-2 h-2 bg-amber-500 rounded-full animate-pulse"></span>
            <span class="text-[10px] font-normal text-slate-400 uppercase">Live Review</span>
          </div>
        </div>
      </div>

      <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse">
          <thead class="bg-[#3f3f46] text-white">
            <tr class="text-[10px] font-normal uppercase tracking-[0.2em] text-white">
              <th class="p-4 w-16 text-center opacity-70 text-white">Ref</th>
              <th class="p-4 text-white">{{ tx.date }}</th>
              <th class="p-4 text-white">{{ tx.product }}</th>
              <th class="p-4 text-center text-white">{{ tx.type }}</th>
              <th class="p-4 text-center text-white">{{ tx.quantity }}</th>
              <th class="p-4 text-white">{{ tx.reason }}</th>
              <th class="p-4 px-6 text-white">{{ tx.cashier }}</th>
            </tr>
          </thead>
          <tbody class="text-xs font-normal text-slate-600 divide-y divide-slate-50" v-if="items.length > 0">
            <tr v-for="(i, idx) in paginatedItems" :key="valueOf(i, 'AdjustmentId', 'adjustmentId', idx)" class="hover:bg-amber-50/30 transition-colors group">
              <td class="p-4 text-center text-slate-300 font-mono text-[10px]">
                {{ (currentPage - 1) * itemsPerPage + idx + 1 }}
              </td>
              <td class="p-4 tabular-nums text-slate-500">
                {{ formatDate(valueOf(i, 'AdjustmentDate', 'adjustmentDate')) }}
              </td>
              <td class="p-4 font-normal text-slate-800 text-sm tracking-tight">
                {{ valueOf(i, 'ProductName', 'productName') }}
              </td>
              <td class="p-4 text-center">
                <span :class="i.Type === 'Addition' ? 'bg-emerald-50 text-emerald-600 border-emerald-100' : 'bg-rose-50 text-rose-600 border-rose-100'"
                  class="px-2 py-0.5 rounded text-[10px] uppercase font-normal border">
                  {{ i.Type === 'Addition' ? (isMM ? 'အပေါင်း' : 'Addition') : (isMM ? 'အနှုတ်' : 'Subtraction') }}
                </span>
              </td>
              <td class="p-4 text-center font-normal tabular-nums">
                {{ formatQuantity(valueOf(i, 'Quantity', 'quantity', 0)) }} {{ valueOf(i, 'UnitName', 'unitName') }}
              </td>
              <td class="p-4 text-slate-500 italic max-w-xs truncate">
                {{ valueOf(i, 'Reason', 'reason') }}
              </td>
              <td class="p-4 text-slate-400 text-[10px] uppercase">
                {{ valueOf(i, 'UserName', 'userName', 'System') }} ({{ valueOf(i, 'BranchName', 'branchName', '-') }})
              </td>
            </tr>
          </tbody>
          <tbody v-else>
            <tr>
              <td colspan="7" class="p-24 text-center">
                <div class="flex flex-col items-center opacity-20 grayscale">
                  <span class="text-4xl mb-2">⚖️</span>
                  <p class="text-[10px] font-normal uppercase tracking-[0.3em] text-slate-900">{{ tx.noAdjustments }}</p>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div v-if="totalPages > 1" class="p-4 bg-slate-50 border-t border-slate-200 flex items-center justify-between">
        <p class="text-[9px] font-normal text-slate-400 uppercase tracking-widest">
          Page {{ currentPage }} of {{ totalPages }}
        </p>
        <div class="flex gap-1.5">
          <button @click="currentPage--" :disabled="currentPage === 1"
            class="px-4 py-1.5 bg-white border border-slate-200 rounded-lg font-normal text-[9px] uppercase tracking-widest shadow-sm disabled:opacity-30 hover:bg-slate-50 transition-all">Prev</button>
          <button @click="currentPage++" :disabled="currentPage === totalPages"
            class="px-4 py-1.5 bg-slate-900 text-white rounded-lg font-normal text-[9px] uppercase tracking-widest shadow-lg disabled:opacity-30 hover:bg-black transition-all">Next</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { useQuantitySettings } from '../../composables/useQuantitySettings';
const { formatQuantity } = useQuantitySettings();
import { ref, computed, onMounted } from 'vue';
import api from '../../services/api';
import { useI18n } from '../../composables/useI18n';
import { useCsvExport } from '../../composables/useCsvExport';

const props = defineProps({
  filters: {
    type: Object,
    required: true
  }
});

const { t, locale } = useI18n();
const { exportToCsv } = useCsvExport();
const items = ref([]);
const isMM = computed(() => locale.value === 'mm');

const tx = computed(() => {
  const views = t('reportViews.adjustment');
  return {
    ...views
  };
});

const currentPage = ref(1);
const itemsPerPage = 12;

const paginatedItems = computed(() => {
  const start = (currentPage.value - 1) * itemsPerPage;
  return items.value.slice(start, start + itemsPerPage);
});

const totalPages = computed(() => Math.ceil(items.value.length / itemsPerPage) || 1);

const handleExportCsv = () => {
  const headers = [
    tx.value.date,
    tx.value.product,
    tx.value.type,
    tx.value.quantity,
    tx.value.reason,
    tx.value.cashier
  ];

  const rows = items.value.map(i => [
    formatDate(valueOf(i, 'AdjustmentDate', 'adjustmentDate')),
    valueOf(i, 'ProductName', 'productName'),
    valueOf(i, 'Type', 'type'),
    `${valueOf(i, 'Quantity', 'quantity', 0)} ${valueOf(i, 'UnitName', 'unitName')}`,
    valueOf(i, 'Reason', 'reason'),
    `${valueOf(i, 'UserName', 'userName', 'System')} (${valueOf(i, 'BranchName', 'branchName', '-')})`
  ]);

  const dateStr = new Date().toISOString().split('T')[0];
  exportToCsv(`Inventory_Adjustments_${dateStr}`, headers, rows);
};

const fetchAdjustments = async () => {
  try {
    const res = await api.get('/Inventory/adjustments', {
      params: {
        fromDate: props.filters.fromDate,
        toDate: props.filters.toDate,
        branchId: props.filters.branchId
      }
    });
    items.value = (res.data || []).map(normalizeAdjustmentRow);
    currentPage.value = 1;
  } catch (err) {
    console.error("Adjustment History Error:", err);
  }
};

import { watch } from 'vue';
watch(() => props.filters, fetchAdjustments, { deep: true });

const formatNumber = (val) => new Intl.NumberFormat().format(val || 0);
const formatDate = (date) => date ? new Date(date).toLocaleString('en-US', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '-';
const valueOf = (row, pascalKey, camelKey, fallback = '') => row?.[pascalKey] ?? row?.[camelKey] ?? fallback;
const normalizeAdjustmentRow = (row) => ({
  ...row,
  AdjustmentId: valueOf(row, 'AdjustmentId', 'adjustmentId'),
  AdjustmentDate: valueOf(row, 'AdjustmentDate', 'adjustmentDate'),
  ProductName: valueOf(row, 'ProductName', 'productName'),
  Type: valueOf(row, 'Type', 'type'),
  Quantity: valueOf(row, 'Quantity', 'quantity', 0),
  UnitName: valueOf(row, 'UnitName', 'unitName'),
  Reason: valueOf(row, 'Reason', 'reason'),
  UserName: valueOf(row, 'UserName', 'userName', 'System'),
  BranchName: valueOf(row, 'BranchName', 'branchName', '-')
});

onMounted(fetchAdjustments);
</script>

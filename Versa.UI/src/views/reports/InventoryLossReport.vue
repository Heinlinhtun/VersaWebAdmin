<template>
  <div class="space-y-6 animate-in fade-in duration-500 text-left">
    <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
      <div class="bg-[#3f3f46] p-6 rounded-xl shadow-lg text-white border-b-4 border-rose-500">
        <p class="text-[10px] uppercase tracking-[0.2em] text-slate-400 mb-1">{{ tx.totalLoss }}</p>
        <p class="text-3xl tabular-nums">{{ formatMoney(totalLoss) }} <span class="text-sm opacity-50">Ks</span></p>
      </div>
      <div class="bg-white p-6 rounded-xl border border-slate-200 shadow-sm">
        <p class="text-[10px] uppercase tracking-[0.2em] text-slate-400 mb-1">{{ tx.records }}</p>
        <p class="text-3xl text-slate-900 tabular-nums">{{ items.length }}</p>
        <p class="text-[10px] uppercase tracking-widest text-rose-500 mt-2">{{ tx.subtitle }}</p>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
      <div class="px-6 py-4 border-b border-slate-100 flex justify-between items-center bg-slate-50/50">
        <h3 class="text-[10px] uppercase text-slate-500 tracking-[0.2em]">{{ tx.title }}</h3>
        <button @click="handleExportCsv" class="text-[10px] text-rose-600 uppercase hover:underline">{{ tx.exportCsv }}</button>
      </div>

      <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse">
          <thead class="bg-[#3f3f46] text-white">
            <tr class="text-[10px] uppercase tracking-[0.16em]">
              <th class="p-4">{{ tx.date }}</th>
              <th class="p-4">{{ tx.product }}</th>
              <th class="p-4 text-center">{{ tx.category }}</th>
              <th class="p-4 text-right">{{ tx.quantity }}</th>
              <th class="p-4 text-right">{{ tx.unitCost }}</th>
              <th class="p-4 text-right">{{ tx.lossAmount }}</th>
              <th class="p-4">{{ tx.notes }}</th>
              <th class="p-4">{{ tx.recordedBy }}</th>
            </tr>
          </thead>
          <tbody v-if="paginatedItems.length" class="text-xs text-slate-600 divide-y divide-slate-100">
            <tr v-for="item in paginatedItems" :key="valueOf(item, 'LossId', 'lossId')" class="hover:bg-rose-50/30">
              <td class="p-4 whitespace-nowrap">{{ formatDate(valueOf(item, 'LossDate', 'lossDate')) }}</td>
              <td class="p-4">
                <p class="text-sm text-slate-900">{{ valueOf(item, 'ProductName', 'productName') }}</p>
                <p class="text-[9px] uppercase text-slate-400">{{ valueOf(item, 'Sku', 'sku', '-') }}</p>
              </td>
              <td class="p-4 text-center"><span class="px-2 py-1 rounded bg-rose-50 text-rose-600 border border-rose-100 uppercase text-[9px]">{{ valueOf(item, 'LossReason', 'lossReason') }}</span></td>
              <td class="p-4 text-right tabular-nums">
                {{ formatQty(valueOf(item, 'Quantity', 'quantity', 0)) }} {{ valueOf(item, 'UnitName', 'unitName') }}
                <p v-if="showBaseQuantity(item)" class="text-[9px] text-slate-400">{{ formatQty(valueOf(item, 'BaseQuantity', 'baseQuantity', 0)) }} {{ valueOf(item, 'BaseUnitName', 'baseUnitName') }}</p>
              </td>
              <td class="p-4 text-right tabular-nums">{{ formatMoney(valueOf(item, 'UnitCost', 'unitCost', 0)) }}</td>
              <td class="p-4 text-right tabular-nums font-medium text-rose-600">{{ formatMoney(valueOf(item, 'LossAmount', 'lossAmount', 0)) }}</td>
              <td class="p-4 max-w-xs truncate italic">{{ valueOf(item, 'Notes', 'notes', '-') }}</td>
              <td class="p-4 text-[10px] uppercase">{{ valueOf(item, 'UserName', 'userName', 'System') }}<br><span class="text-slate-400">{{ valueOf(item, 'BranchName', 'branchName', '-') }}</span></td>
            </tr>
          </tbody>
          <tbody v-else>
            <tr><td colspan="8" class="p-20 text-center text-[10px] uppercase tracking-[0.25em] text-slate-400">{{ tx.noLosses }}</td></tr>
          </tbody>
        </table>
      </div>

      <div v-if="totalPages > 1" class="p-4 bg-slate-50 border-t border-slate-200 flex justify-between items-center">
        <span class="text-[9px] text-slate-400 uppercase">{{ currentPage }} / {{ totalPages }}</span>
        <div class="flex gap-2">
          <button @click="currentPage--" :disabled="currentPage === 1" class="px-4 py-1.5 bg-white border rounded-lg text-[9px] uppercase disabled:opacity-30">Prev</button>
          <button @click="currentPage++" :disabled="currentPage === totalPages" class="px-4 py-1.5 bg-slate-900 text-white rounded-lg text-[9px] uppercase disabled:opacity-30">Next</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue';
import api from '../../services/api';
import { useCsvExport } from '../../composables/useCsvExport';
import { useI18n } from '../../composables/useI18n';

const props = defineProps({ filters: { type: Object, required: true } });
const { t } = useI18n();
const { exportToCsv } = useCsvExport();
const items = ref([]);
const currentPage = ref(1);
const itemsPerPage = 12;
const tx = computed(() => t('reportViews.inventoryLoss'));
const valueOf = (row, pascal, camel, fallback = '') => row?.[pascal] ?? row?.[camel] ?? fallback;
const asNumber = (value) => Number(value || 0);
const formatMoney = (value) => asNumber(value).toLocaleString(undefined, { maximumFractionDigits: 2 });
const formatQty = (value) => asNumber(value).toLocaleString(undefined, { maximumFractionDigits: 4 });
const formatDate = (value) => value ? new Date(value).toLocaleString() : '-';
const totalLoss = computed(() => items.value.reduce((sum, item) => sum + asNumber(valueOf(item, 'LossAmount', 'lossAmount', 0)), 0));
const totalPages = computed(() => Math.max(1, Math.ceil(items.value.length / itemsPerPage)));
const paginatedItems = computed(() => items.value.slice((currentPage.value - 1) * itemsPerPage, currentPage.value * itemsPerPage));
const showBaseQuantity = (item) => Math.abs(asNumber(valueOf(item, 'BaseQuantity', 'baseQuantity')) - asNumber(valueOf(item, 'Quantity', 'quantity'))) > 0.0001;

const fetchLosses = async () => {
  try {
    const response = await api.get('/Inventory/losses', { params: props.filters });
    items.value = Array.isArray(response.data) ? response.data : [];
    currentPage.value = 1;
  } catch (error) {
    console.error('Inventory loss report error:', error);
    items.value = [];
  }
};

const handleExportCsv = () => {
  const headers = [tx.value.date, tx.value.product, 'SKU', tx.value.category, tx.value.quantity, tx.value.unitCost, tx.value.lossAmount, tx.value.notes, tx.value.recordedBy];
  const rows = items.value.map(item => [
    formatDate(valueOf(item, 'LossDate', 'lossDate')),
    valueOf(item, 'ProductName', 'productName'),
    valueOf(item, 'Sku', 'sku'),
    valueOf(item, 'LossReason', 'lossReason'),
    `${valueOf(item, 'Quantity', 'quantity', 0)} ${valueOf(item, 'UnitName', 'unitName')}`,
    valueOf(item, 'UnitCost', 'unitCost', 0),
    valueOf(item, 'LossAmount', 'lossAmount', 0),
    valueOf(item, 'Notes', 'notes'),
    `${valueOf(item, 'UserName', 'userName', 'System')} (${valueOf(item, 'BranchName', 'branchName', '-')})`,
  ]);
  exportToCsv(`Inventory_Losses_${new Date().toISOString().split('T')[0]}`, headers, rows);
};

watch(() => props.filters, fetchLosses, { deep: true });
onMounted(fetchLosses);
</script>

<style scoped>
.tabular-nums { font-variant-numeric: tabular-nums; }
</style>

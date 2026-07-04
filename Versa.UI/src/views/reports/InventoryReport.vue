<template>
  <div class="space-y-6 animate-in fade-in duration-500">

    <div
      class="bg-[#3f3f46] p-6 rounded-xl shadow-lg flex justify-between items-center text-white border-b-4 border-emerald-500">
      <div>
        <h2 class="text-[10px] font-normal uppercase tracking-[0.2em] text-slate-400 mb-1">{{ tx.totalAssetValuation }}</h2>
        <div class="text-3xl font-normal tracking-tight tabular-nums text-white">
          {{ formatNumber(totalStockValue) }} <span class="text-sm font-normal opacity-50 uppercase ml-1">MMK</span>
        </div>
      </div>
      <div class="text-right bg-white/5 p-3 rounded-lg border border-white/10">
        <div class="text-[9px] font-normal uppercase text-emerald-400 tracking-widest mb-1">{{ tx.stockStatus }}</div>
        <div class="text-xs font-normal font-mono text-emerald-50">{{ tx.operationalItems }}: {{ inventory.length }}</div>
        <div class="text-xs font-normal font-mono text-emerald-50">{{ tx.totalQty }}: {{ formatNumber(totalStockQty) }}</div>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
      <div class="px-6 py-4 border-b border-slate-100 flex justify-between items-center bg-slate-50/50">
        <h3 class="font-normal text-[10px] uppercase text-slate-500 tracking-[0.2em]">{{ tx.realtimeStockLevels }}</h3>
        <div class="flex items-center gap-4">
          <button @click="handleExportCsv" class="text-[10px] font-normal text-emerald-600 uppercase hover:underline">EXPORT CSV</button>
          <div class="flex items-center gap-2">
            <span class="w-2 h-2 bg-emerald-500 rounded-full animate-pulse"></span>
            <span class="text-[10px] font-normal text-slate-400 uppercase">{{ tx.liveAudit }}</span>
          </div>
        </div>
      </div>

      <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse">
          <thead class="bg-[#3f3f46] text-white">
            <tr class="text-[10px] font-normal uppercase tracking-[0.2em] text-white">
              <th class="p-4 w-16 text-center opacity-70 text-white">Ref</th>
              <th class="p-4 text-white">{{ tx.skuItemName }}</th>
              <th class="p-4 text-center text-white">{{ tx.inStock }}</th>
              <th class="p-4 text-right text-white">{{ tx.avgUnitCost }}</th>
              <th class="p-4 text-right px-6 text-white">{{ tx.inventoryValue }}</th>
            </tr>
          </thead>
          <tbody class="text-xs font-normal text-slate-600 divide-y divide-slate-50" v-if="inventory.length > 0">
            <tr v-for="(i, idx) in paginatedItems" :key="i.ProductId ?? i.productId ?? i.Name ?? i.name" class="hover:bg-slate-50/80 transition-colors group">
              <td class="p-4 text-center text-slate-300 font-mono text-[10px]">
                {{ (currentPage - 1) * itemsPerPage + idx + 1 }}
              </td>
              <td class="p-4">
                <div class="font-normal text-slate-800 text-sm tracking-tight">{{ valueOf(i, 'Name', 'name') }}</div>
                <div class="text-[9px] text-slate-400 font-normal uppercase mt-0.5">{{ valueOf(i, 'SKU', 'sku') || tx.noSku }}</div>
              </td>
              <td class="p-4 text-center">
                <span :class="[
                  'px-3 py-1 rounded-lg font-normal tabular-nums border transition-all duration-500',
                  Number(valueOf(i, 'StockQty', 'stockQty', 0)) < 10
                    ? 'bg-rose-50 text-rose-600 border-rose-100 shadow-[0_0_10px_rgba(225,29,72,0.1)]'
                    : 'bg-slate-50 text-slate-600 border-slate-100'
                ]">
                  {{ formatNumber(valueOf(i, 'StockQty', 'stockQty', 0)) }}
                </span>
              </td>
              <td class="p-4 text-right tabular-nums text-slate-500">
                {{ formatNumber(valueOf(i, 'UnitCost', 'unitCost', 0)) }} <small class="opacity-50">Ks</small>
              </td>
              <td class="p-4 text-right font-normal text-slate-900 tabular-nums bg-slate-50/30">
                {{ formatNumber(valueOf(i, 'TotalValue', 'totalValue', 0)) }} <small class="text-[10px] opacity-40">Ks</small>
              </td>
            </tr>
          </tbody>
          <tbody v-else>
            <tr>
              <td colspan="5" class="p-24 text-center">
                <div class="flex flex-col items-center opacity-20 grayscale">
                  <span class="text-4xl mb-2">📊</span>
                  <p class="text-[10px] font-normal uppercase tracking-[0.3em] text-slate-900">{{ tx.awaitingInventoryData }}</p>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div v-if="totalPages > 1" class="p-4 bg-slate-50 border-t border-slate-200 flex items-center justify-between">
        <p class="text-[9px] font-normal text-slate-400 uppercase tracking-widest">
          {{ tx.page }} {{ currentPage }} {{ tx.of }} {{ totalPages }}
        </p>
        <div class="flex gap-1.5">
          <button @click="currentPage--" :disabled="currentPage === 1"
            class="px-4 py-1.5 bg-white border border-slate-200 rounded-lg font-normal text-[9px] uppercase tracking-widest shadow-sm disabled:opacity-30 hover:bg-slate-50 transition-all">{{ tx.prev }}</button>
          <button @click="currentPage++" :disabled="currentPage === totalPages"
            class="px-4 py-1.5 bg-slate-900 text-white rounded-lg font-normal text-[9px] uppercase tracking-widest shadow-lg disabled:opacity-30 hover:bg-black transition-all">{{ tx.next }}</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue';
import api from '../../services/api';
import { useI18n } from '../../composables/useI18n';
import { useCsvExport } from '../../composables/useCsvExport';

const props = defineProps({
  filters: {
    type: Object,
    required: true
  }
});

const { t } = useI18n();
const { exportToCsv } = useCsvExport();
const inventory = ref([]);
const totalStockValue = ref(0);
const totalStockQty = ref(0);
const tx = computed(() => ({
  totalAssetValuation: t('reportViews.inventory.totalAssetValuation'),
  stockStatus: t('reportViews.inventory.stockStatus'),
  operationalItems: t('reportViews.inventory.operationalItems'),
  totalQty: t('reportViews.inventory.totalQty') || 'Total Qty',
  realtimeStockLevels: t('reportViews.inventory.realtimeStockLevels'),
  liveAudit: t('reportViews.inventory.liveAudit'),
  skuItemName: t('reportViews.inventory.skuItemName'),
  inStock: t('reportViews.inventory.inStock'),
  avgUnitCost: t('reportViews.inventory.avgUnitCost'),
  inventoryValue: t('reportViews.inventory.inventoryValue'),
  noSku: t('reportViews.inventory.noSku'),
  awaitingInventoryData: t('reportViews.inventory.awaitingInventoryData'),
  page: t('reportViews.inventory.page'),
  of: t('reportViews.inventory.of'),
  prev: t('reportViews.inventory.prev'),
  next: t('reportViews.inventory.next')
}));

const currentPage = ref(1);
const itemsPerPage = 12; // Increased for better full-page density

const paginatedItems = computed(() => {
  const start = (currentPage.value - 1) * itemsPerPage;
  return inventory.value.slice(start, start + itemsPerPage);
});

const totalPages = computed(() => Math.ceil(inventory.value.length / itemsPerPage) || 1);

const handleExportCsv = () => {
  const headers = [
    t('reportViews.inventory.skuItemName'),
    t('reportViews.inventory.inStock'),
    t('reportViews.inventory.avgUnitCost'),
    t('reportViews.inventory.inventoryValue')
  ];

  const rows = inventory.value.map(i => [
    valueOf(i, 'Name', 'name'),
    valueOf(i, 'StockQty', 'stockQty', 0),
    valueOf(i, 'UnitCost', 'unitCost', 0),
    valueOf(i, 'TotalValue', 'totalValue', 0)
  ]);

  const dateStr = new Date().toISOString().split('T')[0];
  exportToCsv(`Inventory_Valuation_${dateStr}`, headers, rows);
};

const fetchInventoryValue = async () => {
  try {
    const res = await api.get('/Reports/inventory-value', {
      params: {
        branchId: props.filters.branchId
      }
    });
    inventory.value = res.data.items || res.data.Items || [];
    totalStockValue.value = res.data.totalStockValue ?? res.data.TotalStockValue ?? 0;
    totalStockQty.value = res.data.totalStockQty ?? res.data.TotalStockQty ?? inventory.value.reduce((sum, item) => sum + Number(valueOf(item, 'StockQty', 'stockQty', 0)), 0);
    currentPage.value = 1;
  } catch (err) {
    console.error("Valuation Error:", err);
  }
};

const formatNumber = (val) => new Intl.NumberFormat().format(val || 0);
const valueOf = (row, pascalKey, camelKey, fallback = '') => row?.[pascalKey] ?? row?.[camelKey] ?? fallback;

onMounted(fetchInventoryValue);
watch(() => props.filters, fetchInventoryValue, { deep: true });
</script>

<style scoped>
.tabular-nums {
  font-variant-numeric: tabular-nums;
}
</style>

<template>
  <div class="px-3 sm:px-5 lg:px-8 pt-2 pb-6 sm:pb-8 bg-gray-50 min-h-screen font-sans">
    <div class="mx-auto">
      <div class="flex flex-col md:flex-row justify-between items-start md:items-center mb-8 gap-4">
        <div>
          <h1 class="text-2xl font-normal text-gray-800 tracking-tight">{{ tx.title }}</h1>
          <div class="flex items-center gap-3 mt-1">
            <p class="text-sm text-gray-500 flex items-center font-normal">
              <span class="w-2 h-2 bg-blue-600 rounded-full mr-2 shadow-sm"></span>
              {{ tx.branch }}: {{ userRole === 'Admin' ? (selectedBranchName || tx.allBranches) : userBranchName }}
            </p>
            <button @click="handleExportCsv" class="text-[10px] font-normal text-blue-600 uppercase hover:underline mr-3">EXPORT CSV</button>
            <button v-if="canManageImports" @click="showImportModal = true" class="text-[10px] font-normal text-emerald-600 uppercase hover:underline">{{ t('inventory.importExcel') }}</button>
          </div>
        </div>

        <div class="flex flex-wrap items-center gap-3 w-full md:w-auto">
          <div class="flex bg-gray-200/60 p-1 rounded-xl border border-gray-200 shadow-inner">
            <button v-for="mode in ['all', 'low', 'out']" :key="mode" @click="filterStatus = mode"
              :class="filterStatus === mode ? 'bg-white shadow-sm text-gray-900 border-gray-100' : 'text-gray-500 hover:text-gray-700'"
              class="px-5 py-1.5 rounded-lg text-[11px] font-normal uppercase transition-all duration-200 tracking-wider border border-transparent">
              {{ mode === 'all' ? tx.showAll : mode === 'low' ? tx.lowStock : tx.outOfStock }}
            </button>
          </div>

          <div ref="supplierPickerRef" class="relative w-full sm:min-w-[160px]">
            <input v-model="supplierSearch" @input="handleSupplierSearch" @focus="openSupplierResults"
              @keydown.down.prevent="moveSupplierDown" @keydown.up.prevent="moveSupplierUp"
              @keydown.enter.prevent="selectHighlightedSupplier" type="text" :placeholder="tx.allSuppliers"
              class="w-full pl-10 pr-8 py-2 text-xs border border-gray-200 rounded-xl font-normal bg-white outline-none focus:ring-2 focus:ring-blue-500/20 shadow-sm text-gray-700" />
            <span class="absolute left-4 top-2.5 opacity-30 text-xs">
              <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
              </svg>
            </span>
            <button v-if="supplierSearch" @click="clearSupplierSearch" type="button"
              class="absolute right-3 top-2 text-slate-300 hover:text-slate-500 text-xs">x</button>
            <div v-if="showSupplierResults && filteredSuppliers.length > 0"
              class="absolute z-[100] left-0 right-0 mt-1 bg-white border border-slate-200 rounded-xl shadow-2xl max-h-64 overflow-y-auto animate-in fade-in slide-in-from-top-2 duration-200">
              <div v-for="(s, index) in filteredSuppliers" :key="s.supplierId || s.SupplierId" @click="selectSupplier(s)"
                :class="['p-3 cursor-pointer border-b border-slate-50 last:border-0 hover:bg-blue-50 transition-colors',
                  supplierHighlightedIndex === index ? 'bg-blue-50' : '']">
                <p class="text-xs font-normal text-slate-800">{{ s.Name || s.SupplierName || s.supplierName }}</p>
                <p class="text-[9px] text-slate-400 font-normal uppercase tracking-tighter">
                  {{ s.Phone || s.phone || tx.allSuppliers }}
                </p>
              </div>
            </div>
          </div>

          <div v-if="userRole === 'Admin'" class="relative w-full sm:min-w-[160px]">
            <select v-model="branchId" @change="onBranchChange"
              class="w-full appearance-none px-4 py-2 text-xs border border-gray-200 rounded-xl font-normal bg-white outline-none focus:ring-2 focus:ring-blue-500/20 shadow-sm text-gray-700 cursor-pointer">
              <option :value="null">{{ tx.allBranches }}</option>
              <option v-for="b in branches" :key="b.branchId" :value="b.branchId">{{ b.branchName }}</option>
            </select>
          </div>

          <div class="relative w-full sm:min-w-[260px]">
            <input v-model="searchQuery" type="text" :placeholder="tx.searchPlaceholder"
              class="w-full pl-10 pr-4 py-2 border border-gray-200 rounded-xl focus:ring-2 focus:ring-blue-500/20 outline-none text-xs font-normal placeholder-gray-400 shadow-sm" />
            <span class="absolute left-4 top-2.5 opacity-30 text-xs"> <svg class="h-4 w-4" fill="none"
                stroke="currentColor" viewBox="0 0 24 24">
                <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
              </svg></span>
          </div>
        </div>
      </div>

      <div class="bg-white rounded-xl shadow-sm border border-gray-200 table-responsive">
        <table class="w-full text-left border-collapse">
          <thead class="bg-[#3f3f46] font-normal uppercase text-[10px] text-white tracking-[0.2em]">
            <tr>
              <th class="p-5 w-16 text-center">{{ tx.no }}</th>
              <th class="p-5">{{ tx.productSku }}</th>
              <th class="p-5 text-center">{{ tx.currentStock }}</th>
              <th class="p-5 text-center">{{ tx.owned }}</th>
              <th class="p-5 text-center">{{ tx.consignment }}</th>
              <th class="p-5 text-right">{{ tx.avgCostWac }}</th>
              <th class="p-5 text-right">{{ tx.latestPurchase }}</th>
              <th class="p-5 text-right">{{ tx.retailPrice }}</th>
              <th class="p-5 text-center">{{ tx.inventoryStatus }}</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-100">
            <tr v-for="(s, index) in paginatedStocks" :key="s.ProductId"
              class="hover:bg-gray-50/80 transition-colors group">
              <td class="p-5 text-center font-normal text-gray-400 text-xs tabular-nums">
                {{ (currentPage - 1) * pageSize + index + 1 }}
              </td>
              <td class="p-5">
                <div class="font-normal text-gray-800 text-sm tracking-tight group-hover:text-blue-700 transition-colors">
                  {{ s.ProductName }}
                </div>
                <div class="text-[10px] font-normal text-gray-400 mt-1 flex flex-wrap items-center gap-2">
                  <span class="bg-blue-50 text-blue-600 px-1.5 py-0.5 rounded tracking-wide uppercase border border-blue-100 font-mono text-[9px]">{{ s.SKU || tx.noSku }}</span>
                  <span class="bg-gray-100 px-1.5 py-0.5 rounded tracking-wide uppercase">{{ tx.minAlert }}: {{ s.AlertQuantity || 0 }}</span>
                </div>
              </td>
              <td class="p-5 text-center">
                <div :class="Number(s.BaseQuantity) <= Number(s.AlertQuantity) ? 'text-rose-600' : 'text-blue-600'"
                  class="text-lg font-normal tabular-nums tracking-tighter">
                  {{ formatNumber(s.BaseQuantity) }}
                </div>
                <div class="text-[9px] text-gray-400 font-normal uppercase tracking-widest mt-0.5">
                  {{ s.BaseUnitName || tx.pcs }}
                </div>
              </td>
              <td class="p-5 text-center font-normal text-slate-700 tabular-nums text-sm">
                {{ formatNumber(s.OwnedQuantity ?? s.ownedQuantity ?? 0) }}
              </td>
              <td class="p-5 text-center font-normal text-indigo-600 tabular-nums text-sm">
                {{ formatNumber(s.ConsignmentQuantity ?? s.consignmentQuantity ?? 0) }}
              </td>
              <td class="p-5 text-right font-normal text-slate-700 tabular-nums text-sm">{{ formatNumber(s.CostPrice) }}
              </td>
              <td class="p-5 text-right bg-blue-50/20 font-normal text-blue-600 tabular-nums text-sm">
                {{ s.LastPurchasePrice > 0 ? formatNumber(s.LastPurchasePrice) : tx.emptyValue }}
              </td>
              <td class="p-5 text-right text-gray-900 font-normal tabular-nums text-sm">{{ formatNumber(s.SalePrice) }}
              </td>
              <td class="p-5">
                <div class="flex justify-center">
                  <span :class="stockStatusClass(s)"
                    class="px-4 py-1.5 rounded-lg text-[10px] font-normal uppercase tracking-wider border shadow-sm">
                    {{ stockStatusText(s) }}
                  </span>
                </div>
              </td>
            </tr>

            <tr v-if="filteredStocks.length === 0">
              <td colspan="9" class="p-24 text-center">
                <div class="text-xs font-normal text-gray-400 uppercase tracking-[0.2em]">{{ tx.noRecords }}</div>
              </td>
            </tr>
          </tbody>
        </table>

        <div class="flex justify-between items-center px-6 py-4 bg-slate-50 border-t border-slate-200">
          <div class="text-[10px] font-normal text-slate-400 uppercase tracking-widest">
            {{ tx.title }}: {{ filteredStocks.length }}
          </div>
          <div class="flex items-center gap-1.5">
            <button @click="currentPage--" :disabled="currentPage === 1"
              class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">{{
                t('common.prev') }}</button>
            <span class="px-3 text-xs font-normal text-slate-400">{{ t('common.page') }} {{ currentPage }} / {{ totalPages
              }}</span>
            <button @click="currentPage++" :disabled="currentPage === totalPages"
              class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">{{
                t('common.next') }}</button>
          </div>
        </div>
      </div>
    </div>
    <ExcelImportModal v-if="showImportModal" @close="showImportModal = false" @refresh="fetchStocks" />
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, watch } from 'vue';
import api from '../services/api';
import { useToast } from '../composables/useToast';
import { useI18n } from '../composables/useI18n';
import { useCsvExport } from '../composables/useCsvExport';
import ExcelImportModal from '../components/ExcelImportModal.vue';
const { addToast } = useToast();
const { t } = useI18n();
const { exportToCsv } = useCsvExport();

const stocks = ref([]);
const branches = ref([]);
const searchQuery = ref('');
const filterStatus = ref('all');
const showImportModal = ref(false);

// ✅ Pagination State
const currentPage = ref(1);
const pageSize = ref(15);

const userRole = ref(localStorage.getItem('Role') || 'Staff');
const canManageImports = computed(() => ['admin', 'manager'].includes(userRole.value.toLowerCase()));
const userBranchId = ref(parseInt(localStorage.getItem('BranchId')) || 0);
const userBranchName = ref(localStorage.getItem('BranchName') || 'Main Shop');

const suppliers = ref([]);
const selectedSupplierId = ref(null);
const supplierSearch = ref('');
const filteredSuppliers = ref([]);
const showSupplierResults = ref(false);
const supplierHighlightedIndex = ref(-1);
const supplierPickerRef = ref(null);
const branchId = ref(userRole.value === 'Admin' ? null : userBranchId.value);
const selectedBranchName = ref('');
const tx = computed(() => ({
  title: t('inventory.title'),
  branch: t('inventory.branch'),
  allBranches: t('inventory.allBranches'),
  showAll: t('inventory.showAll'),
  lowStock: t('inventory.lowStock'),
  outOfStock: t('inventory.outOfStock'),
  allSuppliers: t('inventory.allSuppliers'),
  searchPlaceholder: t('inventory.searchPlaceholder'),
  currentStock: t('inventory.currentStock'),
  owned: t('inventory.owned'),
  consignment: t('inventory.consignment'),
  no: t('inventory.no'),
  productSku: t('inventory.productSku'),
  avgCostWac: t('inventory.avgCostWac'),
  latestPurchase: t('inventory.latestPurchase'),
  retailPrice: t('inventory.retailPrice'),
  inventoryStatus: t('inventory.inventoryStatus'),
  minAlert: t('inventory.minAlert'),
  pcs: t('inventory.pcs'),
  emptyValue: t('inventory.emptyValue'),
  noRecords: t('inventory.noRecords'),
  total: t('inventory.total'),
  items: t('inventory.items'),
  page: t('inventory.page'),
  of: t('inventory.of'),
  prev: t('inventory.prev'),
  next: t('inventory.next'),
  fetchFailed: t('inventory.fetchFailed'),
  outOfStockText: t('inventory.outOfStockText'),
  lowStockAlert: t('inventory.lowStockAlert'),
  healthyStock: t('inventory.healthyStock'),
  noSku: t('inventory.noSku'),
}));

// Logic
const fetchSuppliers = async () => {
  try {
    const res = await api.get('/Suppliers');
    suppliers.value = (Array.isArray(res.data) ? res.data : []).map(s => ({
      ...s,
      supplierId: s.supplierId ?? s.SupplierId,
      supplierName: s.supplierName ?? s.SupplierName ?? s.Name,
      Phone: s.Phone ?? s.phone ?? ''
    }));
  } catch (err) { console.error(err); }
};

const supplierMatches = (supplier, query) => {
  const name = supplier.supplierName || supplier.SupplierName || supplier.Name || '';
  const phone = supplier.Phone || supplier.phone || '';
  return name.toLowerCase().includes(query) || String(phone).toLowerCase().includes(query);
};

const openSupplierResults = () => {
  const query = supplierSearch.value.toLowerCase().trim();
  filteredSuppliers.value = (query ? suppliers.value.filter(s => supplierMatches(s, query)) : suppliers.value).slice(0, 50);
  showSupplierResults.value = filteredSuppliers.value.length > 0;
  supplierHighlightedIndex.value = filteredSuppliers.value.length > 0 ? 0 : -1;
};

const closeSupplierResults = () => {
  showSupplierResults.value = false;
  supplierHighlightedIndex.value = -1;
};

const handleSupplierOutsideClick = (event) => {
  if (!supplierPickerRef.value?.contains(event.target)) {
    closeSupplierResults();
  }
};

const handleSupplierSearch = () => {
  selectedSupplierId.value = null;
  const query = supplierSearch.value.toLowerCase().trim();
  filteredSuppliers.value = (query ? suppliers.value.filter(s => supplierMatches(s, query)) : suppliers.value).slice(0, 50);
  showSupplierResults.value = filteredSuppliers.value.length > 0;
  supplierHighlightedIndex.value = filteredSuppliers.value.length > 0 ? 0 : -1;
  fetchStocks();
};

const selectSupplier = (supplier) => {
  selectedSupplierId.value = supplier.supplierId ?? supplier.SupplierId;
  supplierSearch.value = supplier.supplierName || supplier.SupplierName || supplier.Name || '';
  filteredSuppliers.value = [];
  showSupplierResults.value = false;
  supplierHighlightedIndex.value = -1;
  fetchStocks();
};

const clearSupplierSearch = () => {
  supplierSearch.value = '';
  selectedSupplierId.value = null;
  filteredSuppliers.value = [];
  showSupplierResults.value = false;
  supplierHighlightedIndex.value = -1;
  fetchStocks();
};

const moveSupplierDown = () => {
  if (supplierHighlightedIndex.value < filteredSuppliers.value.length - 1) supplierHighlightedIndex.value++;
};

const moveSupplierUp = () => {
  if (supplierHighlightedIndex.value > 0) supplierHighlightedIndex.value--;
};

const selectHighlightedSupplier = () => {
  if (supplierHighlightedIndex.value >= 0 && supplierHighlightedIndex.value < filteredSuppliers.value.length) {
    selectSupplier(filteredSuppliers.value[supplierHighlightedIndex.value]);
  }
};

const fetchBranches = async () => {
  if (userRole.value === 'Admin') {
    try {
      const res = await api.get('/Branches');
      branches.value = res.data;
    } catch (err) { console.error(err); }
  }
};

const fetchStocks = async () => {
  try {
    const url = branchId.value === null ? '/inventory/all' : `/inventory/branch/${branchId.value}`;
    const res = await api.get(url, { params: { supplierId: selectedSupplierId.value } });
    stocks.value = Array.isArray(res.data) ? res.data : [];
  } catch (err) {
    addToast(tx.value.fetchFailed, "error");
  }
};

const handleExportCsv = () => {
  const headers = [
    tx.value.no,
    tx.value.productSku,
    tx.value.currentStock,
    tx.value.owned,
    tx.value.consignment,
    tx.value.avgCostWac,
    tx.value.latestPurchase,
    tx.value.retailPrice,
    tx.value.inventoryStatus
  ];

  const rows = filteredStocks.value.map((s, index) => [
    index + 1,
    s.ProductName,
    `${s.BaseQuantity} ${s.BaseUnitName || tx.value.pcs}`,
    s.OwnedQuantity ?? s.ownedQuantity ?? 0,
    s.ConsignmentQuantity ?? s.consignmentQuantity ?? 0,
    s.CostPrice,
    s.LastPurchasePrice,
    s.SalePrice,
    stockStatusText(s)
  ]);

  const dateStr = new Date().toISOString().split('T')[0];
  exportToCsv(`Inventory_List_${dateStr}`, headers, rows);
};

const onBranchChange = () => {
  const b = branches.value.find(x => x.branchId === branchId.value);
  selectedBranchName.value = b ? b.branchName : tx.value.allBranches;
  fetchStocks();
};

const filteredStocks = computed(() => {
  return stocks.value.filter(s => {
    const qty = Number(s.BaseQuantity || 0);
    const alertQty = Number(s.AlertQuantity || 0);
    const q = (searchQuery.value || '').toLowerCase();
    const name = (s.ProductName || '').toLowerCase();
    const sku = String(s.SKU ?? s.sku ?? '').toLowerCase();
    const matchesSearch = !q || name.includes(q) || sku.includes(q);

    let matchesStatus = true;
    if (filterStatus.value === 'low') matchesStatus = qty > 0 && qty <= alertQty;
    if (filterStatus.value === 'out') matchesStatus = qty <= 0;

    return matchesSearch && matchesStatus;
  });
});

// ✅ Pagination Logic
const totalPages = computed(() => Math.ceil(filteredStocks.value.length / pageSize.value) || 1);

const paginatedStocks = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value;
  return filteredStocks.value.slice(start, start + pageSize.value);
});

// ✅ Filter ပြောင်းရင် Page 1 ကို ပြန်သွားဖို့ Watch လုပ်ပါ
watch([searchQuery, filterStatus, selectedSupplierId, branchId], () => {
  currentPage.value = 1;
});

const stockStatusText = (item) => {
  const q = Number(item.BaseQuantity) || 0;
  const alert = Number(item.AlertQuantity) || 0;
  if (q <= 0) return tx.value.outOfStockText;
  if (q <= alert) return tx.value.lowStockAlert;
  return tx.value.healthyStock;
};

const stockStatusClass = (item) => {
  const q = Number(item.BaseQuantity) || 0;
  const alert = Number(item.AlertQuantity) || 0;
  if (q <= 0) return 'bg-rose-50 text-rose-600 border-rose-100';
  if (q <= alert) return 'bg-amber-50 text-amber-600 border-amber-100';
  return 'bg-emerald-50 text-emerald-600 border-emerald-100';
};

const formatNumber = (val) => new Intl.NumberFormat().format(val || 0);

onMounted(() => {
  document.addEventListener('mousedown', handleSupplierOutsideClick);
  document.addEventListener('touchstart', handleSupplierOutsideClick);
  fetchBranches();
  fetchSuppliers();
  fetchStocks();
});

onUnmounted(() => {
  document.removeEventListener('mousedown', handleSupplierOutsideClick);
  document.removeEventListener('touchstart', handleSupplierOutsideClick);
});
</script>

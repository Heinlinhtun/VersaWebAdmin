<template>
  <div class="px-3 sm:px-5 lg:px-8 pt-2 pb-8 bg-gray-50 min-h-screen font-sans">
    <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 mb-6">
      <div>
        <h1 class="text-2xl font-normal text-slate-800 tracking-tight">{{ tx.title }}</h1>
        <p class="text-sm text-slate-500 mt-1">{{ tx.subtitle }}</p>
      </div>
      <div class="flex flex-col sm:flex-row sm:items-center gap-3 w-full md:w-auto md:min-w-[460px]">
        <div ref="supplierPickerRef" class="relative w-full sm:w-64">
          <input v-model="supplierSearch" @input="handleSupplierSearch" @focus="openSupplierResults"
            @keydown.down.prevent="moveSupplierDown" @keydown.up.prevent="moveSupplierUp"
            @keydown.enter.prevent="selectHighlightedSupplier" type="text" :placeholder="tx.allSuppliers"
            class="w-full pl-10 pr-8 py-2 text-xs border border-gray-200 rounded-xl bg-white outline-none focus:ring-2 focus:ring-indigo-500/20 shadow-sm text-slate-700" />
          <span class="absolute left-4 top-2.5 text-slate-300">
            <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
            </svg>
          </span>
          <button v-if="supplierSearch" @click="clearSupplierSearch" type="button"
            class="absolute right-3 top-2 text-slate-300 hover:text-slate-500 text-xs">x</button>
          <div v-if="showSupplierResults && filteredSuppliers.length > 0"
            class="absolute z-[100] left-0 right-0 mt-1 bg-white border border-slate-200 rounded-xl shadow-2xl max-h-64 overflow-y-auto animate-in fade-in slide-in-from-top-2 duration-200">
            <div v-for="(s, index) in filteredSuppliers" :key="s.supplierId || s.SupplierId" @click="selectSupplier(s)"
              :class="['p-3 cursor-pointer border-b border-slate-50 last:border-0 hover:bg-indigo-50 transition-colors',
                supplierHighlightedIndex === index ? 'bg-indigo-50' : '']">
              <p class="text-xs font-normal text-slate-800">{{ supplierName(s) }}</p>
              <p class="text-[9px] text-slate-400 font-normal uppercase tracking-tighter">
                {{ s.Phone || s.phone || tx.supplier }}
              </p>
            </div>
          </div>
        </div>
        <button @click="settleAll"
          :disabled="isOffline"
          class="w-full sm:w-auto sm:ml-auto px-5 py-2 bg-indigo-600 text-white rounded-xl text-xs uppercase tracking-widest shadow-sm hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed">
          {{ tx.payoutSoldItems }}
        </button>
        <button @click="handleExportCsv"
          class="w-full sm:w-auto px-5 py-2 bg-white border border-slate-200 text-indigo-600 rounded-xl text-xs uppercase tracking-widest shadow-sm hover:bg-indigo-50">
          EXPORT CSV
        </button>
      </div>
    </div>
    <div v-if="isOffline" class="mb-6 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
      {{ tx.onlineOnlyNotice }}
    </div>

    <div class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
      <div class="bg-white border border-slate-200 rounded-xl p-5">
        <p class="text-[10px] uppercase tracking-widest text-slate-400">{{ tx.available }}</p>
        <p class="text-2xl font-normal text-blue-600 tabular-nums">{{ formatNumber(summary.available) }}</p>
      </div>
      <div class="bg-white border border-slate-200 rounded-xl p-5">
        <p class="text-[10px] uppercase tracking-widest text-slate-400">{{ tx.sold }}</p>
        <p class="text-2xl font-normal text-emerald-600 tabular-nums">{{ formatNumber(summary.sold) }}</p>
      </div>
      <div class="bg-white border border-slate-200 rounded-xl p-5">
        <p class="text-[10px] uppercase tracking-widest text-slate-400">{{ tx.unsettledQty }}</p>
        <p class="text-2xl font-normal text-indigo-600 tabular-nums">{{ formatQuantity(summary.unsettledQty) }}</p>
      </div>
      <div class="bg-white border border-slate-200 rounded-xl p-5">
        <p class="text-[10px] uppercase tracking-widest text-slate-400">{{ tx.payoutDue }}</p>
        <p class="text-2xl font-normal text-rose-600 tabular-nums">{{ formatNumber(summary.unsettledAmount) }}</p>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-200 overflow-x-auto">
      <table class="w-full text-sm">
        <thead class="bg-[#3f3f46] text-white uppercase text-[10px] tracking-[0.18em]">
          <tr>
            <th class="p-4 text-left">{{ tx.product }}</th>
            <th class="p-4 text-left">{{ tx.supplier }}</th>
            <th class="p-4 text-center">{{ tx.qty }}</th>
            <th class="p-4 text-center">{{ tx.available }}</th>
            <th class="p-4 text-center">{{ tx.sold }}</th>
            <th class="p-4 text-center">{{ tx.returned }}</th>
            <th class="p-4 text-center">{{ tx.unsettled }}</th>
            <th class="p-4 text-right">{{ tx.cost }}</th>
            <th class="p-4 text-right">{{ tx.payoutDue }}</th>
            <th class="p-4 text-center">{{ tx.action }}</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-slate-100">
          <tr v-for="item in paginatedInventory" :key="item.ConsignItemId || item.consignItemId" class="hover:bg-slate-50">
            <td class="p-4">
              <div class="font-normal text-slate-800">{{ item.ProductName || item.productName }}</div>
              <div class="text-[10px] text-slate-400 uppercase">{{ item.Sku || item.sku || tx.noSku }}</div>
            </td>
            <td class="p-4 text-slate-600">{{ item.SupplierName || item.supplierName }}</td>
            <td class="p-4 text-center tabular-nums">{{ formatQuantity(item.Quantity || item.quantity) }}</td>
            <td class="p-4 text-center text-blue-600 tabular-nums">{{ formatQuantity(item.AvailableQuantity || item.availableQuantity) }}</td>
            <td class="p-4 text-center text-emerald-600 tabular-nums">{{ formatQuantity(item.SoldQuantity || item.soldQuantity) }}</td>
            <td class="p-4 text-center text-slate-500 tabular-nums">{{ formatQuantity(item.ReturnedQuantity || item.returnedQuantity) }}</td>
            <td class="p-4 text-center text-indigo-600 tabular-nums">{{ formatQuantity(item.UnsettledQuantity || item.unsettledQuantity) }}</td>
            <td class="p-4 text-right tabular-nums">{{ formatNumber(item.UnitPrice || item.unitPrice) }}</td>
            <td class="p-4 text-right text-rose-600 tabular-nums">{{ formatNumber(item.UnsettledAmount || item.unsettledAmount) }}</td>
            <td class="p-4 text-center">
              <button @click="openReturnModal(item)"
                :disabled="isOffline || Number(item.AvailableQuantity ?? item.availableQuantity ?? 0) <= 0"
                class="px-3 py-1.5 rounded-lg border border-rose-200 text-[10px] uppercase tracking-widest text-rose-600 hover:bg-rose-50 disabled:opacity-40 disabled:cursor-not-allowed">
                {{ tx.return }}
              </button>
            </td>
          </tr>
          <tr v-if="inventory.length === 0">
            <td colspan="10" class="p-16 text-center text-xs text-slate-400 uppercase tracking-widest">{{ tx.noRecords }}</td>
          </tr>
        </tbody>
      </table>

      <div v-if="inventory.length > 0" class="flex justify-between items-center px-6 py-4 bg-slate-50 border-t border-slate-200">
        <div class="text-[10px] font-normal text-slate-400 uppercase tracking-widest">
          {{ tx.title }}: {{ inventory.length }}
        </div>
        <div class="flex items-center gap-1.5">
          <button @click="currentPage--" :disabled="currentPage === 1"
            class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">
            {{ tx.prev }}
          </button>
          <span class="px-3 text-xs font-normal text-slate-400">{{ tx.page }} {{ currentPage }} / {{ totalPages }}</span>
          <button @click="currentPage++" :disabled="currentPage === totalPages"
            class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">
            {{ tx.next }}
          </button>
        </div>
      </div>
    </div>

    <div v-if="showReturnModal" class="fixed inset-0 z-[120] bg-slate-950/50 backdrop-blur-sm flex items-center justify-center p-4">
      <div class="bg-white w-full max-w-md rounded-2xl shadow-2xl border border-slate-200 overflow-hidden">
        <div class="bg-[#3f3f46] px-5 py-4 text-white flex items-center justify-between">
          <h3 class="text-xs uppercase tracking-widest">{{ tx.returnConsignment }}</h3>
          <button @click="closeReturnModal" class="text-white/70 hover:text-white text-xl leading-none">x</button>
        </div>
        <div class="p-5 space-y-4">
          <div>
            <p class="text-sm font-normal text-slate-800">{{ returnItem?.ProductName || returnItem?.productName }}</p>
            <p class="text-[10px] uppercase tracking-widest text-slate-400">
              {{ tx.available }}: {{ formatQuantity(returnAvailableQty) }}
            </p>
          </div>
          <div class="space-y-1.5">
            <label class="text-[10px] uppercase tracking-widest text-slate-400">{{ tx.returnQty }}</label>
            <input v-quantity v-model.number="returnForm.quantity" type="number" min="0" :max="returnAvailableQty"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-rose-500/20 text-sm" />
          </div>
          <div class="space-y-1.5">
            <label class="text-[10px] uppercase tracking-widest text-slate-400">{{ tx.reason }}</label>
            <textarea v-model="returnForm.reason" rows="3"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-rose-500/20 text-sm resize-none"></textarea>
          </div>
          <div class="flex gap-3 pt-2">
            <button @click="closeReturnModal"
              class="flex-1 py-3 rounded-xl bg-slate-100 text-slate-600 text-xs uppercase tracking-widest">
              {{ tx.cancel }}
            </button>
            <button @click="submitReturn"
              class="flex-1 py-3 rounded-xl bg-rose-600 text-white text-xs uppercase tracking-widest hover:bg-rose-700">
              {{ tx.confirmReturn }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { useQuantitySettings } from '../composables/useQuantitySettings';
const { formatQuantity } = useQuantitySettings();
import { computed, onMounted, onUnmounted, ref } from 'vue';
import api from '../services/api';
import { useToast } from '../composables/useToast';
import androidBridge from '../services/androidBridge';
import { useI18n } from '../composables/useI18n';
import { useCsvExport } from '../composables/useCsvExport';

const { addToast } = useToast();
const { t } = useI18n();
const { exportToCsv } = useCsvExport();
const suppliers = ref([]);
const inventory = ref([]);
const selectedSupplierId = ref(null);
const supplierSearch = ref('');
const filteredSuppliers = ref([]);
const showSupplierResults = ref(false);
const supplierHighlightedIndex = ref(-1);
const supplierPickerRef = ref(null);
const isOffline = ref(false);
const currentPage = ref(1);
const pageSize = 30;
const showReturnModal = ref(false);
const returnItem = ref(null);
const returnForm = ref({ quantity: 0, reason: '' });
const branchId = Number(localStorage.getItem('BranchId') || 0) || null;
const role = localStorage.getItem('Role') || 'Staff';

const tx = computed(() => ({
  title: t('consignments.title'),
  subtitle: t('consignments.subtitle'),
  allSuppliers: t('consignments.allSuppliers'),
  payoutSoldItems: t('consignments.payoutSoldItems'),
  onlineOnlyNotice: t('consignments.onlineOnlyNotice'),
  available: t('consignments.available'),
  sold: t('consignments.sold'),
  returned: t('consignments.returned'),
  unsettledQty: t('consignments.unsettledQty'),
  payoutDue: t('consignments.payoutDue'),
  product: t('consignments.product'),
  supplier: t('consignments.supplier'),
  qty: t('consignments.qty'),
  unsettled: t('consignments.unsettled'),
  cost: t('consignments.cost'),
  action: t('consignments.action'),
  return: t('consignments.return'),
  returnConsignment: t('consignments.returnConsignment'),
  returnQty: t('consignments.returnQty'),
  reason: t('consignments.reason'),
  cancel: t('common.cancel'),
  confirmReturn: t('consignments.confirmReturn'),
  noSku: t('consignments.noSku'),
  noRecords: t('consignments.noRecords'),
  loadFailed: t('consignments.loadFailed'),
  onlineRequired: t('consignments.onlineRequired'),
  selectSupplierBeforePayout: t('consignments.selectSupplierBeforePayout'),
  payoutCompleted: t('consignments.payoutCompleted'),
  payoutFailed: t('consignments.payoutFailed'),
  returnCompleted: t('consignments.returnCompleted'),
  returnFailed: t('consignments.returnFailed'),
  invalidReturnQty: t('consignments.invalidReturnQty'),
  payoutNote: t('consignments.payoutNote'),
  showing: t('common.results'),
  to: t('reportViews.sale.to'),
  of: t('common.of'),
  page: t('common.page'),
  prev: t('common.prev'),
  next: t('common.next')
}));

const refreshOnlineStatus = () => {
  isOffline.value = androidBridge.isAvailable() ? !androidBridge.isOnline() : !navigator.onLine;
};

const requestParams = computed(() => ({
  supplierId: selectedSupplierId.value || null,
  branchId: role === 'Admin' ? null : branchId
}));

const summary = computed(() => inventory.value.reduce((acc, item) => {
  acc.available += Number(item.AvailableQuantity ?? item.availableQuantity ?? 0);
  acc.sold += Number(item.SoldQuantity ?? item.soldQuantity ?? 0);
  acc.unsettledQty += Number(item.UnsettledQuantity ?? item.unsettledQuantity ?? 0);
  acc.unsettledAmount += Number(item.UnsettledAmount ?? item.unsettledAmount ?? 0);
  return acc;
}, { available: 0, sold: 0, unsettledQty: 0, unsettledAmount: 0 }));

const totalPages = computed(() => Math.max(1, Math.ceil(inventory.value.length / pageSize)));
const paginatedInventory = computed(() => {
  const start = (currentPage.value - 1) * pageSize;
  return inventory.value.slice(start, start + pageSize);
});
const pageStart = computed(() => inventory.value.length === 0 ? 0 : ((currentPage.value - 1) * pageSize) + 1);
const pageEnd = computed(() => Math.min(currentPage.value * pageSize, inventory.value.length));
const returnAvailableQty = computed(() => Number(returnItem.value?.AvailableQuantity ?? returnItem.value?.availableQuantity ?? 0));

const supplierName = (supplier) => supplier?.supplierName || supplier?.SupplierName || supplier?.Name || '';

const supplierMatches = (supplier, query) => {
  const name = supplierName(supplier);
  const phone = supplier?.Phone || supplier?.phone || '';
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
  if (!query) fetchAll();
};

const selectSupplier = (supplier) => {
  selectedSupplierId.value = supplier?.supplierId || supplier?.SupplierId || null;
  supplierSearch.value = supplierName(supplier);
  filteredSuppliers.value = [];
  showSupplierResults.value = false;
  supplierHighlightedIndex.value = -1;
  fetchAll();
};

const clearSupplierSearch = () => {
  supplierSearch.value = '';
  selectedSupplierId.value = null;
  filteredSuppliers.value = [];
  showSupplierResults.value = false;
  supplierHighlightedIndex.value = -1;
  fetchAll();
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

const fetchAll = async () => {
  refreshOnlineStatus();
  try {
    const [supplierRes, inventoryRes] = await Promise.all([
      api.get('/suppliers'),
      api.get('/consignments/inventory', { params: requestParams.value })
    ]);
    suppliers.value = (Array.isArray(supplierRes.data) ? supplierRes.data : []).map(s => ({
      ...s,
      supplierId: s.supplierId ?? s.SupplierId,
      supplierName: s.supplierName ?? s.SupplierName ?? s.Name,
      Phone: s.Phone ?? s.phone ?? ''
    }));
    inventory.value = Array.isArray(inventoryRes.data) ? inventoryRes.data : [];
    currentPage.value = 1;
  } catch (err) {
    addToast(tx.value.loadFailed, 'error');
  }
};

const settleAll = async () => {
  refreshOnlineStatus();
  if (isOffline.value) {
    addToast(tx.value.onlineRequired, 'warning');
    return;
  }

  if (!selectedSupplierId.value) {
    addToast(tx.value.selectSupplierBeforePayout, 'warning');
    return;
  }

  try {
    const res = await api.post('/consignments/settlements', {
      SupplierId: Number(selectedSupplierId.value),
      BranchId: role === 'Admin' ? null : branchId,
      PaymentMethod: 'Cash',
      Note: tx.value.payoutNote
    });
    addToast(res.data?.Message || tx.value.payoutCompleted, 'success');
    fetchAll();
  } catch (err) {
    addToast(err?.response?.data?.Message || tx.value.payoutFailed, 'error');
  }
};

const openReturnModal = (item) => {
  refreshOnlineStatus();
  if (isOffline.value) {
    addToast(tx.value.onlineRequired, 'warning');
    return;
  }

  returnItem.value = item;
  returnForm.value = {
    quantity: Number(item.AvailableQuantity ?? item.availableQuantity ?? 0),
    reason: 'Supplier consignment return'
  };
  showReturnModal.value = true;
};

const closeReturnModal = () => {
  showReturnModal.value = false;
  returnItem.value = null;
  returnForm.value = { quantity: 0, reason: '' };
};

const submitReturn = async () => {
  if (!returnItem.value) return;
  const quantity = Number(returnForm.value.quantity || 0);
  if (quantity <= 0 || quantity > returnAvailableQty.value) {
    addToast(tx.value.invalidReturnQty, 'warning');
    return;
  }

  try {
    const res = await api.post('/consignments/returns', {
      ConsignItemId: Number(returnItem.value.ConsignItemId || returnItem.value.consignItemId),
      Quantity: quantity,
      Reason: returnForm.value.reason
    });
    addToast(res.data?.Message || tx.value.returnCompleted, 'success');
    closeReturnModal();
    fetchAll();
  } catch (err) {
    addToast(err?.response?.data?.Message || tx.value.returnFailed, 'error');
  }
};

const handleExportCsv = () => {
  const headers = [
    tx.value.product,
    tx.value.supplier,
    tx.value.qty,
    tx.value.available,
    tx.value.sold,
    tx.value.returned,
    tx.value.unsettled,
    tx.value.cost,
    tx.value.payoutDue
  ];
  const rows = inventory.value.map(item => [
    item.ProductName || item.productName || '',
    item.SupplierName || item.supplierName || '',
    item.Quantity ?? item.quantity ?? 0,
    item.AvailableQuantity ?? item.availableQuantity ?? 0,
    item.SoldQuantity ?? item.soldQuantity ?? 0,
    item.ReturnedQuantity ?? item.returnedQuantity ?? 0,
    item.UnsettledQuantity ?? item.unsettledQuantity ?? 0,
    item.UnitPrice ?? item.unitPrice ?? 0,
    item.UnsettledAmount ?? item.unsettledAmount ?? 0
  ]);
  const dateStr = new Date().toISOString().split('T')[0];
  exportToCsv(`Consignments_${dateStr}`, headers, rows);
};

const formatNumber = (value) => new Intl.NumberFormat().format(Number(value || 0));

onMounted(() => {
  refreshOnlineStatus();
  window.addEventListener('online', refreshOnlineStatus);
  window.addEventListener('offline', refreshOnlineStatus);
  document.addEventListener('mousedown', handleSupplierOutsideClick);
  document.addEventListener('touchstart', handleSupplierOutsideClick);
  fetchAll();
});

onUnmounted(() => {
  window.removeEventListener('online', refreshOnlineStatus);
  window.removeEventListener('offline', refreshOnlineStatus);
  document.removeEventListener('mousedown', handleSupplierOutsideClick);
  document.removeEventListener('touchstart', handleSupplierOutsideClick);
});
</script>

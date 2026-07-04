<template>
  <div class="space-y-6 animate-in fade-in duration-500">

    <div
      class="bg-[#3f3f46] p-6 rounded-xl shadow-lg flex justify-between items-center text-white border-b-4 border-blue-600">
      <div>
        <h2 class="text-[10px] font-normal uppercase tracking-[0.2em] text-slate-400 mb-1">{{ tx.totalProcurementWorth }}</h2>
        <div class="text-3xl font-normal tracking-tight tabular-nums">
          {{ formatNumber(totalPurchaseWorth) }} <span class="text-sm font-normal opacity-50 uppercase ml-1">MMK</span>
        </div>
      </div>
      <div class="text-right bg-white/5 p-3 rounded-lg border border-white/10">
        <div class="text-[9px] font-normal uppercase text-blue-400 tracking-widest mb-1">{{ tx.reportPeriod }}</div>
        <div class="text-xs font-normal font-mono">{{ formatDateShort(filters.fromDate) }} — {{
          formatDateShort(filters.toDate) }}</div>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
      <div class="px-6 py-4 border-b border-slate-100 flex justify-between items-center bg-slate-50/50">
        <h3 class="font-normal text-[10px] uppercase text-slate-500 tracking-[0.2em]">{{ tx.purchaseHistoryLedger }}</h3>
        <div class="flex flex-wrap items-center justify-end gap-3">
          <label class="flex items-center gap-2 text-[9px] font-normal uppercase tracking-wider text-slate-400">
            <span>{{ tx.supplierVendor }}</span>
            <select v-model="selectedSupplierId"
              class="min-w-40 rounded-md border border-slate-200 bg-white px-2.5 py-1.5 text-[10px] normal-case tracking-normal text-slate-700 outline-none focus:border-blue-400">
              <option value="">{{ tx.allSuppliers }}</option>
              <option v-for="supplier in suppliers" :key="supplier.SupplierId ?? supplier.supplierId"
                :value="String(supplier.SupplierId ?? supplier.supplierId)">
                {{ supplier.SupplierName ?? supplier.supplierName }}
              </option>
            </select>
          </label>
          <div class="inline-flex p-1 bg-slate-200/70 rounded-lg border border-slate-200">
            <button @click="reportView = 'voucher'"
              :class="reportView === 'voucher' ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500 hover:text-slate-700'"
              class="px-3 py-1.5 rounded-md text-[9px] font-normal uppercase tracking-widest transition-all">
              {{ tx.voucherView }}
            </button>
            <button @click="reportView = 'item'"
              :class="reportView === 'item' ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500 hover:text-slate-700'"
              class="px-3 py-1.5 rounded-md text-[9px] font-normal uppercase tracking-widest transition-all">
              {{ tx.itemView }}
            </button>
          </div>
          <button @click="handleExportCsv" class="text-[10px] font-normal text-blue-600 uppercase hover:underline">EXPORT CSV</button>
          <div class="text-[10px] font-normal text-slate-400 uppercase">{{ tx.records }}: {{ activeRows.length }}</div>
        </div>
      </div>

      <div v-if="reportView === 'voucher'" class="overflow-x-auto">
        <table class="w-full text-left border-collapse">
          <thead class="bg-[#3f3f46] text-white">
            <tr class="text-[10px] font-normal uppercase tracking-[0.2em] text-white">
              <th class="p-4 w-16 text-center opacity-70 text-white">Ref</th>
              <th class="p-4 text-white">{{ tx.poNumber }}</th>
              <th class="p-4 text-white">{{ tx.supplierVendor }}</th>
              <th class="p-4 text-right text-white">{{ tx.settlementAmount }}</th>
              <th class="p-4 text-white">{{ tx.paymentMethod || 'Payment' }}</th>
              <th class="p-4 text-center px-8 text-white">{{ tx.fulfillmentDate }}</th>
            </tr>
          </thead>
          <tbody class="text-xs font-normal text-slate-600 divide-y divide-slate-50" v-if="purchases.length > 0">
            <tr v-for="(p, i) in paginatedPurchases" :key="p.PurchaseId"
              class="hover:bg-blue-50/30 transition-colors group">
              <td class="p-4 text-center text-slate-300 font-mono text-[10px]">
                {{ (currentPage - 1) * itemsPerPage + (i + 1) }}
              </td>
              <td class="p-4 font-normal text-blue-600 tracking-tighter uppercase">
                PO-{{ String(p.PurchaseId || p.poId).padStart(5, '0') }}
              </td>
              <td class="p-4 text-slate-900 font-normal italic">
                {{ p.SupplierName || p.supplierName || tx.generalVendor }}</td>
              <td class="p-4 text-right font-normal text-slate-900 tabular-nums">
                {{ formatNumber(getPurchaseAmount(p)) }} <small class="text-[10px] opacity-40">Ks</small>
              </td>
              <td class="p-4 text-slate-700 text-[11px] tabular-nums">{{ p.PaymentMethod || p.paymentMethod || '-' }}</td>
              <td class="p-4 text-center text-slate-500 tabular-nums">{{ formatDate(p.PurchaseDate || p.purchaseDate ||
                p.orderDate) }}</td>
            </tr>
          </tbody>
          <tbody v-else>
            <tr>
              <td colspan="5" class="p-24 text-center">
                <div class="flex flex-col items-center opacity-20 grayscale">
                  <span class="text-4xl mb-2">📦</span>
                  <p class="text-[10px] font-normal uppercase tracking-[0.3em] text-slate-900">{{ tx.noPurchaseRecordsFound }}
                  </p>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div v-else class="overflow-x-auto">
        <table class="w-full text-left border-collapse min-w-[920px]">
          <thead class="bg-[#3f3f46] text-white">
            <tr class="text-[10px] font-normal uppercase tracking-[0.2em] text-white">
              <th class="p-4 w-16 text-center opacity-70 text-white">Ref</th>
              <th class="p-4 text-white">{{ tx.poNumber }}</th>
              <th class="p-4 text-white">{{ tx.product }}</th>
              <th class="p-4 text-white">{{ tx.supplierVendor }}</th>
              <th class="p-4 text-center text-white">{{ tx.qty }}</th>
              <th class="p-4 text-center text-white">{{ tx.foc }}</th>
              <th class="p-4 text-center text-white">{{ tx.returned }}</th>
              <th class="p-4 text-white">{{ tx.unit }}</th>
              <th class="p-4 text-right text-white">{{ tx.unitCost }}</th>
              <th class="p-4 text-right text-white">{{ tx.lineTotal }}</th>
              <th class="p-4 text-center px-8 text-white">{{ tx.fulfillmentDate }}</th>
            </tr>
          </thead>
          <tbody class="text-xs font-normal text-slate-600 divide-y divide-slate-50" v-if="purchaseItems.length > 0">
            <tr v-for="(item, i) in paginatedPurchaseItems" :key="item.POItemId || item.poItemId"
              class="hover:bg-blue-50/30 transition-colors group">
              <td class="p-4 text-center text-slate-300 font-mono text-[10px]">
                {{ (currentPage - 1) * itemsPerPage + (i + 1) }}
              </td>
              <td class="p-4 font-normal text-blue-600 tracking-tighter uppercase">
                {{ item.VoucherNo || item.voucherNo || `PO-${String(item.PurchaseId || item.purchaseId).padStart(5, '0')}` }}
              </td>
              <td class="p-4">
                <div class="font-normal text-slate-900">{{ item.ProductName || item.productName }}</div>
                <div class="text-[9px] text-slate-400 uppercase tracking-wider">{{ item.Sku || item.sku || '-' }}</div>
              </td>
              <td class="p-4 text-slate-700 italic">{{ item.SupplierName || item.supplierName || tx.generalVendor }}</td>
              <td class="p-4 text-center tabular-nums">{{ formatNumber(item.Quantity ?? item.quantity) }}</td>
              <td class="p-4 text-center tabular-nums text-emerald-600">{{ formatNumber(item.FOCQuantity ?? item.focQuantity) }}</td>
              <td class="p-4 text-center tabular-nums text-rose-600">{{ formatNumber(item.ReturnedQuantity ?? item.returnedQuantity) }}</td>
              <td class="p-4 text-slate-500">{{ item.UnitName || item.unitName || '-' }}</td>
              <td class="p-4 text-right tabular-nums">{{ formatNumber(item.CostPrice ?? item.costPrice) }}</td>
              <td class="p-4 text-right font-normal text-slate-900 tabular-nums">
                {{ formatNumber(item.LineTotal ?? item.lineTotal) }} <small class="text-[10px] opacity-40">Ks</small>
              </td>
              <td class="p-4 text-center text-slate-500 tabular-nums">{{ formatDate(item.PurchaseDate || item.purchaseDate ||
                item.orderDate) }}</td>
            </tr>
          </tbody>
          <tbody v-else>
            <tr>
              <td colspan="11" class="p-24 text-center">
                <div class="flex flex-col items-center opacity-20 grayscale">
                  <span class="text-4xl mb-2">Box</span>
                  <p class="text-[10px] font-normal uppercase tracking-[0.3em] text-slate-900">{{ tx.noPurchaseRecordsFound }}
                  </p>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="p-4 bg-slate-50 border-t border-slate-200 flex items-center justify-between">
        <p class="text-[9px] font-normal text-slate-400 uppercase tracking-widest">
          {{ tx.viewingItems }} {{ activeRows.length ? ((currentPage - 1) * itemsPerPage + 1) : 0 }} {{ tx.to }} {{ Math.min(currentPage * itemsPerPage,
            activeRows.length) }}
        </p>
        <div class="flex gap-1.5">
          <button @click="currentPage--" :disabled="currentPage === 1"
            class="px-4 py-1.5 bg-white border border-slate-200 rounded-lg font-normal text-[9px] uppercase tracking-widest shadow-sm disabled:opacity-30 hover:bg-slate-50 transition-all">{{ tx.prev }}</button>
          <button @click="currentPage++" :disabled="currentPage >= totalPages"
            class="px-4 py-1.5 bg-blue-600 text-white rounded-lg font-normal text-[9px] uppercase tracking-widest shadow-lg shadow-blue-100 disabled:opacity-30 hover:bg-blue-700 transition-all">{{ tx.next }}</button>
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

const props = defineProps(['filters']);
const { t } = useI18n();
const { exportToCsv } = useCsvExport();
const purchases = ref([]);
const purchaseItems = ref([]);
const suppliers = ref([]);
const selectedSupplierId = ref('');
const reportView = ref('voucher');
const tx = computed(() => ({
  totalProcurementWorth: t('reportViews.purchase.totalProcurementWorth'),
  reportPeriod: t('reportViews.purchase.reportPeriod'),
  purchaseHistoryLedger: t('reportViews.purchase.purchaseHistoryLedger'),
  records: t('reportViews.purchase.records'),
  voucherView: t('reportViews.purchase.voucherView'),
  itemView: t('reportViews.purchase.itemView'),
  poNumber: t('reportViews.purchase.poNumber'),
  product: t('reportViews.purchase.product'),
  qty: t('reportViews.purchase.qty'),
  foc: t('reportViews.purchase.foc'),
  returned: t('reportViews.purchase.returned'),
  unit: t('reportViews.purchase.unit'),
  unitCost: t('reportViews.purchase.unitCost'),
  lineTotal: t('reportViews.purchase.lineTotal'),
  supplierVendor: t('reportViews.purchase.supplierVendor'),
  allSuppliers: t('reportViews.purchase.allSuppliers'),
  settlementAmount: t('reportViews.purchase.settlementAmount'),
  paymentMethod: t('reportViews.purchase.paymentMethod'),
  fulfillmentDate: t('reportViews.purchase.fulfillmentDate'),
  generalVendor: t('reportViews.purchase.generalVendor'),
  noPurchaseRecordsFound: t('reportViews.purchase.noPurchaseRecordsFound'),
  viewingItems: t('reportViews.purchase.viewingItems'),
  to: t('reportViews.purchase.to'),
  prev: t('reportViews.purchase.prev'),
  next: t('reportViews.purchase.next')
}));

// Pagination Logic
const currentPage = ref(1);
const itemsPerPage = 12; // Increased for better full-page use
const activeRows = computed(() => reportView.value === 'item' ? purchaseItems.value : purchases.value);
const totalPages = computed(() => Math.ceil(activeRows.value.length / itemsPerPage) || 1);
const paginatedPurchases = computed(() => {
  const start = (currentPage.value - 1) * itemsPerPage;
  return purchases.value.slice(start, start + itemsPerPage);
});

const paginatedPurchaseItems = computed(() => {
  const start = (currentPage.value - 1) * itemsPerPage;
  return purchaseItems.value.slice(start, start + itemsPerPage);
});

const toNumber = (value) => {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : 0;
};

const getPurchaseAmount = (purchase) => {
  const netAmount = purchase.NetTotalAmount ?? purchase.netTotalAmount;
  if (netAmount !== undefined && netAmount !== null) return toNumber(netAmount);

  const totalAmount = purchase.TotalAmount ?? purchase.totalAmount;
  const returnedAmount = purchase.ReturnedAmount ?? purchase.returnedAmount;
  return Math.max(0, toNumber(totalAmount) - toNumber(returnedAmount));
};

const totalPurchaseWorth = computed(() => {
  return purchases.value.reduce((sum, purchase) => sum + getPurchaseAmount(purchase), 0);
});

const handleExportCsv = () => {
  if (reportView.value === 'item') {
    const headers = [
      t('reportViews.purchase.poNumber'),
      t('reportViews.purchase.product'),
      'SKU',
      t('reportViews.purchase.supplierVendor'),
      t('reportViews.purchase.qty'),
      t('reportViews.purchase.foc'),
      t('reportViews.purchase.returned'),
      t('reportViews.purchase.unit'),
      t('reportViews.purchase.unitCost'),
      t('reportViews.purchase.lineTotal'),
      t('reportViews.purchase.fulfillmentDate')
    ];

    const rows = purchaseItems.value.map(item => [
      item.VoucherNo || item.voucherNo || `PO-${String(item.PurchaseId || item.purchaseId).padStart(5, '0')}`,
      item.ProductName || item.productName || '',
      item.Sku || item.sku || '',
      item.SupplierName || item.supplierName || tx.value.generalVendor,
      toNumber(item.Quantity ?? item.quantity),
      toNumber(item.FOCQuantity ?? item.focQuantity),
      toNumber(item.ReturnedQuantity ?? item.returnedQuantity),
      item.UnitName || item.unitName || '',
      toNumber(item.CostPrice ?? item.costPrice),
      toNumber(item.LineTotal ?? item.lineTotal),
      formatDate(item.PurchaseDate || item.purchaseDate || item.orderDate)
    ]);

    const dateStr = new Date().toISOString().split('T')[0];
    exportToCsv(`Purchase_Item_Report_${dateStr}`, headers, rows);
    return;
  }

  const headers = [
    t('reportViews.purchase.poNumber'),
    t('reportViews.purchase.supplierVendor'),
    t('reportViews.purchase.settlementAmount'),
    (t('reportViews.purchase.paymentMethod') || 'Payment'),
    t('reportViews.purchase.fulfillmentDate')
  ];

  const rows = purchases.value.map(p => [
    `PO-${String(p.PurchaseId || p.poId).padStart(5, '0')}`,
    p.SupplierName || p.supplierName || tx.value.generalVendor,
    getPurchaseAmount(p),
    (p.PaymentMethod || p.paymentMethod || ''),
    formatDate(p.PurchaseDate || p.purchaseDate || p.orderDate)
  ]);

  const dateStr = new Date().toISOString().split('T')[0];
  exportToCsv(`Purchase_Report_${dateStr}`, headers, rows);
};

const fetchPurchases = async () => {
  try {
    const params = {
      ...props.filters,
      supplierId: selectedSupplierId.value ? Number(selectedSupplierId.value) : null
    };
    const [res, itemRes] = await Promise.all([
      api.get('/Reports/purchase-report', { params }),
      api.get('/Reports/purchase-items', { params })
    ]);
    purchases.value = res.data;
    purchaseItems.value = itemRes.data;
  } catch (err) {
    console.error("Procurement Audit Error:", err);
  }
};

const fetchSuppliers = async () => {
  try {
    const res = await api.get('/Suppliers');
    suppliers.value = Array.isArray(res.data) ? res.data : [];
  } catch (err) {
    console.error('Supplier filter load error:', err);
    suppliers.value = [];
  }
};

const formatNumber = (val) => new Intl.NumberFormat().format(val || 0);
const formatDate = (date) => date ? new Date(date).toLocaleDateString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric' }) : '-';
const formatDateShort = (date) => date ? new Date(date).toLocaleDateString('en-GB') : 'N/A';

watch(() => props.filters, () => {
  currentPage.value = 1;
  fetchPurchases();
}, { deep: true });

watch(reportView, () => {
  currentPage.value = 1;
});

watch(selectedSupplierId, () => {
  currentPage.value = 1;
  fetchPurchases();
});

onMounted(() => {
  fetchSuppliers();
  fetchPurchases();
});
</script>

<style scoped>
.tabular-nums {
  font-variant-numeric: tabular-nums;
}
</style>

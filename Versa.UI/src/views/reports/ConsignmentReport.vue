<template>
  <div class="space-y-6 animate-in fade-in duration-500">
    <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
      <div class="bg-white border border-slate-200 rounded-xl p-5">
        <p class="text-[10px] uppercase tracking-widest text-slate-400">{{ tx.salesRevenue }}</p>
        <p class="text-2xl font-normal text-blue-600 tabular-nums">{{ formatNumber(summary.SalesRevenue) }}</p>
      </div>
      <div class="bg-white border border-slate-200 rounded-xl p-5">
        <p class="text-[10px] uppercase tracking-widest text-slate-400">{{ tx.consignmentCost }}</p>
        <p class="text-2xl font-normal text-rose-600 tabular-nums">{{ formatNumber(summary.ConsignmentCost) }}</p>
      </div>
      <div class="bg-white border border-slate-200 rounded-xl p-5">
        <p class="text-[10px] uppercase tracking-widest text-slate-400">{{ tx.grossProfit }}</p>
        <p class="text-2xl font-normal text-emerald-600 tabular-nums">{{ formatNumber(summary.GrossProfit) }}</p>
      </div>
      <div class="bg-white border border-slate-200 rounded-xl p-5">
        <p class="text-[10px] uppercase tracking-widest text-slate-400">{{ tx.payoutDue }}</p>
        <p class="text-2xl font-normal text-indigo-600 tabular-nums">{{ formatNumber(summary.CurrentPayoutDue) }}</p>
      </div>
    </div>

    <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
      <div class="bg-slate-50 border border-slate-200 rounded-xl p-4">
        <p class="text-[10px] uppercase tracking-widest text-slate-400">{{ tx.soldQty }}</p>
        <p class="text-lg font-normal text-slate-800 tabular-nums">{{ formatNumber(summary.SoldQuantity) }}</p>
      </div>
      <div class="bg-slate-50 border border-slate-200 rounded-xl p-4">
        <p class="text-[10px] uppercase tracking-widest text-slate-400">{{ tx.availableQty }}</p>
        <p class="text-lg font-normal text-slate-800 tabular-nums">{{ formatNumber(summary.AvailableQuantity) }}</p>
      </div>
      <div class="bg-slate-50 border border-slate-200 rounded-xl p-4">
        <p class="text-[10px] uppercase tracking-widest text-slate-400">{{ tx.unsettledQty }}</p>
        <p class="text-lg font-normal text-slate-800 tabular-nums">{{ formatNumber(summary.CurrentUnsettledQuantity) }}</p>
      </div>
      <div class="bg-slate-50 border border-slate-200 rounded-xl p-4">
        <p class="text-[10px] uppercase tracking-widest text-slate-400">{{ tx.payoutPaid }}</p>
        <p class="text-lg font-normal text-slate-800 tabular-nums">{{ formatNumber(summary.PayoutPaid) }}</p>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
      <div class="px-6 py-4 border-b border-slate-100 flex justify-between items-center bg-slate-50/50">
        <h3 class="font-normal text-[10px] uppercase text-slate-500 tracking-[0.2em]">{{ tx.currentStock }}</h3>
        <div class="text-right">
          <p class="text-[10px] uppercase tracking-widest text-slate-400">{{ tx.availableValue }}</p>
          <p class="text-lg text-emerald-600 tabular-nums">{{ formatNumber(currentAvailableValue) }}</p>
        </div>
      </div>
      <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse text-sm">
          <thead class="bg-slate-100 text-slate-500 text-[10px] uppercase tracking-[0.18em]">
            <tr>
              <th class="p-4">{{ tx.date }}</th>
              <th class="p-4">{{ tx.product }}</th>
              <th class="p-4">{{ tx.supplier }}</th>
              <th class="p-4">{{ tx.branch }}</th>
              <th class="p-4 text-right">{{ tx.qty }}</th>
              <th class="p-4 text-right">{{ tx.availableQty }}</th>
              <th class="p-4 text-right">{{ tx.unitCost }}</th>
              <th class="p-4 text-right">{{ tx.value }}</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-slate-100">
            <tr v-for="row in inventoryItems" :key="row.ConsignItemId || row.consignItemId" class="hover:bg-slate-50">
              <td class="p-4 text-slate-500">{{ formatDate(row.OrderDate || row.orderDate) }}</td>
              <td class="p-4 text-slate-800">
                <div>{{ row.ProductName || row.productName }}</div>
                <div class="text-[10px] text-slate-400 uppercase">{{ row.Sku || row.sku || tx.noSku }}</div>
              </td>
              <td class="p-4 text-slate-600">{{ row.SupplierName || row.supplierName || '-' }}</td>
              <td class="p-4 text-slate-600">{{ row.BranchName || row.branchName || '-' }}</td>
              <td class="p-4 text-right tabular-nums">{{ formatNumber(amount(row, 'Quantity')) }}</td>
              <td class="p-4 text-right tabular-nums">{{ formatNumber(amount(row, 'AvailableQuantity')) }}</td>
              <td class="p-4 text-right tabular-nums">{{ formatNumber(amount(row, 'UnitCost')) }}</td>
              <td class="p-4 text-right text-emerald-600 tabular-nums">{{ formatNumber(amount(row, 'AvailableValue')) }}</td>
            </tr>
            <tr v-if="inventoryItems.length === 0">
              <td colspan="8" class="p-10 text-center text-xs text-slate-400 uppercase tracking-widest">{{ tx.noCurrentStock }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
      <div class="px-6 py-4 border-b border-slate-100 flex justify-between items-center bg-slate-50/50">
        <h3 class="font-normal text-[10px] uppercase text-slate-500 tracking-[0.2em]">{{ tx.saleLines }}</h3>
        <button @click="handleExportCsv" class="text-[10px] font-normal text-blue-600 uppercase hover:underline">{{ tx.exportCsv }}</button>
      </div>
      <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse text-sm">
          <thead class="bg-[#3f3f46] text-white text-[10px] uppercase tracking-[0.18em]">
            <tr>
              <th class="p-4">{{ tx.date }}</th>
              <th class="p-4">{{ tx.voucher }}</th>
              <th class="p-4">{{ tx.product }}</th>
              <th class="p-4">{{ tx.supplier }}</th>
              <th class="p-4 text-right">{{ tx.qty }}</th>
              <th class="p-4 text-right">{{ tx.revenue }}</th>
              <th class="p-4 text-right">{{ tx.cost }}</th>
              <th class="p-4 text-right">{{ tx.profit }}</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-slate-100">
            <tr v-for="row in items" :key="`${row.VoucherNo || row.voucherNo}-${row.ProductName || row.productName}`" class="hover:bg-slate-50">
              <td class="p-4 text-slate-500">{{ formatDate(row.SaleDate || row.saleDate) }}</td>
              <td class="p-4 text-blue-600">{{ row.VoucherNo || row.voucherNo }}</td>
              <td class="p-4 text-slate-800">
                <div>{{ row.ProductName || row.productName }}</div>
                <div class="text-[10px] text-slate-400 uppercase">{{ row.Sku || row.sku || tx.noSku }}</div>
              </td>
              <td class="p-4 text-slate-600">{{ row.SupplierName || row.supplierName || '-' }}</td>
              <td class="p-4 text-right tabular-nums">{{ formatNumber(amount(row, 'Quantity')) }}</td>
              <td class="p-4 text-right tabular-nums">{{ formatNumber(amount(row, 'Revenue')) }}</td>
              <td class="p-4 text-right tabular-nums">{{ formatNumber(amount(row, 'Cost')) }}</td>
              <td class="p-4 text-right text-emerald-600 tabular-nums">{{ formatNumber(amount(row, 'Profit')) }}</td>
            </tr>
            <tr v-if="items.length === 0">
              <td colspan="8" class="p-16 text-center text-xs text-slate-400 uppercase tracking-widest">{{ tx.noSaleLines }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
      <div class="px-6 py-4 border-b border-slate-100 bg-slate-50/50">
        <h3 class="font-normal text-[10px] uppercase text-slate-500 tracking-[0.2em]">{{ tx.payoutHistory }}</h3>
      </div>
      <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse text-sm">
          <thead class="bg-slate-100 text-slate-500 text-[10px] uppercase tracking-[0.18em]">
            <tr>
              <th class="p-4">{{ tx.date }}</th>
              <th class="p-4">{{ tx.supplier }}</th>
              <th class="p-4">{{ tx.branch }}</th>
              <th class="p-4">{{ tx.payment }}</th>
              <th class="p-4 text-right">{{ tx.amount }}</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-slate-100">
            <tr v-for="row in settlements" :key="row.SettlementId || row.settlementId">
              <td class="p-4 text-slate-500">{{ formatDate(row.SettlementDate || row.settlementDate) }}</td>
              <td class="p-4 text-slate-800">{{ row.SupplierName || row.supplierName || '-' }}</td>
              <td class="p-4 text-slate-600">{{ row.BranchName || row.branchName || '-' }}</td>
              <td class="p-4 text-slate-600">{{ row.PaymentMethod || row.paymentMethod || '-' }}</td>
              <td class="p-4 text-right tabular-nums">{{ formatNumber(amount(row, 'TotalAmount')) }}</td>
            </tr>
            <tr v-if="settlements.length === 0">
              <td colspan="5" class="p-10 text-center text-xs text-slate-400 uppercase tracking-widest">{{ tx.noPayouts }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue';
import api from '../../services/api';
import { useCsvExport } from '../../composables/useCsvExport';
import { useI18n } from '../../composables/useI18n';

const props = defineProps(['filters']);
const { t } = useI18n();
const { exportToCsv } = useCsvExport();
const summary = ref({});
const items = ref([]);
const settlements = ref([]);
const inventoryItems = ref([]);

const tx = computed(() => ({
  salesRevenue: t('reportViews.consignment.salesRevenue'),
  consignmentCost: t('reportViews.consignment.consignmentCost'),
  grossProfit: t('reportViews.consignment.grossProfit'),
  payoutDue: t('reportViews.consignment.payoutDue'),
  soldQty: t('reportViews.consignment.soldQty'),
  availableQty: t('reportViews.consignment.availableQty'),
  unsettledQty: t('reportViews.consignment.unsettledQty'),
  payoutPaid: t('reportViews.consignment.payoutPaid'),
  saleLines: t('reportViews.consignment.saleLines'),
  payoutHistory: t('reportViews.consignment.payoutHistory'),
  exportCsv: t('reportViews.consignment.exportCsv'),
  date: t('reportViews.consignment.date'),
  voucher: t('reportViews.consignment.voucher'),
  product: t('reportViews.consignment.product'),
  supplier: t('reportViews.consignment.supplier'),
  branch: t('reportViews.consignment.branch'),
  qty: t('reportViews.consignment.qty'),
  revenue: t('reportViews.consignment.revenue'),
  cost: t('reportViews.consignment.cost'),
  profit: t('reportViews.consignment.profit'),
  payment: t('reportViews.consignment.payment'),
  amount: t('reportViews.consignment.amount'),
  currentStock: t('reportViews.consignment.currentStock'),
  availableValue: t('reportViews.consignment.availableValue'),
  unitCost: t('reportViews.consignment.unitCost'),
  value: t('reportViews.consignment.value'),
  noSku: t('reportViews.consignment.noSku'),
  noSaleLines: t('reportViews.consignment.noSaleLines'),
  noPayouts: t('reportViews.consignment.noPayouts'),
  noCurrentStock: t('reportViews.consignment.noCurrentStock')
}));

const currentAvailableValue = computed(() => {
  const apiTotal = Number(summary.value?.CurrentAvailableValue ?? summary.value?.currentAvailableValue ?? 0);
  if (Number.isFinite(apiTotal) && apiTotal > 0) return apiTotal;
  return inventoryItems.value.reduce((sum, row) => sum + amount(row, 'AvailableValue'), 0);
});

const amount = (obj, key) => {
  const camel = key.charAt(0).toLowerCase() + key.slice(1);
  const parsed = Number(obj?.[key] ?? obj?.[camel] ?? 0);
  return Number.isFinite(parsed) ? parsed : 0;
};

const fetchData = async () => {
  try {
    const res = await api.get('/Reports/consignment-report', { params: props.filters });
    summary.value = res.data?.Summary || res.data?.summary || {};
    items.value = res.data?.Items || res.data?.items || [];
    settlements.value = res.data?.Settlements || res.data?.settlements || [];
    inventoryItems.value = res.data?.InventoryItems || res.data?.inventoryItems || [];
    summary.value.CurrentAvailableValue = res.data?.CurrentAvailableValue ?? res.data?.currentAvailableValue ?? currentAvailableValue.value;
  } catch (err) {
    console.error('Consignment report error:', err);
  }
};

const handleExportCsv = () => {
  const headers = [tx.value.date, tx.value.voucher, tx.value.product, tx.value.supplier, tx.value.qty, tx.value.revenue, tx.value.cost, tx.value.profit];
  const rows = items.value.map(row => [
    formatDate(row.SaleDate || row.saleDate),
    row.VoucherNo || row.voucherNo,
    row.ProductName || row.productName,
    row.SupplierName || row.supplierName || '',
    amount(row, 'Quantity'),
    amount(row, 'Revenue'),
    amount(row, 'Cost'),
    amount(row, 'Profit')
  ]);
  exportToCsv(`Consignment_Report_${new Date().toISOString().slice(0, 10)}`, headers, rows);
};

const formatNumber = (value) => new Intl.NumberFormat().format(Number(value || 0));
const formatDate = (value) => value ? new Date(value).toLocaleDateString('en-GB') : '-';

watch(() => props.filters, fetchData, { deep: true });
onMounted(fetchData);
</script>

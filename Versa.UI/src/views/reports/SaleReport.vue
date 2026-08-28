<template>
  <div v-if="summary" class="space-y-8 animate-in fade-in duration-500">

    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
      <div class="bg-white p-5 rounded-xl border border-slate-200 shadow-sm relative overflow-hidden group">
        <div class="absolute top-0 left-0 w-1 h-full bg-blue-600"></div>
        <div class="flex justify-between items-start mb-2">
          <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest leading-none">{{ tx.netRevenue }}</label>
          <svg class="w-5 h-5 text-blue-100 group-hover:text-blue-500 transition-colors" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">`n            <path stroke-linecap="round" stroke-linejoin="round" d="M12 3v18m4-14H9.5a2.5 2.5 0 000 5H14a2.5 2.5 0 010 5H8" />`n          </svg>
        </div>
        <div class="text-2xl font-normal text-slate-900 tabular-nums">
          {{ formatNumber(resolvedSummary.NetRevenue) }} <small class="text-xs font-normal text-slate-400">Ks</small>
        </div>
      </div>

      <div class="bg-white p-5 rounded-xl border border-slate-200 shadow-sm relative overflow-hidden group">
        <div class="absolute top-0 left-0 w-1 h-full bg-amber-500"></div>
        <div class="flex justify-between items-start mb-2">
          <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest leading-none">{{ tx.voucherSavings }}</label>
          <svg class="w-5 h-5 text-amber-100 group-hover:text-amber-500 transition-colors" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">`n            <path stroke-linecap="round" stroke-linejoin="round" d="M3 9l8.25-6a1.5 1.5 0 011.5 0L21 9v10.5A1.5 1.5 0 0119.5 21h-15A1.5 1.5 0 013 19.5V9z" />`n            <path stroke-linecap="round" stroke-linejoin="round" d="M9 12h6M9 15h4" />`n          </svg>
        </div>
        <div class="text-2xl font-normal text-slate-900 tabular-nums">
          {{ formatNumber(resolvedSummary.TotalDiscounts) }} <small class="text-xs font-normal text-slate-400">Ks</small>
        </div>
      </div>

      <div class="bg-white p-5 rounded-xl border border-slate-200 shadow-sm relative overflow-hidden group">
        <div class="absolute top-0 left-0 w-1 h-full bg-emerald-500"></div>
        <div class="flex justify-between items-start mb-2">
          <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest leading-none">{{ tx.liquidityFlow }}</label>
          <svg class="w-5 h-5 text-emerald-100 group-hover:text-emerald-500 transition-colors" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">`n            <path stroke-linecap="round" stroke-linejoin="round" d="M4 12h16m-6-6l6 6-6 6" />`n          </svg>
        </div>
        <div class="text-2xl font-normal text-slate-900 tabular-nums">
          {{ formatNumber(resolvedSummary.TotalCollected) }} <small class="text-xs font-normal text-slate-400">Ks</small>
        </div>
      </div>

      <div class="bg-white p-5 rounded-xl border border-slate-200 shadow-sm relative overflow-hidden group">
        <div class="absolute top-0 left-0 w-1 h-full bg-slate-800"></div>
        <div class="flex justify-between items-start mb-2">
          <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest leading-none">{{ tx.logisticsFees }}</label>
          <svg class="w-5 h-5 text-slate-200 group-hover:text-slate-800 transition-colors" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">`n            <path stroke-linecap="round" stroke-linejoin="round" d="M3 7h11v10H3zM14 10h4l3 3v4h-7z" />`n            <circle cx="7.5" cy="18" r="1.5" fill="currentColor" stroke="none" />`n            <circle cx="18.5" cy="18" r="1.5" fill="currentColor" stroke="none" />`n          </svg>
        </div>
        <div class="text-2xl font-normal text-slate-900 tabular-nums">
          {{ formatNumber(resolvedSummary.TotalDeliveryFees) }} <small class="text-xs font-normal text-slate-400">Ks</small>
        </div>
      </div>
    </div>

    <div class="grid grid-cols-1 lg:grid-cols-3 gap-8">

      <div class="lg:col-span-2 bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden flex flex-col">
        <div class="px-6 py-4 border-b border-slate-100 flex justify-between items-center bg-slate-50/50">
          <h3 class="font-normal text-[10px] uppercase text-slate-500 tracking-[0.2em]">{{ tx.transactionLedger }}</h3>
          <div class="flex items-center gap-3">
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
              <button @click="reportView = 'collection'"
                :class="reportView === 'collection' ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500 hover:text-slate-700'"
                class="px-3 py-1.5 rounded-md text-[9px] font-normal uppercase tracking-widest transition-all">
                {{ tx.collections }}
              </button>
            </div>
            <button @click="handleExportCsv" class="text-[10px] font-normal text-blue-600 uppercase hover:underline">{{ tx.exportCsv }}</button>
          </div>
        </div>

        <div v-if="reportView === 'voucher'" class="flex-1 overflow-x-auto">
          <table class="w-full text-left border-collapse min-w-[600px]">
            <thead class="bg-[#3f3f46] text-white">
              <tr class="text-[10px] font-normal uppercase tracking-[0.2em] text-white">
                <th class="p-4 w-12 text-center opacity-70 text-white">Ref</th>
                <th class="p-4 text-white">{{ tx.voucher }}</th>
                <th class="p-4 text-white">{{ tx.customer }}</th>
                <th class="p-4 text-right text-white">{{ tx.subTotal }}</th>
                <th class="p-4 text-right text-white">Disc.</th>
                <th class="p-4 text-right px-6 text-white">{{ tx.netTotal }}</th>
                <th class="p-4 text-white">Payment</th>
                <th class="p-4 text-right text-white">Remaining</th>
              </tr>
            </thead>
            <tbody class="text-xs font-normal text-slate-600 divide-y divide-slate-50">
              <tr v-for="(s, i) in paginatedSales" :key="s.SaleId" class="hover:bg-blue-50/30 transition-colors group">
                <td class="p-4 text-center text-slate-300 font-mono text-[10px]">
                  {{ (currentPage - 1) * itemsPerPage + (i + 1) }}
                </td>
                <td class="p-4 font-normal text-blue-600 tabular-nums tracking-tighter">
                  {{ formatSaleVoucher(s.SaleId) }}
                </td>
                <td class="p-4 text-slate-900 font-normal italic">{{ s.CustomerName || tx.walkInClient }}</td>
                <td class="p-4 text-right tabular-nums">{{ formatNumber(getSaleSubTotal(s)) }}</td>
                <td class="p-4 text-right text-amber-600 tabular-nums">-{{ formatNumber(s.DiscountAmount ?? s.discountAmount) }}</td>
                <td class="p-4 text-right font-normal text-slate-900 tabular-nums bg-slate-50/30">
                  {{ formatNumber(getSaleNetTotal(s)) }}
                </td>
                <td class="p-4 text-slate-700 text-[11px] tabular-nums">{{ getPaymentSummary(s) || '-' }}</td>
                <td class="p-4 text-right font-normal text-rose-600 tabular-nums">
                  {{ formatNumber(getSaleRemaining(s)) }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div v-else-if="reportView === 'item'" class="flex-1 overflow-x-auto">
          <div v-if="selectedItemBar" class="px-4 py-2.5 bg-blue-50 border-b border-blue-100 flex items-center justify-between gap-3">
            <div class="min-w-0 text-[10px] font-normal uppercase tracking-wider text-blue-700 truncate">
              {{ selectedItemBar.name }}
              <span class="text-blue-400">({{ selectedItemBar.unit }})</span>
            </div>
            <button type="button" @click="clearItemFilter"
              class="w-7 h-7 shrink-0 inline-flex items-center justify-center rounded-md text-blue-500 hover:text-blue-700 hover:bg-blue-100 transition-colors"
              :title="tx.clearItemFilter" :aria-label="tx.clearItemFilter">
              <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>
          <table class="w-full text-left border-collapse min-w-[900px]">
            <thead class="bg-[#3f3f46] text-white">
              <tr class="text-[10px] font-normal uppercase tracking-[0.2em] text-white">
                <th class="p-4 w-12 text-center opacity-70 text-white">Ref</th>
                <th class="p-4 text-white">{{ tx.voucher }}</th>
                <th class="p-4 text-white">{{ tx.item }}</th>
                <th class="p-4 text-white">{{ tx.customer }}</th>
                <th class="p-4 text-center text-white">{{ tx.qty }}</th>
                <th class="p-4 text-white">{{ tx.unit }}</th>
                <th class="p-4 text-right text-white">{{ tx.unitPrice }}</th>
                <th class="p-4 text-right text-white">Disc.</th>
                <th class="p-4 text-right px-6 text-white">{{ tx.lineTotal }}</th>
              </tr>
            </thead>
            <tbody class="text-xs font-normal text-slate-600 divide-y divide-slate-50">
              <tr v-for="(item, i) in paginatedSaleItems" :key="item.SaleItemId || item.saleItemId"
                class="hover:bg-blue-50/30 transition-colors group">
                <td class="p-4 text-center text-slate-300 font-mono text-[10px]">
                  {{ (currentPage - 1) * itemsPerPage + (i + 1) }}
                </td>
                <td class="p-4 font-normal text-blue-600 tabular-nums tracking-tighter">
                  {{ normalizeSaleVoucher(item.VoucherNo || item.voucherNo, item.SaleId || item.saleId) }}
                </td>
                <td class="p-4">
                  <div class="font-normal text-slate-900">{{ item.ItemName || item.itemName }}</div>
                  <div class="text-[9px] text-slate-400 uppercase tracking-wider">{{ item.Sku || item.sku || '-' }}</div>
                </td>
                <td class="p-4 text-slate-700 italic">{{ item.CustomerName || item.customerName || tx.walkInClient }}</td>
                <td class="p-4 text-center tabular-nums">{{ formatNumber(toNumber(item.Quantity ?? item.quantity)) }}</td>
                <td class="p-4 text-slate-500">{{ item.UnitName || item.unitName || '-' }}</td>
                <td class="p-4 text-right tabular-nums">{{ formatNumber(item.UnitPrice ?? item.unitPrice) }}</td>
                <td class="p-4 text-right text-amber-600 tabular-nums">-{{ formatNumber(item.DiscountAmount ?? item.discountAmount) }}</td>
                <td class="p-4 text-right font-normal text-slate-900 tabular-nums bg-slate-50/30">
                  {{ formatNumber(item.LineTotal ?? item.lineTotal) }}
                </td>
              </tr>
              <tr v-if="filteredSaleItems.length === 0">
                <td colspan="9" class="p-8 text-center text-slate-400 text-[10px] uppercase tracking-widest">
                  {{ tx.awaitingData }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div v-else class="flex-1 overflow-x-auto">
          <table class="w-full text-left border-collapse min-w-[760px]">
            <thead class="bg-[#3f3f46] text-white">
              <tr class="text-[10px] font-normal uppercase tracking-[0.2em] text-white">
                <th class="p-4 w-12 text-center opacity-70 text-white">Ref</th>
                <th class="p-4 text-white">{{ tx.collectionDate }}</th>
                <th class="p-4 text-white">{{ tx.voucher }}</th>
                <th class="p-4 text-white">{{ tx.customer }}</th>
                <th class="p-4 text-white">{{ tx.method }}</th>
                <th class="p-4 text-right text-white">{{ tx.collected }}</th>
                <th class="p-4 text-right text-white">{{ tx.remaining }}</th>
              </tr>
            </thead>
            <tbody class="text-xs font-normal text-slate-600 divide-y divide-slate-50">
              <tr v-for="(p, i) in paginatedCollections" :key="p.PaymentId || p.paymentId" class="hover:bg-emerald-50/30 transition-colors group">
                <td class="p-4 text-center text-slate-300 font-mono text-[10px]">
                  {{ (currentPage - 1) * itemsPerPage + (i + 1) }}
                </td>
                <td class="p-4 text-slate-500 tabular-nums">{{ formatDateTime(p.PaymentDate || p.paymentDate) }}</td>
                <td class="p-4 font-normal text-blue-600 tabular-nums tracking-tighter">{{ p.VoucherNo || p.voucherNo }}</td>
                <td class="p-4 text-slate-900 font-normal italic">{{ p.CustomerName || p.customerName || tx.walkInClient }}</td>
                <td class="p-4 text-slate-700">{{ p.PaymentMethod || p.paymentMethod || '-' }}</td>
                <td class="p-4 text-right font-normal text-emerald-600 tabular-nums">{{ formatNumber(p.Amount || p.amount) }}</td>
                <td class="p-4 text-right text-rose-600 tabular-nums">{{ formatNumber(p.RemainingDue || p.remainingDue) }}</td>
              </tr>
              <tr v-if="collections.length === 0">
                <td colspan="7" class="p-8 text-center text-slate-400 text-[10px] uppercase tracking-widest">
                  {{ tx.noCollections }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="p-4 bg-slate-50 border-t border-slate-200 flex items-center justify-between">
          <p class="text-[9px] font-normal text-slate-400 uppercase tracking-widest">
            {{ tx.showing }} {{ activeRows.length ? ((currentPage - 1) * itemsPerPage + 1) : 0 }} {{ tx.to }} {{ Math.min(currentPage * itemsPerPage, activeRows.length)
            }} {{ tx.of }} {{ activeRows.length }}
          </p>
          <div class="flex gap-1.5">
            <button @click="currentPage--" :disabled="currentPage === 1"
              class="px-4 py-1.5 bg-white border border-slate-200 rounded-lg font-normal text-[9px] uppercase tracking-widest shadow-sm disabled:opacity-30 hover:bg-slate-50 transition-all">{{ tx.prev }}</button>
            <button @click="currentPage++" :disabled="currentPage >= totalPages"
              class="px-4 py-1.5 bg-blue-600 text-white rounded-lg font-normal text-[9px] uppercase tracking-widest shadow-lg shadow-blue-100 disabled:opacity-30 hover:bg-blue-700 transition-all">{{ tx.next }}</button>
          </div>
        </div>
      </div>

      <div class="bg-white p-6 rounded-xl border border-slate-200 shadow-sm flex flex-col">
        <h3 class="font-normal text-[10px] uppercase text-slate-500 tracking-[0.2em] mb-4 text-center">
          {{ reportView === 'item' ? tx.soldItemsByName : tx.settlementChannels }}
        </h3>

        <div v-if="reportView === 'item'" class="flex-1 min-h-[420px] overflow-y-auto custom-scrollbar pr-1 space-y-3">
          <div v-if="soldItemBars.length === 0" class="h-full flex flex-col items-center justify-center opacity-20 grayscale">
            <svg class="w-10 h-10 mb-2 text-slate-500" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">`n              <path stroke-linecap="round" stroke-linejoin="round" d="M4 19V5M4 19h16M8 16h2M12 13h2M16 9h2" />`n            </svg>
            <p class="text-[10px] font-normal uppercase tracking-widest">{{ tx.awaitingData }}</p>
          </div>

          <button v-for="item in soldItemBars" :key="item.key" type="button" @click="toggleItemFilter(item)"
            :class="selectedItemKey === item.key ? 'bg-blue-50 border-blue-200 shadow-sm' : 'border-transparent hover:bg-slate-50 hover:border-slate-200'"
            class="w-full text-left p-2.5 border rounded-lg space-y-1.5 transition-colors"
            :aria-pressed="selectedItemKey === item.key" :title="item.name">
            <div class="flex items-start justify-between gap-3">
              <div class="min-w-0">
                <div :class="selectedItemKey === item.key ? 'text-blue-700' : 'text-slate-700'"
                  class="text-[11px] font-normal truncate">{{ item.name }} <span class="text-[9px] text-slate-400 font-normal">({{ item.unit }})</span></div>
                <div class="text-[9px] font-normal text-slate-400 uppercase tracking-wider">
                  {{ formatNumber(item.quantity) }} {{ item.unit }} · {{ formatNumber(item.amount) }} Ks
                </div>
              </div>
              <div class="text-[10px] font-normal text-blue-600 tabular-nums shrink-0">
                {{ item.percent }}%
              </div>
            </div>
            <div class="h-2.5 bg-slate-100 rounded-full overflow-hidden">
              <div :class="selectedItemKey === item.key ? 'bg-blue-700' : 'bg-blue-500'"
                class="h-full rounded-full transition-all duration-500" :style="{ width: `${item.percent}%` }"></div>
            </div>
          </button>
        </div>

        <div v-else class="flex-1 min-h-[420px] flex gap-5 items-center">
          <div class="flex-1 min-w-0 h-full relative flex justify-center items-center">
            <Pie v-if="chartData.datasets[0].data.length > 0" :data="chartData" :options="chartOptions" />
            <div v-else class="flex flex-col items-center justify-center h-full opacity-20 grayscale">
              <svg class="w-10 h-10 mb-2 text-slate-500" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">`n                <circle cx="12" cy="12" r="9" />`n                <path stroke-linecap="round" stroke-linejoin="round" d="M12 12V7M12 12l4 3" />`n              </svg>
              <p class="text-[10px] font-normal uppercase tracking-widest">{{ tx.awaitingData }}</p>
            </div>
          </div>

          <div v-if="settlementBreakdown.length > 0" class="w-48 shrink-0 h-full overflow-y-auto custom-scrollbar pr-1">
            <div v-for="row in settlementBreakdown" :key="row.label" class="py-2 border-b border-slate-100 last:border-0">
              <div class="text-[10px] font-normal uppercase tracking-widest text-slate-500">{{ row.label }}</div>
              <div class="text-sm font-normal text-slate-900 tabular-nums">
                {{ formatNumber(row.amount) }} <small class="text-[10px] text-slate-400">Ks</small>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>

  <div v-else class="flex flex-col items-center justify-center h-96 space-y-4">
    <div class="w-10 h-10 border-2 border-blue-600 border-t-transparent rounded-full animate-spin"></div>
    <p class="text-[10px] font-normal text-slate-400 uppercase tracking-[0.4em] animate-pulse">{{ tx.analyzingSalesFlows }}</p>
  </div>
</template>

<script setup>
import { ref, onMounted, watch, reactive, computed } from 'vue';
import { Pie } from 'vue-chartjs';
import { Chart as ChartJS, ArcElement, Tooltip, Legend } from 'chart.js';
import api from '../../services/api';
import { useI18n } from '../../composables/useI18n';
import { useToast } from '../../composables/useToast';
import { useCsvExport } from '../../composables/useCsvExport';

ChartJS.register(ArcElement, Tooltip, Legend);

const props = defineProps(['filters']);
const { t } = useI18n();
const { addToast } = useToast();
const { exportToCsv } = useCsvExport();
const summary = ref(null);
const sales = ref([]);
const saleItems = ref([]);
const collections = ref([]);
const settlementRows = ref([]);
const reportView = ref('voucher');
const selectedItemKey = ref('');
const tx = computed(() => ({
  netRevenue: t('reportViews.sale.netRevenue'),
  voucherSavings: t('reportViews.sale.voucherSavings'),
  liquidityFlow: t('reportViews.sale.liquidityFlow'),
  logisticsFees: t('reportViews.sale.logisticsFees'),
  transactionLedger: t('reportViews.sale.transactionLedger'),
  exportCsv: t('reportViews.sale.exportCsv'),
  voucher: t('reportViews.sale.voucher'),
  voucherView: t('reportViews.sale.voucherView'),
  itemView: t('reportViews.sale.itemView'),
  collections: t('reportViews.sale.collections'),
  collectionDate: t('reportViews.sale.collectionDate'),
  item: t('reportViews.sale.item'),
  qty: t('reportViews.sale.qty'),
  unit: t('reportViews.sale.unit'),
  unitPrice: t('reportViews.sale.unitPrice'),
  lineTotal: t('reportViews.sale.lineTotal'),
  customer: t('reportViews.sale.customer'),
  subTotal: t('reportViews.sale.subTotal'),
  netTotal: t('reportViews.sale.netTotal'),
  method: t('reportViews.sale.method'),
  collected: t('reportViews.sale.collected'),
  remaining: t('reportViews.sale.remaining'),
  noCollections: t('reportViews.sale.noCollections'),
  walkInClient: t('reportViews.sale.walkInClient'),
  showing: t('reportViews.sale.showing'),
  to: t('reportViews.sale.to'),
  of: t('reportViews.sale.of'),
  prev: t('reportViews.sale.prev'),
  next: t('reportViews.sale.next'),
  settlementChannels: t('reportViews.sale.settlementChannels'),
  soldItemsByName: t('reportViews.sale.soldItemsByName'),
  clearItemFilter: t('common.clear') || 'Clear',
  awaitingData: t('reportViews.sale.awaitingData'),
  analyzingSalesFlows: t('reportViews.sale.analyzingSalesFlows')
}));

const currentPage = ref(1);
const itemsPerPage = 10;

const toNumber = (value) => {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : 0;
};

const getSummaryAmount = (pascalName) => {
  if (!summary.value) return 0;
  const camelName = pascalName.charAt(0).toLowerCase() + pascalName.slice(1);
  return toNumber(summary.value[pascalName] ?? summary.value[camelName]);
};

const resolvedSummary = computed(() => ({
  NetRevenue: getSummaryAmount('NetRevenue'),
  TotalDiscounts: getSummaryAmount('TotalDiscounts'),
  TotalCollected: getSummaryAmount('TotalCollected'),
  TotalDeliveryFees: getSummaryAmount('TotalDeliveryFees')
}));

const getSaleSubTotal = (sale) => {
  const netSubTotal = sale.NetSubTotal ?? sale.netSubTotal;
  if (netSubTotal !== undefined && netSubTotal !== null) return toNumber(netSubTotal);

  const subTotal = toNumber(sale.SubTotal ?? sale.subTotal);
  const returned = toNumber(sale.ReturnedAmount ?? sale.returnedAmount);
  return Math.max(0, subTotal - returned);
};

const getSaleNetTotal = (sale) => {
  const netTotal = sale.NetTotal ?? sale.netTotal;
  if (netTotal !== undefined && netTotal !== null) return toNumber(netTotal);

  const subTotal = toNumber(sale.SubTotal ?? sale.subTotal);
  const discount = toNumber(sale.DiscountAmount ?? sale.discountAmount);
  const delivery = toNumber(sale.DeliveryFee ?? sale.deliveryFee);
  const returned = toNumber(sale.ReturnedAmount ?? sale.returnedAmount);
  return Math.max(0, subTotal - discount + delivery - returned);
};

const itemBarKey = (item) => {
  const itemName = item.ItemName || item.itemName || '-';
  const unitName = item.UnitName || item.unitName || '-';
  return `${itemName}|||${unitName}`;
};

const filteredSaleItems = computed(() => {
  if (!selectedItemKey.value) return saleItems.value;
  return saleItems.value.filter(item => itemBarKey(item) === selectedItemKey.value);
});

const activeRows = computed(() => {
  if (reportView.value === 'item') return filteredSaleItems.value;
  if (reportView.value === 'collection') return collections.value;
  return sales.value;
});
const totalPages = computed(() => Math.ceil(activeRows.value.length / itemsPerPage) || 1);

const paginatedSales = computed(() => {
  const start = (currentPage.value - 1) * itemsPerPage;
  return sales.value.slice(start, start + itemsPerPage);
});

const paginatedSaleItems = computed(() => {
  const start = (currentPage.value - 1) * itemsPerPage;
  return filteredSaleItems.value.slice(start, start + itemsPerPage);
});

const paginatedCollections = computed(() => {
  const start = (currentPage.value - 1) * itemsPerPage;
  return collections.value.slice(start, start + itemsPerPage);
});

const chartData = reactive({
  labels: [],
  datasets: [{
    backgroundColor: ['#3b82f6', '#10b981', '#f59e0b', '#ef4444', '#6366f1'],
    borderWidth: 0,
    hoverOffset: 15,
    data: []
  }]
});

const chartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: {
      position: 'bottom',
      labels: {
        padding: 20,
        usePointStyle: true,
        font: { size: 10, family: 'Inter', weight: '600' },
        color: '#64748b'
      }
    }
  }
};

const formatNumber = (val) => new Intl.NumberFormat().format(val || 0);

const formatDateTime = (value) => {
  if (!value) return '-';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '-';
  return date.toLocaleString();
};

const formatSaleVoucher = (saleId) => `SA${String(saleId || '').padStart(7, '0')}`;

const normalizeSaleVoucher = (voucherNo, saleId) => {
  if (voucherNo) return String(voucherNo).replace(/^SA-/, 'SA');
  return formatSaleVoucher(saleId);
};

const getPaymentSummary = (sale) => {
  const summary = sale.PaymentSummary ?? sale.paymentSummary;
  if (summary !== undefined && summary !== null) return summary;

  const method = sale.PaymentMethod ?? sale.paymentMethod;
  const paid = toNumber(sale.PaidAmount ?? sale.paidAmount);
  if (!method || paid <= 0) return '';
  return `${method} - ${formatNumber(paid)}`;
};

const getSaleRemaining = (sale) => {
  const netTotal = getSaleNetTotal(sale);
  const paidAmount = toNumber(sale.PaidAmount ?? sale.paidAmount);
  return Math.max(0, netTotal - paidAmount);
};

const settlementBreakdown = computed(() => {
  return settlementRows.value
    .map(row => ({ label: row.label, amount: toNumber(row.amount) }))
    .filter(x => x.amount !== 0);
});

const soldItemBars = computed(() => {
  const grouped = new Map();

  saleItems.value.forEach(item => {
    const itemName = item.ItemName || item.itemName || '-';
    const unitName = item.UnitName || item.unitName || '-';
    // Group by both item name AND unit so different units aren't summed together
    const key = itemBarKey(item);
    const existing = grouped.get(key) || { key, name: itemName, unit: unitName, quantity: 0, amount: 0 };
    existing.quantity += toNumber(item.Quantity ?? item.quantity);
    existing.amount += toNumber(item.LineTotal ?? item.lineTotal);
    grouped.set(key, existing);
  });

  const rows = Array.from(grouped.values())
    .filter(item => item.quantity > 0 || item.amount > 0)
    .sort((a, b) => b.quantity - a.quantity || b.amount - a.amount);

  const maxQty = Math.max(...rows.map(item => item.quantity), 1);
  return rows.map(item => ({
    ...item,
    percent: Math.max(4, Math.round((item.quantity / maxQty) * 100))
  }));
});

const selectedItemBar = computed(() => {
  if (!selectedItemKey.value) return null;
  return soldItemBars.value.find(item => item.key === selectedItemKey.value) || null;
});

const clearItemFilter = () => {
  selectedItemKey.value = '';
  currentPage.value = 1;
};

const toggleItemFilter = (item) => {
  selectedItemKey.value = selectedItemKey.value === item.key ? '' : item.key;
  currentPage.value = 1;
};

const handleExportCsv = () => {
  if (reportView.value === 'item') {
    const headers = [
      t('reportViews.sale.voucher'),
      t('reportViews.sale.item'),
      'SKU',
      t('reportViews.sale.customer'),
      t('reportViews.sale.qty'),
      t('reportViews.sale.unit'),
      t('reportViews.sale.unitPrice'),
      'Discount',
      t('reportViews.sale.lineTotal')
    ];

    const rows = filteredSaleItems.value.map(item => [
      normalizeSaleVoucher(item.VoucherNo || item.voucherNo, item.SaleId || item.saleId),
      item.ItemName || item.itemName || '',
      item.Sku || item.sku || '',
      item.CustomerName || item.customerName || t('reportViews.sale.walkInClient'),
      toNumber(item.Quantity ?? item.quantity),
      item.UnitName || item.unitName || '',
      toNumber(item.UnitPrice ?? item.unitPrice),
      toNumber(item.DiscountAmount ?? item.discountAmount),
      toNumber(item.LineTotal ?? item.lineTotal)
    ]);

    const dateStr = new Date().toISOString().split('T')[0];
    exportToCsv(`Sales_Item_Report_${dateStr}`, headers, rows);
    return;
  }

  if (reportView.value === 'collection') {
    const headers = [
      t('reportViews.sale.collectionDate'),
      t('reportViews.sale.voucher'),
      t('reportViews.sale.customer'),
      t('reportViews.sale.method'),
      t('reportViews.sale.collected'),
      t('reportViews.sale.remaining')
    ];
    const rows = collections.value.map(p => [
      formatDateTime(p.PaymentDate || p.paymentDate),
      p.VoucherNo || p.voucherNo || '',
      p.CustomerName || p.customerName || t('reportViews.sale.walkInClient'),
      p.PaymentMethod || p.paymentMethod || '',
      toNumber(p.Amount ?? p.amount),
      toNumber(p.RemainingDue ?? p.remainingDue)
    ]);

    const dateStr = new Date().toISOString().split('T')[0];
    exportToCsv(`Credit_Collections_${dateStr}`, headers, rows);
    return;
  }

  const headers = [
    t('reportViews.sale.voucher'),
    t('reportViews.sale.customer'),
    'Payment',
    t('reportViews.sale.subTotal'),
    'Discount',
    t('reportViews.sale.netTotal'),
    'Remaining'
  ];

  const rows = sales.value.map(s => [
    formatSaleVoucher(s.SaleId),
    s.CustomerName || t('reportViews.sale.walkInClient'),
    getPaymentSummary(s),
    getSaleSubTotal(s),
    toNumber(s.DiscountAmount ?? s.discountAmount),
    getSaleNetTotal(s),
    getSaleRemaining(s)
  ]);
  
  const dateStr = new Date().toISOString().split('T')[0];
  exportToCsv(`Sales_Report_${dateStr}`, headers, rows);
};

const fetchData = async () => {
  try {
    const params = {
      fromDate: props.filters.fromDate,
      toDate: props.filters.toDate,
      branchId: props.filters.branchId
    };
    const [resSum, resSales, resItems, resCollections, resPayments] = await Promise.all([
      api.get('/Reports/sales-summary', { params }),
      api.get('/Reports/sales-list', { params }),
      api.get('/Reports/sales-items', { params }),
      api.get('/Reports/credit-collections', { params }),
      api.get('/Reports/payment-methods-summary', { params })
    ]);

    summary.value = resSum.data;
    sales.value = resSales.data;
    saleItems.value = resItems.data;
    collections.value = Array.isArray(resCollections.data) ? resCollections.data : [];
    settlementRows.value = Array.isArray(resPayments.data)
      ? resPayments.data.map(p => ({
        label: p.Method ?? p.method ?? 'Unknown',
        amount: toNumber(p.TotalAmount ?? p.totalAmount)
      }))
      : [];
    const positiveSettlementRows = settlementRows.value.filter(row => row.amount > 0);
    chartData.labels = positiveSettlementRows.map(row => row.label);
    chartData.datasets[0].data = positiveSettlementRows.map(row => row.amount);
  } catch (err) {
    console.error("Sales Audit Error:", err);
  }
};

watch(() => props.filters, () => {
  currentPage.value = 1;
  selectedItemKey.value = '';
  fetchData();
}, { deep: true });

watch(reportView, () => {
  currentPage.value = 1;
});

onMounted(fetchData);
</script>

<style scoped>
.tabular-nums {
  font-variant-numeric: tabular-nums;
}
</style>

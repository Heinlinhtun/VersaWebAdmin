<template>
  <div class="space-y-8 animate-in fade-in duration-500">

    <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">

      <div
        class="lg:col-span-2 bg-[#3f3f46] p-8 rounded-xl text-white shadow-xl relative overflow-hidden group border-b-4 border-blue-500">
        <div class="relative z-10 h-full flex flex-col justify-between">
          <div>
            <label class="text-[10px] font-normal uppercase text-slate-400 tracking-[0.2em]">{{ tx.finalBottomLineProfit
              }}</label>
            <div class="text-6xl font-normal mt-2 tracking-tighter tabular-nums flex items-baseline gap-3">
              {{ formatNumber(profitData.FinalNetProfit) }}
              <span class="text-xl font-normal text-slate-400 uppercase">Ks</span>
            </div>
          </div>

          <div class="mt-10 flex flex-wrap gap-3">
            <div class="bg-white/5 px-4 py-2.5 rounded-lg border border-white/10 backdrop-blur-sm">
              <p class="text-[9px] font-normal uppercase text-slate-400 tracking-wider">{{ tx.totalWriteoffs }}</p>
              <p class="text-sm font-normal text-orange-400 tabular-nums">-{{ formatNumber(profitData.TotalDiscounts) }}
                Ks</p>
            </div>
            <div class="bg-white/5 px-4 py-2.5 rounded-lg border border-white/10 backdrop-blur-sm">
              <p class="text-[9px] font-normal uppercase text-slate-400 tracking-wider">{{ tx.totalOverheads }}</p>
              <p class="text-sm font-normal text-rose-400 tabular-nums">-{{ formatNumber(profitData.TotalExpenses) }} Ks
              </p>
            </div>
            <div class="bg-blue-500/10 px-4 py-2.5 rounded-lg border border-blue-500/20 backdrop-blur-sm">
              <p class="text-[9px] font-normal uppercase text-blue-300 tracking-wider">{{ tx.netProfitMargin }}</p>
              <p class="text-sm font-normal text-blue-400 tabular-nums">{{ calculateMargin(profitData.FinalNetProfit,
                profitData.SalesTotal) }}%</p>
            </div>
          </div>
        </div>
        <div class="absolute -right-8 -bottom-8 opacity-5 group-hover:opacity-10 transition-opacity">
          <svg class="w-64 h-64" fill="currentColor" viewBox="0 0 24 24">
            <path
              d="M7 15h2V9H7v6zm3 0h2V7h-2v8zm3 0h2v-4h-2v4zm3 0h2v-2h-2v2zm4-12H4c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 16H4V5h16v14z" />
          </svg>
        </div>
      </div>

      <div
        class="bg-white p-8 rounded-xl border border-slate-200 shadow-sm relative overflow-hidden flex flex-col justify-between">
        <div>
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-5">
            <div class="bg-blue-50/70 border border-blue-100 rounded-lg p-3">
              <p class="text-[9px] font-normal text-blue-500 uppercase tracking-wider mb-1">{{ tx.grossRevenue }}</p>
              <p class="text-sm font-normal text-blue-700 tabular-nums">
                {{ formatNumber(profitData.GrossSalesTotal) }} <span class="text-[10px] opacity-60 uppercase">Ks</span>
              </p>
            </div>
            <div class="bg-amber-50/70 border border-amber-100 rounded-lg p-3">
              <p class="text-[9px] font-normal text-amber-500 uppercase tracking-wider mb-1">{{ tx.returnedRevenue }}</p>
              <p class="text-sm font-normal text-amber-700 tabular-nums">
                {{ formatNumber(profitData.ReturnedRevenue) }} <span class="text-[10px] opacity-60 uppercase">Ks</span>
              </p>
            </div>
          </div>
         

          <div class="mt-6 space-y-2.5">
            <div class="flex items-center justify-between gap-3">
              <p class="text-[10px] font-normal uppercase text-slate-400 tracking-[0.2em]">{{ tx.calculationGuide }}</p>
              <button
                type="button"
                @click="showQuickCheck = !showQuickCheck"
                class="text-[10px] font-normal uppercase tracking-[0.2em] text-blue-600 hover:text-blue-700 transition-colors"
              >
                {{ showQuickCheck ? tx.hideQuickCheck : tx.showQuickCheck }}
              </button>
            </div>
            <div v-if="showQuickCheck" class="rounded-lg border border-slate-200 bg-slate-50/80 px-3 py-2.5">
              <div class="flex items-center justify-between gap-3 text-[10px] uppercase tracking-wider text-slate-500">
                <span>{{ tx.returnedRevenueFormula }}</span>
                <span class="font-normal text-slate-700 tabular-nums">
                  {{ formatNumber(profitData.GrossSalesTotal) }} - {{ formatNumber(profitData.ReturnedRevenue) }} = {{ formatNumber(profitData.SalesTotal) }}
                </span>
              </div>
            </div>
            <div v-if="showQuickCheck" class="rounded-lg border border-slate-200 bg-slate-50/80 px-3 py-2.5">
              <div class="flex items-center justify-between gap-3 text-[10px] uppercase tracking-wider text-slate-500">
                <span>{{ tx.grossProfitFormula }}</span>
                <span class="font-normal text-slate-700 tabular-nums">
                  {{ formatNumber(profitData.SalesTotal) }} - {{ formatNumber(profitData.TotalCost) }} = {{ formatNumber(profitData.GrossProfit) }}
                </span>
              </div>
            </div>
            <div v-if="showQuickCheck" class="rounded-lg border border-indigo-100 bg-indigo-50/70 px-3 py-2.5">
              <div class="flex items-center justify-between gap-3 text-[10px] uppercase tracking-wider text-indigo-600">
                <span>{{ tx.consignmentProfitFormula }}</span>
                <span class="font-normal text-indigo-700 tabular-nums">
                  {{ formatNumber(profitData.ConsignmentRevenue) }} - {{ formatNumber(profitData.ConsignmentCost) }} = {{ formatNumber(profitData.ConsignmentProfit) }}
                </span>
              </div>
            </div>
            <div v-if="showQuickCheck" class="rounded-lg border border-slate-200 bg-slate-50/80 px-3 py-2.5">
              <div class="flex items-center justify-between gap-3 text-[10px] uppercase tracking-wider text-slate-500">
                <span>{{ tx.finalProfitFormula }}</span>
                <span class="font-normal text-slate-700 tabular-nums">
                  {{ formatNumber(profitData.GrossProfit) }} - {{ formatNumber(profitData.TotalDiscounts) }} - {{ formatNumber(profitData.TotalExpenses) }} = {{ formatNumber(profitData.FinalNetProfit) }}
                </span>
              </div>
            </div>
          </div>
        </div>

         <label class="text-[10px] mt-8 font-normal uppercase text-slate-400 tracking-[0.2em]">{{ tx.grossProfitAfterCogs
            }}</label>
          <div class="text-3xl font-normal mt-2 tabular-nums text-slate-900 leading-tight">
            {{ formatNumber(profitData.GrossProfit) }}
            <span class="text-sm font-normal text-slate-400">KS</span>
          </div>
        <div class="mt-8">
          <div class="flex justify-between items-end mb-2.5">
            <span class="text-[10px] font-normal uppercase text-slate-500 tracking-widest">{{ tx.cogsWeightage }}</span>
            <span class="text-lg font-normal text-blue-600 tabular-nums">
              {{ calculateMargin(profitData.TotalCost, profitData.SalesTotal) }}%
            </span>
          </div>
          <div class="w-full bg-slate-100 h-2.5 rounded-full overflow-hidden border border-slate-200/50 p-0.5">
            <div
              class="bg-blue-600 h-full rounded-full transition-all duration-1000 ease-out shadow-[0_0_8px_rgba(37,99,235,0.4)]"
              :style="{ width: calculateMargin(profitData.TotalCost, profitData.SalesTotal) + '%' }">
            </div>
          </div>
        </div>
      </div>
    </div>

        <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-6">
      <div class="bg-white p-5 rounded-xl border border-slate-200 shadow-sm flex items-center gap-4 group">
        <div class="bg-blue-50 text-blue-600 p-3.5 rounded-lg group-hover:scale-110 transition-transform">
          <svg class="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 3v18m4-14H9.5a2.5 2.5 0 000 5H14a2.5 2.5 0 010 5H8" />
          </svg>
        </div>
        <div>
          <p class="text-[9px] font-normal text-slate-400 uppercase tracking-widest leading-none mb-1">{{ tx.grossRevenue }}</p>
          <p class="text-lg font-normal text-slate-900 tabular-nums">{{ formatNumber(profitData.GrossSalesTotal) }}</p>
        </div>
      </div>

      <div class="bg-white p-5 rounded-xl border border-amber-100 shadow-sm flex items-center gap-4 group">
        <div class="bg-amber-50 text-amber-600 p-3.5 rounded-lg group-hover:scale-110 transition-transform">
          <svg class="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 8v8m4-4H8" />
            <path stroke-linecap="round" stroke-linejoin="round" d="M4 12a8 8 0 1016 0 8 8 0 10-16 0" />
          </svg>
        </div>
        <div>
          <p class="text-[9px] font-normal text-amber-500 uppercase tracking-widest leading-none mb-1">{{ tx.returnedRevenue }}</p>
          <p class="text-lg font-normal text-slate-900 tabular-nums">{{ formatNumber(profitData.ReturnedRevenue) }}</p>
        </div>
      </div>

      <div class="bg-white p-5 rounded-xl border border-slate-200 shadow-sm flex items-center gap-4 group">
        <div class="bg-blue-50 text-blue-600 p-3.5 rounded-lg group-hover:scale-110 transition-transform">
          <svg class="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 3v18m4-14H9.5a2.5 2.5 0 000 5H14a2.5 2.5 0 010 5H8" />
          </svg>
        </div>
        <div>
          <p class="text-[9px] font-normal text-slate-400 uppercase tracking-widest leading-none mb-1">{{ tx.totalRevenue }}</p>
          <p class="text-lg font-normal text-slate-900 tabular-nums">{{ formatNumber(profitData.SalesTotal) }}</p>
        </div>
      </div>

      <button type="button" @click="openCogsDetails"
        class="bg-white p-5 rounded-xl border border-slate-200 shadow-sm flex items-center gap-4 group text-left w-full hover:shadow-md transition-shadow cursor-pointer focus:outline-none focus:ring-2 focus:ring-blue-500/30">
        <div class="bg-slate-100 text-slate-600 p-3.5 rounded-lg group-hover:scale-110 transition-transform">
          <svg class="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
            <path stroke-linecap="round" stroke-linejoin="round" d="M4 7l8-4 8 4-8 4-8-4zM4 7v10l8 4 8-4V7M12 11v10" />
          </svg>
        </div>
        <div>
          <p class="text-[9px] font-normal text-slate-400 uppercase tracking-widest leading-none mb-1">{{ tx.totalCogs }}</p>
          <p class="text-lg font-normal text-slate-900 tabular-nums">{{ formatNumber(profitData.TotalCost) }}</p>
        </div>
      </button>

      <div class="bg-white p-5 rounded-xl border border-rose-100 shadow-sm flex items-center gap-4 group">
        <div class="bg-rose-50 text-rose-600 p-3.5 rounded-lg group-hover:scale-110 transition-transform">
          <svg class="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
            <path stroke-linecap="round" stroke-linejoin="round" d="M3 7h18v10H3z" />
            <circle cx="12" cy="12" r="2.5" />
            <path stroke-linecap="round" d="M7 10h.01M17 14h.01" />
          </svg>
        </div>
        <div>
          <p class="text-[9px] font-normal text-rose-400 uppercase tracking-widest leading-none mb-1">{{ tx.expenditures }}</p>
          <p class="text-lg font-normal text-slate-900 tabular-nums">{{ formatNumber(profitData.TotalExpenses) }}</p>
        </div>
      </div>

      <div class="bg-white p-5 rounded-xl border border-orange-100 shadow-sm flex items-center gap-4 group">
        <div class="bg-orange-50 text-orange-600 p-3.5 rounded-lg group-hover:scale-110 transition-transform">
          <svg class="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
            <path stroke-linecap="round" stroke-linejoin="round" d="M20.5 13.5L12 22l-9-9V4h9l8.5 8.5z" />
            <circle cx="8" cy="8" r="1.5" />
          </svg>
        </div>
        <div>
          <p class="text-[9px] font-normal text-orange-400 uppercase tracking-widest leading-none mb-1">{{ tx.voucherDiscounts }}</p>
          <p class="text-lg font-normal text-slate-900 tabular-nums">{{ formatNumber(profitData.TotalDiscounts) }}</p>
        </div>
      </div>

      <div class="bg-white p-5 rounded-xl border border-indigo-100 shadow-sm flex items-center gap-4 group">
        <div class="bg-indigo-50 text-indigo-600 p-3.5 rounded-lg group-hover:scale-110 transition-transform">
          <svg class="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
            <path stroke-linecap="round" stroke-linejoin="round" d="M4 7h16M6 7v12h12V7M9 7V5h6v2" />
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 12h6" />
          </svg>
        </div>
        <div>
          <p class="text-[9px] font-normal text-indigo-500 uppercase tracking-widest leading-none mb-1">{{ tx.consignmentProfit }}</p>
          <p class="text-lg font-normal text-slate-900 tabular-nums">{{ formatNumber(profitData.ConsignmentProfit) }}</p>
        </div>
      </div>

      <div class="bg-white p-5 rounded-xl border border-indigo-100 shadow-sm flex items-center gap-4 group">
        <div class="bg-indigo-50 text-indigo-600 p-3.5 rounded-lg group-hover:scale-110 transition-transform">
          <svg class="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 3v18m4-14H9.5a2.5 2.5 0 000 5H14a2.5 2.5 0 010 5H8" />
          </svg>
        </div>
        <div>
          <p class="text-[9px] font-normal text-indigo-500 uppercase tracking-widest leading-none mb-1">{{ tx.consignmentPayoutDue }}</p>
          <p class="text-lg font-normal text-slate-900 tabular-nums">{{ formatNumber(profitData.ConsignmentPayoutDue) }}</p>
        </div>
      </div>
    </div>
    <div class="bg-white p-8 rounded-xl border border-slate-200 shadow-sm">
      <div class="flex items-center justify-between mb-10">
        <h3 class="font-normal text-[10px] uppercase text-slate-500 tracking-[0.3em]">{{ tx.geographicBranchPerformance
          }}</h3>
        <span class="text-[10px] font-normal text-blue-600 bg-blue-50 px-2 py-1 rounded">{{ tx.rankedByNetMargin
          }}</span>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-10">
        <div v-for="b in branchComparison" :key="b.BranchName || b.branchName" class="relative group">
          <div class="flex justify-between items-center mb-4">
            <div>
              <div class="font-normal text-slate-800 uppercase text-xs tracking-tight">
                {{ b.BranchName || b.branchName }}
              </div>
              <div class="text-[9px] font-normal text-slate-400 uppercase tracking-widest">
                {{ b.TotalInvoices || b.totalInvoices || 0 }} {{ tx.transactions }}
              </div>
            </div>
            <div class="text-right">
              <span class="text-blue-600 font-normal tabular-nums text-sm">
                {{ formatNumber(b.TotalNetProfit || b.totalNetProfit) }}
              </span>
              <p class="text-[8px] font-normal text-slate-400 uppercase leading-none mt-0.5">{{ tx.netProfit }}</p>
            </div>
          </div>

          <div class="space-y-1.5">
            <div class="w-full bg-slate-100 h-1.5 rounded-full overflow-hidden">
              <div class="bg-slate-800 h-full rounded-full transition-all duration-1000 group-hover:bg-blue-600"
                :style="{ width: getBranchPercentage(b.TotalNetProfit || b.totalNetProfit) + '%' }">
              </div>
            </div>
          </div>
        </div>
      </div>

      <div v-if="branchComparison.length === 0" class="py-20 text-center grayscale opacity-20">
        <p class="font-normal uppercase tracking-[0.3em] text-[10px] text-slate-900">{{ tx.awaitingMultiBranchData }}
        </p>
      </div>
    </div>

    <div v-if="showCogsModal" class="fixed inset-0 z-50 bg-slate-950/60 backdrop-blur-sm flex items-center justify-center p-4">
      <div class="bg-white w-full max-w-6xl rounded-2xl shadow-2xl border border-slate-200 overflow-hidden">
        <div class="flex items-center justify-between px-6 py-4 border-b border-slate-100">
          <div>
            <h3 class="text-base font-normal text-slate-900">Item-level COGS</h3>
            <p class="text-xs text-slate-500">Shows the exact cost source used for each sold line.</p>
          </div>
          <button type="button" @click="showCogsModal = false"
            class="text-slate-400 hover:text-slate-700 transition-colors text-2xl leading-none">
            x
          </button>
        </div>

        <div class="px-6 py-4 bg-slate-50 border-b border-slate-100 flex items-center justify-between gap-3">
          <div class="text-sm text-slate-600">
            <span class="font-normal text-slate-900">{{ formatNumber(modalTotalCogs) }}</span> total COGS
          </div>
          <div class="text-xs text-slate-500 uppercase tracking-widest">
            {{ modalRows.length }} item lines
          </div>
        </div>

        <div class="max-h-[70vh] overflow-auto">
          <table class="w-full text-left text-sm">
            <thead class="sticky top-0 bg-slate-900 text-white text-[10px] uppercase tracking-widest">
              <tr>
                <th class="px-4 py-3 font-normal">Date</th>
                <th class="px-4 py-3 font-normal">Voucher</th>
                <th class="px-4 py-3 font-normal">Item</th>
                <th class="px-4 py-3 font-normal">Type</th>
                <th class="px-4 py-3 font-normal text-right">Qty</th>
                <th class="px-4 py-3 font-normal text-right">Revenue</th>
                <th class="px-4 py-3 font-normal text-right">COGS</th>
                <th class="px-4 py-3 font-normal text-right">Profit</th>
                <th class="px-4 py-3 font-normal">Cost Source</th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="isLoadingCogs" class="border-b border-slate-100">
                <td colspan="9" class="px-4 py-10 text-center text-slate-500">Loading item COGS...</td>
              </tr>
              <tr v-else v-for="row in modalRows" :key="row.SaleItemId || row.saleItemId" class="border-b border-slate-100 hover:bg-slate-50">
                <td class="px-4 py-3 text-slate-500">{{ formatDate(row.SaleDate || row.saleDate) }}</td>
                <td class="px-4 py-3 text-blue-600">{{ row.VoucherNo || row.voucherNo }}</td>
                <td class="px-4 py-3 text-slate-900">{{ row.ItemName || row.itemName }}</td>
                <td class="px-4 py-3 text-slate-600">{{ row.ItemTypeLabel || row.itemTypeLabel }}</td>
                <td class="px-4 py-3 text-right tabular-nums">{{ formatNumber(getAmount(row, 'Quantity')) }}</td>
                <td class="px-4 py-3 text-right tabular-nums">{{ formatNumber(getAmount(row, 'Revenue')) }}</td>
                <td class="px-4 py-3 text-right tabular-nums">{{ formatNumber(getAmount(row, 'COGS')) }}</td>
                <td class="px-4 py-3 text-right tabular-nums">{{ formatNumber(getAmount(row, 'ItemProfit')) }}</td>
                <td class="px-4 py-3 text-slate-500">{{ row.CostSource || row.costSource }}</td>
              </tr>
              <tr v-if="!isLoadingCogs && modalRows.length === 0">
                <td colspan="9" class="px-4 py-10 text-center text-slate-500">No sold items found for the selected range.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, watch, computed } from 'vue';
import api from '../../services/api';
import { useI18n } from '../../composables/useI18n';

const props = defineProps(['filters']);
const { t } = useI18n();
const showQuickCheck = ref(true);
const showCogsModal = ref(false);
const isLoadingCogs = ref(false);
const modalRows = ref([]);
const modalTotalCogs = computed(() => modalRows.value.reduce((sum, row) => sum + getAmount(row, 'COGS'), 0));
const profitData = ref({
  NetProfit: 0,
  SalesTotal: 0,
  ItemSoldValue: 0,
  ServiceRevenue: 0,
  GrossSalesTotal: 0,
  GrossItemSoldValue: 0,
  GrossServiceRevenue: 0,
  ReturnedRevenue: 0,
  ReturnedItemRevenue: 0,
  ReturnedServiceRevenue: 0,
  TotalCost: 0,
  ReturnedCost: 0,
  NetCost: 0,
  TotalDiscounts: 0,
  GrossProfit: 0,
  FinalNetProfit: 0,
  TotalExpenses: 0,
  ConsignmentRevenue: 0,
  ConsignmentCost: 0,
  ConsignmentProfit: 0,
  ConsignmentPayoutDue: 0
});
const branchComparison = ref([]);
const tx = computed(() => ({
  finalBottomLineProfit: t('reportViews.profit.finalBottomLineProfit'),
  totalWriteoffs: t('reportViews.profit.totalWriteoffs'),
  totalOverheads: t('reportViews.profit.totalOverheads'),
  netProfitMargin: t('reportViews.profit.netProfitMargin'),
  productRevenue: t('reportViews.profit.productRevenue'),
  serviceRevenue: t('reportViews.profit.serviceRevenue'),
  grossProfitAfterCogs: t('reportViews.profit.grossProfitAfterCogs'),
  calculationGuide: t('reportViews.profit.calculationGuide'),
  showQuickCheck: t('reportViews.profit.showQuickCheck'),
  hideQuickCheck: t('reportViews.profit.hideQuickCheck'),
  revenueFormula: t('reportViews.profit.revenueFormula'),
  returnedRevenueFormula: t('reportViews.profit.returnedRevenueFormula'),
  grossProfitFormula: t('reportViews.profit.grossProfitFormula'),
  finalProfitFormula: t('reportViews.profit.finalProfitFormula'),
  cogsWeightage: t('reportViews.profit.cogsWeightage'),
  totalRevenue: t('reportViews.profit.totalRevenue'),
  grossRevenue: t('reportViews.profit.grossRevenue'),
  returnedRevenue: t('reportViews.profit.returnedRevenue'),
  totalCogs: t('reportViews.profit.totalCogs'),
  expenditures: t('reportViews.profit.expenditures'),
  voucherDiscounts: t('reportViews.profit.voucherDiscounts'),
  geographicBranchPerformance: t('reportViews.profit.geographicBranchPerformance'),
  rankedByNetMargin: t('reportViews.profit.rankedByNetMargin'),
  transactions: t('reportViews.profit.transactions'),
  netProfit: t('reportViews.profit.netProfit'),
  awaitingMultiBranchData: t('reportViews.profit.awaitingMultiBranchData'),
  consignmentProfit: t('reportViews.profit.consignmentProfit'),
  consignmentPayoutDue: t('reportViews.profit.consignmentPayoutDue'),
  consignmentProfitFormula: t('reportViews.profit.consignmentProfitFormula')
}));

const asNumber = (val) => {
  const n = Number(val);
  return Number.isFinite(n) ? n : 0;
};

const pickAmount = (obj, key) => {
  if (!obj) return 0;
  const camel = key.charAt(0).toLowerCase() + key.slice(1);
  return asNumber(obj[key] ?? obj[camel]);
};

const getAmount = (obj, key) => {
  if (!obj) return 0;
  const camel = key.charAt(0).toLowerCase() + key.slice(1);
  return asNumber(obj[key] ?? obj[camel]);
};

const formatDate = (value) => {
  if (!value) return '-';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '-';
  return date.toLocaleDateString();
};

const openCogsDetails = async () => {
  showCogsModal.value = true;
  isLoadingCogs.value = true;
  modalRows.value = [];

  try {
    const res = await api.get('/Reports/profit-item-details', { params: props.filters });
    modalRows.value = Array.isArray(res.data) ? res.data : [];
  } catch (err) {
    console.error('Profit item COGS error:', err);
  } finally {
    isLoadingCogs.value = false;
  }
};

const fetchData = async () => {
  try {
    const resProfit = await api.get('/Reports/profit-report', { params: props.filters });
    const payload = resProfit.data || {};
    profitData.value = {
      NetProfit: pickAmount(payload, 'NetProfit'),
      SalesTotal: pickAmount(payload, 'SalesTotal'),
      ItemSoldValue: pickAmount(payload, 'ItemSoldValue'),
      ServiceRevenue: pickAmount(payload, 'ServiceRevenue'),
      GrossSalesTotal: pickAmount(payload, 'GrossSalesTotal'),
      GrossItemSoldValue: pickAmount(payload, 'GrossItemSoldValue'),
      GrossServiceRevenue: pickAmount(payload, 'GrossServiceRevenue'),
      ReturnedRevenue: pickAmount(payload, 'ReturnedRevenue'),
      ReturnedItemRevenue: pickAmount(payload, 'ReturnedItemRevenue'),
      ReturnedServiceRevenue: pickAmount(payload, 'ReturnedServiceRevenue'),
      TotalCost: pickAmount(payload, 'TotalCost'),
      ReturnedCost: pickAmount(payload, 'ReturnedCost'),
      NetCost: pickAmount(payload, 'NetCost'),
      TotalDiscounts: pickAmount(payload, 'TotalDiscounts'),
      GrossProfit: pickAmount(payload, 'GrossProfit'),
      FinalNetProfit: pickAmount(payload, 'FinalNetProfit'),
      TotalExpenses: pickAmount(payload, 'TotalExpenses'),
      ConsignmentRevenue: pickAmount(payload, 'ConsignmentRevenue'),
      ConsignmentCost: pickAmount(payload, 'ConsignmentCost'),
      ConsignmentProfit: pickAmount(payload, 'ConsignmentProfit'),
      ConsignmentPayoutDue: pickAmount(payload, 'ConsignmentPayoutDue')
    };

    const resBranch = await api.get('/Reports/branch-comparison', { params: props.filters });
    branchComparison.value = Array.isArray(resBranch.data) ? resBranch.data : [];
  } catch (err) {
    console.error("Profit Audit Error:", err);
  }
};

const formatNumber = (val) => new Intl.NumberFormat().format(val || 0);

const calculateMargin = (profit, sales) => {
  if (!sales || sales === 0) return 0;
  return ((profit / sales) * 100).toFixed(1);
};

const getBranchPercentage = (val) => {
  if (!branchComparison.value || branchComparison.value.length === 0) return 0;

  // Array á€‘á€²á€€ á€¡á€™á€¼á€á€ºá€¡á€™á€»á€¬á€¸á€†á€¯á€¶á€¸á€€á€­á€¯ á€›á€¾á€¬á€žá€Šá€·á€ºá€¡á€á€« á€¡á€€á€¹á€á€›á€¬ á€”á€¾á€…á€ºá€™á€»á€­á€¯á€¸á€œá€¯á€¶á€¸á€€á€­á€¯ á€…á€…á€ºá€›á€”á€º
  const profits = branchComparison.value.map(b => b.TotalNetProfit || b.totalNetProfit || 0);
  const max = Math.max(...profits, 1);

  const currentVal = Number(val) || 0;
  return (currentVal / max) * 100;
};

watch(() => props.filters, fetchData, { deep: true });
onMounted(fetchData);
</script>

<style scoped>
.tabular-nums {
  font-variant-numeric: tabular-nums;
}
</style>

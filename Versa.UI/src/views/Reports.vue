<template>
  <div class="w-full max-w-full overflow-x-hidden px-3 sm:px-5 lg:px-8 pt-2 pb-6 sm:pb-8 bg-[#f8fafc] min-h-screen font-sans text-left">
    <div
      class="flex flex-col xl:flex-row xl:items-center xl:justify-between gap-4 sm:gap-6 mb-5 sm:mb-8 bg-white p-4 sm:p-5 rounded-xl shadow-sm border border-slate-200 min-w-0">
      <div class="min-w-0">
        <h1 class="text-xl sm:text-2xl font-normal text-slate-900 tracking-tight">{{ tx.title }}</h1>
        <p class="text-[10px] sm:text-[11px] font-normal text-slate-400 uppercase tracking-widest mt-1 flex items-start sm:items-center leading-relaxed">
          <span class="w-2 h-2 bg-blue-500 rounded-full mr-2 shadow-[0_0_8px_rgba(59,130,246,0.5)]"></span>
          <span class="min-w-0">{{ tx.subtitle }}</span>
        </p>
      </div>

      <div class="flex flex-col sm:flex-row sm:items-center gap-2 sm:gap-3 bg-slate-50 p-2 rounded-lg border border-slate-200 shadow-inner w-full xl:w-auto min-w-0">
        <div class="flex items-center justify-between sm:justify-start gap-2 px-3 py-2 bg-white sm:bg-transparent rounded-md sm:rounded-none min-w-0 sm:w-52 xl:w-auto">
          <span class="text-[10px] font-normal text-slate-400 uppercase shrink-0">{{ tx.branch }}</span>
          <select v-model="selectedBranchId" :disabled="!isAdmin"
            class="min-w-0 w-full bg-white text-slate-700 outline-none font-normal text-xs cursor-pointer rounded px-2 py-1 border border-slate-200 disabled:text-slate-500 disabled:cursor-not-allowed">
            <option v-if="isAdmin" value="">{{ tx.allBranches }}</option>
            <option
              v-for="b in branches"
              :key="b.BranchId ?? b.branchId"
              :value="String(b.BranchId ?? b.branchId)"
              class="text-slate-800 bg-white">
              {{ b.BranchName ?? b.branchName }}
            </option>
          </select>
        </div>
        <div class="hidden sm:block h-4 w-[1px] bg-slate-300 self-center -ml-2"></div>
        <div class="flex items-center justify-between sm:justify-start gap-2 px-3 py-2 bg-white sm:bg-transparent rounded-md sm:rounded-none min-w-0 sm:w-44 xl:w-auto">
          <span class="text-[10px] font-normal text-slate-400 uppercase shrink-0">{{ tx.from }}</span>
          <input v-model="filters.fromDate" type="date"
            class="min-w-0 w-full bg-transparent outline-none font-normal text-blue-600 text-xs cursor-pointer">
        </div>
        <div class="hidden sm:block h-4 w-[1px] bg-slate-300 self-center -ml-2"></div>
        <div class="flex items-center justify-between sm:justify-start gap-2 px-3 py-2 bg-white sm:bg-transparent rounded-md sm:rounded-none min-w-0 sm:w-44 xl:w-auto">
          <span class="text-[10px] font-normal text-slate-400 uppercase shrink-0">{{ tx.to }}</span>
          <input v-model="filters.toDate" type="date"
            class="min-w-0 w-full bg-transparent outline-none font-normal text-blue-600 text-xs cursor-pointer">
        </div>
      </div>
    </div>

    <div class="-mx-3 sm:mx-0 mb-5 sm:mb-8 overflow-x-auto px-3 sm:px-0 report-tab-scroll">
      <div class="inline-flex min-w-max items-center p-1 bg-slate-200/60 rounded-xl border border-slate-200 transition-all">
        <button v-for="tab in reportTabs" :key="tab.id" @click="activeTab = tab.id" :class="activeTab === tab.id
          ? 'bg-white text-slate-900 shadow-sm border-slate-200'
          : 'text-slate-500 hover:text-slate-700 border-transparent'"
          class="flex shrink-0 items-center gap-2 px-4 sm:px-6 py-2.5 rounded-lg border transition-all duration-300">
          <span class="text-sm opacity-80">{{ tab.icon }}</span>
          <span class="font-normal text-[10px] uppercase tracking-wider whitespace-nowrap">{{ tab.name }}</span>
        </button>
      </div>
    </div>

    <div
      class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden min-h-[520px] sm:min-h-[600px] animate-in fade-in duration-500 min-w-0">
      <div class="bg-[#3f3f46] px-4 sm:px-6 py-3 flex items-center justify-between border-b border-slate-700 min-w-0">
        <span class="text-[10px] font-normal text-white uppercase tracking-[0.16em] sm:tracking-[0.2em] flex items-center gap-2 min-w-0">
          <span class="opacity-50 tracking-normal shrink-0">{{ activeReportTab.icon }}</span>
          <span class="truncate">{{ activeReportTab.name }} {{ tx.analysis }}</span>
        </span>
      </div>

      <div class="p-3 sm:p-5 lg:p-8 min-w-0 overflow-x-hidden">
        <div v-if="activeTab === 'sales'">
          <SaleReport :key="reportRefreshKey" :filters="filters" />
        </div>
        <div v-if="activeTab === 'purchase'">
          <PurchaseReport :key="reportRefreshKey" :filters="filters" />
        </div>
        <div v-if="activeTab === 'consignment'">
          <ConsignmentReport :key="reportRefreshKey" :filters="filters" />
        </div>
        <div v-if="activeTab === 'inventory'">
          <InventoryReport :key="reportRefreshKey" :filters="filters" />
        </div>
        <div v-if="activeTab === 'discount'">
          <DiscountReport :key="reportRefreshKey" :filters="filters" />
        </div>
        <div v-if="activeTab === 'expense'">
          <ExpenseReport :key="reportRefreshKey" :filters="filters" />
        </div>
        <div v-if="activeTab === 'profit'">
          <ProfitReport :key="reportRefreshKey" :filters="filters" />
        </div>
        <div v-if="activeTab === 'expired'">
          <ExpiredReport :key="reportRefreshKey" :filters="filters" />
        </div>
        <div v-if="activeTab === 'adjustment'">
          <AdjustmentReport :key="reportRefreshKey" :filters="filters" />
        </div>
        <div v-if="activeTab === 'loss'">
          <InventoryLossReport :key="reportRefreshKey" :filters="filters" />
        </div>
      </div>
    </div>

    <div class="mt-6 flex justify-end">
      <p class="text-[10px] font-normal text-slate-400 uppercase tracking-widest italic">
        {{ tx.syncedToday }} {{ new Date().toLocaleDateString('en-GB') }}
      </p>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue';
import { useI18n } from '../composables/useI18n';
import api from '../services/api';
import SaleReport from './reports/SaleReport.vue';
import PurchaseReport from './reports/PurchaseReport.vue';
import ConsignmentReport from './reports/ConsignmentReport.vue';
import InventoryReport from './reports/InventoryReport.vue';
import DiscountReport from './reports/DiscountReport.vue';
import ExpenseReport from './reports/ExpenseReport.vue';
import ProfitReport from './reports/ProfitReport.vue';
import ExpiredReport from './reports/ExpiredReport.vue';
import AdjustmentReport from './reports/AdjustmentReport.vue';
import InventoryLossReport from './reports/InventoryLossReport.vue';

const activeTab = ref('sales');
const { t } = useI18n();

const tx = computed(() => ({
  title: t('reports.title'),
  subtitle: t('reports.subtitle'),
  branch: t('layout.branch'),
  allBranches: t('common.allBranches'),
  from: t('reports.from'),
  to: t('reports.to'),
  analysis: t('reports.analysis'),
  syncedToday: t('reports.syncedToday'),
  sales: t('reports.sales'),
  purchase: t('reports.purchase'),
  consignment: t('reports.consignment'),
  inventory: t('reports.inventory'),
  discount: t('reports.discount'),
  expenses: t('reports.expenses'),
  profits: t('reports.profits'),
  expired: t('reports.expired'),
  adjustment: t('reports.adjustment'),
  inventoryLoss: t('reports.inventoryLoss')
}));

const today = new Date().toISOString().substr(0, 10);
const userRole = (localStorage.getItem('Role') || '').toLowerCase();
const isAdmin = userRole === 'admin';
const userBranchId = localStorage.getItem('BranchId');
const branches = ref([]);
const selectedBranchId = ref(isAdmin ? '' : (userBranchId || ''));

const filters = ref({
  fromDate: today,
  toDate: today,
  branchId: isAdmin ? null : (userBranchId ? Number(userBranchId) : null)
});
const reportRefreshKey = computed(() => `${filters.value.fromDate}|${filters.value.toDate}|${filters.value.branchId ?? 'all'}|${activeTab.value}`);

const loadBranches = async () => {
  if (!isAdmin) {
    return;
  }

  try {
    const res = await api.get('/Branches');
    branches.value = Array.isArray(res.data) ? res.data : [];
  } catch (err) {
    console.error('Failed to load branches', err);
  }
};

watch(selectedBranchId, (value) => {
  filters.value.branchId = value ? Number(value) : null;
});

onMounted(loadBranches);

const reportTabs = computed(() => [
  { id: 'sales', name: tx.value.sales, icon: 'SA' },
  { id: 'purchase', name: tx.value.purchase, icon: 'PU' },
  { id: 'consignment', name: tx.value.consignment, icon: 'CN' },
  { id: 'inventory', name: tx.value.inventory, icon: 'IV' },
  { id: 'discount', name: tx.value.discount, icon: 'DS' },
  { id: 'expense', name: tx.value.expenses, icon: 'EX' },
  { id: 'profit', name: tx.value.profits, icon: 'PF' },
  { id: 'expired', name: tx.value.expired, icon: 'EP' },
  { id: 'adjustment', name: tx.value.adjustment, icon: 'AD' },
  { id: 'loss', name: tx.value.inventoryLoss, icon: 'LS' }
]);

const activeReportTab = computed(() => reportTabs.value.find(t => t.id === activeTab.value) || reportTabs.value[0]);
</script>

<style scoped>
.report-tab-scroll {
  scrollbar-width: thin;
  scrollbar-color: rgb(148 163 184 / 0.8) transparent;
}

.report-tab-scroll::-webkit-scrollbar {
  height: 6px;
}

.report-tab-scroll::-webkit-scrollbar-thumb {
  background: rgb(148 163 184 / 0.7);
  border-radius: 999px;
}

input[type="date"]::-webkit-calendar-picker-indicator {
  opacity: 0.4;
  cursor: pointer;
  filter: invert(10%);
}

input[type="date"]::-webkit-calendar-picker-indicator:hover {
  opacity: 0.8;
}
</style>


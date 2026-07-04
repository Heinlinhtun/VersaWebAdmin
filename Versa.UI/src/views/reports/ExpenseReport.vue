<template>
  <div class="space-y-8 animate-in fade-in duration-500">

    <div class="flex flex-col gap-1">
      <h2 class="text-xl font-normal text-slate-900 tracking-tight">{{ tx.expenseAnalytics }}</h2>
      <p class="text-[11px] font-normal text-slate-400 uppercase tracking-widest flex items-center">
        <span class="w-2 h-2 bg-blue-500 rounded-full mr-2 shadow-[0_0_8px_rgba(59,130,246,0.5)]"></span>
        {{ tx.operationalOverhead }}
      </p>
    </div>

    <div class="grid grid-cols-1 lg:grid-cols-2 gap-8">

      <div class="bg-white p-8 rounded-xl border border-slate-200 shadow-sm flex flex-col h-full">
        <div class="flex justify-between items-center mb-10">
          <h3 class="font-normal text-[10px] uppercase text-slate-500 tracking-[0.2em]">{{ tx.spendingByCategory }}</h3>
          <span class="text-[10px] font-normal text-slate-400 bg-slate-50 px-2 py-1 rounded">{{ tx.volumeWeighted }}</span>
        </div>

        <div class="flex-1 space-y-8">
          <div v-for="cat in analytics.categories" :key="cat.Category" class="group">
            <div class="flex justify-between items-end mb-2.5">
              <span class="text-xs font-normal text-slate-700 uppercase tracking-wide">{{ cat.Category }}</span>
              <span class="text-sm font-normal text-slate-900 tabular-nums">{{ formatNumber(cat.Total) }} <small
                  class="text-[10px] font-normal text-slate-400">Ks</small></span>
            </div>
            <div class="w-full bg-slate-100 h-2.5 rounded-full overflow-hidden">
              <div
                class="bg-blue-600 h-full rounded-full transition-all duration-1000 ease-out group-hover:bg-blue-500 group-hover:shadow-[0_0_12px_rgba(37,99,235,0.3)]"
                :style="{ width: getPercentage(cat.Total) + '%' }"></div>
            </div>
          </div>
        </div>

        <div v-if="analytics.categories.length === 0" class="py-24 text-center">
          <div class="opacity-20 grayscale flex flex-col items-center">
            <span class="text-4xl mb-2">💸</span>
            <p class="font-normal uppercase tracking-[0.3em] text-[10px] text-slate-900">{{ tx.awaitingData }}</p>
          </div>
        </div>
      </div>

      <div class="bg-white p-8 rounded-xl border border-slate-200 shadow-sm flex flex-col h-full">
        <div class="flex justify-between items-center mb-10">
          <h3 class="font-normal text-[10px] uppercase text-slate-500 tracking-[0.2em]">{{ tx.sixMonthBurnRate }}</h3>
          <span class="text-[10px] font-normal text-emerald-500 bg-emerald-50 px-2 py-1 rounded">{{ tx.liveTrend }}</span>
        </div>

        <div class="flex-1 flex items-end justify-around gap-4 pt-10 pb-4 h-64 border-b border-slate-100">
          <div v-for="m in analytics.monthly" :key="m.Month"
            class="flex flex-col items-center flex-1 group h-full justify-end">
            <div
              class="w-full max-w-[32px] bg-slate-800 rounded-t-lg relative transition-all duration-500 hover:bg-blue-600 cursor-pointer shadow-sm"
              :style="{ height: getTrendPercentage(m.Total) + '%' }">

              <div
                class="absolute -top-10 left-1/2 -translate-x-1/2 bg-slate-900 text-white text-[10px] px-3 py-1.5 rounded-lg opacity-0 group-hover:opacity-100 transition-all font-normal tabular-nums whitespace-nowrap z-10 shadow-xl border border-white/10">
                {{ formatNumber(m.Total) }} Ks
                <div class="absolute -bottom-1 left-1/2 -translate-x-1/2 w-2 h-2 bg-slate-900 rotate-45"></div>
              </div>
            </div>
            <div
              class="mt-6 text-[9px] font-normal text-slate-400 uppercase tracking-tighter group-hover:text-slate-900 transition-colors">
              {{ formatMonth(m.Month) }}</div>
          </div>
        </div>

        <div v-if="analytics.monthly.length === 0" class="py-24 text-center grayscale opacity-20">
          <span class="text-4xl mb-2">📉</span>
          <p class="font-normal uppercase tracking-[0.3em] text-[10px] text-slate-900">{{ tx.awaitingTrendData }}</p>
        </div>
      </div>

    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, computed, watch } from 'vue';
import api from '../../services/api';
import { useI18n } from '../../composables/useI18n';

const props = defineProps({
  filters: {
    type: Object,
    required: true
  }
});

const { t } = useI18n();
const analytics = ref({ categories: [], monthly: [] });
const tx = computed(() => ({
  expenseAnalytics: t('reportViews.expense.expenseAnalytics'),
  operationalOverhead: t('reportViews.expense.operationalOverhead'),
  spendingByCategory: t('reportViews.expense.spendingByCategory'),
  volumeWeighted: t('reportViews.expense.volumeWeighted'),
  awaitingData: t('reportViews.expense.awaitingData'),
  sixMonthBurnRate: t('reportViews.expense.sixMonthBurnRate'),
  liveTrend: t('reportViews.expense.liveTrend'),
  awaitingTrendData: t('reportViews.expense.awaitingTrendData')
}));

const fetchAnalytics = async () => {
  try {
    const res = await api.get('/Reports/expense-analytics', {
      params: {
        branchId: props.filters.branchId
      }
    });
    analytics.value = res.data;
  } catch (err) {
    console.error("Audit Analytics Error:", err);
  }
};

const getPercentage = (value) => {
  const max = Math.max(...analytics.value.categories.map(c => c.Total), 1);
  return (value / max) * 100;
};

const getTrendPercentage = (value) => {
  const max = Math.max(...analytics.value.monthly.map(m => m.Total), 1);
  return Math.max(8, (value / max) * 100);
};

const formatMonth = (monthStr) => {
  const [year, month] = monthStr.split('-');
  const date = new Date(year, month - 1);
  return date.toLocaleString('default', { month: 'short' }) + ' ' + year.slice(-2);
};

const formatNumber = (val) => new Intl.NumberFormat().format(val || 0);

onMounted(fetchAnalytics);
watch(() => props.filters, fetchAnalytics, { deep: true });
</script>

<style scoped>
.tabular-nums {
  font-variant-numeric: tabular-nums;
}
</style>

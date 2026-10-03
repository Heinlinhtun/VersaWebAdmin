<template>
  <div class="space-y-6 animate-in fade-in duration-500">

    <div
      class="bg-[#3f3f46] p-6 rounded-xl shadow-lg flex justify-between items-center text-white border-b-4 border-orange-500">
      <div>
        <h2 class="text-[10px] font-normal uppercase tracking-[0.2em] text-slate-400 mb-1">{{ tx.cumulativeDiscountAsset }}</h2>
        <div class="text-3xl font-normal tracking-tight tabular-nums">
          {{ formatNumber(totalDiscountSum) }} <span class="text-sm font-normal opacity-50 uppercase ml-1">MMK</span>
        </div>
      </div>
      <div class="text-right bg-white/5 p-3 rounded-lg border border-white/10">
        <div class="text-[9px] font-normal uppercase text-orange-400 tracking-widest mb-1">{{ tx.promotionImpact }}</div>
        <div class="text-xs font-normal font-mono">{{ discounts.length }} {{ tx.vouchersImpacted }}</div>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
      <div class="px-6 py-4 border-b border-slate-100 flex justify-between items-center bg-slate-50/50">
        <h3 class="font-normal text-[10px] uppercase text-slate-500 tracking-[0.2em]">{{ tx.discountGiftAudit }}</h3>
        <div class="flex items-center gap-4">
          <button @click="handleExportCsv" class="text-[10px] font-normal text-orange-600 uppercase hover:underline">EXPORT CSV</button>
          <div class="flex items-center gap-2">
            <span class="w-2 h-2 bg-orange-500 rounded-full animate-pulse"></span>
            <span class="text-[10px] font-normal text-slate-400 uppercase">{{ tx.liveReview }}</span>
          </div>
        </div>
      </div>

      <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse">
          <thead class="bg-[#3f3f46] text-white">
            <tr class="text-[10px] font-normal uppercase tracking-[0.2em] text-white">
              <th class="p-4 w-12 text-center opacity-70 text-white">Ref</th>
              <th class="p-4 text-white">{{ tx.voucherTimestamp }}</th>
              <th class="p-4 text-white">{{ tx.customerEntity }}</th>
              <th class="p-4 text-right text-white">{{ tx.itemsDisc }}</th>
              <th class="p-4 text-right text-white">{{ tx.voucherDisc }}</th>
              <th class="p-4 text-right text-white">{{ tx.totalWriteoff }}</th>
              <th class="p-4 text-center px-8 text-white">{{ tx.gifting }}</th>
            </tr>
          </thead>
          <tbody class="text-xs font-normal text-slate-600">
            <template v-for="(d, idx) in paginatedDiscounts" :key="d.SaleId">
              <tr class="hover:bg-slate-50/80 transition-colors group">
                <td class="p-4 text-center text-slate-300 font-mono text-[10px]">
                  {{ (currentPage - 1) * itemsPerPage + idx + 1 }}
                </td>
                <td class="p-4">
                  <div class="font-normal text-blue-600 tracking-tighter uppercase">{{ d.SaleNo }}</div>
                  <div class="text-[10px] text-slate-400 mt-0.5 tabular-nums">{{ formatDate(d.SaleDate) }}</div>
                </td>
                <td class="p-4">
                  <div class="text-slate-900 font-normal italic">{{ d.CustomerName || tx.walkInClient }}</div>
                  <div class="text-[9px] text-slate-400 uppercase font-normal tracking-tighter">{{ tx.ops }}: {{ d.CashierName ||
                    tx.system }}</div>
                </td>
                <td class="p-4 text-right tabular-nums text-slate-500">
                  {{ formatNumber(d.ItemTotalDiscount) }}
                </td>
                <td class="p-4 text-right tabular-nums text-slate-500">
                  {{ formatNumber(d.VoucherDiscount) }}
                </td>
                <td class="p-4 text-right font-normal text-orange-600 tabular-nums bg-orange-50/20">
                  -{{ formatNumber(d.ItemTotalDiscount + d.VoucherDiscount) }}
                </td>
                <td class="p-4 text-center">
                  <span v-if="d.HasFOC"
                    class="bg-emerald-50 text-emerald-600 border border-emerald-100 px-2.5 py-1 rounded-md text-[9px] font-normal uppercase tracking-tighter shadow-sm">
                    {{ tx.giftIncluded }}
                  </span>
                  <span v-else class="text-slate-300">—</span>
                </td>
              </tr>

              <tr v-if="d.DiscountedItems && d.DiscountedItems.length > 0">
                <td colspan="7" class="p-0 bg-slate-50/30">
                  <div class="mx-14 my-2 p-3 border-l-2 border-slate-200 bg-white rounded-r-lg shadow-sm">
                    <div class="text-[9px] uppercase text-slate-400 mb-2 font-normal tracking-widest">{{ tx.itemizedBreakdown }}
                    </div>
                    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-x-8 gap-y-1">
                      <div v-for="item in d.DiscountedItems" :key="item.Id"
                        class="flex justify-between items-center text-[10px] py-1 border-b border-slate-50 last:border-0">
                        <span class="text-slate-700 font-normal truncate pr-2">{{ item.ProductName }} <small
                            class="text-slate-400">(x{{ formatQuantity(item.Quantity) }})</small></span>
                        <span class="text-orange-600 font-normal tabular-nums">
                          -{{ formatNumber(item.DiscountAmount) }}
                          <span v-if="item.IsFOC"
                            class="ml-1 text-[8px] bg-emerald-100 text-emerald-700 px-1 rounded uppercase">FOC</span>
                        </span>
                      </div>
                    </div>
                  </div>
                </td>
              </tr>
            </template>
          </tbody>
        </table>

        <div v-if="discounts.length === 0" class="p-24 text-center">
          <div class="flex flex-col items-center opacity-20 grayscale">
            <span class="text-4xl mb-2">🏷️</span>
                  <p class="text-[10px] font-normal uppercase tracking-[0.3em] text-slate-900">{{ tx.noConcessionsFound }}</p>
          </div>
        </div>
      </div>

      <div v-if="totalPages > 1" class="p-4 bg-slate-50 border-t border-slate-200 flex items-center justify-between">
        <p class="text-[9px] font-normal text-slate-400 uppercase tracking-widest font-mono">
          {{ tx.vouchersAudited }}: {{ discounts.length }} | {{ tx.page }} {{ currentPage }} {{ tx.of }} {{ totalPages }}
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
import { useQuantitySettings } from '../../composables/useQuantitySettings';
const { formatQuantity } = useQuantitySettings();
import { ref, computed, onMounted, watch } from 'vue';
import api from '../../services/api';
import { useI18n } from '../../composables/useI18n';
import { useCsvExport } from '../../composables/useCsvExport';

const props = defineProps(['filters']);
const { t } = useI18n();
const { exportToCsv } = useCsvExport();
const discounts = ref([]);
const tx = computed(() => ({
  cumulativeDiscountAsset: t('reportViews.discount.cumulativeDiscountAsset'),
  promotionImpact: t('reportViews.discount.promotionImpact'),
  vouchersImpacted: t('reportViews.discount.vouchersImpacted'),
  discountGiftAudit: t('reportViews.discount.discountGiftAudit'),
  liveReview: t('reportViews.discount.liveReview'),
  voucherTimestamp: t('reportViews.discount.voucherTimestamp'),
  customerEntity: t('reportViews.discount.customerEntity'),
  itemsDisc: t('reportViews.discount.itemsDisc'),
  voucherDisc: t('reportViews.discount.voucherDisc'),
  totalWriteoff: t('reportViews.discount.totalWriteoff'),
  gifting: t('reportViews.discount.gifting'),
  walkInClient: t('reportViews.discount.walkInClient'),
  ops: t('reportViews.discount.ops'),
  system: t('reportViews.discount.system'),
  giftIncluded: t('reportViews.discount.giftIncluded'),
  itemizedBreakdown: t('reportViews.discount.itemizedBreakdown'),
  noConcessionsFound: t('reportViews.discount.noConcessionsFound'),
  vouchersAudited: t('reportViews.discount.vouchersAudited'),
  page: t('reportViews.discount.page'),
  of: t('reportViews.discount.of'),
  prev: t('reportViews.discount.prev'),
  next: t('reportViews.discount.next')
}));

const currentPage = ref(1);
const itemsPerPage = 15; // Adjusted for better nested row visibility

const paginatedDiscounts = computed(() => {
  const start = (currentPage.value - 1) * itemsPerPage;
  return discounts.value.slice(start, start + itemsPerPage);
});

const totalPages = computed(() => Math.ceil(discounts.value.length / itemsPerPage) || 1);

const handleExportCsv = () => {
  const headers = [
    t('reportViews.discount.voucherTimestamp'),
    t('reportViews.discount.customerEntity'),
    t('reportViews.discount.itemsDisc'),
    t('reportViews.discount.voucherDisc'),
    t('reportViews.discount.totalWriteoff')
  ];

  const rows = discounts.value.map(d => [
    `${d.SaleNo} (${formatDate(d.SaleDate)})`,
    d.CustomerName || tx.value.walkInClient,
    d.ItemTotalDiscount,
    d.VoucherDiscount,
    d.ItemTotalDiscount + d.VoucherDiscount
  ]);

  const dateStr = new Date().toISOString().split('T')[0];
  exportToCsv(`Discount_Audit_${dateStr}`, headers, rows);
};

const totalDiscountSum = computed(() => {
  return discounts.value.reduce((sum, d) => sum + ((d.ItemTotalDiscount || 0) + (d.VoucherDiscount || 0)), 0);
});

const fetchDiscounts = async () => {
  try {
    const res = await api.get('/Reports/discount-report', { params: props.filters });
    discounts.value = Array.isArray(res.data) ? res.data : [];
    currentPage.value = 1;
  } catch (error) {
    console.error("Concession Audit Error:", error);
    discounts.value = [];
  }
};

const formatNumber = (val) => new Intl.NumberFormat().format(val || 0);
const formatDate = (date) => {
  if (!date) return '-';
  const d = new Date(date);
  return d.toLocaleDateString('en-GB') + ' ' + d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
};

watch(() => props.filters, fetchDiscounts, { deep: true });

onMounted(fetchDiscounts);
</script>

<style scoped>
.tabular-nums {
  font-variant-numeric: tabular-nums;
}
</style>

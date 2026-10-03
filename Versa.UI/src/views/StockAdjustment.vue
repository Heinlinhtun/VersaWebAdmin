<template>
  <div class="px-3 sm:px-5 lg:px-6 py-4 sm:py-6 bg-gray-50 min-h-screen">
    <div class="max-w-4xl mx-auto bg-white p-8 rounded-3xl shadow-sm border">
      <h2 class="text-xl font-normal uppercase text-gray-800 mb-6">{{ tx.title }}</h2>

      <div class="space-y-6">
        <div>
          <label class="text-[10px] font-normal text-gray-400 uppercase ml-1">{{ tx.selectProduct }}</label>
          <div ref="productSearchWrap" class="relative group">
            <input v-model="productSearch" @input="handleProductSearch" @focus="showProductResults = filteredProducts.length > 0"
              @keydown.down.prevent="moveProductDown" @keydown.up.prevent="moveProductUp" @keydown.enter.prevent="selectHighlightedProduct"
              type="text" :placeholder="tx.selectProduct"
              class="w-full p-2.5 bg-white border border-slate-200 rounded-lg font-normal text-sm shadow-sm outline-none focus:ring-2 focus:ring-indigo-500/20 pl-9 pr-9"/>
            <span class="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400"><svg class="h-4 w-4" fill="none"
                stroke="currentColor" viewBox="0 0 24 24">
                <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
              </svg></span>
            <button v-if="productSearch" @click="clearProductSearch"
              class="absolute right-3 top-1/2 -translate-y-1/2 text-slate-300 hover:text-slate-500">✖</button>

            <div v-if="showProductResults && filteredProducts.length > 0"
              class="absolute z-[100] left-0 right-0 mt-1 bg-white border border-slate-200 rounded-xl shadow-2xl max-h-64 overflow-y-auto animate-in fade-in slide-in-from-top-2 duration-200">
              <div v-for="(p, idx) in filteredProducts" :key="p.ProductId || p.productId" @click="selectProduct(p)"
                class="p-3 flex items-center justify-between cursor-pointer border-b border-slate-50 last:border-0 hover:bg-indigo-50 transition-colors"
                :class="idx === highlightedProductIndex ? 'bg-indigo-50' : ''">
                <div class="flex-1 min-w-0">
                  <p class="text-xs font-normal text-slate-800 truncate">{{ p.ProductName || p.productName }}</p>
                  <p class="text-[9px] text-slate-400 font-normal uppercase tracking-tighter">{{ ((p.SKU || p.sku || p.ProductSku || p.productSku || '').toString()) || 'No SKU' }}</p>
                </div>
                <div class="text-right pl-3">
                  <p class="text-[10px] font-normal text-indigo-600 tabular-nums">
                    {{ tx.current }}: {{ p.BaseQuantity ?? p.baseQuantity ?? 0 }}
                  </p>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label class="text-[10px] font-normal text-gray-400 uppercase ml-1">{{ tx.adjustmentType }}</label>
            <select v-model="form.Type" class="w-full p-4 bg-gray-50 border rounded-2xl font-normal outline-none focus:ring-2 focus:ring-indigo-500">
              <option value="Addition">{{ tx.addition }}</option>
              <option value="Subtraction">{{ tx.subtraction }}</option>
            </select>
          </div>

          <div>
            <label class="text-[10px] font-normal text-gray-400 uppercase ml-1">{{ tx.quantity }}</label>
            <input v-quantity v-model="form.Quantity" type="number" class="w-full p-4 bg-gray-50 border rounded-2xl font-normal text-xl outline-none focus:ring-2 focus:ring-indigo-500">
          </div>
        </div>

        <div>
          <label class="text-[10px] font-normal text-gray-400 uppercase ml-1">{{ tx.reasonRemark }}</label>
          <textarea v-model="form.Reason" rows="3" :placeholder="tx.reasonPlaceholder" class="w-full p-4 bg-gray-50 border rounded-2xl outline-none focus:ring-2 focus:ring-indigo-500 font-normal"></textarea>
        </div>

        <button @click="submitAdjustment" class="w-full bg-gray-800 text-white py-5 rounded-2xl font-normal text-lg shadow-xl hover:bg-black transition active:scale-95 uppercase">
          {{ tx.confirmAdjustment }}
        </button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted, computed } from 'vue';
import api from '../services/api';
import { useToast } from '../composables/useToast';
import { useI18n } from '../composables/useI18n';

const { addToast } = useToast();
const { t } = useI18n();

const products = ref([]);
const productSearch = ref('');
const showProductResults = ref(false);
const highlightedProductIndex = ref(-1);
const productSearchWrap = ref(null);
const form = ref({
  BranchId: localStorage.getItem('BranchId'),
  ProductId: null,
  Type: 'Subtraction',
  Quantity: 0,
  Reason: ''
});

const tx = computed(() => ({
  title: t('stockAdjustment.title'),
  selectProduct: t('stockAdjustment.selectProduct'),
  current: t('stockAdjustment.current'),
  adjustmentType: t('stockAdjustment.adjustmentType'),
  addition: t('stockAdjustment.addition'),
  subtraction: t('stockAdjustment.subtraction'),
  quantity: t('stockAdjustment.quantity'),
  reasonRemark: t('stockAdjustment.reasonRemark'),
  reasonPlaceholder: t('stockAdjustment.reasonPlaceholder'),
  confirmAdjustment: t('stockAdjustment.confirmAdjustment'),
  invalidQty: t('stockAdjustment.invalidQty'),
  success: t('stockAdjustment.success'),
  error: t('stockAdjustment.error')
}));

const fetchProducts = async () => {
  try {
    const rawBranchId = localStorage.getItem('BranchId');
    const bId = parseInt(rawBranchId);
    if (isNaN(bId)) return;

    const res = await api.get(`/Inventory/branch/${bId}`);
    products.value = res.data;
    // Don't auto-select — leave search box empty like supplier picker
  } catch (err) {
    console.error('Failed to fetch products:', err);
  }
};

const filteredProducts = computed(() => {
  const q = (productSearch.value || '').trim().toLowerCase();
  if (!q) return products.value.slice(0, 30);
  return products.value.filter(p => {
    const name = (p.ProductName || p.productName || '').toLowerCase();
    const sku = (p.SKU || p.sku || p.ProductSku || p.productSku || '').toString().toLowerCase();
    return name.includes(q) || sku.includes(q);
  }).slice(0, 30);
});

const handleProductSearch = () => {
  const q = (productSearch.value || '').trim().toLowerCase();
  showProductResults.value = filteredProducts.value.length > 0;
  highlightedProductIndex.value = filteredProducts.value.length > 0 ? 0 : -1;
  form.value.ProductId = null;
};

const selectProduct = (p) => {
  form.value.ProductId = p.ProductId || p.productId;
  productSearch.value = p.ProductName || p.productName || '';
  showProductResults.value = false;
  highlightedProductIndex.value = -1;
};

const clearProductSearch = () => {
  productSearch.value = '';
  showProductResults.value = false;
  highlightedProductIndex.value = -1;
  form.value.ProductId = null;
};

const moveProductDown = () => {
  if (filteredProducts.value.length === 0) return;
  highlightedProductIndex.value = (highlightedProductIndex.value + 1) % filteredProducts.value.length;
};

const moveProductUp = () => {
  if (filteredProducts.value.length === 0) return;
  highlightedProductIndex.value = (highlightedProductIndex.value - 1 + filteredProducts.value.length) % filteredProducts.value.length;
};

const selectHighlightedProduct = () => {
  if (highlightedProductIndex.value < 0) return;
  const p = filteredProducts.value[highlightedProductIndex.value];
  if (p) selectProduct(p);
};

const handleClickOutside = (e) => {
  const el = productSearchWrap.value;
  if (!el) return;
  if (el.contains(e.target)) return;
  showProductResults.value = false;
  highlightedProductIndex.value = -1;
};

const submitAdjustment = async () => {
  if (form.value.Quantity <= 0) return addToast(tx.value.invalidQty, 'warning');
  try {
    await api.post('/inventory/adjust', form.value);
    addToast(tx.value.success, 'success');
    form.value.Quantity = 0;
    form.value.Reason = '';
    fetchProducts();
  } catch (err) {
    addToast(tx.value.error, 'error');
  }
};

onMounted(() => {
  fetchProducts();
  window.addEventListener('click', handleClickOutside);
});

onUnmounted(() => {
  window.removeEventListener('click', handleClickOutside);
});
</script>

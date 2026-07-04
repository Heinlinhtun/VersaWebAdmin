<template>
  <div class="px-3 sm:px-5 lg:px-6 py-4 sm:py-6 bg-gray-50 min-h-screen">
    <div class="max-w-4xl mx-auto space-y-6">
      <div class="bg-white p-8 rounded-3xl shadow-sm border border-gray-100">
        <h2 class="text-xl font-normal uppercase text-gray-800 mb-6">{{ tx.saleReturnTitle }}</h2>
        <div class="flex gap-4">
          <div class="flex-1">
            <input v-model="searchId" type="text" :placeholder="tx.enterVoucherId"
              class="w-full p-4 bg-gray-50 border rounded-2xl font-normal outline-none focus:ring-2 focus:ring-indigo-500">
          </div>
          <button @click="searchVoucher"
            class="bg-indigo-600 text-white px-8 rounded-2xl font-normal hover:bg-indigo-700 transition">
            {{ tx.search }}
          </button>
        </div>
      </div>

      <div v-if="voucherItems.length > 0" class="bg-white rounded-3xl shadow-sm border overflow-x-auto">
        <table class="w-full text-left">
          <thead
            class="bg-[#3f3f46] font-normal uppercase text-[10px] text-white border-b border-gray-600 tracking-widest">
            <tr>
              <th class="p-5 text-left text-white">{{ tx.product }}</th>
              <th class="p-5 text-center text-white">{{ tx.soldQty }}</th>
              <th class="p-5 text-center text-white">{{ tx.returnQty }}</th>
              <th class="p-5 text-right text-white">{{ tx.price }}</th>
              <th class="p-5 text-right text-white">{{ tx.action }}</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-50">
            <tr v-for="item in voucherItems" :key="item.SaleItemId || item.Id" class="hover:bg-gray-50">
              <td class="p-5 font-normal">
                {{ item.ProductName }}
                <span class="text-[10px] text-slate-400 ml-1">({{ item.ItemType }})</span>
                <span v-if="isFocItem(item)" class="ml-2 text-[10px] bg-emerald-100 text-emerald-700 px-2 py-0.5 rounded uppercase">FOC</span>
              </td>
              <td class="p-5 text-center font-normal text-blue-600">{{ item.Quantity }} {{ item.UnitName || '-' }}</td>
              <td class="p-5 text-center">
                <input v-model.number="item.returnQty" type="number"
                  class="w-20 p-2 border rounded-lg text-center font-normal" :max="item.Quantity">
              </td>
              <td class="p-5 text-right font-normal">{{ Number(isFocItem(item) ? 0 : item.UnitPrice || 0).toLocaleString() }}</td>
              <td class="p-5 text-right">
                <button @click="processReturn(item)" :disabled="isReturning"
                  class="bg-red-50 text-red-600 px-4 py-2 rounded-xl font-normal text-xs uppercase hover:bg-red-100 disabled:opacity-50 disabled:cursor-not-allowed">
                  {{ tx.return }}
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>

  <div v-if="showReturnModal" class="fixed inset-0 bg-slate-900/40 backdrop-blur-sm flex items-center justify-center p-4 z-[100]">
    <div class="bg-white rounded-2xl w-full max-w-md border border-slate-200 shadow-2xl overflow-hidden">
      <div class="bg-[#3f3f46] px-5 py-4 text-white flex items-center justify-between">
        <h3 class="text-sm uppercase tracking-wider">{{ tx.selectReturnPaymentMethod }}</h3>
        <button @click="closeReturnModal" class="text-white/70 hover:text-white">✕</button>
      </div>
      <div class="p-5 space-y-3">
        <p class="text-xs text-slate-500">{{ tx.chooseDeductionChannel }}</p>
        <select v-model="selectedReturnPaymentMethod" class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg text-sm outline-none focus:ring-2 focus:ring-indigo-500/20">
          <option v-for="m in returnPaymentMethods" :key="m.method" :value="m.method">
            {{ m.method }} - {{ Number(m.amount || 0).toLocaleString() }}
          </option>
        </select>
        <div class="flex gap-2 pt-2">
          <button @click="closeReturnModal" class="flex-1 py-2.5 rounded-lg bg-slate-100 text-slate-700 text-xs uppercase">{{ tx.cancel }}</button>
          <button @click="confirmReturn" :disabled="isReturning" class="flex-1 py-2.5 rounded-lg bg-red-600 text-white text-xs uppercase hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed">{{ tx.return }}</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue';
import api from '../services/api';
import { useToast } from '../composables/useToast';
import { useI18n } from '../composables/useI18n';

const { addToast } = useToast();
const { locale } = useI18n();

const searchId = ref('');
const voucherItems = ref([]);
const showReturnModal = ref(false);
const pendingReturnItem = ref(null);
const isReturning = ref(false);
const returnPaymentMethods = ref([]);
const selectedReturnPaymentMethod = ref('');
const tx = computed(() => locale.value === 'mm' ? {
  saleReturnTitle: 'Sale Return',
  enterVoucherId: 'Enter Voucher ID (e.g. 1001 or SA0001001)',
  search: 'Search',
  product: 'Product',
  soldQty: 'Sold Qty',
  returnQty: 'Return Qty',
  price: 'Price',
  action: 'Action',
  return: 'Return',
  cancel: 'Cancel',
  selectReturnPaymentMethod: 'Select Payment Method',
  chooseDeductionChannel: 'Choose which payment method should be deducted in reports.',
  invoiceNotFound: 'Invoice Number not found',
  invalidQty: 'Invalid Quantity',
  successReturned: 'Successfully returned!',
  errorProcessing: 'Error processing return'
} : {
  saleReturnTitle: 'Sale Return',
  enterVoucherId: 'Enter Voucher ID (e.g. 1001 or SA0001001)',
  search: 'Search',
  product: 'Product',
  soldQty: 'Sold Qty',
  returnQty: 'Return Qty',
  price: 'Price',
  action: 'Action',
  return: 'Return',
  cancel: 'Cancel',
  selectReturnPaymentMethod: 'Select Payment Method',
  chooseDeductionChannel: 'Choose which payment method should be deducted in reports.',
  invoiceNotFound: 'Invoice Number not found',
  invalidQty: 'Invalid Quantity',
  successReturned: 'Successfully returned!',
  errorProcessing: 'Error processing return'
});

const searchVoucher = async () => {
  const raw = String(searchId.value || '').trim();
  const normalized = raw.replace(/^SA/i, '').replace(/^INV-/i, '').replace(/\D/g, '');
  const saleId = parseInt(normalized || '0', 10);

  if (!saleId) {
    addToast(tx.value.invoiceNotFound, 'error');
    voucherItems.value = [];
    return;
  }

  try {
    const res = await api.get(`/Sales/find-voucher/${saleId}`);
    const rows = Array.isArray(res.data) ? res.data : [];
    if (!rows.length) throw new Error('Empty voucher');
    voucherItems.value = rows.map(item => ({ ...item, returnQty: 1 }));
  } catch (err) {
    try {
      // Fallback path: details endpoint can include Product/Service/Bundle rows reliably.
      const detailRes = await api.get(`/Sales/details/${saleId}`);
      const items = Array.isArray(detailRes.data?.items) ? detailRes.data.items : [];
      if (!items.length) throw new Error('No items');

      voucherItems.value = items.map(item => ({
        ...item,
        ProductName: item.ItemName || item.ProductName || 'Unknown',
        ItemType: item.ItemType || 'Product',
        returnQty: 1
      }));
    } catch {
      addToast(tx.value.invoiceNotFound, 'error');
      voucherItems.value = [];
    }
  }
};

const closeReturnModal = () => {
  showReturnModal.value = false;
  pendingReturnItem.value = null;
  returnPaymentMethods.value = [];
  selectedReturnPaymentMethod.value = '';
};

const isFocItem = (item) => Boolean(item?.IsFOC ?? item?.isFOC);

const processReturn = async (item) => {
  if (item.returnQty <= 0 || item.returnQty > item.Quantity) {
    return addToast(tx.value.invalidQty, 'error');
  }

  if (isFocItem(item)) {
    pendingReturnItem.value = item;
    selectedReturnPaymentMethod.value = '';
    return confirmReturn();
  }

  try {
    const resMethods = await api.get(`/Inventory/sale-payment-methods/${item.SaleId}`);
    const methods = Array.isArray(resMethods.data) ? resMethods.data : [];
    returnPaymentMethods.value = methods
      .map(x => ({
        method: x.Method ?? x.method ?? 'Unknown',
        amount: Number(x.Amount ?? x.amount ?? 0)
      }))
      .filter(x => x.amount >= 0);
    selectedReturnPaymentMethod.value = returnPaymentMethods.value[0]?.method || 'Unknown';
  } catch {
    returnPaymentMethods.value = [{ method: 'Unknown', amount: 0 }];
    selectedReturnPaymentMethod.value = 'Unknown';
  }

  pendingReturnItem.value = item;
  showReturnModal.value = true;
};

const confirmReturn = async () => {
  if (isReturning.value) return;

  const item = pendingReturnItem.value;
  if (!item) return;

  const payload = {
    SaleId: item.SaleId,
    BranchId: parseInt(localStorage.getItem('BranchId') || 1),
    ItemType: item.ItemType,
    ProductId: item.ProductId,
    BundleId: item.BundleId,
    ServiceId: item.ServiceId,
    UnitId: item.UnitId,
    Quantity: item.returnQty,
    RefundAmount: isFocItem(item) ? 0 : (Number(item.UnitPrice || 0) * item.returnQty),
    Reason: 'Customer Return',
    PreferredPaymentMethod: selectedReturnPaymentMethod.value || 'Unknown'
  };

  try {
    isReturning.value = true;
    await api.post('/Inventory/sale-return-any', payload);
    addToast(tx.value.successReturned, 'success');
    closeReturnModal();
    searchVoucher();
  } catch (err) {
    addToast(err.response?.data?.Message || tx.value.errorProcessing, 'error');
  } finally {
    isReturning.value = false;
  }
};
</script>


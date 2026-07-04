<template>
  <div class="px-3 sm:px-5 lg:px-6 py-4 sm:py-6 bg-gray-50 min-h-screen">
    <div class="max-w-4xl mx-auto space-y-6">

      <div class="bg-white p-8 rounded-3xl shadow-sm border border-gray-100">
        <h2 class="text-xl font-normal uppercase text-gray-800 mb-6">{{ tx.purchaseReturnTitle }}</h2>
        <div class="flex gap-4">
          <div class="flex-1">
            <label class="text-[10px] font-normal text-gray-400 uppercase ml-1">{{ tx.purchaseVoucherId }}</label>
            <input v-model="searchId" type="number" :placeholder="tx.enterPoId"
              class="w-full p-4 bg-gray-50 border rounded-2xl font-normal outline-none focus:ring-2 focus:ring-orange-500">
          </div>
          <button @click="searchPO" :disabled="!searchId"
            class="bg-orange-600 text-white px-8 rounded-2xl font-normal hover:bg-orange-700 transition self-end h-[58px] disabled:bg-gray-300">
            {{ tx.findVoucher }}
          </button>
        </div>
      </div>

      <div v-if="poItems.length > 0"
        class="bg-white rounded-3xl shadow-sm border border-gray-100 overflow-x-auto animate-in fade-in slide-in-from-bottom-4">
        <div class="p-5 border-b bg-orange-50/50">
          <span class="text-xs font-normal text-orange-600 uppercase">{{ tx.itemsInVoucher }} #{{ searchId }}</span>
        </div>
        <table class="w-full text-left">
          <thead
            class="bg-[#3f3f46] font-normal uppercase text-[10px] text-white border-b border-gray-600 tracking-widest">
            <tr>
              <th class="p-5 text-left text-white">{{ tx.productName }}</th>
              <th class="p-5 text-center text-white">{{ tx.purchasedQty }}</th>
              <th class="p-5 text-center text-white">{{ tx.returnableQty }}</th>
              <th class="p-5 text-center text-white w-32">{{ tx.returnQty }}</th>
              <th class="p-5 text-right text-white">{{ tx.unitCost }}</th>
              <th class="p-5 text-right text-white">{{ tx.action }}</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-50">
            <tr v-for="item in poItems" :key="item.ProductId" class="hover:bg-orange-50/30 transition">
              <td class="p-5">
                <div class="font-normal text-gray-800">{{ item.ProductName }}</div>
                <div class="text-[10px] text-gray-400">ID: {{ item.ProductId }}</div>
              </td>
              <td class="p-5 text-center">
                <span class="px-3 py-1 bg-gray-100 rounded-full font-normal text-gray-600">
                  {{ item.Quantity }}
                </span>
              </td>
              <td class="p-5 text-center">
                <span class="px-3 py-1 bg-emerald-50 text-emerald-700 rounded-full font-normal">
                  {{ formatQty(item.ReturnableQty ?? item.returnableQty ?? item.Quantity) }} {{ item.UnitName }}
                </span>
              </td>
              <td class="p-5 text-center">
                <input v-model.number="item.returnQty" type="number"
                  class="w-full p-2 border rounded-xl text-center font-normal focus:ring-2 focus:ring-orange-500 outline-none"
                  :max="item.ReturnableQty ?? item.returnableQty ?? item.Quantity" min="1">
              </td>
              <td class="p-5 text-right font-normal text-gray-700">
                {{ item.CostPrice.toLocaleString() }}
              </td>
              <td class="p-5 text-right">
                <button @click="processReturn(item)"
                  class="bg-orange-600 text-white px-5 py-2 rounded-xl font-normal text-xs uppercase hover:bg-orange-700 shadow-lg shadow-orange-100">
                  {{ tx.return }}
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div v-else-if="searched" class="text-center p-20 bg-white rounded-3xl border border-dashed">
        <p class="text-gray-400 font-normal italic">{{ tx.voucherNotFound }}</p>
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

const searchId = ref(null);
const poItems = ref([]);
const searched = ref(false);
const tx = computed(() => locale.value === 'mm' ? {
  purchaseReturnTitle: 'Purchase Return',
  purchaseVoucherId: 'Purchase Voucher ID',
  enterPoId: 'Enter PO ID (e.g. 501)',
  findVoucher: 'Find Voucher',
  itemsInVoucher: 'Items in Voucher',
  productName: 'Product Name',
  purchasedQty: 'Purchased Qty',
  returnableQty: 'Returnable Qty',
  returnQty: 'Return Qty',
  unitCost: 'Unit Cost',
  action: 'Action',
  return: 'Return',
  voucherNotFound: 'Voucher not found. Please check the ID.',
  invoiceNotFound: 'Invoice Number not found',
  returnedSuccess: 'Successfully Returned !',
  returnFailed: 'Return failed'
} : {
  purchaseReturnTitle: 'Purchase Return',
  purchaseVoucherId: 'Purchase Voucher ID',
  enterPoId: 'Enter PO ID (e.g. 501)',
  findVoucher: 'Find Voucher',
  itemsInVoucher: 'Items in Voucher',
  productName: 'Product Name',
  purchasedQty: 'Purchased Qty',
  returnableQty: 'Returnable Qty',
  returnQty: 'Return Qty',
  unitCost: 'Unit Cost',
  action: 'Action',
  return: 'Return',
  voucherNotFound: 'Voucher not found. Please check the ID.',
  invoiceNotFound: 'Invoice Number not found',
  returnedSuccess: 'Successfully Returned !',
  returnFailed: 'Return failed'
});

const searchPO = async () => {
  if (!searchId.value) return;
  try {
    const branchId = localStorage.getItem('BranchId');
    const res = await api.get(`/Inventory/find-po/${searchId.value}?branchId=${branchId}`);
    poItems.value = res.data.map(item => ({
      ...item,
      returnQty: Math.min(1, Number(item.ReturnableQty ?? item.returnableQty ?? item.Quantity ?? 0))
    }));
    searched.value = true;
  } catch (err) {
    addToast(tx.value.invoiceNotFound, 'error');
    poItems.value = [];
    searched.value = true;
  }
};

const formatQty = (value) => Number(value || 0).toLocaleString(undefined, {
  minimumFractionDigits: 0,
  maximumFractionDigits: 2
});

const getApiErrorMessage = (err, fallback) => (
  err.response?.data?.Message
  || err.response?.data?.message
  || err.response?.data?.title
  || fallback
);

const processReturn = async (item) => {
  const unitPrice = parseFloat(item.UnitPrice || item.CostPrice || item.Price || 0);
  const qty = parseFloat(item.returnQty || 0);
  const maxReturnable = Number(item.ReturnableQty ?? item.returnableQty ?? item.Quantity ?? 0);
  if (qty <= 0) return addToast(tx.value.returnFailed, 'error');
  if (qty > maxReturnable) return addToast(`${tx.value.returnQty} > ${tx.value.returnableQty} (${formatQty(maxReturnable)})`, 'error');
  const refund = unitPrice * qty;

  const payload = {
    POItemId: parseInt(item.POItemId || item.poItemId),
    POId: parseInt(item.POId),
    ProductId: parseInt(item.ProductId),
    BranchId: parseInt(localStorage.getItem('BranchId')),
    Quantity: qty,
    UnitId: parseInt(item.UnitId || item.unitId),
    RefundAmount: refund,
    Reason: 'Supplier Return: Defective Goods',
  };

  try {
    await api.post('/Inventory/purchase-return', payload);
    addToast(tx.value.returnedSuccess, 'success');
    await searchPO();
  } catch (err) {
    addToast(getApiErrorMessage(err, tx.value.returnFailed), 'error');
  }
};
</script>


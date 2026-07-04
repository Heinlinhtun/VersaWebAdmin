<template>
  <div class="px-3 sm:px-5 lg:px-8 pt-2 pb-6 sm:pb-8 bg-[#f8fafc] min-h-screen font-sans text-left">

    <div class="flex flex-col md:flex-row justify-between items-start md:items-end mb-8 gap-4 text-left">
      <div>
        <h1 class="text-2xl font-normal text-slate-900 tracking-tight">{{ t('suppliers.title') }}</h1>
        <p class="text-[11px] font-normal text-slate-400 uppercase tracking-widest mt-1 flex items-center">
          <span class="w-2 h-2 bg-blue-500 rounded-full mr-2 shadow-[0_0_8px_rgba(59,130,246,0.5)]"></span>
          {{ t('suppliers.subtitle') }}
        </p>
      </div>

      <div class="flex gap-3 w-full md:w-auto">
        <div class="relative flex-1 md:w-64">
          <span class="absolute inset-y-0 left-0 pl-3 flex items-center text-slate-400">
            <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
            </svg>
          </span>
          <input v-model="searchQuery" type="text" :placeholder="t('suppliers.searchPlaceholder')"
            class="w-full pl-10 pr-4 py-2.5 bg-white border border-slate-200 rounded-xl text-xs font-normal outline-none focus:ring-2 focus:ring-blue-500/20 transition-all">
        </div>

        <button @click="openModal()"
          class="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2.5 rounded-xl font-normal shadow-lg shadow-blue-100 transition-all active:scale-95 uppercase text-xs tracking-widest flex items-center gap-2 whitespace-nowrap">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
            <path fill-rule="evenodd"
              d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z"
              clip-rule="evenodd" />
          </svg>
          {{ t('suppliers.addSupplier') }}
        </button>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-x-auto">
      <table class="w-full text-left border-collapse">
        <thead class="bg-[#3f3f46] text-white">
          <tr class="text-[10px] font-normal uppercase tracking-[0.2em]">
            <th class="p-5 w-20 text-center opacity-70">{{ t('suppliers.tableNo') }}</th>
            <th class="p-5">{{ t('suppliers.tableName') }}</th>
            <th class="p-5">{{ t('suppliers.tableContact') }}</th>
            <th class="p-5">{{ t('suppliers.tablePhone') }}</th>
            <th class="p-5 text-right">{{ t('suppliers.tableDebt') }}</th>
            <th class="p-5 text-right px-8">{{ t('suppliers.tableActions') }}</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-slate-50">
          <tr v-for="(item, index) in paginatedSuppliers" :key="item.SupplierId"
            class="hover:bg-slate-50/80 transition-colors group">
            <td class="p-5 text-center text-slate-400 font-normal tabular-nums">
              {{ ((currentPage - 1) * pageSize) + index + 1 }}
            </td>
            <td class="p-5">
              <div class="flex items-center gap-4">
                <div
                  class="w-9 h-9 rounded-lg bg-blue-50 flex items-center justify-center text-blue-600 font-normal uppercase text-xs border border-blue-100 transition-transform group-hover:scale-110">
                  {{ (item.SupplierName || item.supplierName || 'S').charAt(0) }}
                </div>
                <div class="font-normal text-slate-800 text-sm tracking-tight">{{ item.SupplierName || item.supplierName
                }}</div>
              </div>
            </td>
            <td class="p-5 text-slate-600 font-normal text-sm">
              {{ item.ContactPerson || item.contactPerson || '-' }}
            </td>
            <td class="p-5">
              <span
                class="bg-slate-100 text-slate-500 font-mono text-[11px] px-2.5 py-1 rounded-md border border-slate-200/50 tabular-nums">
                {{ item.Phone || item.phone || '-' }}
              </span>
            </td>
            <td class="p-5 text-right font-normal tabular-nums text-sm">
              <span :class="(item.CurrentDebt || item.currentDebt) > 0 ? 'text-rose-600' : 'text-emerald-600'">
                {{ (item.CurrentDebt || item.currentDebt || 0).toLocaleString() }} <small
                  class="text-[10px] font-normal opacity-60">KS</small>
              </span>
            </td>
            <td class="p-5 text-right px-8">
              <div class="flex justify-end gap-2">
                <button v-if="(item.CurrentDebt || item.currentDebt) > 0" @click="openPaymentModal(item)"
                  class="bg-blue-600 hover:bg-blue-700 text-white px-3 py-1.5 rounded-lg text-[10px] font-normal uppercase tracking-wider transition-all shadow-sm active:scale-95">
                  {{ t('suppliers.makePayment') }}
                </button>

                <button @click="viewHistory(item)"
                  class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-indigo-600 hover:bg-indigo-50 transition-all shadow-sm"
                  title="Ledger History">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                    <path d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                </button>

                <button @click="openModal(item)"
                  class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-blue-600 hover:bg-blue-50 transition-all shadow-sm"
                  title="Edit Supplier">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                    <path
                      d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                  </svg>
                </button>

                <button @click="deleteSupplier(item.SupplierId || item.supplierId)"
                  class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-rose-600 hover:bg-rose-50 transition-all shadow-sm"
                  title="Remove">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                    <path
                      d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                  </svg>
                </button>
              </div>
            </td>
          </tr>

          <tr v-if="suppliers.length === 0">
            <td colspan="6" class="p-24 text-center">
              <div class="flex flex-col items-center opacity-20 grayscale">
                <svg class="w-12 h-12 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path
                    d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4"
                    stroke-width="2" />
                </svg>
                <p class="text-xs font-normal uppercase tracking-[0.3em] text-slate-900">{{ t('suppliers.noRecords') }}
                </p>
              </div>
            </td>
          </tr>
        </tbody>
      </table>

      <div v-if="filteredSuppliers.length > 0"
        class="flex justify-between items-center px-6 py-4 bg-slate-50 border-t border-slate-200">
        <div class="text-[10px] font-normal text-slate-400 uppercase tracking-widest">
          {{ t('suppliers.title') }}: {{ filteredSuppliers.length }}
        </div>
        <div class="flex items-center gap-1.5">
          <button @click="currentPage--" :disabled="currentPage === 1"
            class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">{{
              t('common.prev') }}</button>
          <span class="px-3 text-xs font-normal text-slate-400">{{ t('common.page') }} {{ currentPage }} / {{ totalPages
            }}</span>
          <button @click="currentPage++" :disabled="currentPage === totalPages"
            class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">{{
              t('common.next') }}</button>
        </div>
      </div>
    </div>

    <div v-if="showModal"
      class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
      <div
        class="bg-white w-full max-w-lg rounded-xl shadow-2xl overflow-hidden animate-in zoom-in duration-200 border border-slate-200">
        <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
          <h3 class="text-lg font-normal uppercase tracking-tight">{{ isEdit ? t('suppliers.modalTitleEdit') :
            t('suppliers.modalTitleAdd') }}
          </h3>
          <button @click="showModal = false"
            class="text-white/60 hover:text-white transition-colors text-2xl">�</button>
        </div>

        <div class="p-8 space-y-5">
          <div class="grid grid-cols-1 md:grid-cols-2 gap-5">
            <div class="md:col-span-2 space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                t('suppliers.labelName') }}</label>
              <input v-model="form.SupplierName" type="text"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all"
                :placeholder="t('suppliers.placeholderName')" />
            </div>
            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                t('suppliers.labelContact') }}</label>
              <input v-model="form.ContactPerson" type="text"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm"
                :placeholder="t('suppliers.placeholderContact')" />
            </div>
            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                t('suppliers.labelPhone') }}</label>
              <input v-model="form.Phone" type="text"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm"
                :placeholder="t('suppliers.placeholderPhone')" />
            </div>
            <div class="md:col-span-2 space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                t('suppliers.labelAddress') }}</label>
              <textarea v-model="form.Address" rows="3"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm resize-none"
                :placeholder="t('suppliers.placeholderAddress')"></textarea>
            </div>
          </div>

          <div class="pt-4 flex gap-3">
            <button @click="showModal = false"
              class="flex-1 py-3 bg-slate-100 text-slate-600 rounded-lg font-normal uppercase text-[10px] hover:bg-slate-200 transition-all">{{
                t('suppliers.btnCancel') }}</button>
            <button @click="saveSupplier"
              class="flex-1 py-3 bg-blue-600 text-white rounded-lg font-normal uppercase text-[10px] shadow-lg shadow-blue-200 hover:bg-blue-700 transition active:scale-95">
              {{ t('suppliers.btnSave') }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <div v-if="showPaymentModal"
      class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
      <div
        class="bg-white w-full max-sm rounded-xl shadow-2xl overflow-hidden animate-in zoom-in duration-200 border border-slate-200">
        <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
          <h3 class="text-lg font-normal uppercase tracking-tight">{{ t('suppliers.paymentTitle') }}</h3>
          <button @click="showPaymentModal = false"
            class="text-white/60 hover:text-white transition-colors text-2xl">�</button>
        </div>

        <div class="p-8 space-y-5">
          <div class="bg-blue-50 border border-blue-100 rounded-xl p-4">
            <p class="text-[10px] text-blue-400 font-normal uppercase leading-none mb-1">Paying To Vendor</p>
            <p class="text-sm font-normal text-blue-800">{{ selectedSupplier?.SupplierName }}</p>
          </div>

          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ t('suppliers.hDate')
              }}</label>
            <input v-model="paymentForm.PaymentDate" type="date"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all">
          </div>

          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
              t('suppliers.paymentAmount') }}
              (Ks)</label>
            <input v-model.number="paymentForm.Amount" type="number"
              class="w-full p-3 bg-slate-50 border rounded-lg outline-none focus:ring-2 font-normal text-xl transition-all tabular-nums"
              :class="paymentForm.Amount > (selectedSupplier?.CurrentDebt || selectedSupplier?.currentDebt) ? 'border-rose-300 text-rose-600 focus:ring-rose-500/20' : 'border-slate-200 text-blue-600 focus:ring-blue-500/20'">

            <p v-if="paymentForm.Amount > (selectedSupplier?.CurrentDebt || selectedSupplier?.currentDebt)"
              class="text-[10px] text-rose-500 font-normal uppercase mt-1">
              ?? Warning: Amount exceeds total debt
            </p>
            <div v-else class="flex justify-between px-1 mt-1">
              <span class="text-[10px] text-slate-400 font-normal uppercase tracking-tighter">Current Balance</span>
              <span class="text-[11px] text-slate-600 font-normal tabular-nums">{{ (selectedSupplier?.CurrentDebt ||
                0).toLocaleString() }} Ks</span>
            </div>
          </div>

          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
              t('suppliers.paymentChannel') }}</label>
            <select v-model="paymentForm.PaymentMethod"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg font-normal text-sm outline-none">
              <option value="Cash">Cash Disbursement</option>
              <option value="KPay">KPay / Wave</option>
              <option value="Bank Transfer">Direct Bank Transfer</option>
            </select>
          </div>

          <button @click="submitPayment"
            class="w-full bg-blue-600 text-white py-4 rounded-xl font-normal uppercase text-xs tracking-widest shadow-lg shadow-blue-100 hover:bg-blue-700 transition active:scale-95 mt-4">
            {{ t('suppliers.paymentTitle') }}
          </button>
        </div>
      </div>
    </div>

    <div v-if="showHistoryModal"
      class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
      <div
        class="bg-white w-full max-w-3xl rounded-xl shadow-2xl overflow-hidden animate-in zoom-in duration-200 border border-slate-200 flex flex-col max-h-[85vh]">
        <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
          <div>
            <h3 class="text-lg font-normal uppercase tracking-tight">{{ t('suppliers.historyTitle') }}</h3>
            <p class="text-blue-300 text-[10px] font-normal uppercase mt-0.5 tracking-widest italic">{{
              selectedSupplier?.SupplierName }}</p>
          </div>
          <button @click="showHistoryModal = false"
            class="text-white/60 hover:text-white transition-colors text-2xl">�</button>
        </div>

        <div class="overflow-y-auto flex-1 custom-scrollbar">
          <table class="w-full text-left border-collapse">
            <thead class="bg-slate-50 border-b border-slate-100 sticky top-0">
              <tr class="text-[10px] font-normal uppercase text-slate-400 tracking-widest">
                <th class="px-6 py-4">{{ t('suppliers.hDate') }}</th>
                <th class="px-6 py-4">{{ t('suppliers.hChannel') }}</th>
                <th class="px-6 py-4">{{ t('suppliers.hRef') }}</th>
                <th class="px-6 py-4 text-right">{{ t('suppliers.hAmount') }}</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-50 text-sm">
              <tr v-for="p in paymentHistory" :key="p.PaymentId" class="hover:bg-slate-50/50 transition-colors">
                <td class="px-6 py-4 font-normal text-slate-500 tabular-nums">{{ new
                  Date(p.PaymentDate).toLocaleDateString('en-GB') }}</td>
                <td class="px-6 py-4">
                  <span
                    class="bg-slate-100 text-slate-600 px-2 py-0.5 rounded text-[9px] font-normal uppercase border border-slate-200/50">{{
                      p.PaymentMethod }}</span>
                </td>
                <td class="px-6 py-4">
                  <button v-if="p.ReferenceNo && p.ReferenceNo.startsWith('PU')"
                    @click="viewPurchaseDetail(p.ReferenceNo)"
                    class="text-blue-600 hover:text-blue-800 font-normal text-xs underline underline-offset-4 decoration-blue-200">
                    {{ p.ReferenceNo }}
                  </button>
                  <span v-else class="text-slate-400 font-normal text-[11px]">{{ p.ReferenceNo || '-' }}</span>
                </td>
                <td class="px-6 py-4 text-right font-normal text-slate-900 tabular-nums">{{ p.Amount.toLocaleString() }}
                  Ks</td>
              </tr>
              <tr v-if="paymentHistory.length === 0">
                <td colspan="4"
                  class="px-6 py-16 text-center text-slate-300 font-normal uppercase tracking-[0.2em] text-xs">{{
                    t('suppliers.noRecords') }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div class="p-5 border-t border-slate-100 flex justify-end bg-slate-50/50">
          <button @click="showHistoryModal = false"
            class="px-6 py-2 bg-white border border-slate-200 text-slate-600 rounded-lg font-normal uppercase text-[10px] tracking-widest hover:bg-slate-100 transition-all">{{
              t('common.close') }}</button>
        </div>
      </div>
    </div>

    <div v-if="showDetailModal"
      class="fixed inset-0 z-[110] flex items-center justify-center bg-slate-900/40 backdrop-blur-sm p-4">
      <div
        class="bg-white rounded-xl max-w-lg w-full shadow-2xl animate-in zoom-in duration-200 overflow-hidden border border-slate-200">
        <div class="bg-[#3f3f46] p-5 text-white flex justify-between items-center">
          <h3 class="text-sm font-normal uppercase tracking-widest">Inventory List: {{ selectedInvoice }}</h3>
          <button @click="showDetailModal = false"
            class="text-white/60 hover:text-white transition-colors text-xl">�</button>
        </div>
        <div class="p-6 max-h-[60vh] overflow-y-auto space-y-3 custom-scrollbar">
          <div v-for="item in purchaseItems" :key="item.POItemId"
            class="flex justify-between items-center p-3 bg-slate-50 rounded-lg border border-slate-200/50">
            <div>
              <p class="text-sm font-normal text-slate-800">{{ item.ProductName }}</p>
              <p class="text-[10px] font-normal text-slate-400 uppercase mt-0.5">{{ item.Quantity }} {{ item.UnitName }} @
                {{ item.CostPrice.toLocaleString() }} Ks</p>
            </div>
            <p class="text-sm font-normal text-blue-600 tabular-nums">{{ (item.Quantity * item.CostPrice).toLocaleString()
            }} Ks</p>
          </div>
        </div>
        <div class="p-4 border-t border-slate-100 bg-white">
          <button @click="showDetailModal = false"
            class="w-full py-2.5 bg-slate-900 text-white rounded-lg font-normal uppercase text-[10px] tracking-widest hover:bg-black transition-all">{{
              t('common.close') }}</button>
        </div>
      </div>
    </div>

  </div>
</template>

<style scoped>
.tabular-nums {
  font-variant-numeric: tabular-nums;
}

.custom-scrollbar::-webkit-scrollbar {
  width: 5px;
}

.custom-scrollbar::-webkit-scrollbar-thumb {
  background: #e2e8f0;
  border-radius: 10px;
}
</style>

<script setup>
import { ref, onMounted, watch, computed } from 'vue';
import api from '../services/api';
import { useToast } from '../composables/useToast';
import { useI18n } from '../composables/useI18n';

const { addToast } = useToast();
const { t } = useI18n();

const suppliers = ref([]);
const showModal = ref(false);
const isEdit = ref(false);
const form = ref({ SupplierId: 0, SupplierName: '', ContactPerson: '', Phone: '', Address: '' });

const showDetailModal = ref(false);
const purchaseItems = ref([]);
const selectedInvoice = ref("");

const searchQuery = ref('');

const viewPurchaseDetail = async (invoiceNo) => {
  selectedInvoice.value = invoiceNo;
  try {
    const res = await api.get(`/Purchases/detail-by-invoice/${invoiceNo}`);
    purchaseItems.value = res.data;
    showDetailModal.value = true;
  } catch (err) {
    addToast(t('suppliers.loadPurchaseItemsError'), "error");
  }
};

const showHistoryModal = ref(false);
const paymentHistory = ref([]);

const viewHistory = async (supplier) => {
  selectedSupplier.value = supplier;
  const sId = supplier.SupplierId || supplier.supplierId;

  try {
    const res = await api.get(`/suppliers/${sId}/payments`);
    paymentHistory.value = res.data;
    showHistoryModal.value = true;
  } catch (err) {
    addToast(t('suppliers.loadHistoryError'), "error");
  }
};

const showPaymentModal = ref(false);
const selectedSupplier = ref(null);
const paymentForm = ref({
  SupplierId: 0,
  Amount: 0,
  PaymentMethod: 'Cash',
  ReferenceNo: '',
  Note: '',
  PaymentDate: new Date().toISOString().split('T')[0]
});

const currentPage = ref(1);
const pageSize = ref(10);

const filteredSuppliers = computed(() => {
  if (!searchQuery.value) return suppliers.value;
  const q = searchQuery.value.toLowerCase();
  return suppliers.value.filter(s =>
    (s.SupplierName || s.supplierName || '').toLowerCase().includes(q) ||
    (s.ContactPerson || s.contactPerson || '').toLowerCase().includes(q) ||
    (s.Phone || s.phone || '').includes(q)
  );
});

const totalPages = computed(() => {
  if (filteredSuppliers.value.length === 0) return 1;
  return Math.ceil(filteredSuppliers.value.length / pageSize.value);
});

const paginatedSuppliers = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value;
  const end = start + pageSize.value;
  return filteredSuppliers.value.slice(start, end);
});

watch(searchQuery, () => {
  currentPage.value = 1;
});

const goToPage = (page) => {
  if (page >= 1 && page <= totalPages.value) currentPage.value = page;
};

const fetchSuppliers = async () => {
  try {
    const res = await api.get('/suppliers');
    suppliers.value = res.data;
  } catch (err) {
    addToast(t('suppliers.fetchError'), "error");
  }
};

const openModal = (item = null) => {
  if (item) {
    isEdit.value = true;
    form.value = {
      SupplierId: item.SupplierId || item.supplierId,
      SupplierName: item.SupplierName || item.supplierName,
      ContactPerson: item.ContactPerson || item.contactPerson,
      Phone: item.Phone || item.phone,
      Address: item.Address || item.address
    };
  } else {
    isEdit.value = false;
    form.value = { SupplierId: 0, SupplierName: '', ContactPerson: '', Phone: '', Address: '' };
  }
  showModal.value = true;
};

const openPaymentModal = (supplier) => {
  selectedSupplier.value = supplier;
  const sId = supplier.SupplierId || supplier.supplierId;
  const debt = supplier.CurrentDebt || supplier.currentDebt || 0;

  paymentForm.value = {
    SupplierId: sId,
    Amount: debt,
    PaymentMethod: 'Cash',
    ReferenceNo: '',
    Note: '',
    PaymentDate: new Date().toISOString().split('T')[0]
  };
  showPaymentModal.value = true;
};

const submitPayment = async () => {
  const currentDebt = selectedSupplier.value?.CurrentDebt || selectedSupplier.value?.currentDebt || 0;

  if (paymentForm.value.Amount <= 0) {
    return addToast(t('suppliers.invalidAmount'), "warning");
  }

  if (paymentForm.value.Amount > currentDebt) {
    return addToast(`${t('suppliers.overDebt')} (${currentDebt.toLocaleString()} Ks).`, "error");
  }

  try {
    await api.post('/suppliers/pay-debt', paymentForm.value);
    addToast(t('suppliers.toastSuccess'), "success");
    showPaymentModal.value = false;
    fetchSuppliers();
  } catch (err) {
    addToast(err.response?.data?.message || t('suppliers.toastError'), "error");
  }
};

const saveSupplier = async () => {
  try {
    const id = form.value.SupplierId;
    if (isEdit.value) {
      await api.put(`/suppliers/${id}`, form.value);
    } else {
      await api.post('/suppliers', form.value);
    }
    showModal.value = false;
    fetchSuppliers();
    addToast(t('suppliers.toastSuccess'), "success");
  } catch (err) {
    addToast(t('suppliers.toastError'), "error");
  }
};

const deleteSupplier = async (id) => {
  if (confirm(t('suppliers.deleteConfirm'))) {
    try {
      await api.delete(`/suppliers/${id}`);
      fetchSuppliers();
      addToast(t('suppliers.deleted'), "success");
    } catch (err) {
      addToast(t('suppliers.deleteError'), "error");
    }
  }
};

onMounted(fetchSuppliers);
</script>

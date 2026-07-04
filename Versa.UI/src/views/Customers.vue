<template>
  <div class="px-3 sm:px-5 lg:px-8 pt-2 pb-6 sm:pb-8 bg-[#f8fafc] min-h-screen font-sans text-left">

    <div class="flex flex-col md:flex-row justify-between items-start md:items-end mb-8 gap-4">
      <div>
        <h1 class="text-2xl font-normal text-slate-900 tracking-tight">{{ t('customers.title') }}</h1>
        <p class="text-[11px] font-normal text-slate-400 uppercase tracking-widest mt-1 flex items-center">
          <span class="w-2 h-2 bg-blue-500 rounded-full mr-2 shadow-[0_0_8px_rgba(59,130,246,0.5)]"></span>
          {{ t('customers.subtitle') }}
        </p>
      </div>

      <div class="flex gap-3 w-full md:w-auto">
        <div class="relative flex-1 md:w-64">
          <span class="absolute inset-y-0 left-0 pl-3 flex items-center text-slate-400">
            <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
          </span>
          <input v-model="searchQuery" type="text" :placeholder="t('customers.searchPlaceholder')"
            class="w-full pl-10 pr-4 py-2.5 bg-white border border-slate-200 rounded-xl text-xs font-normal outline-none focus:ring-2 focus:ring-blue-500/20 transition-all">
        </div>

        <button @click="openModal()"
          class="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2.5 rounded-xl font-normal shadow-lg shadow-blue-100 transition-all active:scale-95 uppercase text-xs tracking-widest flex items-center gap-2">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
            <path fill-rule="evenodd"
              d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z"
              clip-rule="evenodd" />
          </svg>
          {{ t('customers.addCustomer') }}
        </button>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-x-auto">
      <table class="w-full text-left border-collapse">
        <thead class="bg-[#3f3f46] text-white">
          <tr class="text-[10px] font-normal uppercase tracking-[0.2em]">
            <th class="p-5 w-20 text-center opacity-70">{{ t('customers.tableNo') }}</th>
            <th class="p-5">{{ t('customers.tableName') }}</th>
            <th class="p-5">{{ t('customers.tablePhone') }}</th>
            <th class="p-5 text-right">{{ t('customers.tableDebt') }}</th>
            <th class="p-5 text-right px-8">{{ t('customers.tableActions') }}</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-slate-50">
          <tr v-for="(item, index) in paginatedData" :key="index" class="hover:bg-slate-50/80 transition-colors group">
            <td class="p-5 text-center text-slate-400 font-normal tabular-nums">
              {{ ((currentPage - 1) * pageSize) + index + 1 }}
            </td>
            <td class="p-5">
              <div class="flex items-center gap-4">
                <div
                  class="w-9 h-9 rounded-lg bg-blue-50 flex items-center justify-center text-blue-600 font-normal uppercase text-xs border border-blue-100 transition-transform group-hover:scale-110">
                  {{ (item.Name || item.name || 'C').charAt(0) }}
                </div>
                <div class="font-normal text-slate-800 text-sm tracking-tight">{{ item.Name || item.name }}</div>
              </div>
            </td>
            <td class="p-5">
              <span
                class="bg-slate-100 text-slate-500 font-mono text-[11px] px-2.5 py-1 rounded-md border border-slate-200/50 tabular-nums">
                {{ item.Phone || item.phone || '-' }}
              </span>
            </td>
            <td class="p-5 text-right font-normal tabular-nums text-sm">
              <span :class="(item.CurrentBalance || item.currentBalance) > 0 ? 'text-rose-600' : 'text-emerald-600'">
                {{ (item.CurrentBalance || item.currentBalance || 0).toLocaleString() }} <small
                  class="text-[10px] font-normal opacity-60">KS</small>
              </span>
            </td>
            <td class="p-5 text-right px-8">
              <div class="flex justify-end gap-2">
                <button v-if="(item.CurrentBalance || item.currentBalance) > 0" @click="openPaymentModal(item)"
                  class="bg-emerald-600 hover:bg-emerald-700 text-white px-3 py-1.5 rounded-lg text-[10px] font-normal uppercase tracking-wider transition-all shadow-sm active:scale-95">
                  {{ t('customers.collectPayment') }}
                </button>

                <button @click="viewHistory(item)"
                  class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-indigo-600 hover:bg-indigo-50 transition-all shadow-sm"
                  title="Payment History">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                    <path d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                </button>

                <button @click="openModal(item)"
                  class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-blue-600 hover:bg-blue-50 transition-all shadow-sm"
                  title="Edit Customer">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                    <path
                      d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                  </svg>
                </button>

                <button @click="deleteCustomer(item.CustomerId || item.customerId)"
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

          <tr v-if="customers.length === 0">
            <td colspan="5" class="p-24 text-center">
              <div class="flex flex-col items-center opacity-20 grayscale">
                <svg class="w-12 h-12 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path
                    d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z"
                    stroke-width="2" />
                </svg>
                <p class="text-xs font-normal uppercase tracking-[0.3em] text-slate-900">{{ t('customers.noRecords') }}</p>
              </div>
            </td>
          </tr>
        </tbody>
      </table>

      <div v-if="customers.length > 0"
        class="flex justify-between items-center px-6 py-4 bg-slate-50 border-t border-slate-200">
        <div class="text-[10px] font-normal text-slate-400 uppercase tracking-widest">
          {{ t('customers.title') }}: {{ customers.length }}
        </div>
        <div class="flex items-center gap-1.5">
          <button @click="currentPage--" :disabled="currentPage === 1"
            class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">{{ t('common.prev') }}</button>
          <span class="px-3 text-xs font-normal text-slate-400">Page {{ currentPage }} / {{ totalPages }}</span>
          <button @click="currentPage++" :disabled="currentPage === totalPages"
            class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">{{ t('common.next') }}</button>
        </div>
      </div>
    </div>

    <div v-if="showPaymentModal"
      class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
      <div
        class="bg-white w-full max-w-md rounded-xl shadow-2xl overflow-hidden animate-in zoom-in duration-200 border border-slate-200">
        <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
          <h3 class="text-lg font-normal uppercase tracking-tight">{{ t('customers.paymentTitle') }}</h3>
          <button @click="showPaymentModal = false"
            class="text-white/60 hover:text-white transition-colors text-2xl">�</button>
        </div>

        <div class="p-8 space-y-5">
          <div class="bg-blue-50 border border-blue-100 rounded-xl p-4 flex items-center gap-3">
            <div class="w-10 h-10 bg-white rounded-lg flex items-center justify-center text-blue-600 shadow-sm">??</div>
            <div>
              <p class="text-[10px] text-blue-400 font-normal uppercase leading-none mb-1">Customer Account</p>
              <p class="text-sm font-normal text-blue-800">{{ selectedCustomer?.Name || selectedCustomer?.name }}</p>
            </div>
          </div>

          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ t('customers.hDate') }}</label>
            <input v-model="paymentForm.PaymentDate" type="date"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all">
          </div>

          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ t('customers.paymentAmount') }}
              (Ks)</label>
            <input v-model.number="paymentForm.Amount" type="number"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-emerald-500/20 font-normal text-xl text-emerald-600 transition-all tabular-nums">
            <div class="flex justify-between px-1">
              <span class="text-[10px] text-slate-400 font-normal uppercase">Current Debt</span>
              <span class="text-[11px] text-rose-500 font-normal tabular-nums">{{ (selectedCustomer?.CurrentBalance ||
                0).toLocaleString() }} Ks</span>
            </div>
          </div>

          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ t('customers.paymentMethod') }}</label>
            <select v-model="paymentForm.PaymentMethod"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm bg-white">
              <option v-for="method in availablePaymentMethods" :key="method" :value="method">
                {{ method }}
              </option>
            </select>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">Reference</label>
              <input v-model="paymentForm.ReferenceNo" type="text" placeholder="Trans-#"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg text-xs font-normal outline-none">
            </div>
            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ t('customers.paymentNote') }}</label>
              <input v-model="paymentForm.Note" type="text" placeholder="Remark"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg text-xs font-normal outline-none">
            </div>
          </div>

          <button @click="submitPayment"
            class="w-full bg-emerald-600 text-white py-4 rounded-xl font-normal uppercase text-xs tracking-widest shadow-lg shadow-emerald-100 hover:bg-emerald-700 transition active:scale-95 mt-4">
            {{ t('customers.paymentTitle') }}
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
            <h3 class="text-lg font-normal uppercase tracking-tight">{{ t('customers.historyTitle') }}</h3>
            <p class="text-blue-300 text-[10px] font-normal uppercase mt-0.5 tracking-widest italic">{{
              selectedCustomer?.Name }}</p>
          </div>
          <button @click="showHistoryModal = false"
            class="text-white/60 hover:text-white transition-colors text-2xl">�</button>
        </div>

        <div class="overflow-y-auto flex-1 custom-scrollbar">
          <table class="w-full text-left border-collapse">
            <thead class="bg-slate-50 border-b border-slate-100 sticky top-0">
              <tr class="text-[10px] font-normal uppercase text-slate-400 tracking-widest">
                <th class="px-6 py-4">{{ t('customers.hDate') }}</th>
                <th class="px-6 py-4">{{ t('customers.paymentMethod') }}</th>
                <th class="px-6 py-4">{{ t('customers.voucherLink') }}</th>
                <th class="px-6 py-4 text-right">{{ t('customers.hAmount') }}</th>
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
                  <button v-if="p.SaleId || p.saleId" @click="goToSaleDetail(p.SaleId || p.saleId)"
                    class="text-blue-600 hover:text-blue-800 font-normal text-xs underline underline-offset-4 decoration-blue-200">
                    INV-{{ p.SaleId || p.saleId }}
                  </button>
                  <span v-else class="text-slate-400 italic text-[10px] font-normal tracking-tight">Manual Credit
                    Entry</span>
                </td>
                <td class="px-6 py-4 text-right font-normal text-slate-900 tabular-nums">{{ (p.Amount ||
                  0).toLocaleString() }} Ks</td>
              </tr>
              <tr v-if="paymentHistory.length === 0">
                <td colspan="4"
                  class="px-6 py-16 text-center text-slate-300 font-normal uppercase tracking-[0.2em] text-xs">{{ t('customers.noRecords') }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div class="p-5 border-t border-slate-100 flex justify-end bg-slate-50/50">
          <button @click="showHistoryModal = false"
            class="px-6 py-2 bg-white border border-slate-200 text-slate-600 rounded-lg font-normal uppercase text-[10px] tracking-widest hover:bg-slate-100 transition-all">{{ t('common.close') }}</button>
        </div>
      </div>
    </div>
    <div v-if="showModal"
      class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
      <div class="bg-white w-full max-w-md rounded-xl shadow-2xl overflow-hidden border border-slate-200">

        <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
          <h3 class="text-lg font-normal uppercase tracking-tight">
            {{ customerForm.CustomerId ? t('customers.modalTitleEdit') : t('customers.modalTitleAdd') }}
          </h3>
          <button @click="showModal = false"
            class="text-white/60 hover:text-white transition-colors text-2xl">�</button>
        </div>

        <div class="p-8 space-y-5">
          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ t('customers.labelName') }}</label>
            <input v-model="customerForm.Name" type="text" :placeholder="t('customers.placeholderName')"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all">
          </div>

          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ t('customers.labelPhone') }}</label>
            <input v-model="customerForm.Phone" type="text" :placeholder="t('customers.placeholderPhone')"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm tracking-widest transition-all">
          </div>

          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ t('customers.labelAddress') }}</label>
            <textarea v-model="customerForm.Address" rows="3" :placeholder="t('customers.placeholderAddress')"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all resize-none"></textarea>
          </div>

          <button @click="submitCustomer"
            class="w-full bg-blue-600 text-white py-4 rounded-lg font-normal text-xs shadow-lg shadow-blue-100 hover:bg-blue-700 transition active:scale-95 mt-4 uppercase tracking-widest">
            {{ customerForm.CustomerId ? t('customers.btnSave') : t('customers.btnSave') }}
          </button>
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
import { useRouter } from 'vue-router';

const { addToast } = useToast();
const router = useRouter();
const { t } = useI18n();
const availablePaymentMethods = ['Cash', 'KPay', 'CB Pay', 'AYA Pay', 'Wave Money', 'Banking'];

const customerForm = ref({
  CustomerId: null,
  Name: '',
  Phone: '',
  Address: ''
});
const customers = ref([]);
const searchQuery = ref('');
const showModal = ref(false);
const showPaymentModal = ref(false);
const showHistoryModal = ref(false);
const selectedCustomer = ref(null);
const paymentHistory = ref([]);
const paymentForm = ref({
  CustomerId: 0,
  Amount: 0,
  PaymentMethod: 'Cash',
  ReferenceNo: '',
  Note: '',
  PaymentDate: new Date().toISOString().substr(0, 10)
});

const filteredCustomers = computed(() => {
  if (!searchQuery.value) return customers.value;

  const q = searchQuery.value.toLowerCase();
  return customers.value.filter(c =>
    (c.Name || c.name || '').toLowerCase().includes(q) ||
    (c.Phone || c.phone || '').includes(q)
  );
});

const submitCustomer = async () => {
  if (!customerForm.value.Name) return addToast(t('customers.identityRequired'), "warning");

  try {
    const payload = { ...customerForm.value };
    if (payload.CustomerId) {
      await api.put(`/customers/${payload.CustomerId}`, payload);
      addToast(t('customers.toastSuccess'), "success");
    } else {
      await api.post('/customers', payload);
      addToast(t('customers.toastSuccess'), "success");
    }
    showModal.value = false;
    fetchCustomers();
  } catch (err) {
    addToast(t('customers.toastError'), "error");
  }
};

const deleteCustomer = async (id) => {
  if (!confirm(t('customers.deleteConfirm'))) return;
  try {
    await api.delete(`/customers/${id}`);
    addToast(t('customers.removed'), "success");
    fetchCustomers();
  } catch (err) { addToast(t('customers.deleteFailed'), "error"); }
};

const openModal = (item = null) => {
  if (item) {
    customerForm.value = {
      CustomerId: item.CustomerId || item.customerId,
      Name: item.Name || item.name,
      Phone: item.Phone || item.phone,
      Address: item.Address || item.address
    };
  } else {
    customerForm.value = { CustomerId: null, Name: '', Phone: '', Address: '' };
  }
  showModal.value = true;
};
const currentPage = ref(1);
const pageSize = ref(10);


const totalPages = computed(() => Math.ceil(filteredCustomers.value.length / pageSize.value) || 1);
const paginatedData = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value;
  return filteredCustomers.value.slice(start, start + pageSize.value);
});


const fetchCustomers = async () => {
  try {
    const res = await api.get('/customers');
    customers.value = res.data;
  } catch (err) { console.error(err); }
};

const openPaymentModal = (customer) => {
  selectedCustomer.value = customer;
  const balance = customer.CurrentBalance || customer.currentBalance || 0;
  paymentForm.value = {
    CustomerId: customer.CustomerId || customer.customerId,
    Amount: balance,
    PaymentMethod: 'Cash',
    ReferenceNo: '',
    Note: '',
    PaymentDate: new Date().toISOString().substr(0, 10)
  };
  showPaymentModal.value = true;
};

const submitPayment = async () => {
  try {
    await api.post('/customers/pay-debt', paymentForm.value);
    addToast(t('customers.paymentSuccess'), "success");
    showPaymentModal.value = false;
    fetchCustomers();
  } catch (err) { addToast(t('customers.errorOccurred'), "error"); }
};

const goToSaleDetail = (saleId) => {
  showHistoryModal.value = false;
  router.push({
    path: '/historycenter',
    query: { tab: 'sale', search: saleId }
  });
};


watch(searchQuery, () => {
  currentPage.value = 1;
});

const viewHistory = async (customer) => {
  selectedCustomer.value = customer;
  const cId = customer.CustomerId || customer.customerId;
  try {
    const res = await api.get(`/customers/${cId}/payments`);
    paymentHistory.value = res.data;
    showHistoryModal.value = true;
  } catch (err) { addToast(t('customers.loadFailed'), "error"); }
};

onMounted(fetchCustomers);
</script>

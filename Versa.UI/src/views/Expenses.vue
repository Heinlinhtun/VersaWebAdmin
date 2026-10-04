<template>
  <div class="px-3 sm:px-5 lg:px-8 pt-2 pb-6 sm:pb-8 bg-[#f8fafc] min-h-screen font-sans">
    <div class="flex flex-col md:flex-row justify-between items-start md:items-end mb-8 gap-4">
      <div>
        <h1 class="text-2xl font-normal text-slate-900 tracking-tight">{{ t('expenses.title') }}</h1>
        <p class="text-[11px] font-normal text-slate-400 uppercase tracking-widest mt-1 flex items-center">
          <span class="w-2 h-2 bg-blue-500 rounded-full mr-2 shadow-[0_0_8px_rgba(59,130,246,0.5)]"></span>
          {{ t('expenses.subtitle') }}
        </p>
      </div>
      <div class="flex items-center gap-3">

        <button @click="openCreateModal"
          class="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2.5 rounded-xl font-normal shadow-lg shadow-blue-100 transition-all active:scale-95 uppercase text-xs tracking-widest flex items-center gap-2 whitespace-nowrap">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
            <path fill-rule="evenodd"
              d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z"
              clip-rule="evenodd" />
          </svg>
          {{ t('expenses.addExpense') }}
        </button>
      </div>
    </div>

    <div class="bg-white p-3 rounded-xl shadow-sm border border-slate-200 mb-6 flex flex-col md:flex-row md:items-center flex-wrap gap-4">
      <div class="relative flex-1 max-w-md ml-1">
        <input v-model="searchQuery" type="text" @input="currentPage = 1"
          class="w-full pl-10 pr-4 py-2.5 bg-slate-50 border border-slate-100 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-xs transition-all"
          :placeholder="t('expenses.searchPlaceholder')">
        <span class="absolute left-3.5 top-3 text-slate-400">
          <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </span>
      </div>
      <div class="w-full md:w-56">
        <select v-model="selectedCategory" @change="currentPage = 1"
          class="w-full px-4 py-2.5 bg-slate-50 border border-slate-100 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-xs transition-all text-slate-700">
          <option value="">{{ t('expenses.allCategories') }}</option>
          <option v-for="category in expenseCategoryOptions" :key="category" :value="category">
            {{ category }}
          </option>
        </select>
      </div>
      <select v-model="paymentStatus" @change="currentPage = 1" :aria-label="t('expenses.paymentStatus')"
        class="px-4 py-2.5 bg-slate-50 border border-slate-100 rounded-lg text-xs">
        <option value="">{{ t('expenses.allPaymentStatuses') }}</option>
        <option value="paid">{{ t('expenses.paid') }}</option>
        <option value="pending">{{ t('expenses.pending') }}</option>
      </select>
      <div class="grid w-full md:w-auto grid-cols-1 sm:grid-cols-2 gap-3">
        <input v-model="fromDate" type="date" @change="currentPage = 1"
          class="w-full px-4 py-2.5 bg-slate-50 border border-slate-100 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-xs transition-all text-slate-700"
          :aria-label="t('expenses.fromDate')">
        <input v-model="toDate" type="date" @change="currentPage = 1"
          class="w-full px-4 py-2.5 bg-slate-50 border border-slate-100 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-xs transition-all text-slate-700"
          :aria-label="t('expenses.toDate')">
      </div>
      <div class="h-8 w-[1px] bg-slate-100 hidden md:block"></div>
      <div class="hidden md:flex items-center px-6 py-2 bg-blue-50/50 rounded-lg border border-blue-100/50">
        <div class="text-right">
          <p class="text-[9px] font-normal text-blue-400 uppercase tracking-widest leading-none mb-1">{{
            t('expenses.totalInView') }}</p>
          <p class="text-lg font-normal text-blue-700 tabular-nums">{{ formatNumber(totalExpenseAmount) }} <small
              class="text-[10px] font-normal">MMK</small></p>
        </div>
      </div>
    </div>
    <div class="text-right">
      <button @click="handleExportCsv" class="text-[10px] font-normal ml-5 text-blue-600 uppercase hover:underline">
        EXPORT CSV
      </button>
    </div>
    <div class="bg-white rounded-xl shadow-sm border border-slate-200 table-responsive">
      <table class="w-full text-left border-collapse">
        <thead class="bg-[#3f3f46] text-white">
          <tr class="text-[10px] font-normal uppercase tracking-[0.2em]">
            <th class="p-5 w-12 text-center opacity-70">{{ t('expenses.tableNo') }}</th>
            <th class="p-5">{{ t('expenses.tableDesc') }}</th>
            <th class="p-5">{{ t('expenses.tableCategory') }}</th>
            <th class="p-5 text-center">{{ t('expenses.tableDate') }}</th>
            <th class="p-5 text-right">{{ t('expenses.tableAmount') }}</th>
            <th class="p-5 text-center">{{ t('expenses.paymentStatus') }}</th>
            <th class="p-5 text-center">{{ t('expenses.paymentMethod') }}</th>
            <th class="p-5 text-right px-8">{{ t('expenses.tableActions') }}</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-slate-50">
          <tr v-for="(e, idx) in paginatedExpenses" :key="valueOf(e, 'ExpenseId', 'expenseId')"
            class="hover:bg-slate-50/80 transition-colors group">
            <td class="p-5 text-center text-slate-400 font-normal tabular-nums">
              {{ ((currentPage - 1) * pageSize) + idx + 1 }}
            </td>
            <td class="p-5">
              <div class="flex items-center gap-4">
                <div
                  class="w-10 h-10 rounded-lg bg-blue-50 flex items-center justify-center text-blue-600 font-normal border border-blue-100 uppercase text-sm">
                  {{ (valueOf(e, 'Title', 'title', 'E') || 'E').charAt(0) }}
                </div>
                <div>
                  <div class="font-normal text-slate-800 text-sm">{{ valueOf(e, 'Title', 'title') }}</div>
                  <div class="text-[10px] text-slate-400 font-normal uppercase tracking-wider mt-0.5">
                    {{ t('expenses.tableBranch') }}: {{ branchNameFor(valueOf(e, 'BranchId', 'branchId')) }}
                  </div>
                </div>
              </div>
            </td>
            <td class="p-5">
              <span
                class="px-3 py-1 bg-slate-100 text-slate-600 rounded-md text-[10px] font-normal uppercase tracking-wide border border-slate-200/50">
                {{ valueOf(e, 'Category', 'category') }}
              </span>
            </td>
            <td class="p-5 text-center text-slate-500 text-xs font-normal tabular-nums">
              {{ valueOf(e, 'ExpenseDate', 'expenseDate') ? new Date(valueOf(e, 'ExpenseDate', 'expenseDate')).toLocaleDateString('en-GB') : '-' }}
            </td>
            <td class="p-5 text-right font-normal text-slate-900 tabular-nums">
              {{ formatNumber(valueOf(e, 'Amount', 'amount', 0)) }}
            </td>
            <td class="p-5 text-center">
              <span class="text-xs rounded px-2 py-1" :class="isPaid(e) ? 'bg-emerald-50 text-emerald-700' : 'bg-amber-50 text-amber-700'">{{ t(isPaid(e) ? 'expenses.paid' : 'expenses.pending') }}</span>
            </td>
            <td class="p-5 text-center">
              <span class="text-[10px] text-slate-500 font-normal uppercase tracking-wide border px-2 py-1 bg-slate-50 rounded">{{ valueOf(e, 'PaymentMethod', 'paymentMethod', 'Cash') }}</span>
            </td>
            <td class="p-5 text-right px-8">
              <div class="flex justify-end gap-2">
                <button @click="openEditModal(e)"
                  class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-blue-600 hover:bg-blue-50 transition-all shadow-sm">
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24"
                    stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                  </svg>
                </button>
                <button @click="confirmDelete(valueOf(e, 'ExpenseId', 'expenseId'))"
                  class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-rose-600 hover:bg-rose-50 transition-all shadow-sm">
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24"
                    stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                  </svg>
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>

      <div v-if="filteredExpenses.length > 0"
        class="flex justify-between items-center px-6 py-4 bg-slate-50 border-t border-slate-200">
        <div class="text-[10px] font-normal text-slate-400 uppercase tracking-widest">
          {{ t('expenses.title') }}: {{ filteredExpenses.length }}
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

      <div v-if="filteredExpenses.length === 0" class="p-24 text-center">
        <div class="flex flex-col items-center opacity-20 grayscale">
          <svg class="w-12 h-12 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path
              d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
          </svg>
          <p class="text-xs font-normal uppercase tracking-[0.3em] text-slate-900">{{ t('expenses.noExpenses') }}</p>
        </div>
      </div>
    </div>

    <div v-if="showModal"
      class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
      <div class="bg-white w-full max-w-md max-h-[90dvh] overflow-y-auto rounded-xl shadow-2xl border border-slate-200">
        <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
          <h3 class="text-lg font-normal uppercase tracking-tight">{{ editMode ? t('expenses.modalTitleEdit') :
            t('expenses.modalTitleAdd') }}
          </h3>
          <button @click="showModal = false"
            class="text-white/60 hover:text-white transition-colors text-2xl">&times;</button>
        </div>
        <div class="p-8 space-y-5">
          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
              t('expenses.labelDesc') }}</label>
            <input v-model="form.Title" type="text" :placeholder="t('expenses.placeholderDesc')"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm">
          </div>
          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
              t('expenses.labelCategory') }}</label>
            <input v-model="form.Category" type="text" list="expense-category-options"
              placeholder="Type or select category"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm">
            <datalist id="expense-category-options">
              <option v-for="category in expenseCategoryOptions" :key="category" :value="category">
                {{ category }}
              </option>
            </datalist>
          </div>
          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
              t('expenses.labelAmount') }}</label>
            <input v-model.number="form.Amount" type="number" :placeholder="t('expenses.placeholderAmount')"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-lg text-blue-700">
          </div>
          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
              t('expenses.tableDate') }}</label>
            <input v-model="form.ExpenseDate" type="date"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm">
          </div>
          <label class="flex items-center gap-3 text-sm text-slate-700">
            <input v-model="form.IsPaid" type="checkbox" class="w-5 h-5 rounded">
            {{ t('expenses.paid') }}
            <span v-if="!form.IsPaid" class="text-amber-700">({{ t('expenses.pending') }})</span>
          </label>
          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ t('expenses.paymentMethod') }}</label>
            <select v-model="form.PaymentMethod"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm">
              <option value="Cash">Cash</option>
              <option value="KPay">KPay</option>
              <option value="Banking">Banking</option>
            </select>
          </div>
          <div v-if="isAdmin" class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
              t('expenses.labelBranch') }}</label>
            <select v-model="form.BranchId"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm">
              <option v-for="b in branches" :key="branchIdOf(b)" :value="branchIdOf(b)">{{ branchNameOf(b) }}</option>
            </select>
          </div>
          <button @click="saveExpense" :disabled="saving"
            class="w-full bg-blue-600 text-white py-4 rounded-lg font-normal text-xs shadow-lg shadow-blue-100 hover:bg-blue-700 transition active:scale-95 mt-4 uppercase tracking-widest">
            {{ t('expenses.btnSave') }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import api from '../services/api';
import { useToast } from '../composables/useToast';
import { useI18n } from '../composables/useI18n';
import { useCsvExport } from '../composables/useCsvExport';

const { addToast } = useToast();
const { t } = useI18n();
const { exportToCsv } = useCsvExport();

const expenses = ref([]);
const branches = ref([]);
const showModal = ref(false);
const editMode = ref(false);
const saving = ref(false);
const searchQuery = ref('');
const selectedCategory = ref('');
const paymentStatus = ref('');
const isPaid = expense => ![false, 0, '0', 'false', 'False'].includes(expense.IsPaid ?? expense.isPaid ?? true);
const fromDate = ref('');
const toDate = ref('');

const currentPage = ref(1);
const pageSize = ref(10);

const storedUser = (() => {
  try {
    return JSON.parse(localStorage.getItem('user') || '{}');
  } catch {
    return {};
  }
})();

const currentUser = ref({
  role: storedUser.role || storedUser.Role || localStorage.getItem('Role') || 'Staff',
  branchId: Number(storedUser.branchId ?? storedUser.BranchId ?? localStorage.getItem('BranchId') ?? 1) || 1
});
const isAdmin = computed(() => String(currentUser.value.role || '').toLowerCase() === 'admin');

const toDateInput = (value = new Date()) => {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  const offsetDate = new Date(date.getTime() - (date.getTimezoneOffset() * 60000));
  return offsetDate.toISOString().slice(0, 10);
};
const todayInput = () => toDateInput();
const form = ref({ ExpenseId: 0, BranchId: currentUser.value.branchId || 1, Title: '', Category: '', Amount: 0, IsPaid: true, PaymentMethod: 'Cash', ExpenseDate: todayInput() });
const defaultExpenseCategories = ['General', 'Electricity', 'Salary', 'Rent', 'Supplies'];

const expenseCategoryOptions = computed(() => {
  const existing = expenses.value
    .map(e => (valueOf(e, 'Category', 'category') || '').trim())
    .filter(Boolean);

  return [...new Set([...defaultExpenseCategories, ...existing])].sort((a, b) => a.localeCompare(b));
});

const fetchBranches = async () => {
  try {
    const res = await api.get('/Branches');
    branches.value = Array.isArray(res.data) ? res.data : [];
  } catch (err) { console.error(err); }
};

const fetchExpenses = async () => {
  try {
    const branchId = isAdmin.value ? 0 : (currentUser.value.branchId || 1);
    const res = await api.get(`/Expenses/${branchId}`);
    expenses.value = Array.isArray(res.data) ? res.data : [];
  } catch (err) {
    addToast(t('expenses.fetchError'), "error");
  }
};

const handleExportCsv = () => {
  const headers = [
    t('expenses.tableNo'),
    t('expenses.tableDesc'),
    t('expenses.tableCategory'),
    t('expenses.tableDate'),
    t('expenses.tableAmount'),
    t('expenses.paymentStatus'),
    t('expenses.paymentMethod'),
    t('expenses.tableBranch')
  ];

  const rows = filteredExpenses.value.map((e, idx) => [
    idx + 1,
    valueOf(e, 'Title', 'title'),
    valueOf(e, 'Category', 'category'),
    valueOf(e, 'ExpenseDate', 'expenseDate') ? new Date(valueOf(e, 'ExpenseDate', 'expenseDate')).toLocaleDateString('en-GB') : '-',
    valueOf(e, 'Amount', 'amount', 0),
    t(isPaid(e) ? 'expenses.paid' : 'expenses.pending'),
    valueOf(e, 'PaymentMethod', 'paymentMethod', 'Cash'),
    branchNameFor(valueOf(e, 'BranchId', 'branchId'))
  ]);

  const dateStr = new Date().toISOString().split('T')[0];
  exportToCsv(`Expenses_${dateStr}`, headers, rows);
};

const filteredExpenses = computed(() => {
  const query = searchQuery.value.toLowerCase().trim();
  return expenses.value.filter(e => {
    const matchesQuery = !query ||
      (valueOf(e, 'Title', 'title') || '').toLowerCase().includes(query) ||
      (valueOf(e, 'Category', 'category') || '').toLowerCase().includes(query);

    const matchesCategory = !selectedCategory.value ||
      (valueOf(e, 'Category', 'category') || '').trim() === selectedCategory.value;

    const expenseDate = normalizeDate(valueOf(e, 'ExpenseDate', 'expenseDate'));
    const matchesFromDate = !fromDate.value || (expenseDate && expenseDate >= fromDate.value);
    const matchesToDate = !toDate.value || (expenseDate && expenseDate <= toDate.value);

    const matchesPayment = !paymentStatus.value || isPaid(e) === (paymentStatus.value === 'paid');
    return matchesQuery && matchesCategory && matchesFromDate && matchesToDate && matchesPayment;
  });
});

const paginatedExpenses = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value;
  const end = start + pageSize.value;
  return filteredExpenses.value.slice(start, end);
});

const totalPages = computed(() => {
  return Math.ceil(filteredExpenses.value.length / pageSize.value) || 1;
});

const totalExpenseAmount = computed(() => {
  return filteredExpenses.value.reduce((sum, e) => sum + (Number(valueOf(e, 'Amount', 'amount', 0)) || 0), 0);
});

const openCreateModal = () => {
  editMode.value = false;
  form.value = { ExpenseId: 0, BranchId: currentUser.value.branchId || 1, Title: '', Category: '', Amount: 0, IsPaid: true, PaymentMethod: 'Cash', ExpenseDate: todayInput() };
  showModal.value = true;
};

const openEditModal = (expense) => {
  editMode.value = true;
  form.value = { 
    ...expense,
    ExpenseId: valueOf(expense, 'ExpenseId', 'expenseId', 0),
    BranchId: valueOf(expense, 'BranchId', 'branchId', currentUser.value.branchId || 1),
    Title: valueOf(expense, 'Title', 'title'),
    Category: valueOf(expense, 'Category', 'category'),
    Amount: valueOf(expense, 'Amount', 'amount', 0),
    IsPaid: isPaid(expense),
    PaymentMethod: valueOf(expense, 'PaymentMethod', 'paymentMethod', 'Cash'),
    ExpenseDate: normalizeDate(valueOf(expense, 'ExpenseDate', 'expenseDate')) || todayInput()
  };
  showModal.value = true;
};

const saveExpense = async () => {
  if (saving.value) return;
  saving.value = true;
  try {
    form.value.Category = (form.value.Category || '').trim() || 'General';
    if (!isAdmin.value) {
      form.value.BranchId = currentUser.value.branchId || 1;
    }
    const payload = { ...form.value };
    if (!editMode.value) {
      // For new expenses, include current time in the date
      const datePart = form.value.ExpenseDate || todayInput();
      payload.ExpenseDate = datePart
        ? `${datePart}T${new Date().toTimeString().split(' ')[0]}`
        : new Date().toISOString();
    } else {
      form.value.ExpenseDate = form.value.ExpenseDate || todayInput();
    }
    if (editMode.value) await api.put(`/Expenses/${form.value.ExpenseId}`, payload);
    else await api.post('/Expenses', payload);
    showModal.value = false;
    fetchExpenses();
    addToast(t('expenses.toastSuccess'), "success");
  } catch (err) { addToast(t('expenses.toastError'), "error"); }
  finally { saving.value = false; }
};

const confirmDelete = async (id) => {
  if (confirm(t('expenses.deleteConfirm'))) {
    try {
      await api.delete(`/Expenses/${id}`);
      fetchExpenses();
      addToast(t('expenses.deleted'), "info");
    } catch (err) {
      addToast(t('expenses.deleteError'), "error");
    }
  }
};

const formatNumber = (val) => new Intl.NumberFormat().format(val || 0);
const valueOf = (row, pascalKey, camelKey, fallback = '') => row?.[pascalKey] ?? row?.[camelKey] ?? fallback;
const branchIdOf = (branch) => branch?.BranchId ?? branch?.branchId;
const branchNameOf = (branch) => branch?.BranchName ?? branch?.branchName;
const branchNameFor = (branchId) => {
  const id = Number(branchId);
  const branch = branches.value.find(b => Number(branchIdOf(b)) === id);
  return branchNameOf(branch) || branchId || '-';
};
const normalizeDate = (value) => {
  if (!value) return '';
  if (typeof value === 'string') {
    const dateOnly = value.match(/^(\d{4}-\d{2}-\d{2})/);
    if (dateOnly) return dateOnly[1];
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  return toDateInput(date);
};

onMounted(() => {
  fetchBranches();
  fetchExpenses();
});
</script>

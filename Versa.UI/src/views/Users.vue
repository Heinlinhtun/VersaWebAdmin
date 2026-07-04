<template>
  <div class="px-3 sm:px-5 lg:px-8 pt-2 pb-6 sm:pb-8 bg-[#f8fafc] min-h-screen font-sans text-left">

    <div class="flex flex-col md:flex-row justify-between items-start md:items-end mb-8 gap-4 text-left">
      <div>
        <h1 class="text-2xl font-normal text-slate-900 tracking-tight">{{ t('users.title') }}</h1>
        <p class="text-[11px] font-normal text-slate-400 uppercase tracking-widest mt-1 flex items-center">
          <span class="w-2 h-2 bg-blue-500 rounded-full mr-2 shadow-[0_0_8px_rgba(99,102,241,0.5)]"></span>
          {{ t('users.subtitle') }}
        </p>
      </div>

      <div class="flex gap-3 w-full md:w-auto">
        <div class="relative flex-1 md:w-64">
          <span class="absolute inset-y-0 left-0 pl-3 flex items-center text-slate-400">
            <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
            </svg>
          </span>
          <input v-model="searchQuery" type="text" :placeholder="t('users.searchPlaceholder')"
            class="w-full pl-10 pr-4 py-2.5 bg-white border border-slate-200 rounded-xl text-xs font-normal outline-none focus:ring-2 focus:ring-blue-500/20 transition-all">
        </div>

        <button v-if="can('canManageUsers')" @click="openCreateModal"
          class="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2.5 rounded-xl font-normal shadow-lg shadow-blue-100 transition-all active:scale-95 uppercase text-xs tracking-widest flex items-center gap-2 whitespace-nowrap">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
            <path fill-rule="evenodd"
              d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z"
              clip-rule="evenodd" />
          </svg>
          {{ t('users.addStaff') }}
        </button>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-x-auto">
      <table class="w-full text-left border-collapse">
        <thead class="bg-[#3f3f46] text-white">
          <tr class="text-[10px] font-normal uppercase tracking-[0.2em]">
            <th class="p-5 w-16 text-center opacity-70">{{ t('common.no') }}</th>
            <th class="p-5">{{ t('users.tableName') }}</th>
            <th class="p-5">{{ t('users.tableBranch') }}</th>
            <th class="p-5 text-center">{{ t('users.tableRole') }}</th>
            <th class="p-5 text-center">{{ t('users.tableStatus') }}</th>
            <th class="p-5 text-right px-8">{{ t('users.tableActions') }}</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-slate-50">
          <tr v-for="(user, idx) in paginatedUsers" :key="user.UserId"
            class="hover:bg-slate-50/80 transition-colors group">
            <td class="p-5 text-center text-slate-400 font-normal tabular-nums">
              {{ ((currentPage - 1) * pageSize) + idx + 1 }}
            </td>
            <td class="p-5">
              <div class="flex items-center gap-4">
                <div
                  class="w-10 h-10 rounded-lg bg-blue-50 flex items-center justify-center text-blue-600 font-normal uppercase text-sm border border-blue-100 transition-transform group-hover:scale-110">
                  {{ (user.Username || 'U').charAt(0) }}
                </div>
                <div class="font-normal text-slate-800 text-sm tracking-tight">{{ user.Username }}</div>
              </div>
            </td>
            <td class="p-5">
              <span
                class="inline-flex items-center px-3 py-1 bg-slate-100 text-slate-600 rounded-md text-[10px] font-normal uppercase tracking-wide border border-slate-200/50">
                <span class="mr-1.5 opacity-50">??</span> {{ user.BranchName || t('users.unassigned') }}
              </span>
            </td>
            <td class="p-5 text-center">
              <span
                :class="user.Role === 'Admin' ? 'text-purple-600 bg-purple-50 border-purple-100' : 'text-blue-600 bg-blue-50 border-blue-100'"
                class="px-2.5 py-1 rounded-lg text-[10px] font-normal uppercase border shadow-sm tracking-tighter">
                {{ user.Role }}
              </span>
            </td>
            <td class="p-5 text-center">
              <div class="flex flex-col items-center gap-1">
                <button @click="confirmToggleStatus(user)" :disabled="user.Role === 'Admin'"
                  :class="[user.IsActive ? 'bg-emerald-500' : 'bg-slate-300', user.Role === 'Admin' ? 'opacity-40 cursor-not-allowed' : 'cursor-pointer hover:shadow-md']"
                  class="relative inline-flex h-5 w-10 items-center rounded-full transition-all duration-300">
                  <span :class="user.IsActive ? 'translate-x-5' : 'translate-x-1'"
                    class="inline-block h-3 w-3 transform rounded-full bg-white transition-transform duration-300">
                  </span>
                </button>
                <span v-if="user.Role === 'Admin'"
                  class="text-[8px] text-purple-400 font-normal uppercase tracking-tighter">{{ t('users.protected')
                  }}</span>
              </div>
            </td>
            <td class="p-5 text-right px-8">
              <div class="flex justify-end gap-2">
                <button v-if="can('canManageUsers')" @click="openEditModal(user)"
                  class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-blue-600 hover:bg-blue-50 transition-all shadow-sm">
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24"
                    stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                  </svg>
                </button>
                <button v-if="can('canManageUsers')" @click="confirmDelete(user)" :disabled="user.Role === 'Admin'"
                  class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-rose-600 hover:bg-rose-50 transition-all shadow-sm disabled:opacity-20">
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24"
                    stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                  </svg>
                </button>
              </div>
            </td>
          </tr>
          <tr v-if="filteredUsers.length === 0">
            <td colspan="6" class="p-24 text-center opacity-30 grayscale">
              <p class="text-xs font-normal uppercase tracking-[0.3em]">{{ t('users.noRecords') }}</p>
            </td>
          </tr>
        </tbody>
      </table>

      <div v-if="filteredUsers.length > 0"
        class="flex justify-between items-center px-6 py-4 bg-slate-50 border-t border-slate-200">
        <div class="text-[10px] font-normal text-slate-400 uppercase tracking-widest text-left">
          {{ t('users.title') }}: {{ filteredUsers.length }}
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
      class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4 text-left">
      <div class="bg-white w-full max-w-md rounded-xl shadow-2xl overflow-hidden border border-slate-200">
        <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
          <h3 class="text-lg font-normal uppercase tracking-tight">{{ isEdit ? t('users.modalTitleEdit') :
            t('users.modalTitleAdd') }}</h3>
          <button @click="showModal = false"
            class="text-white/60 hover:text-white transition-colors text-2xl">�</button>
        </div>
        <div class="p-8 space-y-5">
          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ t('users.labelUser')
            }}</label>
            <input v-model="form.Username" type="text"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm"
              :placeholder="t('users.placeholderUser')">
          </div>
          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">Secure Password {{ isEdit
              ? '(Optional)' : '' }}</label>
            <input v-model="form.Password" type="password"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm"
              :placeholder="isEdit ? 'Leave blank to keep current' : '��������'">
          </div>
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 text-left">
            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ t('users.labelRole')
              }}</label>
              <select v-model="form.Role"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg font-normal text-sm outline-none bg-white">
                <option value="Admin">{{ t('users.administrator') }}</option>
                <option value="Manager">Manager</option>
                <option value="Staff">{{ t('users.staffMember') }}</option>
                <option value="Cashier">Cashier</option>
              </select>
            </div>
            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                t('users.labelBranch') }}</label>
              <select v-model="form.BranchId"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg font-normal text-sm outline-none bg-white">
                <option v-for="b in branches" :key="b.branchId || b.BranchId" :value="b.branchId || b.BranchId">{{
                  b.branchName || b.BranchName }}</option>
              </select>
            </div>
          </div>
          <div class="pt-4 flex gap-3">
            <button @click="showModal = false"
              class="flex-1 py-3 bg-slate-100 text-slate-600 rounded-lg font-normal uppercase text-[10px]">{{
                t('users.btnCancel') }}</button>
            <button @click="saveUser"
              class="flex-1 py-3 bg-blue-600 text-white rounded-lg font-normal uppercase text-[10px] shadow-lg shadow-blue-200">
              {{ t('users.btnSave') }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, watch, computed, onMounted } from 'vue';
import api from '../services/api';
import { useToast } from '../composables/useToast';
import { useI18n } from '../composables/useI18n';
import { usePermissions } from '../composables/usePermissions';

const { addToast } = useToast();
const { t } = useI18n();
const { can } = usePermissions();

const users = ref([]);
const branches = ref([]);
const showModal = ref(false);
const searchQuery = ref('');
const isEdit = ref(false);
const currentPage = ref(1);
const pageSize = ref(10);
const form = ref({ UserId: 0, Username: '', Password: '', Role: 'Staff', BranchId: 1, IsActive: true });

const filteredUsers = computed(() => {
  if (!searchQuery.value) return users.value;
  const q = searchQuery.value.toLowerCase();
  return users.value.filter(u =>
    (u.Username || '').toLowerCase().includes(q) ||
    (u.BranchName || '').toLowerCase().includes(q) ||
    (u.Role || '').toLowerCase().includes(q)
  );
});

const totalPages = computed(() => Math.max(1, Math.ceil(filteredUsers.value.length / pageSize.value)));
const paginatedUsers = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value;
  return filteredUsers.value.slice(start, start + pageSize.value);
});

const goToPage = (p) => { if (p >= 1 && p <= totalPages.value) currentPage.value = p; };
watch(searchQuery, () => { currentPage.value = 1; });

const fetchUsers = async () => {
  try {
    const res = await api.get('/ShopUsers');
    users.value = Array.isArray(res.data) ? res.data : [];
  } catch (err) { addToast(t('users.toastError'), "error"); }
};

const fetchBranches = async () => {
  try {
    const res = await api.get('/Branches');
    branches.value = res.data;
  } catch (err) { addToast(t('users.toastError'), "error"); }
};

const openCreateModal = () => {
  isEdit.value = false;
  form.value = { UserId: 0, Username: '', Password: '', Role: 'Staff', BranchId: branches.value[0]?.branchId || branches.value[0]?.BranchId || 1, IsActive: true };
  showModal.value = true;
};

const openEditModal = (user) => {
  isEdit.value = true;
  form.value = { UserId: user.UserId, Username: user.Username, Role: user.Role, BranchId: user.BranchId, IsActive: user.IsActive, Password: '' };
  showModal.value = true;
};

const saveUser = async () => {
  try {
    const { BranchName, ...payload } = form.value;
    if (isEdit.value) await api.put(`/ShopUsers/${form.value.UserId}`, payload);
    else await api.post('/ShopUsers', payload);
    showModal.value = false;
    addToast(t('users.toastSuccess'), "success");
    fetchUsers();
  } catch (err) { addToast(err.response?.data?.Message || t('users.toastError'), "error"); }
};

const confirmToggleStatus = async (user) => {
  if (user.Role === 'Admin') return addToast(t('users.adminProtected'), "warning");
  if (confirm(t('common.confirmChange'))) {
    try {
      await api.put(`/ShopUsers/${user.UserId}`, { ...user, IsActive: !user.IsActive });
      fetchUsers();
    } catch (err) { addToast(err.response?.data?.Message || err.response?.data?.message || t('users.toastError'), "error"); }
  }
};

const confirmDelete = async (user) => {
  if (user.Role === 'Admin') return addToast(t('users.adminProtected'), "warning");
  if (confirm(t('users.deleteConfirm'))) {
    try {
      await api.delete(`/ShopUsers/${user.UserId}`);
      fetchUsers();
      addToast(t('users.deleted'), "success");
    } catch (err) { addToast(err.response?.data?.Message || err.response?.data?.message || t('users.toastError'), "error"); }
  }
};

onMounted(() => { fetchUsers(); fetchBranches(); });
</script>

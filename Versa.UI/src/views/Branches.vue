<template>
  <div class="px-3 sm:px-5 lg:px-8 pt-2 pb-6 sm:pb-8 bg-[#f8fafc] min-h-screen font-sans text-left">

    <div class="flex flex-col md:flex-row justify-between items-start md:items-end mb-8 gap-4 text-left">
      <div>
        <h1 class="text-2xl font-normal text-slate-900 tracking-tight">{{ t('branches.title') }}</h1>
        <p class="text-[11px] font-normal text-slate-400 uppercase tracking-widest mt-1 flex items-center">
          <span class="w-2 h-2 bg-blue-500 rounded-full mr-2 shadow-[0_0_8px_rgba(59,130,246,0.5)]"></span>
          {{ t('branches.subtitle') }}
        </p>
      </div>

      <div class="flex gap-3 w-full md:w-auto">
        <div class="relative flex-1 md:w-64">
          <span class="absolute inset-y-0 left-0 pl-3 flex items-center text-slate-400">
            <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
            </svg>
          </span>
          <input v-model="searchQuery" type="text" :placeholder="t('branches.searchPlaceholder')"
            class="w-full pl-10 pr-4 py-2.5 bg-white border border-slate-200 rounded-xl text-xs font-normal outline-none focus:ring-2 focus:ring-blue-500/20 transition-all">
        </div>

        <button @click="openCreateModal"
          class="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2.5 rounded-xl font-normal shadow-lg shadow-blue-100 transition-all active:scale-95 uppercase text-xs tracking-widest flex items-center gap-2 whitespace-nowrap">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
            <path fill-rule="evenodd"
              d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z"
              clip-rule="evenodd" />
          </svg>
          {{ t('branches.addBranch') }}
        </button>
      </div>
    </div>

    <div class="bg-white p-4 rounded-2xl shadow-sm border border-slate-200 mb-6 flex items-center justify-between">
      <div class="flex items-center gap-2 ml-2">
        <span class="text-blue-600">???</span>
        <span class="text-[10px] font-normal text-slate-400 uppercase tracking-widest">{{ t('branches.globalStatus')
          }}</span>
      </div>
      <div class="flex items-center px-6 py-2 bg-blue-50/50 rounded-xl border border-blue-100/50">
        <div class="text-right">
          <p class="text-[9px] font-normal text-blue-400 uppercase tracking-widest leading-none mb-1">{{
            t('branches.liveOutlets') }}</p>
          <p class="text-lg font-normal text-blue-700 tabular-nums">{{ filteredBranches.length }} <small
              class="text-[10px] font-normal uppercase ml-1">{{ t('branches.statusActive') }}</small></p>
        </div>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-x-auto">
      <table class="w-full text-left border-collapse">
        <thead class="bg-[#3f3f46] text-white">
          <tr class="text-[10px] font-normal uppercase tracking-[0.2em]">
            <th class="p-5">{{ t('branches.tableName') }}</th>
            <th class="p-5">{{ t('branches.tableLocation') }}</th>
            <th class="p-5 text-center">{{ t('branches.tableStatus') }}</th>
            <th class="p-5 text-center">{{ t('branches.tableRegistration') }}</th>
            <th class="p-5 text-right px-8">{{ t('branches.tableActions') }}</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-slate-50">
          <tr v-for="b in paginatedBranches" :key="b.branchId" class="hover:bg-slate-50/80 transition-colors group">
            <td class="p-5">
              <div class="flex items-center gap-4 text-left">
                <div
                  class="w-10 h-10 rounded-lg bg-blue-50 flex items-center justify-center text-blue-600 font-normal uppercase text-sm border border-blue-100 transition-transform group-hover:scale-110">
                  {{ (b.branchName || 'B').charAt(0) }}
                </div>
                <div>
                  <div class="font-normal text-slate-800 text-sm">{{ b.branchName }}</div>
                  <span v-if="b.isMainBranch || b.IsMainBranch"
                    class="inline-flex items-center px-2 py-0.5 rounded text-[9px] font-normal uppercase bg-amber-50 text-amber-600 border border-amber-100 mt-1">
                    {{ t('branches.mainHQ') }}
                  </span>
                </div>
              </div>
            </td>
            <td class="p-5 text-left">
              <div class="text-sm font-normal text-slate-700">{{ b.location || '-' }}</div>
              <div class="text-[11px] text-slate-400 font-normal mt-0.5 flex items-center gap-1">
                <svg class="w-3 h-3 opacity-50" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path
                    d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z"
                    stroke-width="2" />
                </svg>
                {{ b.phone || '-' }}
              </div>
            </td>
            <td class="p-5 text-center">
              <span
                :class="b.isActive || b.IsActive ? 'bg-emerald-50 text-emerald-600 border-emerald-100' : 'bg-rose-50 text-rose-600 border-rose-100'"
                class="px-3 py-1 rounded-lg text-[10px] font-normal uppercase border shadow-sm tracking-wide">
                {{ (b.isActive || b.IsActive) ? t('branches.statusActive') : t('branches.statusInactive') }}
              </span>
            </td>
            <td class="p-5 text-center text-slate-500 text-xs font-normal tabular-nums">
              {{ b.createdAt ? new Date(b.createdAt).toLocaleDateString('en-GB') : '-' }}
            </td>
            <td class="p-5 text-right px-8">
              <div class="flex justify-end gap-2">
                <button @click="openEditModal(b)"
                  class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-blue-600 hover:bg-blue-50 transition-all shadow-sm">
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24"
                    stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                  </svg>
                </button>
                <button @click="confirmDelete(b.branchId || b.BranchId)"
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
          <tr v-if="filteredBranches.length === 0">
            <td colspan="5" class="p-24 text-center opacity-30 grayscale">
              <p class="text-xs font-normal uppercase tracking-[0.3em]">{{ t('branches.noRecords') }}</p>
            </td>
          </tr>
        </tbody>
      </table>

      <div v-if="filteredBranches.length > 0"
        class="flex justify-between items-center px-6 py-4 bg-slate-50 border-t border-slate-200">
        <div class="text-[10px] font-normal text-slate-400 uppercase tracking-widest text-left">
          {{ t('branches.title') }}: {{ filteredBranches.length }}
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
      <div
        class="bg-white w-full max-w-md rounded-xl shadow-2xl overflow-hidden animate-in zoom-in duration-200 border border-slate-200">
        <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center text-left">
          <h3 class="text-lg font-normal uppercase tracking-tight">{{ editMode ? t('branches.modalTitleEdit') :
            t('branches.modalTitleAdd') }}
          </h3>
          <button @click="showModal = false"
            class="text-white/60 hover:text-white transition-colors text-2xl">�</button>
        </div>
        <div class="p-8 space-y-5 text-left">
          <div class="space-y-1.5 text-left">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
              t('branches.labelName') }}</label>
            <input v-model="form.BranchName" type="text"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all"
              :placeholder="t('branches.placeholderName')">
          </div>
          <div class="space-y-1.5 text-left">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
              t('branches.labelLocation') }}</label>
            <input v-model="form.Location" type="text"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all"
              :placeholder="t('branches.placeholderLocation')">
          </div>
          <div class="space-y-1.5 text-left">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
              t('branches.labelPhone') }}</label>
            <input v-model="form.Phone" type="text"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all"
              :placeholder="t('branches.placeholderPhone')">
          </div>
          <div class="grid grid-cols-1 gap-3 pt-2 text-left">
            <label
              class="flex items-center justify-between p-3 bg-slate-50 border border-slate-100 rounded-lg cursor-pointer hover:border-blue-200 transition-colors">
              <div class="flex flex-col">
                <span class="text-xs font-normal text-slate-700">{{ t('branches.mainHQ') }}</span>
                <span class="text-[9px] text-slate-400 uppercase font-normal tracking-tight italic">Central office
                  status</span>
              </div>
              <input v-model="form.IsMainBranch" type="checkbox"
                class="w-5 h-5 text-blue-600 rounded-md border-slate-300">
            </label>
            <label
              class="flex items-center justify-between p-3 bg-slate-50 border border-slate-100 rounded-lg cursor-pointer hover:border-emerald-200 transition-colors">
              <span class="text-xs font-normal text-slate-700">{{ t('branches.operationalStatus') }}</span>
              <input v-model="form.IsActive" type="checkbox"
                class="w-5 h-5 text-emerald-600 rounded-md border-slate-300">
            </label>
          </div>
          <div class="pt-4 flex gap-3">
            <button @click="showModal = false"
              class="flex-1 py-3 bg-slate-100 text-slate-600 rounded-lg font-normal uppercase text-[10px] hover:bg-slate-200 transition-all">{{
                t('branches.btnCancel') }}</button>
            <button @click="saveBranch"
              class="flex-1 py-3 bg-blue-600 text-white rounded-lg font-normal uppercase text-[10px] shadow-lg shadow-blue-200 hover:bg-blue-700 transition active:scale-95">
              {{ t('branches.btnSave') }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted } from 'vue';
import api from '../services/api';
import { useToast } from '../composables/useToast';
import { useI18n } from '../composables/useI18n';

const { addToast } = useToast();
const { t } = useI18n();

const branches = ref([]);
const searchQuery = ref('');
const showModal = ref(false);
const editMode = ref(false);
const currentPage = ref(1);
const pageSize = ref(10);
const form = ref({ BranchId: null, BranchName: '', Location: '', Phone: '', IsMainBranch: false, IsActive: true });

const filteredBranches = computed(() => {
  const query = searchQuery.value.toLowerCase().trim();
  if (!query) return branches.value;
  return branches.value.filter(b =>
    (b.branchName || b.BranchName || '').toLowerCase().includes(query) ||
    (b.location || b.Location || '').toLowerCase().includes(query) ||
    (b.phone || b.Phone || '').includes(query)
  );
});

const totalPages = computed(() => Math.max(1, Math.ceil(filteredBranches.value.length / pageSize.value)));
const paginatedBranches = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value;
  return filteredBranches.value.slice(start, start + pageSize.value);
});

const goToPage = (p) => { if (p >= 1 && p <= totalPages.value) currentPage.value = p; };
watch(searchQuery, () => { currentPage.value = 1; });

const fetchBranches = async () => {
  try {
    const res = await api.get('/Branches');
    branches.value = Array.isArray(res.data) ? res.data : [];
  } catch (err) { console.error("Fetch Error:", err); }
};

const confirmDelete = async (id) => {
  if (confirm(t('branches.deleteConfirm'))) {
    try {
      await api.delete(`/Branches/${id}`);
      fetchBranches();
      addToast(t('branches.deleted'), "success");
    } catch (err) { addToast(err.response?.data?.Message || t('branches.actionFailed'), "error"); }
  }
};

const openCreateModal = () => {
  editMode.value = false;
  form.value = { BranchId: null, BranchName: '', Location: '', Phone: '', IsMainBranch: false, IsActive: true };
  showModal.value = true;
};

const openEditModal = (branch) => {
  editMode.value = true;
  form.value = {
    BranchId: branch.branchId || branch.BranchId,
    BranchName: branch.branchName || branch.BranchName,
    Location: branch.location || branch.Location,
    Phone: branch.phone || branch.Phone,
    IsMainBranch: branch.isMainBranch || branch.IsMainBranch,
    IsActive: branch.isActive || branch.IsActive
  };
  showModal.value = true;
};

const saveBranch = async () => {
  try {
    const payload = { ...form.value, BranchId: form.value.BranchId || 0 };
    if (editMode.value) await api.put(`/Branches/${payload.BranchId}`, payload);
    else await api.post('/Branches', payload);
    showModal.value = false;
    fetchBranches();
    addToast(t('branches.toastSuccess'), "success");
  } catch (err) { addToast(t('branches.toastError'), "error"); }
};

onMounted(fetchBranches);
</script>

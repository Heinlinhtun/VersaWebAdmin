<template>
  <div class="px-3 sm:px-5 lg:px-8 pt-2 pb-6 sm:pb-8 bg-[#f8fafc] min-h-screen font-sans text-left">

    <div class="flex flex-col md:flex-row justify-between items-start md:items-end mb-8 gap-4 text-left">
      <div>
        <h1 class="text-2xl font-normal text-slate-900 tracking-tight">{{ t('units.title') }}</h1>
        <p class="text-[11px] font-normal text-slate-400 uppercase tracking-widest mt-1 flex items-center">
          <span class="w-2 h-2 bg-blue-500 rounded-full mr-2 shadow-[0_0_8px_rgba(59,130,246,0.5)]"></span>
          {{ t('units.subtitle') }}
        </p>
      </div>

      <div class="flex gap-3 w-full md:w-auto">
        <div class="relative flex-1 md:w-64">
          <span class="absolute inset-y-0 left-0 pl-3 flex items-center text-slate-400">
            <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
            </svg>
          </span>
          <input v-model="searchQuery" type="text" :placeholder="t('units.searchPlaceholder')"
            class="w-full pl-10 pr-4 py-2.5 bg-white border border-slate-200 rounded-xl text-xs font-normal outline-none focus:ring-2 focus:ring-blue-500/20 transition-all">
        </div>

        <button @click="openModal()"
          class="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2.5 rounded-xl font-normal shadow-lg shadow-blue-100 transition-all active:scale-95 uppercase text-xs tracking-widest flex items-center gap-2 whitespace-nowrap">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
            <path fill-rule="evenodd"
              d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z"
              clip-rule="evenodd" />
          </svg>
          {{ t('units.addUnit') }}
        </button>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-x-auto">
      <table class="w-full text-left border-collapse">
        <thead class="bg-[#3f3f46] text-white">
          <tr class="text-[10px] font-normal uppercase tracking-[0.2em]">
            <th class="p-5 w-20 text-center opacity-70">{{ t('units.tableNo') }}</th>
            <th class="p-5">{{ t('units.tableName') }}</th>
            <th class="p-5">{{ t('units.abbreviation') }}</th>
            <th class="p-5 text-right px-8">{{ t('units.tableActions') }}</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-slate-50">
          <tr v-for="(unit, index) in paginatedUnits" :key="unit.unitId"
            class="hover:bg-slate-50/80 transition-colors group">
            <td class="p-5 text-center text-slate-400 font-normal tabular-nums">
              {{ ((currentPage - 1) * pageSize) + index + 1 }}
            </td>
            <td class="p-5">
              <div class="flex items-center gap-4">
                <div
                  class="w-9 h-9 rounded-lg bg-blue-50 flex items-center justify-center text-blue-600 font-normal uppercase text-xs border border-blue-100 transition-transform group-hover:scale-110">
                  {{ (unit.unitName || 'U').charAt(0) }}
                </div>
                <div class="font-normal text-slate-800 text-sm tracking-tight">{{ unit.unitName }}</div>
              </div>
            </td>
            <td class="p-5">
              <span
                class="bg-slate-100 text-slate-500 font-mono text-[11px] px-2.5 py-1 rounded-md border border-slate-200/50">
                {{ unit.shortName }}
              </span>
            </td>
            <td class="p-5 text-right px-8">
              <div class="flex justify-end gap-2">
                <button @click="openModal(unit)"
                  class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-blue-600 hover:bg-blue-50 transition-all shadow-sm"
                  title="Edit">
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24"
                    stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                  </svg>
                </button>
                <button @click="deleteUnit(unit.unitId)"
                  class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-rose-600 hover:bg-rose-50 transition-all shadow-sm"
                  title="Delete">
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24"
                    stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                  </svg>
                </button>
              </div>
            </td>
          </tr>

          <tr v-if="units.length === 0">
            <td colspan="4" class="p-24 text-center">
              <div class="flex flex-col items-center opacity-20 grayscale">
                <svg class="w-12 h-12 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path
                    d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10"
                    stroke-width="2" />
                </svg>
                <p class="text-xs font-normal uppercase tracking-[0.3em] text-slate-900">{{ t('units.noUnits') }}</p>
              </div>
            </td>
          </tr>
        </tbody>
      </table>

      <div v-if="filteredUnits.length > 0"
        class="flex justify-between items-center px-6 py-4 bg-slate-50 border-t border-slate-200">
        <div class="text-[10px] font-normal text-slate-400 uppercase tracking-widest">
          {{ t('units.title') }}: {{ filteredUnits.length }}
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
        class="bg-white w-full max-w-sm rounded-xl shadow-2xl overflow-hidden animate-in zoom-in duration-200 border border-slate-200">

        <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center text-left">
          <h3 class="text-lg font-normal uppercase tracking-tight">{{ isEdit ? t('units.modalTitleEdit') :
            t('units.modalTitleAdd') }}</h3>
          <button @click="showModal = false"
            class="text-white/60 hover:text-white transition-colors text-2xl">�</button>
        </div>

        <div class="p-8 text-left">
          <div class="space-y-4 mb-8">
            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ t('units.labelName')
                }}</label>
              <input v-model="form.unitName" type="text"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all"
                :placeholder="t('units.placeholderName')" />
            </div>
            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ t('units.shortName')
                }}</label>
              <input v-model="form.shortName" type="text"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all"
                :placeholder="t('units.shortNamePlaceholder')" />
            </div>
          </div>

          <div class="flex gap-3">
            <button @click="showModal = false"
              class="flex-1 py-3 bg-slate-100 text-slate-600 rounded-lg font-normal uppercase text-[10px] hover:bg-slate-200 transition-all">{{
                t('units.btnCancel') }}</button>
            <button @click="saveUnit"
              class="flex-1 py-3 bg-blue-600 text-white rounded-lg font-normal uppercase text-[10px] shadow-lg shadow-blue-200 hover:bg-blue-700 transition active:scale-95">
              {{ t('units.btnSave') }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, watch, computed } from 'vue';
import api from '../services/api';
import { useToast } from '../composables/useToast';
import { useI18n } from '../composables/useI18n';

const { addToast } = useToast();
const { t } = useI18n();

const units = ref([]);
const showModal = ref(false);
const searchQuery = ref('');
const isEdit = ref(false);
const form = ref({ unitId: 0, unitName: '', shortName: '' });

const currentPage = ref(1);
const pageSize = ref(10);

const filteredUnits = computed(() => {
  if (!searchQuery.value) return units.value;
  const q = searchQuery.value.toLowerCase();
  return units.value.filter(u =>
    (u.unitName || '').toLowerCase().includes(q) ||
    (u.shortName || '').toLowerCase().includes(q)
  );
});

const totalPages = computed(() => {
  if (filteredUnits.value.length === 0) return 1;
  return Math.ceil(filteredUnits.value.length / pageSize.value);
});

const paginatedUnits = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value;
  const end = start + pageSize.value;
  return filteredUnits.value.slice(start, end);
});

const goToPage = (page) => {
  if (page >= 1 && page <= totalPages.value) {
    currentPage.value = page;
  }
};

const fetchUnits = async () => {
  try {
    const res = await api.get('/units');
    units.value = res.data;
  } catch (err) {
    addToast(t('units.fetchError'), "error");
  }
};

const openModal = (item = null) => {
  if (item) {
    isEdit.value = true;
    form.value = { ...item };
  } else {
    isEdit.value = false;
    form.value = { unitId: 0, unitName: '', shortName: '' };
  }
  showModal.value = true;
};

const saveUnit = async () => {
  try {
    if (isEdit.value) {
      await api.put(`/units/${form.value.unitId}`, form.value);
    } else {
      await api.post('/units', form.value);
    }
    showModal.value = false;
    addToast(t('units.toastSuccess'), "success");
    fetchUnits();
  } catch (err) {
    addToast(t('units.toastError'), "error");
  }
};

watch(searchQuery, () => {
  currentPage.value = 1;
});

const deleteUnit = async (id) => {
  if (confirm(t('units.deleteConfirm'))) {
    try {
      await api.delete(`/units/${id}`);
      fetchUnits();
      addToast(t('units.deleted'), "success");
    } catch (err) {
      addToast(t('units.deleteError'), "error");
    }
  }
};

onMounted(fetchUnits);
</script>

<template>
  <div class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
    <div class="bg-white w-full max-w-lg rounded-xl shadow-2xl overflow-hidden animate-in zoom-in duration-200 border border-slate-200">
      <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
        <h3 class="text-lg font-normal uppercase tracking-tight">{{ t('inventory.importModalTitle') }}</h3>
        <button @click="$emit('close')" class="text-white/60 hover:text-white transition-colors text-2xl">&times;</button>
      </div>

      <div class="p-8 space-y-6">
        <!-- Template Download Section -->
        <div class="bg-blue-50 border border-blue-100 rounded-xl p-5 flex items-center justify-between">
          <div>
            <p class="text-[11px] text-blue-400 font-normal uppercase tracking-widest mb-1">Step 1</p>
            <p class="text-sm font-normal text-blue-800">{{ t('inventory.downloadTemplate') }}</p>
          </div>
          <button @click="downloadTemplate" 
            class="bg-white text-blue-600 border border-blue-200 px-4 py-2 rounded-lg text-[10px] font-normal uppercase hover:bg-blue-50 transition-all shadow-sm active:scale-95 flex items-center gap-2">
            <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path d="M4 16v1a2 2 0 002 2h12a2 2 0 002-2v-1M7 10l5 5m0 0l5-5m-5 5V3" stroke-width="2" />
            </svg>
            GET TEMPLATE
          </button>
        </div>

        <!-- Upload Section -->
        <div class="space-y-4">
          <div>
            <p class="text-[11px] text-slate-400 font-normal uppercase tracking-widest mb-2 ml-1">Step 2: {{ t('inventory.uploadExcel') }}</p>
            <label class="group relative flex flex-col items-center justify-center w-full h-32 border-2 border-dashed border-slate-200 rounded-xl cursor-pointer hover:bg-slate-50 transition-all hover:border-blue-400 overflow-hidden">
              <div class="flex flex-col items-center justify-center pt-5 pb-6">
                <svg v-if="!selectedFile" class="w-8 h-8 mb-3 text-slate-300 group-hover:text-blue-400 transition-colors" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" />
                </svg>
                <div v-else class="text-blue-600 mb-2">
                  <svg class="w-8 h-8" fill="currentColor" viewBox="0 0 20 20">
                    <path fill-rule="evenodd" d="M4 4a2 2 0 012-2h4.586A2 2 0 0112 2.586L15.414 6A2 2 0 0116 7.414V16a2 2 0 01-2 2H6a2 2 0 01-2-2V4zm2 6a1 1 0 011-1h6a1 1 0 110 2H7a1 1 0 01-1-1zm1 3a1 1 0 100 2h6a1 1 0 100-2H7z" clip-rule="evenodd" />
                  </svg>
                </div>
                <p class="text-xs text-slate-500 font-normal">{{ selectedFile ? selectedFile.name : 'Select file (xls, xlsx, csv)' }}</p>
              </div>
              <input type="file" @change="onFileChange" class="hidden" accept=".xlsx, .xls, .csv" />
            </label>
          </div>

          <button @click="handleUpload" 
            :disabled="!selectedFile || importing"
            class="w-full bg-blue-600 text-white py-4 rounded-xl font-normal uppercase text-xs tracking-widest shadow-lg shadow-blue-100 hover:bg-blue-700 transition-all active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2">
            <span v-if="importing" class="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin"></span>
             {{ importing ? 'Processing...' : 'START IMPORT' }}
          </button>
        </div>

        <!-- Progress Indicator -->
        <div v-if="importing" class="space-y-2 pt-2 transition-all duration-300">
          <div class="flex justify-between items-center">
            <span class="text-[11px] font-medium text-blue-700 uppercase tracking-wider">Validating & Importing...</span>
            <span class="text-[10px] font-medium text-blue-600 animate-pulse">Please wait</span>
          </div>
          <div class="w-full bg-blue-100 rounded-full h-1.5 overflow-hidden">
            <div class="bg-blue-600 h-1.5 rounded-full w-full animate-progress origin-left"></div>
          </div>
          <p class="text-[10px] text-slate-500 text-center animate-pulse">Large files may take a minute or more to process completely. Do not close this window.</p>
        </div>

        <!-- Helpful Tip -->
        <div v-else class="text-[10px] text-slate-400 font-normal border-t border-slate-100 pt-4 px-2">
          Tip: Make sure columns match the template exactly for best results.
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
@keyframes indeterminate-progress {
  0% { transform: translateX(-100%) scaleX(0.2); }
  50% { transform: translateX(0%) scaleX(0.5); }
  100% { transform: translateX(100%) scaleX(0.2); }
}
.animate-progress {
  animation: indeterminate-progress 2s infinite linear;
}
</style>

<script setup>
import { ref } from 'vue';
import api from '../services/api';
import { useToast } from '../composables/useToast';
import { useI18n } from '../composables/useI18n';
import { useCsvExport } from '../composables/useCsvExport';
import { sanitizeError } from '../utils/sanitize';

const emit = defineEmits(['close', 'refresh']);
const { addToast } = useToast();
const { t } = useI18n();
const { exportToCsv } = useCsvExport();

const selectedFile = ref(null);
const importing = ref(false);

const onFileChange = (e) => {
  const files = e.target.files;
  if (files.length > 0) {
    selectedFile.value = files[0];
  }
};

const downloadTemplate = () => {
  const headers = [
    "Branch Name", "Item Name", "SKU/Barcode", "Category", "Basic Unit", 
    "Qty", "Purchase Price", "Selling Price", "Expiry Date", "Warning Day count", "Supplier Name"
  ];
  const exampleRow = [
    "Main Branch", "Example Item", "SKU123", "General", "pcs", 
    "100", "1500", "2000", "2026-12-31", "30", "Main Supplier"
  ];
  
  exportToCsv("Inventory_Import_Template", headers, [exampleRow]);
};

const handleUpload = async () => {
  if (!selectedFile.value) return;

  const formData = new FormData();
  formData.append("file", selectedFile.value);

  importing.value = true;
  try {
    const res = await api.post('/ExcelImport/import', formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      }
    });

    addToast(t('inventory.importSuccess'), "success");
    emit('refresh');
    emit('close');
  } catch (err) {
    console.error("Import error:", err);
    // If it's a string (from line 25 of controller), show it. If it's an object with Message, show that.
    const errorMsg = sanitizeError(err.response?.data?.Message || err.response?.data || t('inventory.importError'));
    addToast(errorMsg, "error");
  } finally {
    importing.value = false;
  }
};
</script>

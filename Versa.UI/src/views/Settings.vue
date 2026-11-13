<template>
    <div class="px-3 sm:px-5 lg:px-8 pt-2 pb-6 sm:pb-8 bg-[#f8fafc] min-h-screen font-sans text-left">
        <div class="flex flex-col md:flex-row justify-between items-start md:items-end mb-8 gap-4">
            <div>
                <h1 class="text-2xl font-normal text-slate-900 tracking-tight">{{ t('settings.title') }}</h1>
                <p class="text-[11px] font-normal text-slate-400 uppercase tracking-widest mt-1 flex items-center">
                    <span class="w-2 h-2 bg-blue-500 rounded-full mr-2 shadow-[0_0_8px_rgba(59,130,246,0.5)]"></span>
                    {{ t('settings.subtitle') }}
                </p>
            </div>

            <button @click="saveSettings" :disabled="loading"
                class="bg-blue-600 hover:bg-blue-700 text-white px-8 py-2.5 rounded-xl font-normal shadow-lg shadow-blue-100 transition-all active:scale-95 uppercase text-xs tracking-widest flex items-center gap-2">
                <svg v-if="!loading" xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24"
                    stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="3" d="M5 13l4 4L19 7" />
                </svg>
                <span v-if="loading"
                    class="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin"></span>
                {{ loading ? t('settings.updating') : t('settings.saveConfig') }}
            </button>
        </div>

        <div class="grid grid-cols-1 lg:grid-cols-3 gap-8">
            <div class="space-y-6">
                <div class="bg-white rounded-xl shadow-sm border border-slate-200 p-8 flex flex-col items-center">
                    <h3 class="text-[10px] font-normal text-slate-400 uppercase tracking-[0.2em] mb-6 self-start">
                        {{ t('settings.officialBranding') }}</h3>

                    <div class="relative group">
                        <div
                            class="w-48 h-48 rounded-2xl bg-slate-50 border-2 border-dashed border-slate-200 flex items-center justify-center overflow-hidden transition-all group-hover:border-blue-300">
                            <img v-if="logoPreview" :src="logoPreview" class="w-full h-full object-contain p-4" />
                            <div v-else class="text-center opacity-20">
                                <svg class="w-12 h-12 text-slate-900 mx-auto mb-2" fill="none" stroke="currentColor"
                                    viewBox="0 0 24 24">
                                    <path
                                        d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z"
                                        stroke-width="1.5" />
                                </svg>
                                <span class="text-[10px] font-normal uppercase tracking-widest">{{ t('settings.noLogo')
                                    }}</span>
                            </div>
                        </div>

                        <label
                            class="absolute -bottom-3 left-1/2 -translate-x-1/2 bg-slate-900 text-white px-4 py-2 rounded-lg text-[9px] font-normal uppercase tracking-tighter cursor-pointer hover:bg-black transition-all shadow-xl whitespace-nowrap">
                            {{ t('settings.changeLogo') }}
                            <input type="file" class="hidden" @change="handleLogoChange" accept="image/*" />
                        </label>
                        <button v-if="logoPreview" @click="removeLogo"
                            class="absolute -top-2 -right-2 bg-red-500 text-white w-6 h-6 rounded-full flex items-center justify-center text-[10px] font-normal opacity-90 hover:opacity-100 transition-opacity shadow-lg">
                            &times;</button>
                    </div>

                    <p class="mt-10 text-[10px] text-slate-400 font-normal text-center italic">
                        {{ t('settings.supportsImage') }}
                    </p>
                </div>
                <div class="mt-4">
                    <label class="block text-xs font-normal uppercase text-gray-500 mb-2">{{ t('settings.logoDisplaySize')
                        }}</label>
                    <div class="flex gap-4">
                        <label v-for="size in ['Small', 'Medium', 'Large']" :key="size"
                            class="flex items-center gap-2 cursor-pointer">
                            <input type="radio" v-model="form.LogoSize" :value="size" class="accent-blue-600">
                            <span class="text-sm font-normal">{{ size }}</span>
                        </label>
                    </div>
                </div>
                <div class="mt-4">
                    <label class="block text-xs font-normal uppercase text-gray-500 mb-2">{{ t('settings.receiptFontSize')
                        }}</label>
                    <div class="flex gap-4">
                        <label v-for="size in ['Small', 'Medium', 'Large']" :key="'font-' + size"
                            class="flex items-center gap-2 cursor-pointer">
                            <input type="radio" v-model="form.ReceiptFontSize" :value="size" class="accent-blue-600">
                            <span class="text-sm font-normal">{{ size }}</span>
                        </label>
                    </div>
                </div>
                <div class="mt-4">
                    <label class="block text-xs font-normal uppercase text-gray-500 mb-2">{{
                        t('settings.preferredPaperSize') }}</label>
                    <select v-model="form.PreferredPaperSize"
                        class="w-full p-3 bg-white border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm text-slate-700">
                        <option value="80mm">{{ t('common.thermal80') }}</option>
                        <option value="104mm">{{ t('common.voucher104') }}</option>
                        <option value="104mm-slip">{{ t('common.voucher104Slip') }}</option>
                        <option value="A4">{{ t('common.a4') }}</option>
                        <option value="A5">{{ t('common.a5') }}</option>
                    </select>
                    <p class="text-[11px] text-slate-400 mt-1.5">{{ t('settings.preferredPaperSizeDesc') }}</p>
                </div>
            </div>

            <div class="lg:col-span-2 space-y-6">
                <div class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
                    <div class="bg-slate-50 px-8 py-4 border-b border-slate-200 flex items-center justify-between">
                        <h3 class="text-[11px] font-normal text-slate-500 uppercase tracking-widest">{{
                            t('settings.storeInfo') }}
                        </h3>
                        <span
                            class="px-2.5 py-1 bg-blue-50 text-blue-700 rounded-lg text-[10px] font-normal border border-blue-100">ID:
                            #{{ form.SettingId }}</span>
                    </div>

                    <div class="p-8 space-y-6">
                        <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
                            <div class="space-y-1.5">
                                <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                    t('settings.shopName') }}</label>
                                <input v-model="form.ShopName" type="text"
                                    class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/10 font-normal text-sm transition-all">
                            </div>
                            <div class="space-y-1.5">
                                <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                    t('settings.hotline') }}</label>
                                <input v-model="form.Phone" type="text"
                                    class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/10 font-normal text-sm tabular-nums">
                            </div>
                        </div>

                        <div class="space-y-1.5">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                t('settings.shopAddress') }}</label>
                            <textarea v-model="form.Address" rows="2"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/10 font-normal text-sm transition-all resize-none"></textarea>
                        </div>

                        <div
                            class="grid grid-cols-1 md:grid-cols-2 gap-6 p-6 bg-slate-50 rounded-xl border border-slate-200">
                            <div class="space-y-1.5">
                                <label class="text-[10px] font-normal text-slate-500 uppercase tracking-widest ml-1">{{
                                    t('settings.currency') }}</label>
                                <input v-model="form.CurrencySymbol" type="text"
                                    class="w-full p-3 bg-white border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-blue-600">
                            </div>
                            <div class="space-y-1.5">
                                <label class="text-[10px] font-normal text-slate-500 uppercase tracking-widest ml-1">{{
                                    t('settings.taxRate') }}</label>
                                <input v-model.number="form.TaxRate" type="number"
                                    class="w-full p-3 bg-white border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-slate-700 tabular-nums">
                            </div>
                        </div>

                        <div class="p-5 bg-blue-50 rounded-xl border border-blue-100">
                            <label class="text-[10px] font-normal text-blue-800 uppercase tracking-widest">{{ t('settings.businessDayCutoff') }}</label>
                            <p class="text-[11px] text-blue-600 mt-1 mb-3">{{ t('settings.businessDayCutoffDesc') }}</p>
                            <input v-model="form.BusinessDayCutoffTime" type="time"
                                class="p-3 bg-white border border-blue-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-slate-700 tabular-nums">
                        </div>

                        <div class="p-5 bg-white rounded-xl border border-slate-200 flex flex-wrap items-center justify-between gap-4">
                            <div>
                                <label class="text-[10px] font-normal text-slate-500 uppercase tracking-widest">{{
                                    t('settings.decimalQuantity') }}</label>
                                <p class="text-[11px] text-slate-400 mt-1">
                                    {{ t('settings.decimalQuantityDesc') }}
                                </p>
                            </div>
                            <div class="flex items-center gap-3 shrink-0">
                              <label class="text-xs text-slate-500">
                                {{ t('settings.quantityDecimalPlaces') }}
                                <select v-model.number="form.QuantityDecimalPlaces" :disabled="!form.UseDecimalQuantity"
                                  class="block mt-1 p-2 border border-slate-200 rounded-lg bg-white disabled:opacity-40">
                                  <option v-for="places in 5" :key="places" :value="places">{{ places }} ({{ (10 ** -places).toFixed(places) }})</option>
                                </select>
                              </label>
                            <label class="relative inline-flex items-center cursor-pointer shrink-0">
                                <input v-model="form.UseDecimalQuantity" type="checkbox" class="sr-only peer">
                                <span
                                    class="w-12 h-6 bg-slate-200 rounded-full peer peer-checked:bg-blue-600 transition-colors after:content-[''] after:absolute after:top-0.5 after:left-0.5 after:w-5 after:h-5 after:bg-white after:rounded-full after:shadow-sm after:transition-transform peer-checked:after:translate-x-6"></span>
                            </label>
                            </div>
                        </div>

                        <div class="p-5 bg-amber-50 rounded-xl border border-amber-100 flex flex-wrap items-center justify-between gap-4">
                            <div>
                                <label class="text-[10px] font-normal text-amber-800 uppercase tracking-widest">{{ t('settings.showLatestPurchasePrice') }}</label>
                                <p class="text-[11px] text-amber-700 mt-1">{{ t('settings.showLatestPurchasePriceDesc') }}</p>
                            </div>
                            <label class="relative inline-flex items-center cursor-pointer shrink-0">
                                <input v-model="form.ShowLatestPurchasePrice" type="checkbox" class="sr-only peer">
                                <span class="w-12 h-6 bg-amber-200 rounded-full peer peer-checked:bg-amber-600 transition-colors after:content-[''] after:absolute after:top-0.5 after:left-0.5 after:w-5 after:h-5 after:bg-white after:rounded-full after:shadow-sm after:transition-transform peer-checked:after:translate-x-6"></span>
                            </label>
                        </div>

                        <div class="space-y-1.5">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                t('settings.receiptFooter') }}</label>
                            <textarea v-model="form.ReceiptFooter" rows="4"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/10 font-normal text-sm italic resize-y"></textarea>
                        </div>
                    </div>
                </div>

                <div class="p-4 bg-blue-50 rounded-xl border border-blue-100 flex items-start gap-4">
                    <div class="p-2 bg-white rounded-lg text-blue-500 shadow-sm flex items-center justify-center">
                        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5"
                                d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                        </svg>
                    </div>
                    <div>
                        <h4 class="text-[10px] font-normal text-blue-800 uppercase tracking-widest">{{
                            t('settings.realtimeSync') }}</h4>
                        <p class="text-[11px] text-blue-600 font-normal leading-relaxed mt-1">
                            {{ t('settings.realtimeSyncDesc') }}
                        </p>
                    </div>
                </div>
            </div>
        </div>
    </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import api from '../services/api';
import { useToast } from '../composables/useToast';
import { applyQuantitySettings, normalizeQuantityDecimals } from '../composables/useQuantitySettings';
import { useI18n } from '../composables/useI18n';

const { addToast } = useToast();
const { t } = useI18n();
const loading = ref(false);
const logoPreview = ref(null);
const logoFile = ref(null);
const toBooleanSetting = (value) => value === true || value === 1 || value === '1' || value === 'true' || value === 'True';

const form = ref({
    SettingId: 0,
    ShopName: '',
    Address: '',
    Phone: '',
    CurrencySymbol: 'Ks',
    TaxRate: 0,
    ReceiptFooter: '',
    LogoData: null,
    LogoSize: 'Large', // Default size
    ReceiptFontSize: 'Medium', // Default receipt font size
    PreferredPaperSize: '80mm',
    QuantityDecimalPlaces: 2,
    UseDecimalQuantity: false,
    ShowLatestPurchasePrice: false,
    BusinessDayCutoffTime: '00:00'
});

// GET API: Settings Management
const saveSettings = async () => {
    if (!form.value.ShopName) return addToast(t('settings.shopNameRequired'), "error");

    loading.value = true;
    try {
        const formData = new FormData();
        Object.keys(form.value).forEach(key => {
            formData.append(key, form.value[key]);
        });

        if (logoFile.value) {
            formData.append('logo', logoFile.value);
        }

        const res = await api.put('/Settings', formData);

        // Local Storage Cache
        const updatedSettings = {
            ...form.value,
            LogoPath: res.data.LogoPath
        };
        localStorage.setItem('ShopSettings', JSON.stringify(updatedSettings));
        applyQuantitySettings(updatedSettings);

        addToast(t('settings.saved'), "success");
        await fetchSettings();
    } catch (err) {
        addToast(t('settings.failed'), "error");
    } finally {
        loading.value = false;
    }
};

const fetchSettings = async () => {
    try {
        const res = await api.get('/Settings');
        if (res.data) {
            form.value = {
                ...res.data,
                PreferredPaperSize: ['80mm', '104mm', '104mm-slip', 'A4', 'A5'].includes(res.data.PreferredPaperSize ?? res.data.preferredPaperSize)
                    ? (res.data.PreferredPaperSize ?? res.data.preferredPaperSize)
                    : '80mm',
                QuantityDecimalPlaces: normalizeQuantityDecimals(res.data.QuantityDecimalPlaces ?? res.data.quantityDecimalPlaces),
                UseDecimalQuantity: toBooleanSetting(res.data.UseDecimalQuantity ?? res.data.useDecimalQuantity),
                ShowLatestPurchasePrice: toBooleanSetting(res.data.ShowLatestPurchasePrice ?? res.data.showLatestPurchasePrice),
                BusinessDayCutoffTime: String(res.data.BusinessDayCutoffTime ?? res.data.businessDayCutoffTime ?? '00:00').slice(0, 5)
            };
            if (res.data.LogoData) {
                logoPreview.value = res.data.LogoData;
            }
            localStorage.setItem('ShopSettings', JSON.stringify(form.value));
            applyQuantitySettings(form.value);
        }
    } catch (err) {
        addToast(t('settings.loadError'), "error");
    }
};

const removeLogo = () => {
    form.value.LogoData = null;
    logoPreview.value = null;
    logoFile.value = null;
};

const handleLogoChange = (e) => {
    const file = e.target.files[0];
    if (file) {
        logoFile.value = file;
        logoPreview.value = URL.createObjectURL(file);
    }
};

// SAVE API: PUT Method with Multipart Form Data


const updateLocalStorage = () => {
    localStorage.setItem('ShopName', form.value.ShopName);
    localStorage.setItem('ShopAddress', form.value.Address);
    localStorage.setItem('ShopPhone', form.value.Phone);
    localStorage.setItem('ReceiptFooter', form.value.ReceiptFooter);
    localStorage.setItem('CurrencySymbol', form.value.CurrencySymbol);
};

onMounted(fetchSettings);
</script>

<style scoped>
.custom-scrollbar::-webkit-scrollbar {
    width: 4px;
}

.custom-scrollbar::-webkit-scrollbar-thumb {
    background: #e2e8f0;
    border-radius: 10px;
}
</style>

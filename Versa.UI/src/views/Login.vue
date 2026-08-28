<template>
  <div class="min-h-screen bg-[#f1f5f9] flex items-center justify-center p-6 font-sans relative overflow-hidden">
    <!-- Language Selector (Top Left) -->
    <div class="absolute top-4 left-4 sm:top-6 sm:left-6 z-[60]">
      <div class="relative">
        <button @click="showLangMenu = !showLangMenu"
          class="flex items-center gap-2 px-3 py-2 bg-white/80 backdrop-blur-md border border-slate-200 rounded-xl shadow-sm hover:border-slate-300 transition-all active:scale-95 group">
          <div class="w-5 h-3.5 rounded-sm overflow-hidden border border-slate-100 shrink-0">
            <template v-if="locale === 'en'">
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 500 300">
                <rect width="500" height="300" fill="#012169" />
                <path d="M0 0l500 300M500 0L0 300" stroke="#fff" stroke-width="60" />
                <path d="M0 0l500 300M500 0L0 300" stroke="#C8102E" stroke-width="40" />
                <path d="M250 0v300M0 150h500" stroke="#fff" stroke-width="100" />
                <path d="M250 0v300M0 150h500" stroke="#C8102E" stroke-width="60" />
              </svg>
            </template>
            <template v-else>
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 900 600">
                <rect width="900" height="600" fill="#FECB00" />
                <rect width="900" height="400" y="200" fill="#34B233" />
                <rect width="900" height="400" y="400" fill="#EA2839" />
                <path d="M450 116l31 92h98l-79 58 30 92-80-57-80 57 30-92-79-58h98z" fill="#fff" />
              </svg>
            </template>
          </div>
          <span class="text-[10px] font-normal text-slate-600 uppercase tracking-widest">{{ locale }}</span>
          <svg class="w-3 h-3 text-slate-400 group-hover:text-slate-600 transition-transform"
            :class="{ 'rotate-180': showLangMenu }" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="3" d="M19 9l-7 7-7-7" />
          </svg>
        </button>

        <!-- Dropdown Menu -->
        <transition enter-active-class="transition duration-200 ease-out"
          enter-from-class="transform scale-95 opacity-0 -translate-y-2"
          enter-to-class="transform scale-100 opacity-100 translate-y-0"
          leave-active-class="transition duration-150 ease-in"
          leave-from-class="transform scale-100 opacity-100 translate-y-0"
          leave-to-class="transform scale-95 opacity-0 -translate-y-2">
          <div v-if="showLangMenu"
            class="absolute left-0 mt-2 w-36 bg-white rounded-2xl shadow-2xl border border-slate-100 p-2 space-y-1">
            <button @click="setLocale('en'); showLangMenu = false"
              :class="locale === 'en' ? 'bg-slate-50 text-blue-600' : 'text-slate-600 hover:bg-slate-50'"
              class="w-full flex items-center gap-3 px-3 py-2.5 rounded-xl transition-colors">
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 500 300"
                class="w-5 h-3 rounded-sm shrink-0 border border-slate-100">
                <rect width="500" height="300" fill="#012169" />
                <path d="M0 0l500 300M500 0L0 300" stroke="#fff" stroke-width="60" />
                <path d="M0 0l500 300M500 0L0 300" stroke="#C8102E" stroke-width="40" />
                <path d="M250 0v300M0 150h500" stroke="#fff" stroke-width="100" />
                <path d="M250 0v300M0 150h500" stroke="#C8102E" stroke-width="60" />
              </svg>
              <span class="text-[10px] font-normal uppercase tracking-widest">English</span>
            </button>
            <button @click="setLocale('mm'); showLangMenu = false"
              :class="locale === 'mm' ? 'bg-slate-50 text-blue-600' : 'text-slate-600 hover:bg-slate-50'"
              class="w-full flex items-center gap-3 px-3 py-2.5 rounded-xl transition-colors">
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 900 600"
                class="w-5 h-3 rounded-sm shrink-0 border border-slate-100">
                <rect width="900" height="600" fill="#FECB00" />
                <rect width="900" height="400" y="200" fill="#34B233" />
                <rect width="900" height="400" y="400" fill="#EA2839" />
                <path d="M450 116l31 92h98l-79 58 30 92-80-57-80 57 30-92-79-58h98z" fill="#fff" />
              </svg>
              <span class="text-[10px] font-normal uppercase tracking-widest">Myanmar</span>
            </button>
          </div>
        </transition>
      </div>
    </div>

    <!-- Theme Toggle (Top Right) -->
    <div class="absolute top-4 right-4 sm:top-6 sm:right-6 z-[60]">
      <button @click="toggleTheme"
        class="p-2 sm:p-2.5 bg-white/80 backdrop-blur-md border border-slate-200 rounded-xl shadow-sm hover:border-slate-300 transition-all active:scale-95 text-slate-500 hover:text-slate-800"
        :title="theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'">
        <svg v-if="theme === 'dark'" class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M21 12.79A9 9 0 1111.21 3c0 .17-.01.34-.01.51A7.5 7.5 0 0020.49 13c.17 0 .34 0 .51-.01z" />
        </svg>
        <svg v-else class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M12 3v2m0 14v2m9-9h-2M5 12H3m15.36 6.36l-1.41-1.41M7.05 7.05 5.64 5.64m12.72 0-1.41 1.41M7.05 16.95l-1.41 1.41M12 8a4 4 0 100 8 4 4 0 000-8z" />
        </svg>
      </button>
    </div>

    <div class="absolute top-[-10%] left-[-10%] w-[500px] h-[500px] bg-slate-200/40 rounded-full blur-[120px]"></div>
    <div class="absolute bottom-[-10%] right-[-10%] w-[500px] h-[500px] bg-blue-100/20 rounded-full blur-[120px]"></div>

    <div class="max-w-[420px] w-full relative z-10">

      <div class="flex flex-col items-center mb-10 animate-in fade-in zoom-in duration-700">
        <div
          class="w-28 h-28 bg-[#1e293b] rounded-full p-5 shadow-2xl border-4 border-white flex items-center justify-center mb-4 overflow-hidden group hover:scale-105 transition-transform duration-500">
          <img
            src="https://blogger.googleusercontent.com/img/b/R29vZ2xl/AVvXsEg4W8bqklINfCdXYMP_AC8Br7gBjPtXi280luUG0Vp3wJpf1YsHc2C4fi5O_kv2QvHEMoxKQxjf8MQzJn5iEBn7ye2eN4pNm6OnxDss-OgxEudg-tFmfAal-lQiRByzm5s3jEy4nxdnVKJU4MMVT6EYa6uTz24cwWE5YXTe12bhrm8yzC-1AYBA3wE_or0d/s320/headerImage.png"
            alt="Versa POS" class="w-full h-full object-contain filter drop-shadow-md" />
        </div>
        <h1 class="text-3xl font-normal text-slate-900 tracking-tighter uppercase italic leading-none">Versa POS</h1>
        <p class="text-[10px] font-normal text-slate-400 uppercase tracking-[0.4em] mt-2">{{ t('login.enterpriseSolution')
        }}</p>
      </div>

      <div
        class="bg-white rounded-[40px] shadow-[0_30px_60px_rgba(0,0,0,0.06)] border border-slate-200/50 p-10 backdrop-blur-sm">
        <form @submit.prevent="handleLogin" class="space-y-6">

          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-2 block">{{
              t('login.organizationId') }}</label>
            <input v-model="form.shopLoginId" type="text" placeholder="e.g. branch_01" required
              class="w-full px-5 py-4 bg-slate-50 border border-slate-200 rounded-2xl font-normal text-slate-800 outline-none focus:ring-4 focus:ring-blue-500/10 focus:border-blue-500 focus:bg-white transition-all placeholder:text-slate-300" />
          </div>

          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-2 block">{{
              t('login.adminUsername') }}</label>
            <input v-model="form.username" type="text" placeholder="Enter username" required
              class="w-full px-5 py-4 bg-slate-50 border border-slate-200 rounded-2xl font-normal text-slate-800 outline-none focus:ring-4 focus:ring-blue-500/10 focus:border-blue-500 focus:bg-white transition-all placeholder:text-slate-300" />
          </div>

          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-2 block">{{
              t('login.secureAccessKey') }}</label>
            <input v-model="form.password" type="password" placeholder="••••••••" required
              class="w-full px-5 py-4 bg-slate-50 border border-slate-200 rounded-2xl font-normal text-slate-800 outline-none focus:ring-4 focus:ring-blue-500/10 focus:border-blue-500 focus:bg-white transition-all placeholder:text-slate-300" />
          </div>

          <button type="submit" :disabled="loading"
            class="w-full bg-[#1e293b] text-white font-normal uppercase text-[11px] tracking-[0.2em] py-5 rounded-2xl shadow-xl shadow-slate-200 hover:bg-slate-900 transition-all duration-300 disabled:opacity-50 disabled:cursor-not-allowed mt-2 active:scale-95">
            <span v-if="loading" class="flex items-center justify-center gap-2">
              <svg class="animate-spin h-4 w-4 text-white/50" viewBox="0 0 24 24" fill="none">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                <path class="opacity-75" fill="currentColor"
                  d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z">
                </path>
              </svg>
              {{ t('login.authenticate') }}
            </span>
            <span v-else>{{ t('login.authorizeAccess') }}</span>
          </button>
        </form>

        <div v-if="errorMessage"
          class="mt-8 p-4 bg-rose-50 rounded-2xl border border-rose-100 text-center animate-in slide-in-from-top-2">
          <p
            class="text-rose-600 text-[10px] font-normal uppercase tracking-wider flex items-center justify-center gap-2">
            <span>⚠️</span> {{ errorMessage }}
          </p>
        </div>
      </div>

      <div class="mt-12 text-center flex flex-col items-center gap-2">
        <p class="text-[9px] text-slate-400 font-normal uppercase tracking-[0.4em] opacity-40">
          {{ t('login.authorizedOnly') }} &copy; {{ new Date().getFullYear() }} Versa Core
        </p>
        <router-link to="/privacy-policy" class="text-[9px] text-slate-400 font-normal uppercase tracking-[0.2em] opacity-60 hover:opacity-100 transition-opacity">Privacy Policy</router-link>
      </div>
    </div>
  </div>
</template>

<script setup>
import { reactive, ref, onMounted, onUnmounted } from 'vue';
import { useRouter } from 'vue-router';
import api from '../services/api';
import { useI18n } from '../composables/useI18n';

const THEME_KEY = 'app_theme';
const router = useRouter();
const loading = ref(false);
const errorMessage = ref('');
const showLangMenu = ref(false);
const theme = ref(localStorage.getItem(THEME_KEY) === 'dark' ? 'dark' : 'light');
const { t, locale, setLocale: setLocaleI18n } = useI18n();

const setLocale = (lang) => {
  setLocaleI18n(lang);
  localStorage.setItem('app_locale', lang);
};

const closeLangMenu = (e) => {
  if (!e.target.closest('.relative')) showLangMenu.value = false;
};

onMounted(() => {
  const savedLocale = localStorage.getItem('app_locale');
  if (savedLocale) setLocaleI18n(savedLocale);
  window.addEventListener('click', closeLangMenu);
});

onUnmounted(() => {
  window.removeEventListener('click', closeLangMenu);
});

const form = reactive({
  shopLoginId: '',
  username: '',
  password: ''
});

const applyTheme = (nextTheme) => {
  const root = document.documentElement;
  const isDark = nextTheme === 'dark';
  root.classList.toggle('theme-dark', isDark);
  root.classList.toggle('theme-light', !isDark);
  root.setAttribute('data-theme', nextTheme);
  localStorage.setItem(THEME_KEY, nextTheme);
};

const toggleTheme = () => {
  theme.value = theme.value === 'dark' ? 'light' : 'dark';
  applyTheme(theme.value);
};

applyTheme(theme.value);

const handleLogin = async () => {
  loading.value = true;
  errorMessage.value = '';
  try {
    const response = await api.post('/auth/login', form);
    const data = response.data;

    if (data.token) {
      localStorage.setItem('token', data.token);
      localStorage.setItem('UserId', data.userId || data.UserId || 0);
      localStorage.setItem('shopName', data.shopName || 'VERSA POS');
      localStorage.setItem('Role', data.role || data.Role || 'Staff');
      localStorage.setItem('AllowBranch', String(data.allowBranch ?? data.AllowBranch ?? false));
      localStorage.setItem('BranchId', data.branchId !== undefined ? data.branchId : (data.BranchId || 0));
      localStorage.setItem('BranchName', data.branchName || data.BranchName || 'Main Branch');
      localStorage.setItem('username', data.username || data.Username || '');

      router.push('/dashboard');
    }
  } catch (error) {
    errorMessage.value = error.response?.data?.message || t('login.accessDenied');
  } finally {
    loading.value = false;
  }
};
</script>

<style scoped>
.animate-in {
  animation: modalIn 0.6s cubic-bezier(0.16, 1, 0.3, 1);
}

@keyframes modalIn {
  from {
    opacity: 0;
    transform: translateY(20px) scale(0.95);
  }

  to {
    opacity: 1;
    transform: translateY(0) scale(1);
  }
}
</style>

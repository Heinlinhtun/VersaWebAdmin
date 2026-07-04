<template>
  <div class="flex h-screen bg-[#f8fafc] overflow-hidden relative font-sans text-left transition-colors duration-300">

    <!-- Toasts container -->
    <div class="fixed top-6 right-6 z-[9999] space-y-3 pointer-events-none">
      <transition-group enter-active-class="transform transition duration-500 ease-out"
        enter-from-class="translate-x-full opacity-0 scale-90" enter-to-class="translate-x-0 opacity-100 scale-100"
        leave-active-class="transition duration-300 ease-in" leave-from-class="opacity-100"
        leave-to-class="opacity-0 -translate-y-2">
        <div v-for="toast in toasts" :key="toast.id"
          class="pointer-events-auto min-w-[320px] max-w-[450px] flex items-center p-4 rounded-xl shadow-2xl border backdrop-blur-md transition-all"
          :class="[toastConfig[toast.type].bg, toastConfig[toast.type].text]">
          <div class="mr-3 text-xl">{{ toastConfig[toast.type].icon }}</div>
          <div class="flex-1 font-normal text-[11px] uppercase tracking-wider leading-relaxed">
            {{ toast.message }}
          </div>
          <button @click="removeToast(toast.id)"
            class="ml-4 hover:scale-125 transition opacity-40 hover:opacity-100">✕</button>
        </div>
      </transition-group>
    </div>

    <!-- Mobile sidebar backdrop -->
    <div v-if="isSidebarOpen" @click="isSidebarOpen = false"
      class="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-40 lg:hidden"></div>

    <!-- Sidebar -->
    <aside
      :class="[
        'fixed inset-y-0 left-0 z-50 w-64 bg-[#1e293b] text-slate-300 flex flex-col shadow-2xl transform transition-all duration-300 ease-in-out lg:static lg:translate-x-0',
        isSidebarOpen ? 'translate-x-0' : '-translate-x-full',
        isSidebarCollapsed ? 'lg:w-20 sidebar-collapsed' : 'lg:w-64'
      ]">

      <div
        class="flex flex-col items-center justify-center shrink-0 border-b border-slate-800/50 transition-all duration-300"
        :class="isSidebarCollapsed ? 'px-3 py-6' : 'px-8 py-10'">
        <div class="select-none pointer-events-none transition-all duration-300"
          :class="isSidebarCollapsed ? 'w-10 h-10' : 'w-20 h-20'">
          <img
            src="https://blogger.googleusercontent.com/img/b/R29vZ2xl/AVvXsEg4W8bqklINfCdXYMP_AC8Br7gBjPtXi280luUG0Vp3wJpf1YsHc2C4fi5O_kv2QvHEMoxKQxjf8MQzJn5iEBn7ye2eN4pNm6OnxDss-OgxEudg-tFmfAal-lQiRByzm5s3jEy4nxdnVKJU4MMVT6EYa6uTz24cwWE5YXTe12bhrm8yzC-1AYBA3wE_or0d/s320/headerImage.png"
            alt="Versa POS" class="w-full h-full object-contain filter drop-shadow-lg" />
        </div>
      </div>

      <nav class="flex-1 p-4 pt-0 space-y-1 overflow-y-auto custom-scrollbar no-scrollbar">

        <router-link v-if="can('canViewDashboard')" to="/dashboard" class="nav-item"
          :title="isSidebarCollapsed ? t('layout.nav.dashboard') : ''">
          <svg class="nav-icon nav-icon--spaced" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <rect x="3" y="3" width="7" height="7"></rect>
            <rect x="14" y="3" width="7" height="7"></rect>
            <rect x="14" y="14" width="7" height="7"></rect>
            <rect x="3" y="14" width="7" height="7"></rect>
          </svg>
          <span class="nav-label">{{ t('layout.nav.dashboard') }}</span>
        </router-link>

        <router-link to="/sale" class="nav-item" :title="isSidebarCollapsed ? t('layout.nav.saleTerminal') : ''">
          <svg class="nav-icon nav-icon--spaced" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="9" cy="21" r="1"></circle>
            <circle cx="20" cy="21" r="1"></circle>
            <path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6"></path>
          </svg>
          <span class="nav-label">{{ t('layout.nav.saleTerminal') }}</span>
        </router-link>

        <template v-if="can('canViewInventory')">
          <div class="nav-header nav-section">{{ t('layout.nav.stockLedger') }}</div>

          <router-link to="/inventory" class="nav-item" :title="isSidebarCollapsed ? t('layout.nav.liveInventory') : ''">
            <svg class="nav-icon nav-icon--spaced" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M21 8V20a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8"></path>
              <path d="M21 3H3v5h18V3z"></path>
            </svg>
            <span class="nav-label">{{ t('layout.nav.liveInventory') }}</span>
          </router-link>

          <router-link to="/products" class="nav-item" :title="isSidebarCollapsed ? t('layout.nav.products') : ''">
            <svg class="nav-icon nav-icon--spaced" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z"></path>
              <line x1="7" y1="7" x2="7.01" y2="7"></line>
            </svg>
            <span class="nav-label">{{ t('layout.nav.products') }}</span>
          </router-link>
        </template>

        <template v-if="can('canViewPurchases')">
          <router-link to="/purchases" class="nav-item" :title="isSidebarCollapsed ? t('layout.nav.purchases') : ''">
            <svg class="nav-icon nav-icon--spaced" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M6 2L3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4Z"></path>
              <line x1="3" y1="6" x2="21" y2="6"></line>
              <path d="M16 10a4 4 0 0 1-8 0"></path>
            </svg>
            <span class="nav-label">{{ t('layout.nav.purchases') }}</span>
          </router-link>

          <router-link to="/consignments" class="nav-item" :title="isSidebarCollapsed ? t('layout.nav.consignments') : ''">
            <svg class="nav-icon nav-icon--spaced" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M20 7H4a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2Z"></path>
              <path d="M16 7V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v2"></path>
              <path d="M12 11v5"></path>
              <path d="M9.5 13.5h5"></path>
            </svg>
            <span class="nav-label">{{ t('layout.nav.consignments') }}</span>
          </router-link>
        </template>

        <router-link v-if="can('canViewAdjustments')" to="/inventory-adjustments" class="nav-item"
          :title="isSidebarCollapsed ? t('layout.nav.adjustments') : ''">
          <svg class="nav-icon nav-icon--spaced" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"></path>
            <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"></path>
          </svg>
          <span class="nav-label">{{ t('layout.nav.adjustments') }}</span>
        </router-link>

        <router-link v-if="can('canViewHistory')" to="/historycenter" class="nav-item"
          :title="isSidebarCollapsed ? t('layout.nav.historyCenter') : ''">
          <svg class="nav-icon nav-icon--spaced" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="12" cy="12" r="10"></circle>
            <polyline points="12 6 12 12 16 14"></polyline>
          </svg>
          <span class="nav-label">{{ t('layout.nav.historyCenter') }}</span>
        </router-link>

        <template v-if="can('canViewMasterData') || can('canViewPartners') || can('canViewExpenses')">
          <div class="nav-header nav-section">{{ t('layout.nav.partnersOrg') }}</div>

          <router-link v-if="can('canViewMasterData')" to="/master-data" class="nav-item"
            :title="isSidebarCollapsed ? t('layout.nav.masterData') : ''">
            <svg class="nav-icon nav-icon--spaced" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M4 6h16M4 10h16M4 14h16M4 18h16"></path>
            </svg>
            <span class="nav-label">{{ t('layout.nav.masterData') }}</span>
          </router-link>

          <router-link v-if="can('canViewPartners')" to="/partners" class="nav-item"
            :title="isSidebarCollapsed ? t('layout.nav.partners') : ''">
            <svg class="nav-icon nav-icon--spaced" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path>
              <circle cx="9" cy="7" r="4"></circle>
              <path d="M23 21v-2a4 4 0 0 0-3-3.87"></path>
              <path d="M16 3.13a4 4 0 0 1 0 7.75"></path>
            </svg>
            <span class="nav-label">{{ t('layout.nav.partners') }}</span>
          </router-link>

          <router-link v-if="can('canViewExpenses')" to="/expenses" class="nav-item"
            :title="isSidebarCollapsed ? t('layout.nav.dailyExpenses') : ''">
            <svg class="nav-icon nav-icon--spaced" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <line x1="12" y1="1" x2="12" y2="23"></line>
              <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path>
            </svg>
            <span class="nav-label">{{ t('layout.nav.dailyExpenses') }}</span>
          </router-link>
        </template>

        <router-link v-if="can('canViewSettings')" to="/settings" class="nav-item"
          :title="isSidebarCollapsed ? t('layout.nav.settings') : ''">
          <svg class="nav-icon nav-icon--spaced" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
            stroke-linecap="round" stroke-linejoin="round">
            <circle cx="12" cy="12" r="3"></circle>
            <path
              d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z">
            </path>
          </svg>
          <span class="nav-label">{{ t('layout.nav.settings') }}</span>
        </router-link>

        <router-link v-if="currentUser.role === 'Admin'" to="/reports"
          class="nav-item mt-6 bg-blue-600/10 border border-blue-500/10 text-blue-400"
          :title="isSidebarCollapsed ? t('layout.nav.businessIntelligence') : ''">
          <svg class="nav-icon nav-icon--spaced" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="18" y1="20" x2="18" y2="10"></line>
            <line x1="12" y1="20" x2="12" y2="4"></line>
            <line x1="6" y1="20" x2="6" y2="14"></line>
          </svg>
          <span class="nav-label">{{ t('layout.nav.businessIntelligence') }}</span>
        </router-link>

        <div class="mt-auto pt-6 border-t border-white/5 px-2">
          <div class="flex items-center gap-2 nav-footer">
            <!-- Language Selector -->
            <div class="relative flex-1">
              <button @click="showLangMenu = !showLangMenu"
                class="w-full h-10 flex items-center justify-between px-3 bg-white/5 hover:bg-white/10 border border-white/5 rounded-xl transition-all active:scale-95 group">
                <div class="flex items-center gap-2">
                  <div class="w-5 h-3.5 rounded-sm overflow-hidden border border-white/10 shrink-0">
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
                  <span class="text-[10px] font-normal text-slate-400 uppercase tracking-widest">{{ locale === 'en' ? 'English' : 'Myanmar' }}</span>
                </div>
                <svg class="w-3 h-3 text-slate-500 group-hover:text-slate-300 transition-transform"
                  :class="{ 'rotate-180': showLangMenu }" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="3" d="M19 9l-7 7-7-7" />
                </svg>
              </button>

              <!-- Dropdown Menu -->
              <transition enter-active-class="transition duration-200 ease-out"
                enter-from-class="transform scale-95 opacity-0 translate-y-2"
                enter-to-class="transform scale-100 opacity-100 translate-y-0"
                leave-active-class="transition duration-150 ease-in"
                leave-from-class="transform scale-100 opacity-100 translate-y-0"
                leave-to-class="transform scale-95 opacity-0 translate-y-2">
                <div v-if="showLangMenu"
                  class="absolute bottom-full left-0 mb-2 w-40 bg-[#1e293b] rounded-2xl shadow-2xl border border-white/5 p-2 space-y-1 z-50">
                  <button @click="setLocale('en'); showLangMenu = false"
                    :class="locale === 'en' ? 'bg-white/10 text-white' : 'text-slate-400 hover:bg-white/5 hover:text-slate-200'"
                    class="w-full flex items-center gap-3 px-3 py-2.5 rounded-xl transition-colors">
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 500 300"
                      class="w-5 h-3 rounded-sm shrink-0 border border-white/10">
                      <rect width="500" height="300" fill="#012169" />
                      <path d="M0 0l500 300M500 0L0 300" stroke="#fff" stroke-width="60" />
                      <path d="M0 0l500 300M500 0L0 300" stroke="#C8102E" stroke-width="40" />
                      <path d="M250 0v300M0 150h500" stroke="#fff" stroke-width="100" />
                      <path d="M250 0v300M0 150h500" stroke="#C8102E" stroke-width="60" />
                    </svg>
                    <span class="text-[10px] font-normal uppercase tracking-widest">English</span>
                  </button>
                  <button @click="setLocale('mm'); showLangMenu = false"
                    :class="locale === 'mm' ? 'bg-white/10 text-white' : 'text-slate-400 hover:bg-white/5 hover:text-slate-200'"
                    class="w-full flex items-center gap-3 px-3 py-2.5 rounded-xl transition-colors">
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 900 600"
                      class="w-5 h-3 rounded-sm shrink-0 border border-white/10">
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

            <!-- Theme Toggle -->
            <button @click="toggleTheme"
              class="w-10 h-10 flex items-center justify-center bg-white/5 hover:bg-white/10 border border-white/5 rounded-xl transition-all active:scale-95 text-slate-400 hover:text-slate-200"
              :title="theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'">
              <svg v-if="theme === 'dark'" class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364 6.364l-.707-.707M6.343 6.343l-.707-.707m12.728 0l-.707.707M6.343 16.95l-.707.707M14.99 5.01a7 7 0 100 13.98A7 7 0 0014.99 5.01z" />
              </svg>
              <svg v-else class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z" />
              </svg>
            </button>
          </div>
        </div>
      </nav>

      <div class="p-4 border-t border-slate-800/50">
        <button @click="handleLogout"
          class="w-full flex items-center p-3 text-rose-400 hover:bg-rose-500/10 rounded-xl transition-all font-normal text-[10px] uppercase tracking-widest"
          :title="isSidebarCollapsed ? t('layout.terminateSession') : ''">
          <svg class="w-4 h-4 nav-icon nav-icon--spaced" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
            <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"></path>
            <polyline points="16 17 21 12 16 7"></polyline>
            <line x1="21" y1="12" x2="9" y2="12"></line>
          </svg>
          <span class="nav-label">{{ t('layout.terminateSession') }}</span>
        </button>
      </div>
    </aside>

    <!-- Logout Confirmation Modal -->
    <transition enter-active-class="transition duration-300 ease-out"
                enter-from-class="opacity-0"
                enter-to-class="opacity-100"
                leave-active-class="transition duration-200 ease-in"
                leave-from-class="opacity-100"
                leave-to-class="opacity-0">
      <div v-if="showLogoutConfirm" class="fixed inset-0 z-[9999] flex items-center justify-center p-4">
        <!-- Backdrop -->
        <div class="absolute inset-0 bg-slate-900/60 backdrop-blur-sm" @click="showLogoutConfirm = false"></div>
        
        <!-- Modal Card -->
        <div class="relative w-full max-w-sm bg-white rounded-3xl shadow-2xl overflow-hidden transform transition-all border border-slate-100">
          <div class="p-8 text-center">
            <div class="w-16 h-16 bg-rose-50 rounded-2xl flex items-center justify-center mx-auto mb-6">
              <svg class="w-8 h-8 text-rose-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
              </svg>
            </div>
            
            <h3 class="text-xl font-semibold text-slate-900 mb-2">{{ t('layout.terminateSession') }}</h3>
            <p class="text-slate-500 text-sm mb-8 leading-relaxed">
              {{ t('layout.endSessionConfirm') }}
            </p>
            
            <div class="flex flex-col gap-3">
              <button @click="proceedLogout" 
                class="w-full py-4 bg-rose-500 hover:bg-rose-600 text-white rounded-2xl font-semibold shadow-lg shadow-rose-200 transition-all active:scale-[0.98]">
                {{ t('common.save') === 'Save' ? 'Sign Out' : 'အကောင့်ထွက်မည်' }}
              </button>
              <button @click="showLogoutConfirm = false"
                class="w-full py-4 bg-slate-50 hover:bg-slate-100 text-slate-600 rounded-2xl font-semibold transition-all">
                {{ t('common.cancel') }}
              </button>
            </div>
          </div>
        </div>
      </div>
    </transition>

    <div class="flex-1 flex flex-col min-w-0">
      <header
        class="bg-white/80 backdrop-blur-md border-b border-slate-200/50 p-4 flex justify-between items-center z-30 sticky top-0">
        <div class="flex items-center">
          <button @click="isSidebarOpen = true"
            class="p-2 mr-4 text-slate-500 lg:hidden hover:bg-slate-100 rounded-xl transition">
            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2"
              stroke="currentColor" class="w-6 h-6">
              <path stroke-linecap="round" stroke-linejoin="round" d="M3.75 6.75h16.5M3.75 12h16.5m-16.5 5.25h16.5" />
            </svg>
          </button>

          <button @click="toggleSidebarCollapsed"
            class="hidden lg:inline-flex p-2 mr-3 text-slate-500 hover:bg-slate-100 rounded-xl transition"
            :title="isSidebarCollapsed ? 'Expand menu' : 'Collapse menu'">
            <svg v-if="isSidebarCollapsed" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none"
              stroke="currentColor" stroke-width="2" class="w-5 h-5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M9 5l7 7-7 7" />
            </svg>
            <svg v-else xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor"
              stroke-width="2" class="w-5 h-5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M15 19l-7-7 7-7" />
            </svg>
          </button>
          <div class="flex items-center gap-2 min-w-0">
            <h2
              class="text-sm font-normal text-slate-900 tracking-tight uppercase leading-normal py-1 truncate max-w-[200px] sm:max-w-none">
              {{ currentShopName }} <span class="text-slate-300 mx-1">/</span> {{ currentUser.branchName }}
            </h2>
          </div>
        </div>

        <div class="flex items-center gap-2 sm:gap-4">
          <div class="flex flex-col text-right">
            <span class="text-[11px] font-normal text-slate-900 uppercase tracking-tight leading-none mb-1">
              {{ currentUser.username }}
            </span>
            <span class="text-[9px] font-normal text-blue-600 uppercase tracking-widest leading-none">{{
              t('layout.systemId') }}: #{{
                currentUser.branchId }}</span>
          </div>
          <div
            class="w-9 h-9 sm:w-10 sm:h-10 bg-slate-900 rounded-xl flex items-center justify-center text-white font-normal shadow-lg shadow-slate-200">
            {{ currentUser.role.charAt(0) }}
          </div>
        </div>
      </header>

      <main class="flex-1 overflow-y-auto bg-[#f8fafc]">
        <router-view></router-view>
      </main>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted, watch } from 'vue';
import { useRouter } from 'vue-router';
import api from '../services/api';
import { useToast } from '../composables/useToast';
import { useI18n } from '../composables/useI18n';
import { usePermissions } from '../composables/usePermissions';

const { can } = usePermissions();

const THEME_KEY = 'app_theme';
const SIDEBAR_COLLAPSED_KEY = 'dashboard_sidebar_collapsed';
const router = useRouter();
const isSidebarOpen = ref(false);
const isSidebarCollapsed = ref(localStorage.getItem(SIDEBAR_COLLAPSED_KEY) === '1');
const showLangMenu = ref(false);
const showLogoutConfirm = ref(false);
const currentShopName = ref(localStorage.getItem('shopName') || 'VERSA POS');
const currentUser = ref({ role: 'Staff', username: '', branchId: 0, branchName: '' });
const theme = ref(localStorage.getItem(THEME_KEY) === 'dark' ? 'dark' : 'light');
const { t, locale, setLocale: setLocaleI18n } = useI18n();

const setLocale = (lang) => {
  setLocaleI18n(lang);
  localStorage.setItem('app_locale', lang);
};

const closeLangMenu = (e) => {
  if (!e.target.closest('.relative')) showLangMenu.value = false;
};

const toggleSidebarCollapsed = () => {
  isSidebarCollapsed.value = !isSidebarCollapsed.value;
  localStorage.setItem(SIDEBAR_COLLAPSED_KEY, isSidebarCollapsed.value ? '1' : '0');
};

const fetchUserInfo = async () => {
  const userId = localStorage.getItem('UserId');
  if (userId && userId !== '0') {
    try {
      const response = await api.get(`/ShopUsers/${userId}`);
      if (response.data && (response.data.username || response.data.Username)) {
        const uname = response.data.username || response.data.Username;
        currentUser.value.username = uname;
        localStorage.setItem('username', uname);
      }
    } catch (error) {
      console.error('Failed to fetch user info:', error);
    }
  }
};

const { toasts, removeToast } = useToast();
const toastConfig = {
  success: { bg: 'bg-emerald-50 border-emerald-100', text: 'text-emerald-900', icon: '✨' },
  error: { bg: 'bg-rose-50 border-rose-100', text: 'text-rose-900', icon: '⚠️' },
  warning: { bg: 'bg-amber-50 border-amber-100', text: 'text-amber-900', icon: '🔔' },
  info: { bg: 'bg-blue-50 border-blue-100', text: 'text-blue-900', icon: 'ℹ️' }
};

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
};

watch(theme, applyTheme);

onMounted(() => {
  currentUser.value = {
    role: localStorage.getItem('Role') || 'Staff',
    username: localStorage.getItem('username') || 'User',
    branchId: parseInt(localStorage.getItem('BranchId')) || 0,
    branchName: localStorage.getItem('BranchName') || 'Main Branch'
  };
  applyTheme(theme.value);
  fetchUserInfo();
  window.addEventListener('click', closeLangMenu);
});

onUnmounted(() => {
  window.removeEventListener('click', closeLangMenu);
});

const handleLogout = () => {
  console.log('Logout modal triggered');
  showLogoutConfirm.value = true;
};

const proceedLogout = () => {
  console.log('Logout confirmed, purging localStorage and redirecting...');
  localStorage.clear();
  window.location.href = '/login';
};
</script>

<style scoped>
.nav-header {
  padding: 1.5rem 1rem 0.5rem;
  font-size: 9px;
  text-transform: uppercase;
  color: #64748b;
  font-weight: 700;
  letter-spacing: 0.2em;
}

.nav-item {
  display: flex;
  align-items: center;
  padding: 0.75rem 1rem;
  border-radius: 10px;
  font-size: 0.8rem;
  font-weight: 600;
  color: #94a3b8;
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
  text-decoration: none;
  border: 1px solid transparent;
}

.nav-icon {
  width: 16px;
  height: 16px;
  stroke-width: 2;
  transition: all 0.2s ease;
}

.nav-icon--spaced {
  margin-right: 12px;
}

.nav-item:hover {
  background-color: rgba(255, 255, 255, 0.03);
  color: #f8fafc;
  transform: translateX(4px);
}

@media (min-width: 1024px) {
  .sidebar-collapsed .nav-label,
  .sidebar-collapsed .nav-section,
  .sidebar-collapsed .nav-footer {
    display: none;
  }

  .sidebar-collapsed .nav-item {
    justify-content: center;
    padding: 0.75rem;
  }

  .sidebar-collapsed .nav-item:hover {
    transform: none;
  }

  .sidebar-collapsed .nav-icon--spaced {
    margin-right: 0;
  }
}

.nav-item:hover .nav-icon {
  color: #3b82f6;
}

/* Global Active State */
.router-link-active {
  background-color: rgba(59, 130, 246, 0.08) !important;
  color: #60a5fa !important;
  border-color: rgba(59, 130, 246, 0.2) !important;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
}

.router-link-active .nav-icon {
  color: #60a5fa;
  stroke-width: 2.5;
}

.custom-scrollbar::-webkit-scrollbar {
  width: 3px;
}

.custom-scrollbar::-webkit-scrollbar-thumb {
  background: #334155;
  border-radius: 10px;
}

.no-scrollbar::-webkit-scrollbar {
  display: none;
}
</style>

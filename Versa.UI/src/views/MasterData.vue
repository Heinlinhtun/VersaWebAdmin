<template>
    <div class="px-3 sm:px-5 lg:px-8 pt-2 pb-6 sm:pb-8 bg-[#f8fafc] min-h-screen font-sans">

        <!-- Page Header -->
        <div class="flex flex-col md:flex-row justify-between items-start md:items-end mb-6 gap-4">
            <div>
                <h1 class="text-2xl font-normal text-slate-900 tracking-tight">{{ t('masterData.title') }}</h1>
                <p class="text-[11px] font-normal text-slate-400 uppercase tracking-widest mt-1 flex items-center">
                    <span class="w-2 h-2 bg-blue-500 rounded-full mr-2 shadow-[0_0_8px_rgba(59,130,246,0.5)]"></span>
                    {{ t('masterData.subtitle') }}
                </p>
            </div>
        </div>

        <!-- Tab Bar -->
        <div
            class="flex flex-wrap gap-1 mb-8 bg-slate-200/60 p-1.5 rounded-xl border border-slate-200 w-fit transition-all">
            <button @click="activeTab = 'categories'"
                :class="activeTab === 'categories' ? 'bg-white text-indigo-600 shadow-sm' : 'text-slate-500 hover:text-slate-700'"
                class="px-8 py-2.5 rounded-lg font-normal text-[11px] uppercase tracking-wide transition-all duration-300">
                {{ t('categories.title') }}
            </button>
            <button @click="activeTab = 'units'"
                :class="activeTab === 'units' ? 'bg-white text-blue-600 shadow-sm' : 'text-slate-500 hover:text-slate-700'"
                class="px-8 py-2.5 rounded-lg font-normal text-[11px] uppercase tracking-wide transition-all duration-300">
                {{ t('units.title') }}
            </button>
            <button v-if="allowBranch" @click="activeTab = 'branches'"
                :class="activeTab === 'branches' ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500 hover:text-slate-700'"
                class="px-8 py-2.5 rounded-lg font-normal text-[11px] uppercase tracking-wide transition-all duration-300">
                {{ t('branches.title') }}
            </button>
        </div>

        <!-- ===================== CATEGORIES TAB ===================== -->
        <div v-if="activeTab === 'categories'">
            <div class="flex flex-col md:flex-row justify-between items-start md:items-end mb-6 gap-4">
                <div class="relative flex-1 md:w-80">
                    <span class="absolute inset-y-0 left-0 pl-3 flex items-center text-slate-400">
                        <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
                        </svg>
                    </span>
                    <input v-model="cat.searchQuery" type="text" :placeholder="t('categories.searchPlaceholder')"
                        class="w-full pl-10 pr-4 py-2.5 bg-white border border-slate-200 rounded-xl text-xs font-normal outline-none focus:ring-2 focus:ring-blue-500/20 transition-all">
                </div>
                <button @click="cat.openModal()"
                    class="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2.5 rounded-xl font-normal shadow-lg shadow-blue-100 transition-all active:scale-95 uppercase text-xs tracking-widest flex items-center gap-2 whitespace-nowrap">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
                        <path fill-rule="evenodd"
                            d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z"
                            clip-rule="evenodd" />
                    </svg>
                    {{ t('categories.addCategory') }}
                </button>
            </div>

            <div class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-x-auto">
                <table class="w-full text-left border-collapse">
                    <thead class="bg-[#3f3f46] text-white">
                        <tr class="text-[10px] font-normal uppercase tracking-[0.2em]">
                            <th class="p-5 w-20 text-center opacity-70">{{ t('categories.tableNo') }}</th>
                            <th class="p-5">{{ t('categories.tableName') }}</th>
                            <th class="p-5 text-right px-8">{{ t('categories.tableActions') }}</th>
                        </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-50">
                        <tr v-for="(item, index) in cat.paginated" :key="item.categoryId"
                            class="hover:bg-slate-50/80 transition-colors group">
                            <td class="p-5 text-center text-slate-400 font-normal tabular-nums">
                                {{ ((cat.currentPage - 1) * cat.pageSize) + index + 1 }}
                            </td>
                            <td class="p-5">
                                <div class="flex items-center gap-4">
                                    <div
                                        class="w-9 h-9 rounded-lg bg-blue-50 flex items-center justify-center text-blue-600 font-normal uppercase text-xs border border-blue-100 transition-transform group-hover:scale-110">
                                        {{ (item.categoryName || 'C').charAt(0) }}
                                    </div>
                                    <div class="font-normal text-slate-800 text-sm tracking-tight">{{ item.categoryName }}
                                    </div>
                                </div>
                            </td>
                            <td class="p-5 text-right px-8">
                                <div class="flex justify-end gap-2">
                                    <button @click="cat.openModal(item)"
                                        class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-blue-600 hover:bg-blue-50 transition-all shadow-sm">
                                        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none"
                                            viewBox="0 0 24 24" stroke="currentColor">
                                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                                                d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                                        </svg>
                                    </button>
                                    <button @click="cat.deleteItem(item.categoryId)"
                                        class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-rose-600 hover:bg-rose-50 transition-all shadow-sm">
                                        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none"
                                            viewBox="0 0 24 24" stroke="currentColor">
                                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                                                d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                                        </svg>
                                    </button>
                                </div>
                            </td>
                        </tr>
                        <tr v-if="cat.list.length === 0">
                            <td colspan="3" class="p-24 text-center">
                                <div class="flex flex-col items-center opacity-20 grayscale">
                                    <svg class="w-12 h-12 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                        <path d="M4 6h16M4 10h16M4 14h16M4 18h16" stroke-width="2" />
                                    </svg>
                                    <p class="text-xs font-normal uppercase tracking-[0.3em] text-slate-900">{{
                                        t('categories.noCategories') }}</p>
                                </div>
                            </td>
                        </tr>
                    </tbody>
                </table>
                <div v-if="cat.filtered.length > 0"
                    class="flex justify-between items-center px-6 py-4 bg-slate-50 border-t border-slate-200">
                    <div class="text-[10px] font-normal text-slate-400 uppercase tracking-widest">{{ t('categories.title')
                    }}: {{ cat.filtered.length }}</div>
                    <div class="flex items-center gap-1.5">
                        <button @click="cat.currentPage--" :disabled="cat.currentPage === 1"
                            class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">{{
                                t('common.prev') }}</button>
                        <span class="px-3 text-xs font-normal text-slate-400">{{ t('common.page') }} {{ cat.currentPage }}
                            / {{ cat.totalPages }}</span>
                        <button @click="cat.currentPage++" :disabled="cat.currentPage === cat.totalPages"
                            class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">{{
                                t('common.next') }}</button>
                    </div>
                </div>
            </div>

            <!-- Category Modal -->
            <div v-if="cat.showModal"
                class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
                <div class="bg-white w-full max-w-sm rounded-xl shadow-2xl overflow-hidden border border-slate-200">
                    <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
                        <h3 class="text-lg font-normal uppercase tracking-tight">{{ cat.isEdit ?
                            t('categories.modalTitleEdit') : t('categories.modalTitleAdd') }}</h3>
                        <button @click="cat.showModal = false"
                            class="text-white/60 hover:text-white transition-colors text-2xl">×</button>
                    </div>
                    <div class="p-8">
                        <div class="space-y-1.5 mb-6">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                t('categories.labelName') }}</label>
                            <input v-model="cat.form.categoryName" type="text"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all"
                                :placeholder="t('categories.placeholderName')" />
                        </div>
                        <div class="flex gap-3">
                            <button @click="cat.showModal = false"
                                class="flex-1 py-3 bg-slate-100 text-slate-600 rounded-lg font-normal uppercase text-[10px] hover:bg-slate-200 transition-all">{{
                                    t('categories.btnCancel') }}</button>
                            <button @click="cat.save()"
                                class="flex-1 py-3 bg-blue-600 text-white rounded-lg font-normal uppercase text-[10px] shadow-lg shadow-blue-200 hover:bg-blue-700 transition active:scale-95">{{
                                    t('categories.btnSave') }}</button>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- ===================== UNITS TAB ===================== -->
        <div v-if="activeTab === 'units'">
            <div class="flex flex-col md:flex-row justify-between items-start md:items-end mb-6 gap-4">
                <div class="relative flex-1 md:w-80">
                    <span class="absolute inset-y-0 left-0 pl-3 flex items-center text-slate-400">
                        <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
                        </svg>
                    </span>
                    <input v-model="unit.searchQuery" type="text" :placeholder="t('units.searchPlaceholder')"
                        class="w-full pl-10 pr-4 py-2.5 bg-white border border-slate-200 rounded-xl text-xs font-normal outline-none focus:ring-2 focus:ring-blue-500/20 transition-all">
                </div>
                <button @click="unit.openModal()"
                    class="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2.5 rounded-xl font-normal shadow-lg shadow-blue-100 transition-all active:scale-95 uppercase text-xs tracking-widest flex items-center gap-2 whitespace-nowrap">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
                        <path fill-rule="evenodd"
                            d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z"
                            clip-rule="evenodd" />
                    </svg>
                    {{ t('units.addUnit') }}
                </button>
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
                        <tr v-for="(item, index) in unit.paginated" :key="item.unitId"
                            class="hover:bg-slate-50/80 transition-colors group">
                            <td class="p-5 text-center text-slate-400 font-normal tabular-nums">
                                {{ ((unit.currentPage - 1) * unit.pageSize) + index + 1 }}
                            </td>
                            <td class="p-5">
                                <div class="flex items-center gap-4">
                                    <div
                                        class="w-9 h-9 rounded-lg bg-blue-50 flex items-center justify-center text-blue-600 font-normal uppercase text-xs border border-blue-100 transition-transform group-hover:scale-110">
                                        {{ (item.unitName || 'U').charAt(0) }}
                                    </div>
                                    <div class="font-normal text-slate-800 text-sm tracking-tight">{{ item.unitName }}
                                    </div>
                                </div>
                            </td>
                            <td class="p-5">
                                <span
                                    class="bg-slate-100 text-slate-500 font-mono text-[11px] px-2.5 py-1 rounded-md border border-slate-200/50">{{
                                        item.shortName }}</span>
                            </td>
                            <td class="p-5 text-right px-8">
                                <div class="flex justify-end gap-2">
                                    <button @click="unit.openModal(item)"
                                        class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-blue-600 hover:bg-blue-50 transition-all shadow-sm">
                                        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none"
                                            viewBox="0 0 24 24" stroke="currentColor">
                                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                                                d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                                        </svg>
                                    </button>
                                    <button @click="unit.deleteItem(item.unitId)"
                                        class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-rose-600 hover:bg-rose-50 transition-all shadow-sm">
                                        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none"
                                            viewBox="0 0 24 24" stroke="currentColor">
                                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                                                d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                                        </svg>
                                    </button>
                                </div>
                            </td>
                        </tr>
                        <tr v-if="unit.list.length === 0">
                            <td colspan="4" class="p-24 text-center">
                                <div class="flex flex-col items-center opacity-20 grayscale">
                                    <svg class="w-12 h-12 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                        <path
                                            d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10"
                                            stroke-width="2" />
                                    </svg>
                                    <p class="text-xs font-normal uppercase tracking-[0.3em] text-slate-900">{{
                                        t('units.noUnits') }}</p>
                                </div>
                            </td>
                        </tr>
                    </tbody>
                </table>
                <div v-if="unit.filtered.length > 0"
                    class="flex justify-between items-center px-6 py-4 bg-slate-50 border-t border-slate-200">
                    <div class="text-[10px] font-normal text-slate-400 uppercase tracking-widest">{{ t('units.title') }}:
                        {{ unit.filtered.length }}</div>
                    <div class="flex items-center gap-1.5">
                        <button @click="unit.currentPage--" :disabled="unit.currentPage === 1"
                            class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">{{
                                t('common.prev') }}</button>
                        <span class="px-3 text-xs font-normal text-slate-400">{{ t('common.page') }} {{ unit.currentPage
                        }} / {{ unit.totalPages }}</span>
                        <button @click="unit.currentPage++" :disabled="unit.currentPage === unit.totalPages"
                            class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">{{
                                t('common.next') }}</button>
                    </div>
                </div>
            </div>

            <!-- Unit Modal -->
            <div v-if="unit.showModal"
                class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
                <div class="bg-white w-full max-w-sm rounded-xl shadow-2xl overflow-hidden border border-slate-200">
                    <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
                        <h3 class="text-lg font-normal uppercase tracking-tight">{{ unit.isEdit ?
                            t('units.modalTitleEdit') : t('units.modalTitleAdd') }}</h3>
                        <button @click="unit.showModal = false"
                            class="text-white/60 hover:text-white transition-colors text-2xl">×</button>
                    </div>
                    <div class="p-8 space-y-4">
                        <div class="space-y-1.5">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                t('units.labelName') }}</label>
                            <input v-model="unit.form.unitName" type="text"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all"
                                :placeholder="t('units.placeholderName')" />
                        </div>
                        <div class="space-y-1.5">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                t('units.shortName') }}</label>
                            <input v-model="unit.form.shortName" type="text"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all"
                                :placeholder="t('units.shortNamePlaceholder')" />
                        </div>
                        <div class="flex gap-3 pt-2">
                            <button @click="unit.showModal = false"
                                class="flex-1 py-3 bg-slate-100 text-slate-600 rounded-lg font-normal uppercase text-[10px] hover:bg-slate-200 transition-all">{{
                                    t('units.btnCancel') }}</button>
                            <button @click="unit.save()"
                                class="flex-1 py-3 bg-blue-600 text-white rounded-lg font-normal uppercase text-[10px] shadow-lg shadow-blue-200 hover:bg-blue-700 transition active:scale-95">{{
                                    t('units.btnSave') }}</button>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- ===================== BRANCHES TAB ===================== -->
        <div v-if="allowBranch && activeTab === 'branches'">
            <div class="flex flex-col md:flex-row justify-between items-start md:items-end mb-6 gap-4">
                <div class="relative flex-1 md:w-80">
                    <span class="absolute inset-y-0 left-0 pl-3 flex items-center text-slate-400">
                        <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
                        </svg>
                    </span>
                    <input v-model="branch.searchQuery" type="text" :placeholder="t('branches.searchPlaceholder')"
                        class="w-full pl-10 pr-4 py-2.5 bg-white border border-slate-200 rounded-xl text-xs font-normal outline-none focus:ring-2 focus:ring-blue-500/20 transition-all">
                </div>
                <button @click="branch.openCreate()"
                    class="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2.5 rounded-xl font-normal shadow-lg shadow-blue-100 transition-all active:scale-95 uppercase text-xs tracking-widest flex items-center gap-2 whitespace-nowrap">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
                        <path fill-rule="evenodd"
                            d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z"
                            clip-rule="evenodd" />
                    </svg>
                    {{ t('branches.addBranch') }}
                </button>
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
                        <tr v-for="b in branch.paginated" :key="b.branchId"
                            class="hover:bg-slate-50/80 transition-colors group">
                            <td class="p-5">
                                <div class="flex items-center gap-4">
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
                            <td class="p-5">
                                <div class="text-sm font-normal text-slate-700">{{ b.location || '-' }}</div>
                                <div class="text-[11px] text-slate-400 font-normal mt-0.5">{{ b.phone || '-' }}</div>
                            </td>
                            <td class="p-5 text-center">
                                <span
                                    :class="b.isActive || b.IsActive ? 'bg-emerald-50 text-emerald-600 border-emerald-100' : 'bg-rose-50 text-rose-600 border-rose-100'"
                                    class="px-3 py-1 rounded-lg text-[10px] font-normal uppercase border shadow-sm tracking-wide">
                                    {{ (b.isActive || b.IsActive) ? t('branches.statusActive') :
                                        t('branches.statusInactive') }}
                                </span>
                            </td>
                            <td class="p-5 text-center text-slate-500 text-xs font-normal tabular-nums">
                                {{ b.createdAt ? new Date(b.createdAt).toLocaleDateString('en-GB') : '-' }}
                            </td>
                            <td class="p-5 text-right px-8">
                                <div class="flex justify-end gap-2">
                                    <button @click="branch.openEdit(b)"
                                        class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-blue-600 hover:bg-blue-50 transition-all shadow-sm">
                                        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none"
                                            viewBox="0 0 24 24" stroke="currentColor">
                                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                                                d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                                        </svg>
                                    </button>
                                    <button @click="branch.deleteItem(b.branchId || b.BranchId)"
                                        class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-rose-600 hover:bg-rose-50 transition-all shadow-sm">
                                        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none"
                                            viewBox="0 0 24 24" stroke="currentColor">
                                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                                                d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                                        </svg>
                                    </button>
                                </div>
                            </td>
                        </tr>
                        <tr v-if="branch.filtered.length === 0">
                            <td colspan="5" class="p-24 text-center opacity-30 grayscale">
                                <p class="text-xs font-normal uppercase tracking-[0.3em]">{{ t('branches.noRecords') }}
                                </p>
                            </td>
                        </tr>
                    </tbody>
                </table>
                <div v-if="branch.filtered.length > 0"
                    class="flex justify-between items-center px-6 py-4 bg-slate-50 border-t border-slate-200">
                    <div class="text-[10px] font-normal text-slate-400 uppercase tracking-widest">{{ t('branches.title')
                    }}: {{ branch.filtered.length }}</div>
                    <div class="flex items-center gap-1.5">
                        <button @click="branch.currentPage--" :disabled="branch.currentPage === 1"
                            class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">{{
                                t('common.prev') }}</button>
                        <span class="px-3 text-xs font-normal text-slate-400">{{ t('common.page') }} {{ branch.currentPage
                        }} / {{ branch.totalPages }}</span>
                        <button @click="branch.currentPage++" :disabled="branch.currentPage === branch.totalPages"
                            class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">{{
                                t('common.next') }}</button>
                    </div>
                </div>
            </div>

            <!-- Branch Modal -->
            <div v-if="branch.showModal"
                class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
                <div class="bg-white w-full max-w-md rounded-xl shadow-2xl overflow-hidden border border-slate-200">
                    <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
                        <h3 class="text-lg font-normal uppercase tracking-tight">{{ branch.editMode ?
                            t('branches.modalTitleEdit') : t('branches.modalTitleAdd') }}</h3>
                        <button @click="branch.showModal = false"
                            class="text-white/60 hover:text-white transition-colors text-2xl">×</button>
                    </div>
                    <div class="p-8 space-y-5">
                        <div class="space-y-1.5">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                t('branches.labelName') }}</label>
                            <input v-model="branch.form.BranchName" type="text"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all"
                                :placeholder="t('branches.placeholderName')">
                        </div>
                        <div class="space-y-1.5">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                t('branches.labelLocation') }}</label>
                            <input v-model="branch.form.Location" type="text"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all"
                                :placeholder="t('branches.placeholderLocation')">
                        </div>
                        <div class="space-y-1.5">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                t('branches.labelPhone') }}</label>
                            <input v-model="branch.form.Phone" type="text"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all"
                                :placeholder="t('branches.placeholderPhone')">
                        </div>
                        <div class="grid grid-cols-1 gap-3 pt-2">
                            <label
                                class="flex items-center justify-between p-3 bg-slate-50 border border-slate-100 rounded-lg cursor-pointer hover:border-blue-200 transition-colors">
                                <div class="flex flex-col">
                                    <span class="text-xs font-normal text-slate-700">{{ t('branches.mainHQ') }}</span>
                                    <span
                                        class="text-[9px] text-slate-400 uppercase font-normal tracking-tight italic">Central
                                        office status</span>
                                </div>
                                <input v-model="branch.form.IsMainBranch" type="checkbox"
                                    class="w-5 h-5 text-blue-600 rounded-md border-slate-300">
                            </label>
                            <label
                                class="flex items-center justify-between p-3 bg-slate-50 border border-slate-100 rounded-lg cursor-pointer hover:border-emerald-200 transition-colors">
                                <span class="text-xs font-normal text-slate-700">{{ t('branches.operationalStatus')
                                }}</span>
                                <input v-model="branch.form.IsActive" type="checkbox"
                                    class="w-5 h-5 text-emerald-600 rounded-md border-slate-300">
                            </label>
                        </div>
                        <div class="pt-4 flex gap-3">
                            <button @click="branch.showModal = false"
                                class="flex-1 py-3 bg-slate-100 text-slate-600 rounded-lg font-normal uppercase text-[10px] hover:bg-slate-200 transition-all">{{
                                    t('branches.btnCancel') }}</button>
                            <button @click="branch.save()"
                                class="flex-1 py-3 bg-blue-600 text-white rounded-lg font-normal uppercase text-[10px] shadow-lg shadow-blue-200 hover:bg-blue-700 transition active:scale-95">{{
                                    t('branches.btnSave') }}</button>
                        </div>
                    </div>
                </div>
            </div>
        </div>

    </div>
</template>

<script setup>
import { ref, computed, onMounted, watch, reactive } from 'vue';
import api from '../services/api';
import { useToast } from '../composables/useToast';
import { useI18n } from '../composables/useI18n';

const { addToast } = useToast();
const { t } = useI18n();

const activeTab = ref('categories');
const allowBranch = localStorage.getItem('AllowBranch') === 'true';
const tabs = computed(() => [
    { id: 'categories', label: t('categories.title') },
    { id: 'units', label: t('units.title') },
    ...(allowBranch ? [{ id: 'branches', label: t('branches.title') }] : []),
]);

const getApiErrorMessage = (err, fallback) => (
    err.response?.data?.Message
    || err.response?.data?.message
    || err.response?.data?.title
    || fallback
);

// ── CATEGORIES ──────────────────────────────────────────
const cat = reactive({
    list: [], showModal: false, isEdit: false, searchQuery: '', currentPage: 1, pageSize: 10,
    form: { categoryId: 0, categoryName: '' },
    filtered: computed(() => {
        const q = cat.searchQuery.toLowerCase();
        return q ? cat.list.filter(c => (c.categoryName || '').toLowerCase().includes(q)) : cat.list;
    }),
    totalPages: computed(() => Math.max(1, Math.ceil(cat.filtered.length / cat.pageSize))),
    paginated: computed(() => {
        const s = (cat.currentPage - 1) * cat.pageSize;
        return cat.filtered.slice(s, s + cat.pageSize);
    }),
    openModal(item = null) {
        if (item) { cat.isEdit = true; cat.form = { ...item }; }
        else { cat.isEdit = false; cat.form = { categoryId: 0, categoryName: '' }; }
        cat.showModal = true;
    },
    async save() {
        try {
            if (cat.isEdit) await api.put(`/categories/${cat.form.categoryId}`, cat.form);
            else await api.post('/categories', cat.form);
            cat.showModal = false;
            addToast(t('categories.toastSuccess'), 'success');
            await fetchCategories();
        } catch { addToast(t('categories.toastError'), 'error'); }
    },
    async deleteItem(id) {
        if (confirm(t('categories.deleteConfirm'))) {
            try {
                await api.delete(`/categories/${id}`);
                await fetchCategories();
            } catch (err) {
                addToast(getApiErrorMessage(err, t('categories.toastError')), 'error');
            }
        }
    },
});
watch(() => cat.searchQuery, () => { cat.currentPage = 1; });

// ── UNITS ──────────────────────────────────────────────
const unit = reactive({
    list: [], showModal: false, isEdit: false, searchQuery: '', currentPage: 1, pageSize: 10,
    form: { unitId: 0, unitName: '', shortName: '' },
    filtered: computed(() => {
        const q = unit.searchQuery.toLowerCase();
        return q ? unit.list.filter(u => (u.unitName || '').toLowerCase().includes(q) || (u.shortName || '').toLowerCase().includes(q)) : unit.list;
    }),
    totalPages: computed(() => Math.max(1, Math.ceil(unit.filtered.length / unit.pageSize))),
    paginated: computed(() => {
        const s = (unit.currentPage - 1) * unit.pageSize;
        return unit.filtered.slice(s, s + unit.pageSize);
    }),
    openModal(item = null) {
        if (item) { unit.isEdit = true; unit.form = { ...item }; }
        else { unit.isEdit = false; unit.form = { unitId: 0, unitName: '', shortName: '' }; }
        unit.showModal = true;
    },
    async save() {
        try {
            if (unit.isEdit) await api.put(`/units/${unit.form.unitId}`, unit.form);
            else await api.post('/units', unit.form);
            unit.showModal = false;
            addToast(t('units.toastSuccess'), 'success');
            await fetchUnits();
        } catch { addToast(t('units.toastError'), 'error'); }
    },
    async deleteItem(id) {
        if (confirm(t('units.deleteConfirm'))) {
            try {
                await api.delete(`/units/${id}`);
                await fetchUnits();
            } catch (err) {
                addToast(getApiErrorMessage(err, t('units.toastError')), 'error');
            }
        }
    },
});
watch(() => unit.searchQuery, () => { unit.currentPage = 1; });

// ── BRANCHES ────────────────────────────────────────────
const branch = reactive({
    list: [], showModal: false, editMode: false, searchQuery: '', currentPage: 1, pageSize: 10,
    form: { BranchId: null, BranchName: '', Location: '', Phone: '', IsMainBranch: false, IsActive: true },
    filtered: computed(() => {
        const q = branch.searchQuery.toLowerCase().trim();
        return q ? branch.list.filter(b =>
            (b.branchName || b.BranchName || '').toLowerCase().includes(q) ||
            (b.location || b.Location || '').toLowerCase().includes(q)
        ) : branch.list;
    }),
    totalPages: computed(() => Math.max(1, Math.ceil(branch.filtered.length / branch.pageSize))),
    paginated: computed(() => {
        const s = (branch.currentPage - 1) * branch.pageSize;
        return branch.filtered.slice(s, s + branch.pageSize);
    }),
    openCreate() {
        branch.editMode = false;
        branch.form = { BranchId: null, BranchName: '', Location: '', Phone: '', IsMainBranch: false, IsActive: true };
        branch.showModal = true;
    },
    openEdit(b) {
        branch.editMode = true;
        branch.form = {
            BranchId: b.branchId || b.BranchId, BranchName: b.branchName || b.BranchName,
            Location: b.location || b.Location, Phone: b.phone || b.Phone,
            IsMainBranch: b.isMainBranch || b.IsMainBranch, IsActive: b.isActive || b.IsActive,
        };
        branch.showModal = true;
    },
    async save() {
        try {
            const payload = { ...branch.form, BranchId: branch.form.BranchId || 0 };
            if (branch.editMode) await api.put(`/Branches/${payload.BranchId}`, payload);
            else await api.post('/Branches', payload);
            branch.showModal = false;
            addToast(t('branches.toastSuccess'), 'success');
            await fetchBranches();
        } catch { addToast(t('branches.toastError'), 'error'); }
    },
    async deleteItem(id) {
        if (confirm(t('branches.deleteConfirm'))) {
            try { await api.delete(`/Branches/${id}`); await fetchBranches(); addToast(t('branches.deleted'), 'success'); }
            catch (err) { addToast(getApiErrorMessage(err, t('branches.actionFailed')), 'error'); }
        }
    },
});
watch(() => branch.searchQuery, () => { branch.currentPage = 1; });

const fetchCategories = async () => { try { const r = await api.get('/categories'); cat.list = r.data; } catch { } };
const fetchUnits = async () => { try { const r = await api.get('/units'); unit.list = r.data; } catch { } };
const fetchBranches = async () => {
    if (!allowBranch) return;
    try { const r = await api.get('/Branches'); branch.list = Array.isArray(r.data) ? r.data : []; } catch { }
};

onMounted(() => { fetchCategories(); fetchUnits(); if (allowBranch) fetchBranches(); });
</script>

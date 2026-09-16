<template>
  <div class="px-3 sm:px-5 lg:px-8 pt-2 pb-6 sm:pb-8 bg-[#f8fafc] min-h-screen font-sans text-left">
    <div class="flex flex-col md:flex-row justify-between items-start md:items-end mb-8 gap-4">
      <div>
        <h1 class="text-2xl font-normal text-slate-900 tracking-tight">{{ tx.catalogManagement }}</h1>
        <p class="text-[11px] font-normal text-slate-400 uppercase tracking-widest mt-1 flex items-center">
          <span class="w-2 h-2 bg-blue-500 rounded-full mr-2 shadow-[0_0_8px_rgba(59,130,246,0.5)]"></span>
          {{ tx.masterInventory }}
        </p>
      </div>

      <div class="flex gap-3 w-full md:w-auto">
        <div class="relative flex-1 md:w-64">
          <span class="absolute inset-y-0 left-0 pl-3 flex items-center text-slate-400">
            <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
            </svg>
          </span>
          <input v-model="searchQuery" type="text" :placeholder="t('products.searchPlaceholder')"
            class="w-full pl-10 pr-4 py-2.5 bg-white border border-slate-200 rounded-xl text-xs font-normal outline-none focus:ring-2 focus:ring-blue-500/20 transition-all">
        </div>

        <button v-if="currentTab === 'products' && can('canEditProducts')" @click="openBulkPriceModal()"
          class="hidden lg:flex bg-indigo-600 hover:bg-indigo-700 text-white px-5 py-2.5 rounded-xl font-normal shadow-lg shadow-indigo-100 transition-all active:scale-95 uppercase text-xs tracking-widest items-center gap-2 text-nowrap">
          <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 6v6m0 0v6m0-6h6m-6 0H6" />
          </svg>
          Bulk Price Adjust
        </button>

        <button @click="openAddModal()"
          class="hidden lg:flex bg-blue-600 hover:bg-blue-700 text-white px-6 py-2.5 rounded-xl font-normal shadow-lg shadow-blue-100 transition-all active:scale-95 uppercase text-xs tracking-widest items-center gap-2 text-nowrap">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
            <path fill-rule="evenodd"
              d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z"
              clip-rule="evenodd" />
          </svg>
          {{ addButtonLabel }}
        </button>
      </div>
    </div>

    <div
      class="flex flex-wrap gap-1 mb-8 bg-slate-200/60 p-1.5 rounded-xl border border-slate-200 w-fit transition-all">
      <button v-for="tab in tabs" :key="tab.id" @click="currentTab = tab.id"
        :class="currentTab === tab.id ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500 hover:text-slate-700'"
        class="px-6 py-2 rounded-lg font-normal text-[11px] uppercase tracking-wide transition-all duration-200">
        {{ tab.label }}
      </button>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-x-auto">
      <table class="w-full text-left border-collapse">
        <thead class="bg-[#3f3f46] text-white">
          <tr class="text-[10px] font-normal uppercase tracking-[0.15em]">
            <th class="p-5 w-16 text-center opacity-70">{{ tx.no }}</th>

            <template v-if="currentTab === 'products'">
              <th class="p-5">{{ tx.skuCode }}</th>
              <th class="p-5">{{ tx.productDetails }}</th>
              <th class="p-5 text-center">{{ tx.baseUnit }}</th>
              <th class="p-5 text-right">{{ tx.standardPrice }}</th>
              <th class="p-5 text-center">{{ tx.inventoryAlert }}</th>
            </template>

            <template v-else-if="currentTab === 'services'">
              <th class="p-5">{{ tx.serviceName }}</th>
              <th class="p-5 text-right">{{ tx.standardFee }}</th>
              <th class="p-5">Description</th>
            </template>

            <template v-else-if="currentTab === 'bundles'">
              <th class="p-5">{{ tx.bundleIdentity }}</th>
              <th class="p-5 text-center">{{ tx.includedItems }}</th>
              <th class="p-5 text-right">{{ tx.packagePrice }}</th>
            </template>

            <th class="p-5 text-right px-8">{{ tx.actions }}</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-slate-50">
          <tr v-for="(item, index) in paginatedData" :key="index" class="hover:bg-slate-50/80 transition-colors group">
            <td class="p-5 text-center text-slate-400 font-normal tabular-nums">
              {{ ((currentPage - 1) * pageSize) + index + 1 }}
            </td>

            <template v-if="currentTab === 'products'">
              <td class="p-5">
                <span
                  class="bg-slate-100 text-slate-500 font-mono text-[11px] px-2 py-1 rounded-md border border-slate-200/50 uppercase">
                  {{ item.Sku || 'NO-SKU' }}
                </span>
              </td>
              <td class="p-5 font-normal text-slate-800 text-sm tracking-tight">{{ item.Name }}</td>
              <td class="p-5 text-center">
                <span
                  class="px-2.5 py-1 bg-blue-50 text-blue-700 rounded-lg text-[10px] font-normal uppercase border border-blue-100">
                  {{ getUnitName(item.UnitId) }}
                </span>
              </td>
              <td class="p-4 text-right font-normal text-sm text-blue-600 tabular-nums">
                {{ formatNumber(item.SalePrice) }} <small class="opacity-40">KS</small>
              </td>
              <td class="p-5 text-center">
                <div v-if="item.AlertQuantity > 0"
                  class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-amber-50 text-amber-600 border border-amber-100 text-[10px] font-normal">
                  <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5"
                      d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                  </svg>
                  {{ item.AlertQuantity }}
                </div>
                <span v-else class="text-slate-200">-</span>
              </td>
            </template>

            <template v-else-if="currentTab === 'services'">
              <td class="p-5 font-normal text-slate-800 text-sm tracking-tight">{{ item.ServiceName || item.serviceName
              }}
              </td>
              <td class="p-5 text-right font-normal text-indigo-600 tabular-nums">
                {{ formatNumber(item.Price || item.price) }} <small class="opacity-40">KS</small>
              </td>
              <td class="p-5 text-xs text-slate-400 italic font-normal">{{ item.Description || item.description || '-'
              }}</td>
            </template>

            <template v-else-if="currentTab === 'bundles'">
              <td class="p-5 font-normal text-slate-800 text-sm tracking-tight">{{ item.BundleName || item.bundleName }}
              </td>
              <td class="p-5 text-center">
                <span
                  class="bg-slate-100 text-slate-600 px-3 py-1 rounded-lg text-[10px] font-normal border border-slate-200">
                  {{ item.ItemsCount || 0 }} Items
                </span>
              </td>
              <td class="p-5 text-right font-normal text-emerald-600 tabular-nums text-lg">
                {{ formatNumber(item.BundlePrice || item.bundlePrice) }} <small class="opacity-40">KS</small>
              </td>
            </template>



            <div v-if="showDetailModal"
              class="fixed inset-0 z-[110] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
              <div
                class="bg-white w-full max-w-lg rounded-2xl shadow-2xl overflow-hidden animate-in zoom-in duration-200 border border-slate-200">
                <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
                  <div>
                    <h3 class="text-lg font-normal uppercase tracking-tight">Bundle Components</h3>
                    <p class="text-emerald-400 text-[10px] font-normal uppercase mt-1 tracking-widest">{{
                      selectedBundleDetail.master?.BundleName }}</p>
                  </div>
                  <button @click="showDetailModal = false" class="text-white/60 hover:text-white transition-colors">
                    <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                    </svg>
                  </button>
                </div>
                <div class="p-6">
                  <div class="overflow-x-auto border border-slate-100 rounded-xl mb-6">
                    <table class="w-full text-left">
                      <thead
                        class="bg-slate-50 text-[10px] font-normal text-slate-400 uppercase tracking-widest border-b">
                        <tr>
                          <th class="p-4">Product Name</th>
                          <th class="p-4 text-center">Quantity</th>
                        </tr>
                      </thead>
                      <tbody class="divide-y divide-slate-50 text-sm">
                        <tr v-for="comp in selectedBundleDetail.items" :key="comp.BundleItemId">
                          <td class="p-4 font-normal text-slate-800">{{ comp.ProductName }}</td>
                          <td class="p-4 text-center font-normal text-blue-600 tabular-nums">{{ comp.Quantity }}</td>
                        </tr>
                      </tbody>
                    </table>
                  </div>
                  <button @click="showDetailModal = false"
                    class="w-full py-4 bg-slate-100 text-slate-600 rounded-xl font-normal uppercase text-[10px] tracking-widest">Close
                    View</button>
                </div>
              </div>
            </div>

            <td class="p-5 text-right px-8">
              <div class="flex justify-end gap-2">
                <button @click="editItem(item)"
                  class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-blue-600 transition-all shadow-sm">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                    <path
                      d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                  </svg>
                </button>
                <button @click="deleteItem(item)"
                  class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-rose-600 transition-all shadow-sm">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                    <path
                      d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                  </svg>
                </button>
                <button v-if="currentTab === 'bundles'" @click="showBundleDetail(item)"
                  class="p-2 rounded-lg bg-emerald-50 text-emerald-600 hover:bg-emerald-100 transition-all shadow-sm"
                  title="View Detail">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                    <path d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                    <path
                      d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                  </svg>
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>

      <div v-if="filteredData.length > 0"
        class="flex justify-between items-center px-6 py-4 bg-slate-50 border-t border-slate-200">
        <div class="text-[10px] font-normal text-slate-400 uppercase tracking-widest">
          {{ tx.catalogManagement }}: {{ filteredData.length }}
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

    <div v-if="showBulkPriceModal"
      class="fixed inset-0 z-[120] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4 overflow-y-auto">
      <div
        class="bg-white w-full max-w-5xl rounded-xl shadow-2xl overflow-hidden animate-in zoom-in duration-200 border border-slate-200 my-auto">
        <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
          <h3 class="text-lg font-normal uppercase tracking-tight">{{ t('products.bulkPriceAdjustment') }}</h3>
          <button @click="showBulkPriceModal = false" class="text-white/60 hover:text-white transition-colors">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <div class="p-8 max-h-[80vh] overflow-y-auto custom-scrollbar flex flex-col gap-6">
          <div class="flex gap-1 p-1 bg-slate-100 rounded-xl w-fit border border-slate-200">
            <button @click="bulkAdjustTab = 'new'"
              :class="bulkAdjustTab === 'new' ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500 hover:text-slate-700'"
              class="px-6 py-2 rounded-lg font-normal text-[10px] uppercase tracking-wider transition-all duration-200">
              {{ t('products.newAdjustment') }}
            </button>
            <button @click="bulkAdjustTab = 'history'"
              :class="bulkAdjustTab === 'history' ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500 hover:text-slate-700'"
              class="px-6 py-2 rounded-lg font-normal text-[10px] uppercase tracking-wider transition-all duration-200">
              {{ t('products.adjustmentHistory') }}
            </button>
          </div>

          <div v-if="bulkAdjustTab === 'new'" class="flex flex-col gap-8">
            <!-- Filters -->
            <div class="bg-slate-50 border border-slate-200 p-6 rounded-xl text-left">
              <h4
                class="font-normal text-slate-800 text-sm uppercase tracking-tight mb-4 border-b border-slate-200 pb-2">
                {{ t('products.step1FilterProducts') }}</h4>
              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div class="space-y-1.5">
                  <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest">{{ t('products.category') }}</label>
                  <select v-model="bulkAdjustFilters.categoryId"
                    class="w-full p-3 bg-white border border-slate-200 rounded-lg outline-none font-normal text-sm">
                    <option :value="null">{{ t('products.allCategories') }}</option>
                    <option v-for="c in categories" :key="c.categoryId" :value="c.categoryId">{{ c.categoryName }}
                    </option>
                  </select>
                </div>
                <div class="space-y-1.5">
                  <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest">{{ t('products.searchNameSku') }}</label>
                  <input v-model="bulkAdjustFilters.search" type="text"
                    class="w-full p-3 bg-white border border-slate-200 rounded-lg outline-none font-normal text-sm"
                    :placeholder="t('products.searchPlaceholder')">
                </div>
              </div>
            </div>

            <!-- Configuration -->
            <div class="bg-blue-50/30 border border-blue-100 p-6 rounded-xl text-left">
              <h4 class="font-normal text-blue-800 text-sm uppercase tracking-tight mb-4 border-b border-blue-100 pb-2">
                {{ t('products.step2AdjustmentRules') }}</h4>
              <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
                <div class="space-y-1.5">
                  <label class="text-[10px] font-normal text-blue-500 uppercase tracking-widest">{{ t('products.targetPrice') }}</label>
                  <select v-model="bulkAdjustConfig.target"
                    class="w-full p-3 bg-white border border-blue-200 rounded-lg outline-none font-normal text-sm text-blue-900">
                    <option value="CostPrice">{{ t('products.costPriceBuy') }}</option>
                    <option value="SalePrice">{{ t('products.standardRetailPrice') }}</option>
                  </select>
                </div>
                <div class="space-y-1.5">
                  <label class="text-[10px] font-normal text-blue-500 uppercase tracking-widest">{{ t('products.operation') }}</label>
                  <select v-model="bulkAdjustConfig.operation"
                    class="w-full p-3 bg-white border border-blue-200 rounded-lg outline-none font-normal text-sm text-blue-900">
                    <option value="add">{{ t('products.addPlus') }}</option>
                    <option value="subtract">{{ t('products.subtractMinus') }}</option>
                  </select>
                </div>
                <div class="space-y-1.5">
                  <label class="text-[10px] font-normal text-blue-500 uppercase tracking-widest">{{ t('products.valueType') }}</label>
                  <select v-model="bulkAdjustConfig.type"
                    class="w-full p-3 bg-white border border-blue-200 rounded-lg outline-none font-normal text-sm text-blue-900">
                    <option value="percentage">{{ t('products.percentage') }}</option>
                    <option value="fixed">{{ t('products.fixedAmountKs') }}</option>
                  </select>
                </div>
                <div class="space-y-1.5">
                  <label class="text-[10px] font-normal text-blue-500 uppercase tracking-widest">{{ t('products.value') }}</label>
                  <input :value="formatMoneyInput(bulkAdjustConfig.value)" @input="updateMoneyField(bulkAdjustConfig, 'value', $event)" type="text" inputmode="decimal"
                    class="w-full p-3 bg-white border border-blue-200 rounded-lg outline-none font-normal text-sm text-blue-900 tabular-nums">
                </div>
              </div>
            </div>

            <!-- Preview Table -->
            <div class="text-left">
              <div class="flex justify-between items-end mb-4">
                <h4
                  class="font-normal text-slate-800 text-sm uppercase tracking-tight border-b border-slate-200 pb-2 flex-1">
                  {{ t('products.step3PreviewChanges') }}</h4>
                <span
                  class="text-[10px] font-normal text-slate-400 bg-slate-100 px-3 py-1 rounded-full uppercase ml-4 border">
                  {{ t('products.affected') }} {{ bulkAdjustPreviewData.length }}
                </span>
              </div>

              <div class="border border-slate-200 rounded-xl overflow-x-auto max-h-[300px] custom-scrollbar">
                <table class="w-full text-left bg-white">
                  <thead
                    class="bg-[#3f3f46] text-white text-[10px] font-normal uppercase tracking-widest sticky top-0 z-10 shadow-sm">
                    <tr>
                      <th class="p-4 opacity-70">{{ t('products.sku') }}</th>
                      <th class="p-4 opacity-70">{{ t('products.productName') }}</th>
                      <th class="p-4 text-right opacity-70">{{ bulkAdjustConfig.target === 'CostPrice' ? t('products.currentCostPrice') : t('products.currentRetailPrice') }}</th>
                      <th class="p-4 text-right opacity-70">{{ t('products.newPricePreview') }}</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-100 text-sm">
                    <tr v-for="item in bulkAdjustPreviewData" :key="item.ProductId">
                      <td class="p-4 font-mono text-[11px] text-slate-500">{{ item.Sku || '-' }}</td>
                      <td class="p-4 font-normal text-slate-800">{{ item.Name }}</td>
                      <td class="p-4 text-right tabular-nums text-slate-500 line-through decoration-rose-300">
                        {{ formatNumber(item.currentPrice) }}
                      </td>
                      <td class="p-4 text-right tabular-nums font-normal text-indigo-600 bg-indigo-50/30">
                        {{ formatNumber(item.newPrice) }}
                        <span class="text-[10px] ml-2 text-indigo-400">
                          ({{ formatNumber(item.newPrice - item.currentPrice) >= 0 ? '+' : '' }}{{
                            formatNumber(item.newPrice - item.currentPrice) }})
                        </span>
                      </td>
                    </tr>
                    <tr v-if="bulkAdjustPreviewData.length === 0">
                      <td colspan="4"
                        class="p-8 text-center text-slate-400 text-sm font-normal uppercase tracking-widest border-t border-dashed">
                        {{ t('products.noMatchingProductsFound') }}</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>

            <!-- Actions -->
            <div class="flex justify-end gap-3 pt-4 border-t border-slate-100 mt-2">
              <button @click="showBulkPriceModal = false"
                class="px-6 py-3 border border-slate-200 text-slate-600 rounded-xl hover:bg-slate-50 font-normal uppercase text-[10px] tracking-widest transition-all">{{ t('products.cancelBtn') }}</button>
              <button @click="applyBulkPriceAdjustment" :disabled="bulkAdjustPreviewData.length === 0"
                class="px-10 py-3 bg-indigo-600 text-white rounded-xl font-normal uppercase text-[10px] tracking-widest shadow-lg shadow-indigo-200 hover:bg-indigo-700 disabled:bg-indigo-300 transition active:scale-95">
                {{ t('products.confirmAndApplyTo') }} {{ bulkAdjustPreviewData.length }} {{ t('products.itemsLabel') }}
              </button>
            </div>
          </div>

          <div v-else class="flex flex-col gap-4 text-left">
            <div class="bg-amber-50 border border-amber-100 p-4 rounded-xl flex items-start gap-4 mb-4">
              <div class="p-2 bg-amber-100 rounded-lg text-amber-600">
                <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                    d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                </svg>
              </div>
              <div>
                <h5 class="font-normal text-amber-900 text-[11px] uppercase tracking-wider mb-1">{{ t('products.rollbackNotice') }}</h5>
                <p class="text-amber-800 text-xs leading-relaxed">{{ t('products.rollbackNoticeDesc') }}</p>
              </div>
            </div>

            <div v-if="priceAdjustHistory.length === 0"
              class="py-20 text-center border-2 border-dashed border-slate-100 rounded-2xl">
              <div class="text-slate-300 font-normal uppercase tracking-[0.2em] text-[10px]">{{ t('products.noAdjustmentHistory') }}</div>
            </div>

            <div v-for="hist in priceAdjustHistory" :key="hist.id"
              class="bg-white border border-slate-200 rounded-xl p-6 shadow-sm hover:shadow-md transition-all group">
              <div class="flex justify-between items-start">
                <div class="flex-1">
                  <div class="flex items-center gap-3 mb-2">
                    <span
                      class="px-2 py-1 bg-slate-900 text-white text-[9px] font-normal rounded uppercase tracking-tighter">{{
                        hist.target === 'SalePrice' ? t('products.retailTarget') : t('products.costTarget') }}</span>
                    <h5 class="font-normal text-slate-900 text-sm tracking-tight">{{ hist.description }}</h5>
                  </div>
                  <div class="flex gap-4 text-[10px] font-normal text-slate-400 uppercase tracking-widest">
                    <span>?? {{ hist.timestamp }}</span>
                    <span>?? {{ hist.count }} {{ t('products.productsAffected') }}</span>
                  </div>
                </div>
                <button @click="rollbackAdjustment(hist)"
                  class="px-4 py-2 bg-slate-100 hover:bg-rose-50 text-slate-600 hover:text-rose-600 rounded-lg font-normal uppercase text-[9px] tracking-widest border border-slate-200 hover:border-rose-100 transition-all flex items-center gap-2">
                  <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M3 10h10a8 8 0 118 8m-5-8l-5-5m0 0l-5 5" />
                  </svg>
                  {{ t('products.rollback') }}
                </button>
              </div>
            </div>
          </div>

        </div>
      </div>
    </div>

    <div v-if="showModal && currentTab === 'products'" :inert="!!lookupCreateKind"
      class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4 overflow-y-auto">
      <div
        class="bg-white w-full max-w-4xl rounded-xl shadow-2xl overflow-hidden animate-in zoom-in duration-200 border border-slate-200 my-auto">
        <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
          <h3 class="text-lg font-normal uppercase tracking-tight">
            {{ isEdit ? t('products.updateProductInformation') : t('products.registerNewItem') }}</h3>
          <button @click="showModal = false" class="text-white/60 hover:text-white transition-colors">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <div class="p-8 max-h-[80vh] overflow-y-auto custom-scrollbar">
          <div class="grid grid-cols-1 md:grid-cols-2 gap-8 mb-10">
            <div class="space-y-5">
              <div class="space-y-1.5">
                <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ t('products.productNameStar') }}</label>
                <input v-model="form.name" type="text"
                  class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all"
                  :placeholder="t('products.enterFullName')">
              </div>
              <div class="space-y-1.5">
                <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ t('products.skuBarcodeIdentifier') }}</label>
                <input v-model="form.sku" type="text"
                  class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all"
                  placeholder="UPC-12345">
              </div>
            </div>
            <div class="space-y-5">
              <div class="space-y-1.5">
                <div class="flex items-center justify-between gap-3">
                  <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ t('products.productCategory') }}</label>
                  <button v-if="can('canEditMasterData')" type="button" @click="lookupCreateKind = 'categories'"
                    class="shrink-0 text-xs text-blue-600 hover:underline py-1 text-right">{{ t('products.addNewCategory') }}</button>
                </div>
                <select v-model="form.categoryId"
                  class="w-full p-3 font-normal text-slate-800 bg-gray-50 border border-slate-200 rounded-lg outline-none focus:bg-white focus:ring-4 focus:ring-blue-500/10 focus:border-blue-500 transition-all shadow-sm appearance-none cursor-pointer text-sm">
                  <option :value="null">{{ t('products.typeOrSelectCategory') }}</option>
                  <option v-for="c in categories" :key="c.categoryId" :value="c.categoryId">{{ c.categoryName }}</option>
                </select>
              </div>
              <div class="space-y-1.5">
                <div class="flex items-center justify-between gap-3">
                  <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ t('products.primaryBaseUnit') }}</label>
                  <button v-if="can('canEditMasterData')" type="button" @click="lookupCreateKind = 'units'"
                    class="shrink-0 text-xs text-blue-600 hover:underline py-1 text-right">{{ t('products.addNewUnit') }}</button>
                </div>
                <select v-model="form.unitId"
                  class="w-full p-3 font-normal text-slate-800 bg-gray-50 border border-slate-200 rounded-lg outline-none focus:bg-white focus:ring-4 focus:ring-blue-500/10 focus:border-blue-500 transition-all shadow-sm appearance-none cursor-pointer text-sm">
                  <option :value="null">{{ t('products.typeOrSelectUnit') }}</option>
                  <option v-for="u in units" :key="u.unitId" :value="u.unitId">{{ u.unitName }}</option>
                </select>
              </div>
            </div>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-3 gap-6 mb-10 p-6 bg-slate-50 rounded-xl border border-slate-200">
            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-slate-500 uppercase tracking-wider ml-1 leading-none">{{ t('products.baseCostBuy') }}</label>
              <input :value="formatMoneyInput(form.costPrice)" @input="updateMoneyField(form, 'costPrice', $event)" type="text" inputmode="decimal"
                class="w-full p-3 bg-white border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm tabular-nums">
            </div>
            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-slate-500 uppercase tracking-wider ml-1 leading-none">{{ t('products.standardRetailPrice') }}</label>
              <input :value="formatMoneyInput(form.salePrice)" @input="updateMoneyField(form, 'salePrice', $event)" type="text" inputmode="decimal"
                class="w-full p-3 bg-white border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm tabular-nums">
            </div>
            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-amber-600 uppercase tracking-wider ml-1 leading-none">{{ t('products.minStockAlertLevel') }}</label>
              <input v-model.number="form.alertQuantity" type="number"
                class="w-full p-3 bg-amber-50 border border-amber-200 rounded-lg outline-none focus:ring-2 focus:ring-amber-500/20 font-normal text-sm text-amber-900 tabular-nums">
            </div>
          </div>

          <div class="mb-10">
            <div class="flex justify-between items-center mb-5 border-b border-slate-100 pb-2">
              <h4 class="font-normal text-slate-800 text-sm uppercase tracking-tight">{{ t('products.scalingUnitsBulkSupport') }}</h4>
              <button @click="addConversion"
                class="text-[10px] font-normal bg-slate-900 text-white px-4 py-2 rounded-lg uppercase tracking-widest hover:bg-black transition-all">{{ t('products.addScalingUnit') }}</button>
            </div>
            <div v-if="form.conversions.length === 0"
              class="text-xs text-slate-400 italic text-center py-6 bg-slate-50 border border-dashed border-slate-200 rounded-xl font-normal uppercase tracking-widest">
              {{ t('products.standardBaseUnitOnly') }}</div>
            <div v-for="(conv, index) in form.conversions" :key="index"
              class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-4 p-4 border border-slate-200 rounded-xl bg-white shadow-sm relative group">
              <div class="space-y-1">
                <label class="text-[9px] font-normal text-slate-400 uppercase tracking-wider ml-1">{{ t('products.scalingFrom') }}</label>
                <select v-model="conv.fromUnitId"
                  class="w-full p-2 border border-slate-200 rounded-lg bg-slate-50 text-[11px] font-normal outline-none">
                  <option v-for="u in units" :key="u.unitId" :value="u.unitId" :disabled="u.unitId === form.unitId">{{
                    u.unitName }}</option>
                </select>
              </div>
              <div class="space-y-1">
                <label class="text-[9px] font-normal text-slate-400 uppercase tracking-wider ml-1">{{ t('products.quantityMultiplier') }}</label>
                <input v-model.number="conv.multiplier" type="number"
                  class="w-full p-2 border border-slate-200 rounded-lg text-[11px] font-normal outline-none tabular-nums">
              </div>
              <div class="space-y-1">
                <label class="text-[9px] font-normal text-slate-400 uppercase tracking-wider ml-1">{{ t('products.specificSalePrice') }}</label>
                <input :value="formatMoneyInput(conv.baseSalePrice)" @input="updateMoneyField(conv, 'baseSalePrice', $event)" type="text" inputmode="decimal"
                  class="w-full p-2 border border-slate-200 rounded-lg text-[11px] font-normal outline-none tabular-nums text-blue-600">
              </div>
              <div class="flex items-end justify-center pb-1">
                <button @click="removeConversion(index)"
                  class="text-rose-400 hover:text-rose-600 p-2 rounded-lg transition-all"><svg class="w-4 h-4"
                    fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                    <path
                      d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                  </svg></button>
              </div>
            </div>
          </div>

          <div>
            <div class="flex justify-between items-center mb-5 border-b border-slate-100 pb-2">
              <h4 class="font-normal text-slate-800 text-sm uppercase tracking-tight">{{ t('products.wholesaleVIPDiscountTiers') }}</h4>
              <button @click="addPriceLevel"
                class="text-[10px] font-normal bg-blue-50 text-blue-600 border border-blue-200 px-4 py-2 rounded-lg uppercase tracking-widest hover:bg-blue-600 hover:text-white transition-all">{{ t('products.addPricingTier') }}</button>
            </div>
            <div v-if="form.additionalPrices.length === 0"
              class="text-xs text-slate-400 italic text-center py-6 bg-slate-50 border border-dashed border-slate-200 rounded-xl font-normal uppercase tracking-widest">
              {{ t('products.standardSingleTierPricing') }}</div>
            <div v-for="(price, idx) in form.additionalPrices" :key="idx"
              class="grid grid-cols-1 md:grid-cols-5 gap-3 mb-3 p-4 border border-blue-50 rounded-xl bg-blue-50/20 group">
              <div class="space-y-1">
                <label class="text-[9px] font-normal text-blue-400 uppercase tracking-wider ml-1">{{ t('products.targetUnit') }}</label>
                <select v-model="price.unitId"
                  class="w-full p-2 border border-blue-100 rounded-lg text-[11px] font-normal bg-white outline-none">
                  <option :value="form.unitId">{{ getUnitName(form.unitId) }} {{ t('products.mainUnit') }}</option>
                  <option v-for="c in form.conversions" :key="c.fromUnitId" :value="c.fromUnitId">{{
                    getUnitName(c.fromUnitId) }}</option>
                </select>
              </div>
              <div class="space-y-1">
                <label class="text-[9px] font-normal text-blue-400 uppercase tracking-wider ml-1">{{ t('products.tierName') }}</label>
                <input v-model="price.priceName" type="text" placeholder="VIP"
                  class="w-full p-2 border border-blue-100 rounded-lg text-[11px] font-normal outline-none">
              </div>
              <div class="space-y-1">
                <label class="text-[9px] font-normal text-blue-400 uppercase tracking-wider ml-1">{{ t('products.tierPrice') }}</label>
                <input :value="formatMoneyInput(price.priceValue)" @input="updateMoneyField(price, 'priceValue', $event)" type="text" inputmode="decimal"
                  class="w-full p-2 border border-blue-100 rounded-lg text-[11px] font-normal outline-none tabular-nums">
              </div>
              <div class="space-y-1">
                <label class="text-[9px] font-normal text-blue-400 uppercase tracking-wider ml-1">{{ t('products.minQty') }}</label>
                <input v-model.number="price.minQuantity" type="number"
                  class="w-full p-2 border border-blue-100 rounded-lg text-[11px] font-normal outline-none tabular-nums">
              </div>
              <div class="flex items-end justify-center pb-1">
                <button @click="removePriceLevel(idx)"
                  class="text-rose-400 hover:text-rose-600 p-2 rounded-lg transition-all"><svg class="w-4 h-4"
                    fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                    <path
                      d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                  </svg></button>
              </div>
            </div>
          </div>

          <div class="mt-10">
            <div class="flex flex-col sm:flex-row sm:justify-between sm:items-center gap-3 mb-5 border-b border-slate-100 pb-2">
              <h4 class="font-normal text-slate-800 text-sm uppercase tracking-tight">{{ t('products.quickPurchaseTitle') || 'Quick Purchase' }}</h4>
              <button @click="showQuickPurchase = !showQuickPurchase"
                class="text-[10px] font-normal bg-emerald-50 text-emerald-700 border border-emerald-200 px-4 py-2 rounded-lg uppercase tracking-widest hover:bg-emerald-600 hover:text-white transition-all">
                {{ showQuickPurchase ? (t('products.hidePurchase') || 'Hide Purchase') : (t('products.addPurchase') || '+ Add Purchase') }}
              </button>
            </div>

            <div v-if="showQuickPurchase" class="p-5 border border-emerald-100 rounded-xl bg-emerald-50/30 space-y-5">
              <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div ref="supplierPickerRef" class="space-y-1.5 md:col-span-2 relative">
                  <div class="flex items-center justify-between gap-3">
                    <label class="text-[9px] font-normal text-emerald-700 uppercase tracking-wider ml-1">{{ t('products.supplier') || 'Supplier' }}</label>
                    <button v-if="can('canEditSuppliers')" type="button" @click="closeSupplierResults(); lookupCreateKind = 'suppliers'"
                      class="shrink-0 text-xs text-blue-600 hover:underline py-1 text-right">{{ t('suppliers.addNewSupplier') }}</button>
                  </div>
                  <div class="relative">
                  <input v-model="supplierSearch" @input="handleSupplierSearch" @focus="openSupplierResults"
                    @keydown.down.prevent="moveSupplierDown" @keydown.up.prevent="moveSupplierUp"
                    @keydown.enter.prevent="selectHighlightedSupplier" type="text"
                    :placeholder="t('products.selectSupplier') || 'Select supplier'"
                    class="w-full pl-10 pr-8 p-3 bg-white border border-emerald-100 rounded-lg outline-none text-sm focus:ring-2 focus:ring-emerald-500/20">
                  <span class="absolute left-4 top-1/2 -translate-y-1/2 opacity-30 text-xs">
                    <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
                    </svg>
                  </span>
                  <button v-if="supplierSearch" @click="clearSupplierSearch" type="button"
                    class="absolute right-3 top-1/2 -translate-y-1/2 text-slate-300 hover:text-slate-500 text-xs">x</button>
                  </div>
                  <div v-if="showSupplierResults && filteredSuppliers.length > 0"
                    class="absolute z-[120] left-0 right-0 mt-1 bg-white border border-slate-200 rounded-xl shadow-2xl max-h-64 overflow-y-auto animate-in fade-in slide-in-from-top-2 duration-200">
                    <div v-for="(s, index) in filteredSuppliers" :key="s.supplierId || s.SupplierId"
                      @click="selectSupplier(s)"
                      :class="['p-3 cursor-pointer border-b border-slate-50 last:border-0 hover:bg-emerald-50 transition-colors',
                        supplierHighlightedIndex === index ? 'bg-emerald-50' : '']">
                      <p class="text-xs font-normal text-slate-800">{{ s.supplierName || s.SupplierName || s.Name }}</p>
                      <p class="text-[9px] text-slate-400 font-normal uppercase tracking-tighter">
                        {{ s.Phone || s.phone || t('products.supplier') || 'Supplier' }}
                      </p>
                    </div>
                  </div>
                </div>
                <div class="space-y-1.5">
                  <label class="text-[9px] font-normal text-emerald-700 uppercase tracking-wider ml-1">{{ t('products.unit') || 'Unit' }}</label>
                  <select v-model.number="quickPurchase.unitId"
                    class="w-full p-3 bg-white border border-emerald-100 rounded-lg outline-none text-sm">
                    <option :value="null">{{ t('products.typeOrSelectUnit') }}</option>
                    <option v-for="u in quickPurchaseUnits" :key="u.unitId" :value="u.unitId">{{ u.unitName }}</option>
                  </select>
                </div>
              </div>

              <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
                <div class="space-y-1.5">
                  <label class="text-[9px] font-normal text-emerald-700 uppercase tracking-wider ml-1">{{ t('products.qty') || 'Qty' }}</label>
                  <input v-model.number="quickPurchase.quantity" type="number" min="0" step="0.01"
                    class="w-full p-3 bg-white border border-emerald-100 rounded-lg outline-none text-sm tabular-nums">
                </div>
                <div class="space-y-1.5">
                  <label class="text-[9px] font-normal text-emerald-700 uppercase tracking-wider ml-1">{{ t('products.expiryDate') || 'Expiry Date' }}</label>
                  <input v-model="quickPurchase.expiryDate" type="date"
                    class="w-full p-3 bg-white border border-emerald-100 rounded-lg outline-none text-sm">
                </div>
                <div class="space-y-1.5">
                  <label class="text-[9px] font-normal text-emerald-700 uppercase tracking-wider ml-1">{{ t('products.alertDays') || 'Alert Days' }}</label>
                  <input v-model.number="quickPurchase.alertDays" type="number" min="0"
                    class="w-full p-3 bg-white border border-emerald-100 rounded-lg outline-none text-sm tabular-nums">
                </div>
                <div class="space-y-1.5">
                  <label class="text-[9px] font-normal text-emerald-700 uppercase tracking-wider ml-1">{{ t('products.totalAmount') || 'Total Amount' }}</label>
                  <input :value="formatMoneyInput(quickPurchaseTotal)" type="text" readonly
                    class="w-full p-3 bg-slate-100 border border-emerald-100 rounded-lg outline-none text-sm tabular-nums text-slate-700">
                </div>
              </div>

              <div class="grid grid-cols-1 md:grid-cols-3 gap-4 items-end">
                <div class="space-y-1.5 md:col-span-1">
                  <label class="text-[9px] font-normal text-emerald-700 uppercase tracking-wider ml-1">{{ t('products.paidAmount') || 'Paid Amount' }}</label>
                  <input :value="formatMoneyInput(quickPurchase.paidAmount)" @input="updateMoneyField(quickPurchase, 'paidAmount', $event)" type="text" inputmode="decimal"
                    class="w-full p-3 bg-white border border-emerald-100 rounded-lg outline-none text-sm tabular-nums">
                </div>
                <p class="md:col-span-2 text-[10px] text-emerald-700/80 leading-relaxed">
                  {{ t('products.quickPurchaseSaveHint') || 'This purchase will be saved when you click Save Product Registry.' }}
                </p>
              </div>
            </div>
          </div>

          <div class="flex justify-end gap-3 pt-8 border-t border-slate-100 mt-8">
            <button @click="showModal = false"
              class="px-6 py-2.5 border border-slate-200 text-slate-600 rounded-xl hover:bg-slate-50 font-normal uppercase text-[10px] tracking-widest transition-all">{{ t('products.discard') }}</button>
            <button @click="saveProduct" :disabled="quickPurchaseBusy"
              class="px-10 py-2.5 bg-blue-600 text-white rounded-xl font-normal uppercase text-[10px] tracking-widest shadow-lg shadow-blue-100 hover:bg-blue-700 transition active:scale-95 disabled:opacity-60">{{ t('products.saveProductRegistry') }}</button>
          </div>
        </div>
      </div>
    </div>

    <div v-if="showModal && currentTab === 'services'"
      class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
      <div
        class="bg-white overflow-hidden w-full max-w-md rounded-2xl shadow-2xl animate-in zoom-in duration-200 border border-slate-200">
        <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
          <h3 class="text-lg font-normal uppercase tracking-tight">{{ isEdit ? tx.editService : tx.addNewService }}</h3>
          <button @click="showModal = false" class="text-white/60 hover:text-white transition-colors">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        <div class="p-8 space-y-6">
          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ t('products.serviceLabelName') }}</label>
            <input v-model="serviceForm.serviceName" type="text"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none font-normal text-sm"
              :placeholder="t('products.egDeliveryCharge')">
          </div>
          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ t('products.serviceFeeKs') }}</label>
            <input :value="formatMoneyInput(serviceForm.price)" @input="updateMoneyField(serviceForm, 'price', $event)" type="text" inputmode="decimal"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none font-normal text-2xl text-blue-600">
          </div>
          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">Brief
              Description</label>
            <textarea v-model="serviceForm.description" rows="3"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none text-sm transition-all resize-none"
              placeholder="Details about this service..."></textarea>
          </div>
          <button @click="saveService"
            class="w-full bg-indigo-600 text-white py-4 rounded-xl font-normal uppercase text-[10px] tracking-widest shadow-lg hover:bg-indigo-700 transition active:scale-95">Confirm
            Service Account</button>
        </div>
      </div>
    </div>

    <div v-if="showModal && currentTab === 'bundles'"
      class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
      <div
        class="bg-white overflow-hidden w-full max-w-2xl rounded-2xl shadow-2xl animate-in zoom-in duration-200 border border-slate-200">
        <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
          <h3 class="text-lg font-normal uppercase tracking-tight">
            {{ isEdit ? t('products.modifyBundle') : t('products.createPromotionBundle') }}
          </h3>
          <button @click="showModal = false" class="text-white/60 hover:text-white transition-colors">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        <div class="p-8 space-y-6 max-h-[85vh] overflow-y-auto custom-scrollbar text-left">
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest">{{ t('products.bundleLabel') }}</label>
              <input v-model="bundleForm.bundleName" type="text"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none font-normal text-sm"
                :placeholder="t('products.egComboSetA')">
            </div>
            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest">{{ t('products.bundlePricePackage') }}</label>
              <input :value="formatMoneyInput(bundleForm.bundlePrice)" @input="updateMoneyField(bundleForm, 'bundlePrice', $event)" type="text" inputmode="decimal"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none font-normal text-xl text-emerald-600">
            </div>
          </div>

          <div class="bg-slate-50 p-5 rounded-xl border border-slate-200">
            <label class="text-[10px] font-normal text-slate-500 uppercase tracking-widest block mb-4">{{ t('products.addProductsToPackage') }}</label>
            <div class="flex gap-2 flex-wrap">
              <select v-model="tempBundleItem.itemType"
                class="w-32 p-3 bg-white border border-slate-200 rounded-lg text-sm font-normal outline-none">
                <option value="Product">Product</option>
                <option value="Service">Service</option>
              </select>

              <select v-if="tempBundleItem.itemType === 'Product'" v-model="tempBundleItem.productId"
                class="flex-1 p-3 bg-white border border-slate-200 rounded-lg text-sm font-normal outline-none">
                <option :value="null">{{ t('products.selectProduct') }}</option>
                <option v-for="p in products" :key="p.ProductId" :value="p.ProductId">{{ p.Name }}</option>
              </select>
              <select v-else v-model="tempBundleItem.serviceId"
                class="flex-1 p-3 bg-white border border-slate-200 rounded-lg text-sm font-normal outline-none">
                <option :value="null">{{ t('products.selectService') || 'Select Service' }}</option>
                <option v-for="s in services" :key="s.ServiceId || s.serviceId" :value="s.ServiceId || s.serviceId">{{ s.ServiceName || s.serviceName }}</option>
              </select>
              <select v-if="tempBundleItem.itemType === 'Product'" v-model="tempBundleItem.unitId"
                class="w-36 p-3 bg-white border border-slate-200 rounded-lg text-sm font-normal outline-none">
                <option :value="null">Unit</option>
                <option v-for="u in tempBundleUnits" :key="u.UnitId || u.unitId" :value="u.UnitId || u.unitId">
                  {{ u.UnitName || u.unitName }}
                </option>
              </select>
              <input v-model.number="tempBundleItem.quantity" type="number"
                class="w-24 p-3 bg-white border border-slate-200 rounded-lg text-sm font-normal text-center"
                :placeholder="t('products.qtyHeader')">
              <button @click="addItemToBundle"
                class="bg-slate-900 text-white px-5 rounded-lg font-normal text-lg hover:bg-black transition-all shadow-md">+</button>
            </div>
          </div>

          <div class="space-y-2">
            <h4 class="text-[9px] font-normal text-slate-400 uppercase tracking-widest">{{ t('products.includedItemsList') }}</h4>
            <div v-if="bundleForm.bundleItems.length === 0"
              class="py-10 text-center border border-dashed rounded-xl text-slate-300 font-normal uppercase text-[10px]">
              {{ t('products.emptyList') }}</div>
            <div v-for="(bi, idx) in bundleForm.bundleItems" :key="idx"
              class="flex justify-between items-center p-3.5 bg-white border border-slate-100 rounded-lg shadow-sm group">
              <div class="flex items-center gap-3">
                <div
                  class="w-8 h-8 rounded bg-blue-50 text-blue-600 flex items-center justify-center font-normal text-xs">
                  {{
                    idx + 1 }}</div>
                <div>
                  <div class="text-sm font-normal text-slate-800">
                    {{ (bi.itemType || 'Product') === 'Service' ? getServiceName(bi.serviceId) : getProductName(bi.productId) }}
                    <span class="text-[9px] ml-2 px-2 py-0.5 rounded bg-slate-100 text-slate-500 uppercase tracking-wider">{{ bi.itemType || 'Product' }}</span>
                  </div>
                  <div class="text-[10px] text-slate-400 font-normal">{{ t('products.qtyHeader') }}: {{ bi.quantity }} {{ (bi.itemType || 'Product') === 'Service' ? t('products.unitsLabel') : (bi.unitName || getUnitName(bi.unitId)) }}</div>
                </div>
              </div>
              <button @click="bundleForm.bundleItems.splice(idx, 1)"
                class="text-rose-300 hover:text-rose-600 p-2 transition-colors">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                  <path d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>
          </div>

          <button @click="saveBundle"
            class="w-full bg-blue-600 text-white py-4 rounded-xl font-normal uppercase text-[10px] tracking-widest shadow-lg shadow-blue-100 mt-4 hover:bg-blue-700 transition active:scale-95">{{ t('products.saveBundleAccount') }}</button>
        </div>
      </div>
    </div>

    <!-- Mobile Floating Action Buttons -->
    <div class="lg:hidden fixed bottom-6 right-6 flex flex-col items-end gap-3 z-[90]">
      <!-- Bulk Price Adjust FAB -->
      <button v-if="currentTab === 'products' && can('canEditProducts')" @click="openBulkPriceModal()"
        class="bg-indigo-600 text-white w-14 h-14 rounded-full shadow-lg shadow-indigo-300 flex items-center justify-center active:scale-95 transition-all outline-none"
        :title="t('products.bulkPriceAdjust')">
        <svg class="h-6 w-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 7h.01M7 3h5c.512 0 1.024.195 1.414.586l7 7a2 2 0 010 2.828l-7 7a2 2 0 01-2.828 0l-7-7A1.994 1.994 0 013 12V7a4 4 0 014-4z" />
        </svg>
      </button>

      <!-- Add New FAB -->
      <button @click="openAddModal()"
        class="bg-blue-600 text-white w-14 h-14 rounded-full shadow-lg shadow-blue-300 flex items-center justify-center active:scale-95 transition-all outline-none"
        :title="addButtonLabel">
        <svg class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
      </button>
    </div>
  </div>
  <ProductLookupCreateModal v-if="lookupCreateKind && showModal" :kind="lookupCreateKind"
    @close="lookupCreateKind = null" @created="selectCreatedLookup" />
</template>

<script setup>
import { ref, onMounted, onUnmounted, computed, watch } from 'vue';
import api from '../services/api';
import { useToast } from '../composables/useToast';
import { useI18n } from '../composables/useI18n';
import { usePermissions } from '../composables/usePermissions';
import ProductLookupCreateModal from '../components/ProductLookupCreateModal.vue';

const { addToast } = useToast();
const { t } = useI18n();
const { can } = usePermissions();
const lookupCreateKind = ref(null);
const selectCreatedLookup = (record) => {
  if (lookupCreateKind.value === 'units') {
    units.value = [...units.value.filter(unit => (unit.unitId ?? unit.UnitId) !== record.unitId), record];
    form.value.unitId = record.unitId;
    form.value.unitName = record.unitName;
  } else if (lookupCreateKind.value === 'categories') {
    categories.value = [...categories.value.filter(category => (category.categoryId ?? category.CategoryId) !== record.categoryId), record];
    form.value.categoryId = record.categoryId;
    form.value.categoryName = record.categoryName;
  } else if (lookupCreateKind.value === 'suppliers') {
    suppliers.value = [...suppliers.value.filter(supplier => (supplier.SupplierId ?? supplier.supplierId) !== record.SupplierId), record];
    selectSupplier(record);
  }
  lookupCreateKind.value = null;
};

// 1. App State & Tabs
const currentTab = ref('products');
const tabs = computed(() => [
  { id: 'products', label: tx.value.products },
  { id: 'services', label: tx.value.services },
  { id: 'bundles', label: tx.value.bundles }
]);
const currentTabLabel = computed(() => tabs.value.find(t => t.id === currentTab.value)?.label || tx.value.products);
const addButtonLabel = computed(() => {
  if (currentTab.value === 'products') return t('products.addNewProduct');
  if (currentTab.value === 'services') return t('products.addNewService');
  if (currentTab.value === 'bundles') return t('products.addNewBundle');
  return `${tx.value.addNew} ${currentTabLabel.value}`;
});
const tx = computed(() => ({
  catalogManagement: t('products.catalogManagement'),
  masterInventory: t('products.masterInventory'),
  addNew: t('products.addNew'),
  no: t('products.no'),
  skuCode: t('products.skuCode'),
  productDetails: t('products.productDetails'),
  baseUnit: t('products.baseUnit'),
  standardPrice: t('products.standardPrice'),
  inventoryAlert: t('products.inventoryAlert'),
  products: t('products.products'),
  services: t('products.services'),
  bundles: t('products.bundles'),
  serviceName: t('products.serviceName'),
  standardFee: t('products.standardFee'),
  bundleIdentity: t('products.bundleIdentity'),
  includedItems: t('products.includedItems'),
  packagePrice: t('products.packagePrice'),
  actions: t('products.actions'),
  editService: t('products.editService'),
  addNewService: t('products.addNewService'),
  couldNotLoadBundleItems: t('products.couldNotLoadBundleItems'),
  failedLoadBundleDetails: t('products.failedLoadBundleDetails'),
  productRegistryUpdated: t('products.productRegistryUpdated'),
  errorSavingProduct: t('products.errorSavingProduct'),
  serviceUpdated: t('products.serviceUpdated'),
  newServiceCreated: t('products.newServiceCreated'),
  packageBundleSaved: t('products.packageBundleSaved'),
  bundleSaveFailed: t('products.bundleSaveFailed'),
  deleteConfirm: t('products.deleteConfirm'),
  invalidItemId: t('products.invalidItemId'),
  recordDeletedSuccessfully: t('products.recordDeletedSuccessfully'),
  deleteOperationFailed: t('products.deleteOperationFailed')
}));

// 2. Data Sources
const products = ref([]);
const services = ref([]);
const bundles = ref([]);
const categories = ref([]);
const units = ref([]);
const suppliers = ref([]);

// 3. Search & Pagination Controls
const searchQuery = ref('');
const currentPage = ref(1);
const pageSize = ref(10);
const showModal = ref(false);
watch(showModal, value => { if (!value) lookupCreateKind.value = null; });
const isEdit = ref(false);

// 4. Form States
const showBulkPriceModal = ref(false);
const bulkAdjustTab = ref('new'); // 'new' or 'history'
const priceAdjustHistory = ref([]);
const ADJ_HISTORY_KEY = 'versa_price_adjust_history';

const loadAdjustHistory = () => {
  const json = localStorage.getItem(ADJ_HISTORY_KEY);
  if (json) priceAdjustHistory.value = JSON.parse(json);
};

const saveAdjustHistory = () => {
  localStorage.setItem(ADJ_HISTORY_KEY, JSON.stringify(priceAdjustHistory.value));
};

const bulkAdjustFilters = ref({
  categoryId: null,
  search: ''
});
const bulkAdjustConfig = ref({
  target: 'SalePrice', // 'CostPrice' or 'SalePrice'
  operation: 'add',    // 'add' or 'subtract'
  type: 'percentage',  // 'percentage' or 'fixed'
  value: 0
});

const form = ref({
  productId: 0, name: '', sku: '', categoryId: null, unitId: null, costPrice: 0, salePrice: 0, alertQuantity: 0,
  conversions: [], additionalPrices: []
});
const showQuickPurchase = ref(false);
const quickPurchaseBusy = ref(false);
const supplierSearch = ref('');
const filteredSuppliers = ref([]);
const showSupplierResults = ref(false);
const supplierHighlightedIndex = ref(-1);
const supplierPickerRef = ref(null);
const quickPurchase = ref({
  supplierId: 0,
  quantity: 1,
  unitId: null,
  expiryDate: '',
  alertDays: 30,
  paidAmount: 0
});
const serviceForm = ref({ serviceId: 0, serviceName: '', price: 0, description: '' });
const bundleForm = ref({ bundleId: 0, bundleName: '', bundlePrice: 0, bundleItems: [] });
const tempBundleItem = ref({ itemType: 'Product', productId: null, serviceId: null, unitId: null, quantity: 1 });
const tempBundleUnits = ref([]);

const moneyFormatter = new Intl.NumberFormat('en-US', {
  minimumFractionDigits: 0,
  maximumFractionDigits: 2
});

const parseMoneyInput = (value) => {
  const raw = String(value ?? '').replace(/,/g, '');
  const cleaned = raw.replace(/[^\d.]/g, '');
  if (!cleaned) return 0;

  const [intPart, ...rest] = cleaned.split('.');
  const decimalPart = rest.join('').slice(0, 2);
  const normalized = decimalPart ? `${intPart}.${decimalPart}` : intPart;
  const parsed = Number(normalized);
  return Number.isFinite(parsed) ? parsed : 0;
};

const formatMoneyInput = (value) => {
  const numeric = Number(value ?? 0);
  if (!Number.isFinite(numeric)) return '0';
  return moneyFormatter.format(numeric);
};

const updateMoneyField = (target, field, event) => {
  target[field] = parseMoneyInput(event.target.value);
};

// ================= COMPUTED LOGIC =================

const bulkAdjustPreviewData = computed(() => {
  let source = products.value;

  // 1. Filter
  if (bulkAdjustFilters.value.categoryId) {
    source = source.filter(p => p.CategoryId === bulkAdjustFilters.value.categoryId);
  }
  if (bulkAdjustFilters.value.search) {
    const q = bulkAdjustFilters.value.search.toLowerCase();
    source = source.filter(p => (p.Name || '').toLowerCase().includes(q) || (p.Sku || '').toLowerCase().includes(q));
  }

  // 2. Map new prices
  return source.map(item => {
    const currentPrice = bulkAdjustConfig.value.target === 'CostPrice' ? (item.CostPrice || 0) : (item.SalePrice || 0);
    let newPrice = currentPrice;

    if (bulkAdjustConfig.value.value > 0) {
      let adjustmentAmount = 0;

      if (bulkAdjustConfig.value.type === 'percentage') {
        adjustmentAmount = currentPrice * (bulkAdjustConfig.value.value / 100);
      } else {
        adjustmentAmount = bulkAdjustConfig.value.value;
      }

      if (bulkAdjustConfig.value.operation === 'add') {
        newPrice = currentPrice + adjustmentAmount;
      } else {
        newPrice = currentPrice - adjustmentAmount;
        if (newPrice < 0) newPrice = 0; // Prevent negative prices
      }
    }

    return {
      ...item,
      currentPrice,
      newPrice: Math.round(newPrice) // Round to nearest whole number for Ks
    };
  });
});

const filteredData = computed(() => {
  const q = searchQuery.value.toLowerCase().trim();
  let source = [];
  if (currentTab.value === 'products') source = products.value;
  else if (currentTab.value === 'services') source = services.value;
  else if (currentTab.value === 'bundles') source = bundles.value;

  if (!q) return source;

  return source.filter(item => {
    if (currentTab.value === 'products') {
      return (item.Name || '').toLowerCase().includes(q) || (item.Sku || '').toLowerCase().includes(q);
    } else if (currentTab.value === 'services') {
      return (item.ServiceName || item.serviceName || '').toLowerCase().includes(q);
    } else if (currentTab.value === 'bundles') {
      return (item.BundleName || item.bundleName || '').toLowerCase().includes(q);
    }
    return false;
  });
});

const getServiceName = (serviceId) => {
  const s = services.value.find(x => (x.ServiceId || x.serviceId) === serviceId);
  return s ? (s.ServiceName || s.serviceName) : '-';
};

const totalRecords = computed(() => filteredData.value.length);
const totalPages = computed(() => Math.ceil(totalRecords.value / pageSize.value) || 0);

const paginatedData = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value;
  return filteredData.value.slice(start, start + pageSize.value);
});

const quickPurchaseUnits = computed(() => {
  const options = [];
  if (form.value.unitId) {
    options.push({ unitId: form.value.unitId, unitName: getUnitName(form.value.unitId) });
  }

  form.value.conversions.forEach(conv => {
    if (!conv.fromUnitId || options.some(u => u.unitId === conv.fromUnitId)) return;
    options.push({ unitId: conv.fromUnitId, unitName: getUnitName(conv.fromUnitId) });
  });

  return options;
});

const quickPurchaseMultiplier = computed(() => {
  if (!quickPurchase.value.unitId || quickPurchase.value.unitId === form.value.unitId) return 1;
  const conversion = form.value.conversions.find(c => c.fromUnitId === quickPurchase.value.unitId);
  return Number(conversion?.multiplier || 1);
});

const quickPurchaseSalePrice = computed(() => {
  if (!quickPurchase.value.unitId || quickPurchase.value.unitId === form.value.unitId) {
    return Number(form.value.salePrice || 0);
  }

  const conversion = form.value.conversions.find(c => c.fromUnitId === quickPurchase.value.unitId);
  return Number(conversion?.baseSalePrice || 0);
});

const quickPurchaseUnitCost = computed(() => Number(form.value.costPrice || 0) * quickPurchaseMultiplier.value);
const quickPurchaseTotal = computed(() => Number(quickPurchase.value.quantity || 0) * quickPurchaseUnitCost.value);


const showDetailModal = ref(false);


const selectedBundleDetail = ref({ master: {}, items: [] });


const showBundleDetail = async (bundle) => {
  try {
    const id = bundle.BundleId || bundle.bundleId;
    if (!id) return;


    const res = await api.get(`/bundles/${id}`);


    selectedBundleDetail.value = res.data;
    showDetailModal.value = true;
  } catch (err) {
    console.error("Detail Load Error:", err);
    addToast(tx.value.couldNotLoadBundleItems, "error");
  }
};

// ================= LOAD FUNCTIONS =================

const fetchAll = async () => {
  try {

    const [pRes, sRes, bRes, cRes, uRes, supplierRes] = await Promise.all([
      api.get('/products').catch(err => { console.error("Products API Missing"); return { data: [] }; }),
      api.get('/services').catch(err => { console.error("Services API Missing"); return { data: [] }; }),
      api.get('/bundles').catch(err => { console.error("Bundles API Missing"); return { data: [] }; }),
      api.get('/categories').catch(err => { console.error("Categories API Missing"); return { data: [] }; }),
      api.get('/units').catch(err => { console.error("Units API Missing"); return { data: [] }; }),
      api.get('/suppliers').catch(err => { console.error("Suppliers API Missing"); return { data: [] }; })
    ]);

    products.value = pRes.data || [];
    services.value = sRes.data || [];
    bundles.value = bRes.data || [];
    categories.value = cRes.data || [];
    units.value = uRes.data || [];
    suppliers.value = (supplierRes.data || []).map(s => ({
      ...s,
      supplierId: s.SupplierId ?? s.supplierId,
      supplierName: s.SupplierName ?? s.supplierName ?? s.Name ?? ''
    }));


    console.log("Products count:", products.value.length);
  } catch (err) {
    console.error("Critical Fetch Error:", err);
  }
};

const getUnitName = (id) => {
  const matched = units.value.find(u => (u.UnitId || u.unitId) === id);
  return matched?.UnitName || matched?.unitName || 'PCS';
};
const getProductName = (id) => products.value.find(p => (p.ProductId || p.productId) === id)?.Name || 'Unknown Product';
const formatNumber = (val) => new Intl.NumberFormat().format(val || 0);

// ================= MODAL LOGIC =================

const openBulkPriceModal = () => {
  bulkAdjustTab.value = 'new';
  loadAdjustHistory();
  bulkAdjustConfig.value = {
    target: 'SalePrice',
    operation: 'add',
    type: 'percentage',
    value: 0
  };
  bulkAdjustFilters.value = {
    categoryId: null,
    search: ''
  };
  showBulkPriceModal.value = true;
};

const openAddModal = () => {
  isEdit.value = false;
  resetForms();
  showQuickPurchase.value = false;
  tempBundleUnits.value = [];
  tempBundleItem.value = { itemType: 'Product', productId: null, serviceId: null, unitId: null, quantity: 1 };
  showModal.value = true;
};

const editItem = async (item) => {
  isEdit.value = true;
  showQuickPurchase.value = false;
  if (currentTab.value === 'products') {
    const res = await api.get(`/products/${item.ProductId || item.productId}`);
    const d = res.data;
    form.value = {
      productId: d.ProductId || d.productId,
      name: d.Name || d.name,
      sku: d.Sku || d.sku,
      categoryId: d.CategoryId || d.categoryId,
      categoryName: categories.value.find(c => c.categoryId === (d.CategoryId || d.categoryId))?.categoryName || '',
      unitId: d.UnitId || d.unitId,
      unitName: units.value.find(u => (u.unitId || u.UnitId) === (d.UnitId || d.unitId))?.unitName || '',
      costPrice: d.CostPrice || d.costPrice,
      salePrice: d.SalePrice || d.salePrice,
      alertQuantity: d.AlertQuantity || d.alertQuantity || 0,
      conversions: (d.Conversions || d.conversions || []).map(c => ({ fromUnitId: c.FromUnitId || c.fromUnitId, multiplier: c.Multiplier || c.multiplier, baseSalePrice: c.BaseSalePrice || c.baseSalePrice })),
      additionalPrices: (d.AdditionalPrices || d.additionalPrices || []).map(p => ({ unitId: p.UnitId || p.unitId, priceName: p.PriceName || p.priceName, priceValue: p.PriceValue || p.priceValue, minQuantity: p.MinQuantity || p.minQuantity }))
    };
  } else if (currentTab.value === 'services') {
    serviceForm.value = {
      serviceId: item.ServiceId || item.serviceId || item.Id,
      serviceName: item.ServiceName || item.serviceName,
      price: item.Price || item.price,
      description: item.Description || item.description || ""
    };
  } else if (currentTab.value === 'bundles') {
    try {
      const id = item.BundleId || item.bundleId;
      const res = await api.get(`/bundles/${id}`);
      const data = res.data;


      bundleForm.value = {
        bundleId: data.master.BundleId || data.master.bundleId,
        bundleName: data.master.BundleName || data.master.bundleName,
        bundlePrice: data.master.BundlePrice || data.master.bundlePrice,
        bundleItems: data.items.map(i => ({
          itemType: i.ItemType || i.itemType || 'Product',
          productId: i.ProductId || i.productId || null,
          serviceId: i.ServiceId || i.serviceId || null,
          unitId: i.UnitId || i.unitId || null,
          unitName: i.UnitName || i.unitName || null,
          quantity: i.Quantity || i.quantity
        }))
      };
    } catch (err) {
      console.error("Bundle Load Error:", err);
      addToast(tx.value.failedLoadBundleDetails, "error");
      return;
    }
  }
  showModal.value = true;
};

// ================= SAVE ACTIONS =================

const applyBulkPriceAdjustment = async () => {
  try {
    const itemsToUpdate = bulkAdjustPreviewData.value;
    if (itemsToUpdate.length === 0) return;

    addToast("Applying bulk adjustments...", "info");

    // Prepare history item
    const historyItem = {
      id: Date.now().toString(),
      timestamp: new Date().toLocaleString(),
      description: `${bulkAdjustConfig.value.operation === 'add' ? 'Increase' : 'Decrease'} ${bulkAdjustConfig.value.target === 'SalePrice' ? 'Retail' : 'Cost'} Price by ${bulkAdjustConfig.value.value}${bulkAdjustConfig.value.type === 'percentage' ? '%' : ' Ks'}`,
      target: bulkAdjustConfig.value.target,
      count: itemsToUpdate.length,
      originalData: {}
    };

    const updates = itemsToUpdate.map(item => {
      let updatedCostPrice = item.CostPrice;
      let updatedSalePrice = item.SalePrice;

      // Save original data for rollback
      historyItem.originalData[item.ProductId] = {
        SalePrice: item.SalePrice,
        CostPrice: item.CostPrice
      };

      if (bulkAdjustConfig.value.target === 'CostPrice') {
        updatedCostPrice = item.newPrice;
      } else {
        updatedSalePrice = item.newPrice;
      }

      return {
        ...item,
        productId: item.ProductId,
        categoryId: item.CategoryId,
        unitId: item.UnitId,
        costPrice: updatedCostPrice,
        salePrice: updatedSalePrice,
        alertQuantity: item.AlertQuantity,
        conversions: (item.Conversions || []).map(c => ({ FromUnitId: c.FromUnitId, ToUnitId: item.UnitId, Multiplier: c.Multiplier, BaseSalePrice: c.BaseSalePrice })),
        additionalPrices: (item.AdditionalPrices || []).map(p => ({ UnitId: p.UnitId, PriceName: p.PriceName, PriceValue: p.PriceValue, MinQuantity: p.MinQuantity }))
      };
    });

    const promises = updates.map(payload => api.put(`/products/${payload.productId}`, payload));
    await Promise.all(promises);

    // Save to history
    priceAdjustHistory.value.unshift(historyItem);
    if (priceAdjustHistory.value.length > 10) priceAdjustHistory.value.pop();
    saveAdjustHistory();

    addToast(`Successfully applied new prices to ${updates.length} products.`, "success");
    showBulkPriceModal.value = false;
    fetchAll();
  } catch (err) {
    addToast("Error applying bulk price adjustments", "error");
    console.error(err);
  }
};

const rollbackAdjustment = async (historyItem) => {
  if (!confirm(`Are you sure you want to rollback "${historyItem.description}"? This will revert ${historyItem.count} products to their original prices.`)) return;

  try {
    addToast("Initiating rollback...", "info");

    const productIds = Object.keys(historyItem.originalData);
    const promises = productIds.map(async (id) => {
      // Find current product state to preserve other fields
      const p = products.value.find(prod => String(prod.ProductId) === String(id));
      if (!p) return;

      const original = historyItem.originalData[id];
      const payload = {
        ...p,
        productId: p.ProductId,
        categoryId: p.CategoryId,
        unitId: p.UnitId,
        costPrice: original.CostPrice,
        salePrice: original.SalePrice,
        alertQuantity: p.AlertQuantity,
        conversions: (p.Conversions || []).map(c => ({ FromUnitId: c.FromUnitId, ToUnitId: p.UnitId, Multiplier: c.Multiplier, BaseSalePrice: c.BaseSalePrice })),
        additionalPrices: (p.AdditionalPrices || []).map(p => ({ UnitId: p.UnitId, PriceName: p.PriceName, PriceValue: p.PriceValue, MinQuantity: p.MinQuantity }))
      };
      return api.put(`/products/${id}`, payload);
    });

    await Promise.all(promises);

    // Remove from history
    priceAdjustHistory.value = priceAdjustHistory.value.filter(h => h.id !== historyItem.id);
    saveAdjustHistory();

    addToast("Rollback successful.", "success");
    showBulkPriceModal.value = false;
    fetchAll();
  } catch (err) {
    console.error("Rollback Error:", err);
    addToast("Failed to rollback adjustment.", "error");
  }
};

const resolveSavedProductId = async (responseData = null) => {
  const responseId = Number(responseData?.productId || responseData?.ProductId || 0);
  if (responseId > 0) return responseId;

  const productSku = (form.value.sku || '').trim().toLowerCase();
  const productName = (form.value.name || '').trim().toLowerCase();
  const res = await api.get('/products');
  const list = Array.isArray(res.data) ? res.data : [];

  const matched = list.find(p => {
    const sku = String(p.Sku || p.sku || '').trim().toLowerCase();
    const name = String(p.Name || p.name || '').trim().toLowerCase();
    return (productSku && sku === productSku) || (!productSku && productName && name === productName);
  });

  return Number(matched?.ProductId || matched?.productId || 0);
};

const saveProduct = async ({ closeModal = true, refresh = true } = {}) => {
  if (!form.value.name) {
    addToast("Product Name is required", "warning");
    return null;
  }
  const shouldCreatePurchase = closeModal && showQuickPurchase.value;
  if (shouldCreatePurchase && !validateQuickPurchase()) return null;

  quickPurchaseBusy.value = true;
  try {
    // No need to resolve IDs, the select binds directly to categoryId and unitId

    const payload = {
      ...form.value,
      AlertQuantity: form.value.alertQuantity,
      Conversions: form.value.conversions.map(c => ({ FromUnitId: c.fromUnitId, ToUnitId: form.value.unitId, Multiplier: c.multiplier, BaseSalePrice: c.baseSalePrice })),
      AdditionalPrices: form.value.additionalPrices.map(p => ({ UnitId: p.unitId, PriceName: p.priceName, PriceValue: p.priceValue, MinQuantity: p.minQuantity }))
    };
    let savedProductId = form.value.productId;
    if (isEdit.value) {
      await api.put(`/products/${form.value.productId}`, payload);
    } else {
      const res = await api.post('/products', payload);
      savedProductId = await resolveSavedProductId(res.data);
      form.value.productId = savedProductId;
    }

    if (shouldCreatePurchase) {
      if (!Number(savedProductId || 0)) {
        throw new Error(t('products.productIdMissing') || 'Product was saved but its id was not returned.');
      }
      await api.post('/purchases', buildQuickPurchasePayload(savedProductId, quickPurchase.value.paidAmount));
      addToast(t('products.productAndPurchaseRecorded') || 'Product saved and purchase voucher recorded.', "success");
    } else {
      addToast(tx.value.productRegistryUpdated, "success");
    }

    if (closeModal) showModal.value = false;
    if (refresh) fetchAll();
    return savedProductId;
  } catch (err) {
    const fallback = shouldCreatePurchase ? (t('products.quickPurchaseFailed') || 'Quick purchase failed.') : tx.value.errorSavingProduct;
    addToast(err.response?.data?.Message || err.response?.data?.message || err.message || fallback, "error");
    return null;
  } finally {
    quickPurchaseBusy.value = false;
  }
};

const supplierMatches = (supplier, query) => {
  const name = supplier.supplierName || supplier.SupplierName || supplier.Name || '';
  const phone = supplier.Phone || supplier.phone || '';
  return name.toLowerCase().includes(query) || String(phone).toLowerCase().includes(query);
};

const openSupplierResults = () => {
  const query = supplierSearch.value.toLowerCase().trim();
  filteredSuppliers.value = (query ? suppliers.value.filter(s => supplierMatches(s, query)) : suppliers.value).slice(0, 50);
  showSupplierResults.value = filteredSuppliers.value.length > 0;
  supplierHighlightedIndex.value = filteredSuppliers.value.length > 0 ? 0 : -1;
};

const closeSupplierResults = () => {
  showSupplierResults.value = false;
  supplierHighlightedIndex.value = -1;
};

const handleSupplierOutsideClick = (event) => {
  if (!supplierPickerRef.value?.contains(event.target)) {
    closeSupplierResults();
  }
};

const handleSupplierSearch = () => {
  quickPurchase.value.supplierId = 0;
  const query = supplierSearch.value.toLowerCase().trim();
  filteredSuppliers.value = (query ? suppliers.value.filter(s => supplierMatches(s, query)) : suppliers.value).slice(0, 50);
  showSupplierResults.value = filteredSuppliers.value.length > 0;
  supplierHighlightedIndex.value = filteredSuppliers.value.length > 0 ? 0 : -1;
};

const selectSupplier = (supplier) => {
  quickPurchase.value.supplierId = Number(supplier.supplierId ?? supplier.SupplierId ?? 0);
  supplierSearch.value = supplier.supplierName || supplier.SupplierName || supplier.Name || '';
  filteredSuppliers.value = [];
  showSupplierResults.value = false;
  supplierHighlightedIndex.value = -1;
};

const clearSupplierSearch = () => {
  supplierSearch.value = '';
  quickPurchase.value.supplierId = 0;
  filteredSuppliers.value = [];
  showSupplierResults.value = false;
  supplierHighlightedIndex.value = -1;
};

const moveSupplierDown = () => {
  if (supplierHighlightedIndex.value < filteredSuppliers.value.length - 1) supplierHighlightedIndex.value++;
};

const moveSupplierUp = () => {
  if (supplierHighlightedIndex.value > 0) supplierHighlightedIndex.value--;
};

const selectHighlightedSupplier = () => {
  if (supplierHighlightedIndex.value >= 0 && supplierHighlightedIndex.value < filteredSuppliers.value.length) {
    selectSupplier(filteredSuppliers.value[supplierHighlightedIndex.value]);
  }
};

const buildQuickPurchasePayload = (productId, paidAmount = 0) => ({
  SupplierId: Number(quickPurchase.value.supplierId),
  BranchId: Number(localStorage.getItem('BranchId') || 1),
  TotalAmount: Number(quickPurchaseTotal.value),
  PaidAmount: Number(paidAmount || 0),
  PaymentMethod: Number(paidAmount || 0) > 0 ? 'Cash' : '',
  PurchaseDate: new Date().toISOString().slice(0, 10),
  Remarks: `Quick purchase from product modal: ${form.value.name}`,
  Items: [{
    ProductId: Number(productId),
    UnitId: Number(quickPurchase.value.unitId),
    Quantity: Number(quickPurchase.value.quantity || 0),
    FOCQuantity: 0,
    CostPrice: Number(quickPurchaseUnitCost.value),
    ExpiryDate: quickPurchase.value.expiryDate || null,
    AlertDays: Number(quickPurchase.value.alertDays || 0),
    NewSalePrice: Number(quickPurchaseSalePrice.value || 0)
  }]
});

const validateQuickPurchase = () => {
  if (!quickPurchase.value.supplierId) {
    addToast(t('products.selectSupplierWarning') || 'Please select supplier.', 'warning');
    return false;
  }
  if (!form.value.unitId || !quickPurchase.value.unitId) {
    addToast(t('products.selectUnitWarning') || 'Please select product unit.', 'warning');
    return false;
  }
  if (Number(quickPurchase.value.quantity || 0) <= 0) {
    addToast(t('products.enterQtyWarning') || 'Please enter purchase quantity.', 'warning');
    return false;
  }
  if (Number(quickPurchase.value.paidAmount || 0) > quickPurchaseTotal.value) {
    addToast(t('products.paidAmountTooHigh') || 'Paid amount cannot be higher than total amount.', 'warning');
    return false;
  }
  return true;
};

const saveService = async () => {
  try {
    const payload = {
      ServiceName: serviceForm.value.serviceName,
      Price: Number(serviceForm.value.price || 0),
      Description: serviceForm.value.description || ""
    };

    if (isEdit.value) {

      const id = serviceForm.value.serviceId;
      await api.put(`/services/${id}`, payload);
      addToast(tx.value.serviceUpdated, "success");
    } else {
      // Add Mode: POST Request
      await api.post('/services', payload);
      addToast(tx.value.newServiceCreated, "success");
    }

    showModal.value = false;
    fetchAll();
  } catch (err) {
    const msg = err.response?.data?.Message || "Operation Failed";
    addToast(msg, "error");
  }
};

const saveBundle = async () => {
  try {
    const payload = {
      ...bundleForm.value,
      bundleItems: (bundleForm.value.bundleItems || []).map(i => ({
        itemType: i.itemType || 'Product',
        productId: i.itemType === 'Service' ? null : i.productId,
        serviceId: i.itemType === 'Service' ? i.serviceId : null,
        unitId: i.itemType === 'Service' ? null : i.unitId,
        quantity: Number(i.quantity || 0)
      }))
    };
    if (isEdit.value) await api.put(`/bundles/${bundleForm.value.bundleId}`, payload);
    else await api.post('/bundles', payload);
    addToast(tx.value.packageBundleSaved, "success");
    showModal.value = false;
    fetchAll();
  } catch (err) { addToast(tx.value.bundleSaveFailed, "error"); }
};

// ================= HELPER ACTIONS =================

const addItemToBundle = () => {
  if (tempBundleItem.value.quantity <= 0) return;

  if (tempBundleItem.value.itemType === 'Service') {
    if (!tempBundleItem.value.serviceId) return;
  } else {
    if (!tempBundleItem.value.productId) return;
    if (!tempBundleItem.value.unitId) return;
  }
  bundleForm.value.bundleItems.push({ ...tempBundleItem.value });
  tempBundleItem.value = { itemType: 'Product', productId: null, serviceId: null, unitId: null, quantity: 1 };
  tempBundleUnits.value = [];
};

const deleteItem = async (item) => {

  const itemName = item.Name || item.ServiceName || item.BundleName || item.name || "this item";
  if (!confirm(`${tx.value.deleteConfirm} "${itemName}"?`)) return;

  try {

    let endpoint = "";
    let id = null;

    if (currentTab.value === 'products') {
      endpoint = "products";
      id = item.ProductId || item.productId;
    } else if (currentTab.value === 'services') {
      endpoint = "services";
      id = item.ServiceId || item.serviceId || item.Id;
    } else if (currentTab.value === 'bundles') {
      endpoint = "bundles";
      id = item.BundleId || item.bundleId;
    }

    if (!id) {
      addToast(tx.value.invalidItemId, "error");
      return;
    }


    await api.delete(`/${endpoint}/${id}`);


    addToast(tx.value.recordDeletedSuccessfully, "success");


    await fetchAll();

  } catch (err) {
    console.error("Delete Error:", err);
    const msg = err.response?.data?.Message || err.response?.data?.message || tx.value.deleteOperationFailed;
    addToast(msg, "error");
  }
};

const addConversion = () => form.value.conversions.push({ fromUnitId: null, multiplier: 1, baseSalePrice: 0 });
const removeConversion = (index) => form.value.conversions.splice(index, 1);
const addPriceLevel = () => form.value.additionalPrices.push({ unitId: form.value.unitId, priceName: '', priceValue: 0, minQuantity: 0 });
const removePriceLevel = (idx) => form.value.additionalPrices.splice(idx, 1);

const resetForms = () => {
  form.value = {
    productId: 0,
    name: '',
    sku: '',
    categoryId: null,
    categoryName: '',
    unitId: null,
    unitName: '',
    costPrice: 0,
    salePrice: 0,
    alertQuantity: 0,
    conversions: [],
    additionalPrices: []
  };
  showQuickPurchase.value = false;
  quickPurchase.value = {
    supplierId: 0,
    quantity: 1,
    unitId: null,
    expiryDate: '',
    alertDays: 30,
    paidAmount: 0
  };
  supplierSearch.value = '';
  filteredSuppliers.value = [];
  showSupplierResults.value = false;
  supplierHighlightedIndex.value = -1;
  serviceForm.value = { serviceId: 0, serviceName: '', price: 0, description: '' };
  bundleForm.value = { bundleId: 0, bundleName: '', bundlePrice: 0, bundleItems: [] };
  tempBundleUnits.value = [];
  tempBundleItem.value = { itemType: 'Product', productId: null, serviceId: null, unitId: null, quantity: 1 };
};

watch(searchQuery, () => { currentPage.value = 1; });
watch(currentTab, () => { currentPage.value = 1; searchQuery.value = ''; });
watch(() => form.value.unitId, (unitId) => {
  if (!quickPurchase.value.unitId || !quickPurchaseUnits.value.some(u => u.unitId === quickPurchase.value.unitId)) {
    quickPurchase.value.unitId = unitId || null;
  }
});
watch(() => form.value.conversions, () => {
  if (quickPurchase.value.unitId && !quickPurchaseUnits.value.some(u => u.unitId === quickPurchase.value.unitId)) {
    quickPurchase.value.unitId = form.value.unitId || null;
  }
}, { deep: true });
watch(() => tempBundleItem.value.itemType, (itemType) => {
  if (itemType === 'Service') {
    tempBundleUnits.value = [];
    tempBundleItem.value.productId = null;
    tempBundleItem.value.unitId = null;
  } else {
    tempBundleItem.value.serviceId = null;
  }
});
watch(() => tempBundleItem.value.productId, async (productId) => {
  if (tempBundleItem.value.itemType !== 'Product' || !productId) {
    tempBundleUnits.value = [];
    tempBundleItem.value.unitId = null;
    return;
  }

  try {
    const res = await api.get(`/Products/units/${productId}`);
    tempBundleUnits.value = res.data || [];
    if (tempBundleUnits.value.length > 0) {
      tempBundleItem.value.unitId = tempBundleUnits.value[0].UnitId || tempBundleUnits.value[0].unitId;
    }
  } catch {
    tempBundleUnits.value = [];
    tempBundleItem.value.unitId = null;
  }
});
onMounted(() => {
  document.addEventListener('mousedown', handleSupplierOutsideClick);
  document.addEventListener('touchstart', handleSupplierOutsideClick);
  fetchAll();
});

onUnmounted(() => {
  document.removeEventListener('mousedown', handleSupplierOutsideClick);
  document.removeEventListener('touchstart', handleSupplierOutsideClick);
});
</script>

<style scoped>
.tabular-nums {
  font-variant-numeric: tabular-nums;
}

.custom-scrollbar::-webkit-scrollbar {
  width: 4px;
}

.custom-scrollbar::-webkit-scrollbar-thumb {
  background: #e2e8f0;
  border-radius: 10px;
}
</style>

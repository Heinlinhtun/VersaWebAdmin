<template>
  <div class="h-full bg-gray-100 flex flex-col overflow-hidden text-sm font-sans" :class="{ 'cursor-wait': isNetworkWaiting }">
    <div
      v-if="connectionNotice"
      class="fixed top-4 left-1/2 -translate-x-1/2 z-[200] max-w-[92vw] rounded-xl border px-4 py-3 shadow-xl flex items-center gap-3 text-xs sm:text-sm"
      :class="connectionStatus === 'offline' ? 'bg-amber-50 border-amber-200 text-amber-800' : 'bg-white border-slate-200 text-slate-700'"
    >
      <span
        v-if="isNetworkWaiting"
        class="h-4 w-4 rounded-full border-2 border-current border-t-transparent animate-spin shrink-0"
      ></span>
      <span v-else class="h-2.5 w-2.5 rounded-full bg-amber-500 shrink-0"></span>
      <span class="font-normal">{{ connectionNotice }}</span>
    </div>

    <!-- Invoice Preview Modal (after sale) -->
    <div
      v-if="showInvoicePreview"
      class="fixed inset-0 bg-slate-900/60 backdrop-blur-sm flex items-stretch sm:items-center justify-center p-2 sm:p-4 z-[150]"
    >
      <div class="sale-invoice-preview-modal bg-white rounded-2xl max-w-lg w-full shadow-2xl overflow-hidden animate-in zoom-in duration-200 border border-slate-200 max-h-[95vh] min-h-0 flex flex-col">
        <!-- Header -->
        <div class="bg-[#3f3f46] p-4 text-white flex justify-between items-center shrink-0">
          <div>
            <h3 class="text-sm font-normal uppercase tracking-tight">{{ t('sale.invoicePreview') }}</h3>
            <p class="text-blue-300 text-[10px] font-normal uppercase mt-0.5 tracking-widest">{{ lastSaleNo }}</p>
          </div>
          <button @click="closeInvoicePreview" class="text-white/60 hover:text-white transition-colors">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <!-- Invoice Preview (scrollable) - shows the actual print-area content -->
        <div
          class="invoice-preview-scroll overflow-y-auto flex-1 min-h-0 p-3 sm:p-4 bg-slate-50 flex"
          :class="receiptSize === '80mm' ? 'justify-center overflow-x-hidden' : 'justify-start overflow-x-auto'"
        >
          <div
            class="bg-white rounded-xl shadow-sm border border-slate-200 invoice-preview-wrapper"
            :class="{ 'invoice-preview-wrapper-wide': receiptSize !== '80mm' }"
            ref="invoicePreviewRef"
          >
            <div v-html="capturedPrintHtml" class="invoice-preview-content"></div>
          </div>
        </div>

        <!-- Footer Actions -->
        <div class="invoice-preview-actions p-4 border-t border-slate-200 bg-white shrink-0 space-y-3">
          <!-- Save as image checkbox -->
          <label class="flex items-center gap-2 text-xs text-slate-600 cursor-pointer select-none">
            <input
              v-model="saveInvoiceAsImage"
              type="checkbox"
              class="h-4 w-4 rounded border-gray-300 text-emerald-600 focus:ring-emerald-500"
            >
            <span>{{ t('sale.saveInvoiceAsImage') }}</span>
          </label>

          <div class="flex flex-col sm:flex-row gap-3">
            <button @click="printFromPreview"
              class="flex-1 py-3 bg-blue-600 text-white rounded-xl font-normal uppercase text-[11px] tracking-wider hover:bg-blue-700 shadow-lg shadow-blue-200 transition-all active:scale-95 flex items-center justify-center gap-2">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 17h2a2 2 0 002-2v-4a2 2 0 00-2-2H5a2 2 0 00-2 2v4a2 2 0 002 2h2m2 4h6a2 2 0 002-2v-4a2 2 0 00-2-2H9a2 2 0 00-2 2v4a2 2 0 002 2zm8-12V5a2 2 0 00-2-2H9a2 2 0 00-2 2v4h10z" />
              </svg>
              {{ t('sale.printInvoice') }}
            </button>
            <button @click="shareFromPreview"
              class="flex-1 py-3 bg-emerald-600 text-white rounded-xl font-normal uppercase text-[11px] tracking-wider hover:bg-emerald-700 shadow-lg shadow-emerald-200 transition-all active:scale-95 flex items-center justify-center gap-2">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8.684 13.342C8.886 12.938 9 12.482 9 12c0-.482-.114-.938-.316-1.342m0 2.684a3 3 0 110-2.684m0 2.684l6.632 3.316m-6.632-6l6.632-3.316m0 0a3 3 0 105.367-2.684 3 3 0 00-5.367 2.684zm0 9.316a3 3 0 105.368 2.684 3 3 0 00-5.368-2.684z" />
              </svg>
              {{ t('sale.shareInvoice') }}
            </button>
          </div>
        </div>
      </div>
    </div>


    <div class="bg-white p-4 shadow-sm flex flex-col gap-3 border-b shrink-0">
      <div class="flex flex-col md:flex-row md:items-start md:justify-between gap-3">
        <div class="flex-1 relative mt-1">
          <input v-model="searchQuery" type="text" ref="searchInput" @input="handleSearch"
            @keydown.enter="handleBarcodeScan" :placeholder="t('sale.searchPlaceholder')"
            class="w-full pl-10 pr-4 py-3 bg-gray-50 rounded-xl outline-none focus:ring-2 focus:ring-blue-500 font-normal border border-gray-200 transition-all">
          <span class="absolute left-3 top-3.5 text-gray-400"> <svg class="h-4 w-4" fill="none" stroke="currentColor"
              viewBox="0 0 24 24">
              <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
            </svg></span>
          <label class="mt-2 inline-flex items-center gap-2 text-[11px] font-normal text-gray-500 select-none">
            <input
              v-model="showOutOfStockItems"
              type="checkbox"
              class="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
              @change="currentPage = 1"
            >
            <span>{{ t('sale.showOutOfStockItems') }}</span>
          </label>
        </div>

        <div class="flex bg-gray-100 p-1 rounded-xl border border-gray-200 self-start md:self-center overflow-x-auto">
          <button v-for="tab in catalogTabs" :key="tab.id" @click="currentTab = tab.id" :class="['px-5 py-2 rounded-lg font-normal text-[10px] uppercase transition-all',
            currentTab === tab.id ? 'bg-white text-blue-600 shadow-sm' : 'text-gray-400 hover:text-gray-600']">
            {{ tab.label }}
          </button>
        </div>
      </div>

      <div v-if="currentTab === 'all' || currentTab === 'products'"
        class="flex flex-wrap gap-2 items-center pb-1">
        <button @click="selectCategory(null)"
          :class="['px-4 py-1.5 rounded-lg font-normal text-[11px] whitespace-nowrap transition', selectedCategory === null ? 'bg-blue-600 text-white shadow-md' : 'bg-white border text-gray-500']">
          {{ t('sale.allCategories') }}
        </button>
        <button v-for="cat in displayedCategories" :key="cat.categoryId" @click="selectCategory(cat.categoryId)"
          :class="['px-4 py-1.5 rounded-lg font-normal text-[11px] whitespace-nowrap transition', selectedCategory === cat.categoryId ? 'bg-blue-600 text-white' : 'bg-white border text-gray-500']">
          {{ cat.categoryName }}
        </button>
        <button
          v-if="categories.length > defaultVisibleCategoryCount"
          @click="toggleCategoryExpand"
          class="px-4 py-1.5 rounded-lg font-normal text-[11px] whitespace-nowrap transition bg-slate-100 border text-slate-600 hover:bg-slate-200">
          {{ showAllCategories ? (t('common.seeLess') || 'See Less') : (t('common.seeAll') || 'See All') }}
        </button>
      </div>
    </div>

    <div class="flex-1 flex flex-col lg:flex-row overflow-y-auto lg:overflow-hidden">

      <div class="w-full lg:flex-1 lg:min-w-0 p-3 sm:p-4 flex flex-col gap-4 bg-gray-50 lg:h-full overflow-hidden min-h-0">
        <div
          class="flex-1 overflow-y-auto grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-4 2xl:grid-cols-5 gap-4 content-start custom-scrollbar pr-1">

          <div v-for="item in paginatedItems" :key="item.uniqueKey" @click="handleItemSelect(item)"
            :class="['p-2.5 sm:p-3.5 rounded-xl shadow-sm hover:shadow-xl cursor-pointer transition border-2 group relative flex flex-col justify-between h-36 sm:h-40 overflow-hidden',
              item.itemType === 'QuickSale' ? 'bg-blue-50 border-blue-200' : 'bg-white border-transparent hover:border-blue-500']">

            <div
              class="absolute top-0 right-0 px-1.5 py-0.5 sm:px-2 sm:py-1 rounded-bl-lg text-[7px] sm:text-[8px] font-normal uppercase text-white shadow-sm"
              :class="getTypeColor(item.itemType)">
              {{ item.itemType }}
            </div>

            <div v-if="item.itemType === 'QuickSale'" class="flex flex-col items-center justify-center flex-1">
              <div class="p-2 sm:p-2.5 bg-blue-100 rounded-full text-blue-600 mb-1 sm:mb-2 group-hover:scale-110 transition-transform">
                <svg class="w-5 h-5 sm:w-6 sm:h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
                </svg>
              </div>
              <div class="font-normal text-blue-800 uppercase text-[9px] sm:text-[10px] tracking-widest text-center">{{
                t('sale.quickSale') || 'Quick Sale' }}</div>
            </div>

            <div v-else>
              <template v-if="item.itemType === 'Product'">
                <span
                  :class="['text-[9px] sm:text-[10px] px-1.5 sm:px-2 py-0.5 rounded-full font-normal uppercase', item.EffectiveStock > 0 ? 'bg-green-100 text-green-600' : 'bg-red-100 text-red-600']">
                  {{ t('sale.stock') }}: {{ item.EffectiveStock }}
                </span>
                <div v-if="item.ConsignmentQuantity > 0"
                  class="mt-1 flex flex-wrap gap-1 text-[8px] sm:text-[9px] uppercase">
                  <span class="px-1.5 py-0.5 rounded bg-indigo-50 text-indigo-600">
                    {{ t('sale.consignmentShort') }}: {{ item.EffectiveConsignmentStock }}
                  </span>
                  <span class="px-1.5 py-0.5 rounded bg-slate-100 text-slate-500">
                    {{ t('sale.owned') }}: {{ item.EffectiveOwnedStock }}
                  </span>
                </div>
              </template>
              <span v-else
                class="text-[9px] sm:text-[10px] px-1.5 sm:px-2 py-0.5 rounded-full font-normal uppercase bg-purple-100 text-purple-600">
                {{ t('sale.available') }}
              </span>

              <div class="font-normal text-gray-800 group-hover:text-blue-600 mt-1 sm:mt-2 leading-tight line-clamp-2 text-xs sm:text-[13px]">
                {{ item.Name }}
              </div>
            </div>

            <div v-if="item.itemType !== 'QuickSale'"
              class="flex justify-between items-end mt-2 sm:mt-3 pt-1 sm:pt-2 border-t border-dashed border-gray-100">
              <div class="text-blue-700 font-semibold text-sm sm:text-[15px]">{{ formatNumber(item.SalePrice) }} <small
                  class="text-[9px] sm:text-[10px] font-normal">{{ currencySymbol }}</small></div>
              <div class="bg-gray-100 text-[8px] sm:text-[9px] px-1.5 sm:px-2 py-0.5 sm:py-1 rounded font-normal text-gray-400 uppercase">
                {{ item.itemType === 'Product' ? item.UnitName : t('sale.fixedUnit') }}
              </div>
            </div>
          </div>

          <div v-if="filteredItems.length === 0" class="col-span-full py-20 text-center opacity-30">
            <div class="text-4xl mb-2">
              <svg class="w-12 h-12 mx-auto" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path d="M9 17v-2m3 2v-4m3 4v-6m2 10H7a2 2 0 01-2-2V5a2 2 0 012-2h5l2 2h5a2 2 0 012 2v10a2 2 0 01-2 2z"
                  stroke-width="1.5" />
              </svg>
            </div>
            <p class="font-normal uppercase tracking-widest text-xs">{{ t('sale.noEntries') }}</p>
          </div>
        </div>

        <div v-if="totalPages > 1"
          class="bg-white p-3 pr-24 lg:pr-3 rounded-2xl flex justify-between items-center shadow-sm border border-gray-200 shrink-0">
          <div class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-2">
            {{ t('layout.nav.products') }}: {{ filteredItems.length }}
          </div>
          <div class="flex items-center gap-1.5">
            <button @click="currentPage--" :disabled="currentPage === 1"
              class="px-4 py-2 bg-gray-50 hover:bg-gray-100 rounded-xl font-normal text-[10px] uppercase transition disabled:opacity-30">{{
                t('common.prev') }}</button>
            <span class="px-3 text-xs font-normal text-slate-400">{{ t('common.page') }} {{ currentPage }} / {{
              totalPages
            }}</span>
            <button @click="currentPage++" :disabled="currentPage >= totalPages"
              class="px-4 py-2 bg-gray-50 hover:bg-gray-100 rounded-xl font-normal text-[10px] uppercase transition disabled:opacity-30">{{
                t('common.next') }}</button>
          </div>
        </div>

      </div>

      <!-- Mobile Floating Cart Button -->
      <button v-show="!isCartOpen" @click="isCartOpen = true"
        class="lg:hidden fixed bottom-6 right-6 z-40 bg-blue-600 text-white p-4 rounded-full shadow-2xl hover:bg-blue-700 active:scale-90 transition-all flex items-center justify-center group">
        <div class="relative">
          <svg class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M3 3h2l.4 2M7 13h10l4-8H5.4M7 13L5.4 5M7 13l-2.293 2.293c-.63.63-.184 1.707.707 1.707H17m0 0a2 2 0 100 4 2 2 0 000-4zm-8 2a2 2 0 11-4 0 2 2 0 014 0z">
            </path>
          </svg>
          <span v-if="cart.length > 0"
            class="absolute -top-2 -right-2 bg-rose-500 text-white text-[10px] font-normal w-5 h-5 flex items-center justify-center rounded-full border-2 border-white shadow-sm animate-bounce">
            {{ cart.length }}
          </span>
        </div>
      </button>

      <div v-show="isCartOpen"
        class="cart-panel w-full lg:w-[420px] lg:min-w-[380px] xl:w-[480px] bg-white shadow-2xl flex flex-col border-t lg:border-t-0 lg:border-l border-gray-100 h-[calc(100vh-80px)] lg:h-full overflow-hidden fixed lg:static bottom-0 inset-x-0 z-50 lg:z-auto lg:!flex rounded-none leading-normal animate-in slide-in-from-bottom lg:animate-none duration-300">
        <div class="p-4 border-b flex justify-between items-center bg-gray-50/50 shrink-0">
          <div class="flex items-center gap-2">
            <h3 class="font-normal text-gray-800 uppercase text-xs tracking-tight">{{ t('sale.currentVoucher') }}</h3>
          </div>
          <div class="flex items-center gap-3">
            <button @click="openHeldSalesModal"
              class="text-blue-600 font-normal uppercase text-[10px] hover:bg-blue-50 px-3 py-1 rounded-lg transition">
              {{ t('sale.heldSales') }}
              <span v-if="heldSales.length" class="ml-1 text-blue-400">({{ heldSales.length }})</span>
            </button>
            <button @click="cart = []"
              class="text-red-500 font-normal uppercase text-[10px] hover:bg-red-50 px-3 py-1 rounded-lg transition">{{
                t('sale.clearAll') }}</button>
            <button @click="isCartOpen = false" class="lg:hidden p-1 hover:bg-gray-200 rounded-full text-gray-500">
              <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
              </svg>
            </button>
          </div>
        </div>

        <div class="flex-1 overflow-y-auto p-4 space-y-3 custom-scrollbar">
          <div v-for="(item, idx) in cart" :key="item.CartLineId || `${item.ItemType || 'Item'}-${item.ProductId || item.ServiceId || item.BundleId || item.Name || 'line'}-${idx}`"
            class="flex flex-col bg-blue-50/20 rounded-2xl border border-blue-100/50 shadow-sm transition-all duration-300">
            <!-- Collapsed Row (Summary) -->
            <div @click="item.isExpanded = !item.isExpanded"
              class="flex items-center gap-2 sm:gap-3 p-3 sm:p-4 cursor-pointer hover:bg-blue-100/20 rounded-t-2xl">
              <div class="flex-1 min-w-0">
                <div class="font-normal text-xs sm:text-[13px] text-gray-800 leading-tight truncate">
                  {{ item.Name }} {{ item.isFOC ? '(FOC)' : '' }}
                </div>
                <div class="flex items-center gap-1.5 sm:gap-2 mt-0.5">
                  <span class="text-[7px] sm:text-[8px] font-normal text-blue-400 uppercase tracking-widest">{{ item.ItemType
                  }}</span>
                  <span class="text-[9px] sm:text-[10px] text-slate-300 font-normal">|</span>
                  <span class="text-[9px] sm:text-[10px] text-slate-500 font-normal">{{ item.Quantity }} {{ getUnitName(item.UnitId)
                  }}</span>
                </div>
              </div>

              <div class="text-right">
                <div :class="['font-normal text-xs sm:text-sm', item.isFOC ? 'text-green-600' : 'text-blue-700']">
                  {{ item.isFOC ? '0' : formatNumber((item.Quantity * item.SelectedPrice) - (item.DiscountAmount || 0))
                  }}
                </div>
              </div>

              <div class="flex items-center ml-1 sm:ml-2 gap-1.5 sm:gap-2">
                <button @click.stop="removeFromCart(idx)"
                  class="p-1 sm:p-1.5 text-gray-300 hover:text-red-500 hover:bg-red-50 rounded-lg transition-all">
                  <svg class="w-3.5 h-3.5 sm:w-4 sm:h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                  </svg>
                </button>
                <div class="transition-transform duration-300" :class="{ 'rotate-180': item.isExpanded }">
                  <svg class="w-3.5 h-3.5 sm:w-4 sm:h-4 text-slate-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
                  </svg>
                </div>
              </div>
            </div>

            <!-- Expanded Details -->
            <div v-if="item.isExpanded"
              class="p-4 pt-2 space-y-4 border-t border-blue-100/30 animate-in slide-in-from-top-2 duration-300">
              <div class="cart-item-fields grid grid-cols-[1fr_auto_1fr] items-end gap-4">
                <div class="cart-item-unit flex flex-col min-w-0">
                  <label class="text-[9px] font-normal text-gray-400 uppercase mb-1">{{ t('sale.unit') }}</label>
                  <select v-if="item.ItemType === 'Product'" v-model.number="item.UnitId" @change="onUnitChange(item)"
                    class="w-full text-[11px] p-2 border rounded-xl bg-white font-normal border-blue-200 outline-none shadow-sm">
                    <option :value="item.BaseUnitId">{{ item.BaseUnitName }}</option>
                    <option v-for="u in item.Conversions" :key="u.UnitId" :value="u.UnitId">
                      {{ getUnitName(u.UnitId) }}
                    </option>
                  </select>
                  <div v-else
                    class="p-2 bg-gray-100 rounded-xl text-[10px] font-normal text-gray-500 text-center border border-gray-200 uppercase">
                    {{ t('sale.fixedUnit') }}</div>
                </div>

                <div class="cart-item-qty flex flex-col items-center">
                  <label class="text-[9px] font-normal text-gray-400 uppercase mb-1">{{ t('sale.qty') }}</label>
                  <div class="cart-qty-control flex items-center gap-1 bg-white border border-blue-100 p-1 rounded-xl shadow-sm">
                    <button @click="updateQty(item, -quantityButtonStep)"
                      class="w-8 h-8 text-blue-600 font-normal hover:bg-blue-50 rounded-lg">-</button>
                    <input v-model.number="item.Quantity" type="number" :min="quantityInputMin" :step="quantityInputStep"
                      @input="onCartQtyInput(item, $event)" @blur="normalizeCartQty(item)"
                      class="w-12 text-center font-normal text-blue-800 text-sm outline-none bg-transparent no-arrows">
                    <button @click="updateQty(item, quantityButtonStep)"
                      class="w-8 h-8 text-blue-600 font-normal hover:bg-blue-50 rounded-lg">+</button>
                  </div>
                </div>

                <div class="cart-item-price flex flex-col items-end min-w-0">
                  <label class="text-[9px] font-normal text-gray-400 uppercase mb-1">{{ t('sale.price') }}</label>
                  <div v-if="item.ItemType === 'Product' && getAvailablePrices(item).length > 0"
                    class="relative w-full" data-price-tier-menu>
                    <button type="button" @click.stop="item.showPriceMenu = !item.showPriceMenu"
                      class="w-full flex items-center justify-between gap-2 text-[10px] px-2 py-1.5 border rounded-xl bg-white font-normal border-blue-200 outline-none shadow-sm leading-tight text-slate-700">
                      <span class="truncate">{{ getPriceTierLabel(item) }}</span>
                      <svg class="w-3 h-3 text-slate-400 transition-transform"
                        :class="{ 'rotate-180': item.showPriceMenu }" fill="none" stroke="currentColor"
                        viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="3" d="M19 9l-7 7-7-7" />
                      </svg>
                    </button>

                    <div v-if="item.showPriceMenu"
                      class="absolute right-0 mt-1 w-full max-h-44 overflow-y-auto bg-white border border-blue-200 rounded-xl shadow-xl z-[80] custom-scrollbar">
                      <button type="button" @click.stop="selectPriceTier(item, item.CurrentBasePrice, 'Normal')"
                        class="w-full text-left px-2.5 py-2 text-[10px] hover:bg-blue-50 transition">
                        {{ t('sale.normalPrice') || 'Normal' }} ({{ formatNumber(item.CurrentBasePrice) }})
                      </button>
                      <button v-for="lvl in getAvailablePrices(item)" :key="(lvl.PriceName || lvl.priceName) + '-' + (lvl.PriceValue || lvl.priceValue)"
                        type="button" @click.stop="selectPriceTier(item, (lvl.PriceValue || lvl.priceValue), (lvl.PriceName || lvl.priceName))"
                        class="w-full text-left px-2.5 py-2 text-[10px] hover:bg-blue-50 transition">
                        {{ (lvl.PriceName || lvl.priceName) }} ({{ formatNumber((lvl.PriceValue || lvl.priceValue)) }})
                      </button>
                    </div>
                  </div>
                  <input :value="formatMoneyInput(item.SelectedPrice)" @input="onCartPriceInput(item, $event)" type="text" inputmode="decimal"
                    :class="[
                      'w-full text-right text-[11px] p-2 border rounded-xl bg-white font-normal border-blue-200 outline-none shadow-sm text-blue-800',
                      (item.ItemType === 'Product' && getAvailablePrices(item).length > 0) ? 'mt-2' : ''
                    ]">
                </div>
              </div>

              <div class="flex items-center justify-between gap-4 pt-3 border-t border-blue-100/30">
                <button @click="toggleFOC(idx)" :class="['px-5 py-2 rounded-xl text-[10px] font-normal uppercase transition-all shadow-sm',
                  item.isFOC ? 'bg-green-600 text-white' : 'bg-gray-100 text-gray-400']">
                  FOC
                </button>

                <div v-if="!item.isFOC" class="flex-1 flex flex-col items-center">
                  <label class="text-[9px] font-normal text-red-400 uppercase mb-0.5 italic">{{ t('sale.discount')
                  }}</label>
                  <input v-model.number="item.DiscountAmount" type="number"
                    class="w-full max-w-[80px] p-1.5 border-2 rounded-xl border-red-50 text-red-600 font-normal text-center outline-none bg-white">
                </div>

                <div class="flex flex-col items-end">
                  <label class="text-[9px] font-normal text-gray-400 uppercase mb-1">{{ t('sale.subtotal') }}</label>
                  <div :class="['text-lg font-normal', item.isFOC ? 'text-green-600' : 'text-blue-700']">
                    {{ item.isFOC ? '0' : formatNumber((item.Quantity * item.SelectedPrice) - (item.DiscountAmount ||
                      0)) }}
                  </div>
                </div>
              </div>

              <!-- Note Field -->
              <div class="px-4 pb-4 border-t border-blue-50/50 pt-3 mt-1">
                <label class="text-[9px] font-normal text-gray-400 uppercase mb-1 block">{{ t('sale.itemNotes') }}</label>
                <textarea v-model="item.Note" rows="1"
                  class="w-full p-2.5 bg-blue-50/30 border border-blue-100/50 rounded-xl text-[11px] font-normal outline-none focus:ring-2 focus:ring-blue-500/20 resize-none transition-all placeholder:text-gray-300"
                  :placeholder="t('sale.itemNotes')"></textarea>
              </div>
            </div>
          </div>
        </div>

        <div class="p-6 bg-white border-t-2 border-gray-50 space-y-4 shrink-0">
          <div class="flex justify-between items-center">
            <span class="text-gray-800 font-normal uppercase text-xs">{{ t('sale.netAmount') }}:</span>
            <span class="text-3xl font-normal text-blue-800 tracking-tighter">{{ formatNumber(totalAmount) }} <small
                class="text-xs">{{ currencySymbol }}</small></span>
          </div>
          <button @click="processCheckout" :disabled="cart.length === 0"
            class="w-full bg-blue-600 text-white py-4 rounded-2xl font-normal text-lg shadow-xl hover:bg-blue-700 active:scale-[0.98] disabled:bg-gray-200 transition-all">
            {{ t('sale.checkout') }}
          </button>
        </div>
      </div>
    </div>
    <div v-if="showHeldSalesModal"
      class="fixed inset-0 z-[120] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
      <div class="bg-white rounded-3xl max-w-lg w-full shadow-2xl overflow-hidden border border-slate-200">
        <div class="bg-slate-900 p-5 text-white flex justify-between items-center">
          <div>
            <h3 class="text-lg font-normal uppercase tracking-tight">{{ t('sale.heldSales') }}</h3>
            <p class="text-[10px] text-white/60 uppercase tracking-widest">{{ t('sale.heldDrafts', { count: heldSales.length }) }}</p>
          </div>
          <button @click="showHeldSalesModal = false" class="text-white/60 hover:text-white transition-colors">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <div class="p-5 max-h-[70vh] overflow-y-auto custom-scrollbar bg-slate-50">
          <div v-if="heldSales.length === 0" class="py-10 text-center text-slate-400 text-sm">
            {{ t('sale.noHeldSales') }}
          </div>
          <div v-else class="space-y-3">
            <div v-for="draft in heldSales" :key="draft.id"
              class="bg-white border border-slate-200 rounded-2xl p-4 shadow-sm">
              <div class="flex justify-between gap-4">
                <div class="min-w-0">
                  <div class="font-normal text-slate-800 text-sm truncate">{{ draft.customerName || 'Walkin Customer' }}</div>
                  <div class="text-[10px] text-slate-400 uppercase tracking-wider mt-1">
                    {{ formatHeldSaleDate(draft.updatedAt) }} - {{ draft.itemCount }} items
                  </div>
                </div>
                <div class="text-right shrink-0">
                  <div class="text-lg font-normal text-blue-700 tabular-nums">{{ formatNumber(draft.totalAmount || 0) }}</div>
                  <div class="text-[10px] text-slate-400">{{ currencySymbol }}</div>
                </div>
              </div>
              <div class="mt-3 text-[11px] text-slate-500 truncate">
                {{ draft.preview }}
              </div>
              <div class="mt-4 grid grid-cols-2 gap-3">
                <button @click="loadHeldSale(draft)"
                  class="py-2.5 bg-blue-600 text-white rounded-xl text-[10px] uppercase tracking-widest hover:bg-blue-700 transition">
                  {{ t('sale.loadHeldSale') }}
                </button>
                <button @click="deleteHeldSale(draft.id)"
                  class="py-2.5 bg-white border border-rose-200 text-rose-500 rounded-xl text-[10px] uppercase tracking-widest hover:bg-rose-50 transition">
                  {{ t('sale.deleteHeldSale') }}
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
    <div v-if="showPaymentModal"
      class="payment-modal-overlay fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4 overflow-y-auto">
      <div
        class="payment-modal-card bg-white rounded-3xl max-w-lg w-full shadow-2xl animate-in zoom-in duration-200 overflow-hidden border border-slate-200 flex flex-col">

        <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center shrink-0">
          <h3 class="text-lg font-normal uppercase tracking-tight">{{ t('sale.finalizeCheckout') }}</h3>
          <button @click="showPaymentModal = false" class="text-white/60 hover:text-white transition-colors">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <div class="payment-modal-body p-4 sm:p-8 space-y-4 sm:space-y-5 overflow-y-auto custom-scrollbar bg-white flex-1 min-h-0">

          <!-- Sale Date Picker -->
          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ t('sale.saleDate') }}</label>
            <input v-model="saleForm.SaleDate" type="date"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-2xl text-sm outline-none focus:ring-2 focus:ring-blue-500/20 font-medium transition-all"
              :max="new Date().toISOString().split('T')[0]">
          </div>

          <div class="space-y-1.5 relative">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
              t('sale.customerSelection') }}</label>
            <div class="group relative">
              <span class="absolute left-4 top-1/2 -translate-y-1/2 text-slate-400"><svg class="h-4 w-4" fill="none"
                  stroke="currentColor" viewBox="0 0 24 24">
                  <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
                </svg></span>
              <input v-model="customerSearch" @input="handleCustomerSearch"
                @focus="openCustomerResults"
                @keydown.down.prevent="moveSearchDown(filteredCustomersForSearch)"
                @keydown.up.prevent="moveSearchUp(filteredCustomersForSearch)"
                @keydown.enter.prevent="selectHighlightedProductFromSearch(filteredCustomersForSearch, 'customer')"
                type="text"
                class="w-full pl-10 pr-10 py-3.5 bg-slate-50 border border-slate-200 rounded-2xl text-sm outline-none focus:ring-2 focus:ring-blue-500/20 font-medium transition-all"
                placeholder="Search or type new customer...">
              <button v-if="customerSearch" @click="clearSearch('customer')"
                class="absolute right-4 top-1/2 -translate-y-1/2 text-slate-300 hover:text-slate-500">✕</button>

              <!-- Customer Search Results -->
              <div v-if="showCustomerResults && filteredCustomersForSearch.length > 0"
                class="absolute z-[110] left-0 right-0 mt-1 bg-white border border-slate-200 rounded-2xl shadow-2xl max-h-48 overflow-y-auto animate-in fade-in slide-in-from-top-2 duration-200 custom-scrollbar">
                <div v-for="(c, idx) in filteredCustomersForSearch" :key="c.customerId" @click="selectCustomer(c)"
                  :class="['p-3.5 flex items-center justify-between cursor-pointer border-b border-slate-100 last:border-0 hover:bg-blue-50 transition-colors', idx === highlightedIndex ? 'bg-blue-50' : '']">
                  <div>
                    <div class="text-sm font-semibold text-slate-800">{{ c.name }}</div>
                    <div class="text-[10px] text-slate-400 font-medium">{{ c.phone || 'No phone' }} • {{
                      formatNumber(c.currentBalance) }} {{ currencySymbol }}</div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 border-t border-slate-50 pt-4">
            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">Discount Type</label>
              <select v-model="voucherDiscountType" @change="syncVoucherDiscount"
                class="w-full p-3 bg-orange-50 border border-orange-100 rounded-lg font-normal outline-none text-orange-600">
                <option value="amount">Amount ({{ currencySymbol }})</option>
                <option value="percentage">Percentage (%)</option>
              </select>
            </div>
            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                t('sale.voucherDiscount') }}</label>
              <div class="relative">
                <input v-model.number="voucherDiscountInput" @input="syncVoucherDiscount" type="number" min="0"
                  :max="voucherDiscountType === 'percentage' ? 100 : undefined"
                  class="w-full p-3 bg-orange-50 border border-orange-100 rounded-lg font-normal outline-none text-orange-600"
                  placeholder="0">
                <span class="absolute right-3 top-3 opacity-30">{{ voucherDiscountType === 'percentage' ? '%' : currencySymbol }}</span>
              </div>
            </div>
            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                t('sale.deliveryFee') }}</label>
              <div class="relative">
                <input v-model.number="saleForm.DeliveryFee" type="number"
                  class="w-full p-3 bg-blue-50 border border-blue-100 rounded-lg font-normal outline-none text-blue-600"
                  placeholder="0">
                <span class="absolute right-3 top-3 opacity-30">{{ currencySymbol }}</span>
              </div>
            </div>
          </div>

          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">Voucher Note</label>
            <textarea v-model="saleForm.Note" rows="2" placeholder="Optional note for this voucher..."
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm resize-none"></textarea>
          </div>

          <div class="pt-4 border-t border-slate-100">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1 mb-3 block">{{
              t('sale.paymentBreakdown') }}</label>
            <div class="space-y-3">
              <div v-for="(p, index) in paymentDetails" :key="index"
                class="flex gap-2 items-center p-2 bg-slate-50 rounded-lg border border-slate-200/50">
                <select v-model="p.method"
                  class="w-1/3 p-2 bg-white border border-slate-200 rounded font-normal text-xs outline-none">
                  <option v-for="m in availableMethods" :key="m" :value="m">{{ m }}</option>
                </select>
                <input v-model.number="p.amount" @input="markPaymentAmountAsManual" @blur="clampPaymentAmount(p)"
                  type="number" min="0" inputmode="decimal"
                  class="w-full p-2 border border-slate-200 rounded text-right font-normal text-slate-800 outline-none tabular-nums focus:border-blue-400">
                <button
                  v-if="paymentDetails.length > 1"
                  type="button"
                  @click="removePaymentMethod(index)"
                  class="text-rose-400 hover:text-rose-600 px-2 transition-colors"
                  :title="t('common.remove') || 'Remove'">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                  </svg>
                </button>
              </div>
              <button @click="addPaymentMethod"
                class="text-[10px] font-normal text-blue-600 uppercase tracking-widest hover:text-blue-800 transition-colors ml-1">
                {{ t('sale.addPaymentMethod') }}
              </button>
            </div>
          </div>

          <div class="p-4 bg-slate-900 rounded-xl text-white flex justify-between items-center shadow-lg">
            <span class="text-[10px] font-normal uppercase tracking-widest opacity-70">{{ t('sale.netAmountDue')
            }}</span>
            <span class="text-xl font-normal tabular-nums">{{ formatNumber(totalAmount) }} <small
                class="text-[10px] opacity-60">{{ currencySymbol }}</small></span>
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="p-3 bg-slate-50 border border-slate-200 rounded-lg">
              <label class="text-[9px] font-normal text-slate-400 uppercase tracking-wider block mb-1">{{
                t('sale.totalPaid') }}</label>
              <div class="text-lg font-normal text-emerald-600 tabular-nums">{{ formatNumber(totalPaid) }}</div>
            </div>
            <div class="p-3 border rounded-lg"
              :class="balanceAmount >= 0 ? 'bg-blue-50 border-blue-100' : 'bg-rose-50 border-rose-100'">
              <label class="text-[9px] font-normal uppercase tracking-wider block mb-1"
                :class="balanceAmount >= 0 ? 'text-blue-400' : 'text-rose-400'">
                {{ balanceAmount >= 0 ? t('sale.change') : t('sale.dueBalance') }}
              </label>
              <div class="text-lg font-normal tabular-nums"
                :class="balanceAmount >= 0 ? 'text-blue-600' : 'text-rose-600'">
                {{ formatNumber(Math.abs(balanceAmount)) }}
              </div>
            </div>
          </div>

          <div class="grid grid-cols-1 gap-4 border-t border-slate-100 pt-4">
            <div>
              <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest block mb-2 ml-1">{{
                t('sale.saleTypeDetection') }}</label>
              <div
                :class="['p-3 rounded-lg font-normal text-center border-2 text-xs transition-all duration-300',
                  totalPaid < totalAmount ? 'border-orange-200 bg-orange-50 text-orange-700' : 'border-emerald-200 bg-emerald-50 text-emerald-700']">
                {{ totalPaid < totalAmount ? t('sale.creditSale') : t('sale.normalSale') }} </div>
              </div>

              <div>
                <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest block mb-2 ml-1">{{
                  t('sale.receiptPrintSize') }}</label>
                <select v-model="receiptSize"
                  class="w-full p-3 border border-slate-200 rounded-lg font-normal bg-slate-50 outline-none focus:border-blue-400 text-xs">
                  <option value="80mm">{{ t('sale.thermalPrinter') }}</option>
                  <option value="A4">{{ t('sale.standardOffice') }}</option>
                  <option value="A5">{{ t('sale.smallPage') }}</option>
                </select>
              </div>
              <label class="flex items-center gap-2.5 p-3 bg-slate-50 border border-slate-200 rounded-lg cursor-pointer select-none">
                <input v-model="showPreviousCredit" type="checkbox"
                  class="h-4 w-4 rounded border-slate-300 text-blue-600 focus:ring-blue-500">
                <span class="text-[10px] font-normal uppercase tracking-wider text-slate-600">{{ t('sale.showPreviousCredit') }}</span>
              </label>
            </div>
          </div>

          <div class="payment-modal-actions p-4 sm:p-5 border-t border-slate-100 bg-slate-50 space-y-3">
            <div class="grid grid-cols-2 gap-3">
            <button @click="confirmPreorder" :disabled="isProcessing || !canPreorder"
              class="py-3 sm:py-3.5 bg-amber-600 text-white rounded-xl font-normal uppercase text-[9px] sm:text-[10px] tracking-widest hover:bg-amber-700 shadow-xl shadow-amber-100 transition-all active:scale-95 disabled:bg-slate-400 disabled:cursor-not-allowed">
              <span v-if="isProcessing">{{ t('sale.processing') || 'Processing...' }}</span>
              <span v-else>{{ t('sale.preorder') || 'Pre-order' }}</span>
            </button>
            <button @click="holdCurrentSale" :disabled="isProcessing || cart.length === 0"
              class="py-3 border border-blue-200 bg-white text-blue-600 rounded-xl font-normal uppercase text-[9px] sm:text-[10px] tracking-widest hover:bg-blue-50 transition-all disabled:text-slate-300 disabled:border-slate-200 disabled:cursor-not-allowed">
              {{ t('sale.holdCurrent') }}
            </button>
            </div>
            <div class="grid grid-cols-[1fr_2fr] gap-3">
            <button @click="showPaymentModal = false"
              class="py-3 sm:py-3.5 border border-slate-300 bg-white text-slate-500 rounded-xl font-normal uppercase text-[9px] sm:text-[10px] tracking-widest hover:bg-gray-50 transition-all">
              {{ t('common.cancel') }}
            </button>
            <button @click="confirmSale" :disabled="isProcessing"
              class="py-3 sm:py-3.5 bg-slate-900 text-white rounded-xl font-normal uppercase text-[9px] sm:text-[10px] tracking-widest hover:bg-black shadow-xl shadow-slate-200 transition-all active:scale-95 disabled:bg-slate-400 disabled:cursor-not-allowed">
              <span v-if="isProcessing">{{ t('sale.processing') || 'Processing...' }}</span>
              <span v-else>{{ t('sale.confirmAndPrint') }}</span>
            </button>
            </div>
          </div>
        </div>
      </div>

      <div id="print-area" style="display: none;">
        <div
          :style="{ width: '76mm', padding: '5px', fontFamily: 'Pyidaungsu, Segoe UI, sans-serif', fontSize: receiptFont.body + 'px', color: 'black', lineHeight: '1.4' }">
          <div style="text-align: center; margin-bottom: 5px;">
            <h2 :style="{ fontSize: receiptFont.h2 + 'px', margin: 0, textTransform: 'uppercase', fontWeight: 'bold' }">{{ shopInfo.ShopName
            }}</h2>
            <p style="margin: 2px 0; font-weight: bold;">({{ receiptBranchName }})</p>
            <p v-if="receiptContact.Address" :style="{ margin: '2px 0', fontSize: receiptFont.small + 'px', whiteSpace: 'pre-line' }">{{ receiptContact.Address }}</p>
            <p v-if="receiptContact.Phone" :style="{ margin: '2px 0', fontSize: receiptFont.small + 'px' }">Tel: {{ receiptContact.Phone }}</p>
          </div>

          <div style="border-bottom: 1px dashed black; margin: 5px 0;"></div>

          <div style="display: flex; justify-content: space-between;">
            <span>No: {{ lastSaleNo }}</span>
            <span>{{ lastSaleDate }}</span>
          </div>
          <div style="display: flex; justify-content: space-between; align-items: flex-start; gap: 8px;">
            <span :style="{ flex: '1 1 auto', minWidth: 0, whiteSpace: 'normal', wordBreak: 'break-word', lineHeight: '1.25' }">
              Cus: {{ selectedCustomerName || 'Walk-in' }}
            </span>
            <span :style="{ flex: '0 0 auto', textAlign: 'right', whiteSpace: 'nowrap' }">Staff: {{ cashierName }}</span>
          </div>
          <div v-if="selectedCustomerObj && (selectedCustomerObj.phone || selectedCustomerObj.address)" :style="{ fontSize: receiptFont.tiny + 'px', color: '#444', marginBottom: '5px' }">
            {{ selectedCustomerObj.phone || '' }} {{ selectedCustomerObj.phone && selectedCustomerObj.address ? '|' : '' }} {{ selectedCustomerObj.address || '' }}
          </div>

          <div style="border-bottom: 1px dashed black; margin: 6px 0 0 0;"></div>

          <table :style="{ width: '100%', borderCollapse: 'collapse', fontSize: receiptFont.small + 'px', lineHeight: '1.25', marginTop: 0 }">
            <thead>
              <tr style="border-bottom: 1px solid black; background: white;">
                <th style="padding: 0; vertical-align: middle; color: #111; -webkit-text-fill-color: #111; opacity: 1;">
                  <div :style="{ height: '34px', display: 'flex', alignItems: 'center', justifyContent: 'flex-start', transform: 'translateY(-8px)', fontSize: Math.max(receiptFont.small, 11) + 'px', lineHeight: '1', whiteSpace: 'nowrap', fontWeight: '700' }">NAME</div>
                </th>
                <th style="padding: 0; vertical-align: middle; color: #111; -webkit-text-fill-color: #111; opacity: 1;">
                  <div :style="{ height: '34px', display: 'flex', alignItems: 'center', justifyContent: 'center', transform: 'translateY(-8px)', fontSize: Math.max(receiptFont.small, 11) + 'px', lineHeight: '1', whiteSpace: 'nowrap', fontWeight: '700' }">QTY</div>
                </th>
                <th style="padding: 0; vertical-align: middle; color: #111; -webkit-text-fill-color: #111; opacity: 1;">
                  <div :style="{ height: '34px', display: 'flex', alignItems: 'center', justifyContent: 'flex-end', transform: 'translateY(-8px)', fontSize: Math.max(receiptFont.small, 11) + 'px', lineHeight: '1', whiteSpace: 'nowrap', fontWeight: '700' }">AMOUNT</div>
                </th>
              </tr>
            </thead>
            <tbody>
                <tr v-for="(item, idx) in cart" :key="item.CartLineId || `${item.ItemType || 'Item'}-${item.ProductId || item.ServiceId || item.BundleId || item.Name || 'line'}-${idx}`">
                <td style="padding: 2px 0 4px 0; vertical-align: top; line-height: 1.18;">
                  <div style="font-weight: bold; line-height: 1.18;">{{ item.Name }}</div>
                  <div :style="{ fontSize: receiptFont.tiny + 'px', color: '#444' }">({{ getUnitName(item.UnitId) }}) @ {{
                    formatNumber(item.SelectedPrice) }}</div>
                  <div v-if="item.DiscountAmount > 0 && !item.isFOC" :style="{ fontSize: receiptFont.tiny + 'px', color: '#dc2626', marginTop: '1px' }">
                    Discount: -{{ formatNumber(item.DiscountAmount) }}
                  </div>
                  <div v-if="item.Note" :style="{ fontSize: receiptFont.tiny + 'px', color: '#555', fontStyle: 'italic', marginTop: '2px' }">
                    * {{ item.Note }}
                  </div>
                </td>
                <td style="text-align: center; vertical-align: top; padding: 2px 0 4px 0; line-height: 1.18;">{{ item.Quantity }}</td>
                <td style="text-align: right; vertical-align: top; padding: 2px 0 4px 0; line-height: 1.18;">{{ item.isFOC ? '0' :
                  formatNumber((item.Quantity * item.SelectedPrice) - (item.DiscountAmount || 0)) }}</td>
              </tr>
            </tbody>

            <tfoot :style="{ fontSize: receiptFont.small + 'px', lineHeight: '1.25' }">
              <tr>
                <td colspan="3" style="border-top: 1px solid black; height: 12px; padding: 0;"></td>
              </tr>
              <tr>
                <td></td>
                <td style="text-align: center; padding: 0 0 8px 0; font-weight: bold; border-bottom: 1px solid black;">{{
                  totalQty }}</td>
                <td></td>
              </tr>

              <tr>
                <td colspan="2" style="text-align: left; padding-top: 6px;">Sub-Total:</td>
                <td style="text-align: right; padding-top: 6px;">{{formatNumber(cart.reduce((sum, i) => sum +
                  (i.Quantity * i.SelectedPrice), 0))}}</td>
              </tr>

              <tr v-if="saleForm.DiscountAmount > 0">
                <td colspan="2" style="text-align: left; color: #444;">{{ currentVoucherDiscountLabel }}</td>
                <td style="text-align: right; color: #444;">{{ formatNumber(saleForm.DiscountAmount) }}</td>
              </tr>

              <tr v-if="saleForm.DeliveryFee > 0">
                <td colspan="2" style="text-align: left;">Delivery (+):</td>
                <td style="text-align: right;">{{ formatNumber(saleForm.DeliveryFee) }}</td>
              </tr>

              <tr v-if="saleForm.Note && saleForm.Note.trim() !== ''">
                <td colspan="3" style="text-align: left; padding-top: 5px; color: #444; font-style: italic;">
                  Note: {{ saleForm.Note }}
                </td>
              </tr>

              <tr>
                <td colspan="2" :style="{ textAlign: 'left', paddingTop: '5px', fontWeight: 'bold', fontSize: receiptFont.total + 'px' }">TOTAL:
                </td>
                <td :style="{ textAlign: 'right', paddingTop: '5px', fontWeight: 'bold', fontSize: receiptFont.total + 'px' }">{{
                  formatNumber(totalAmount) }}</td>
              </tr>

              <tr v-for="p in receiptData.payments" :key="p.method">
                <td colspan="2" style="text-align: left; padding-top: 5px; font-weight: bold;">{{ receiptPaidLabel(p.method) }}
                </td>
                <td style="text-align: right; padding-top: 5px; font-weight: bold;">{{ formatNumber(p.amount) }}</td>
              </tr>

              <tr v-if="balanceAmount !== 0">
                <td colspan="2" style="text-align: left; padding-top: 5px; font-weight: bold;">{{ receiptBalanceLabel(balanceAmount) }}</td>
                <td style="text-align: right; padding-top: 5px; font-weight: bold;">{{
                  formatNumber(Math.abs(balanceAmount)) }}</td>
              </tr>
              <tr v-if="showPreviousCredit && receiptTotalCredit > 0">
                <td colspan="2" style="text-align: left; padding-top: 5px; font-weight: bold;">{{ t('sale.totalCredit') }}:</td>
                <td style="text-align: right; padding-top: 5px; font-weight: bold; color: #dc2626;">{{ formatNumber(receiptTotalCredit) }}</td>
              </tr>
            </tfoot>
          </table>

          <div style="text-align: center; margin-top: 25px; border-top: 1px dashed black; padding-top: 10px;">
            <p v-for="(line, idx) in receiptFooterLines" :key="idx" :style="{ fontSize: receiptFont.footer + 'px', fontWeight: 'bold', margin: '2px 0' }">{{ line }}</p>
          </div>
        </div>
      </div>

      <div id="print-area-a4" style="display: none;">
        <div :style="{
          width: receiptSize === 'A4' ? '210mm' : '148mm',
          minHeight: 'auto',
          padding: receiptSize === 'A5' ? '4mm' : '10mm',
          backgroundColor: 'white',
          fontFamily: 'Pyidaungsu, sans-serif',
          color: 'black'
        }">

          <div style="text-align: center; margin-bottom: 10px;">
            <h1 :style="{ margin: 0, fontSize: (receiptSize === 'A5' ? receiptFont.h2 : receiptFont.h1) + 'px', fontWeight: 'bold' }">{{
              shopInfo.ShopName }}</h1>
            <p :style="{ margin: '2px 0', fontSize: receiptFont.small + 'px' }">{{ receiptBranchName }}<span v-if="receiptContact.Address"> | {{ receiptContact.Address }}</span></p>
            <p v-if="receiptContact.Phone" :style="{ margin: '2px 0', fontSize: receiptFont.small + 'px' }">Tel: {{ receiptContact.Phone }}</p>
          </div>

          <div :style="{ display: 'flex', justifyContent: 'space-between', fontSize: receiptFont.small + 'px', marginBottom: '5px' }">
            <span>Voucher: {{ lastSaleNo }}</span>
            <span>{{ lastSaleDate }}</span>
          </div>
          <div :style="{ display: 'flex', justifyContent: 'space-between', fontSize: receiptFont.small + 'px', marginBottom: '10px' }">
            <span>Customer: {{ selectedCustomerName || 'Walk-in' }}
              <br v-if="selectedCustomerObj && (selectedCustomerObj.phone || selectedCustomerObj.address)">
              <span v-if="selectedCustomerObj && (selectedCustomerObj.phone || selectedCustomerObj.address)" :style="{ fontSize: receiptFont.tiny + 'px', color: '#555' }">
                {{ selectedCustomerObj.phone || '' }} {{ selectedCustomerObj.phone && selectedCustomerObj.address ? '|' : '' }} {{ selectedCustomerObj.address || '' }}
              </span>
            </span>
            <span>Staff: {{ cashierName }}</span>
          </div>

          <table style="width: 100%; border-collapse: collapse; table-layout: fixed; border: 1px solid #333;">
            <thead>
              <tr
                style="background-color: #71717a !important; -webkit-print-color-adjust: exact; print-color-adjust: exact;">
                <th
                  :style="{ width: '35px', padding: '6px', border: '1px solid #333', color: 'white !important', fontSize: receiptFont.small + 'px', textAlign: 'center' }">
                  No</th>
                <th
                  :style="{ padding: '6px', border: '1px solid #333', color: 'white !important', fontSize: receiptFont.small + 'px', textAlign: 'left' }">
                  Item Name</th>
                <th
                  :style="{ width: '45px', padding: '6px', border: '1px solid #333', color: 'white !important', fontSize: receiptFont.small + 'px', textAlign: 'center' }">
                  Qty</th>
                <th
                  :style="{ width: '45px', padding: '6px', border: '1px solid #333', color: 'white !important', fontSize: receiptFont.small + 'px', textAlign: 'center' }">
                  Unit</th>
                <th
                  :style="{ width: '85px', padding: '6px', border: '1px solid #333', color: 'white !important', fontSize: receiptFont.small + 'px', textAlign: 'right' }">
                  Price</th>
                <th
                  :style="{ width: '85px', padding: '6px', border: '1px solid #333', color: 'white !important', fontSize: receiptFont.small + 'px', textAlign: 'right' }">
                  Total</th>
              </tr>
            </thead>
            <tbody :style="{ fontSize: receiptFont.small + 'px' }">
                <tr v-for="(item, idx) in cart" :key="item.CartLineId || `${item.ItemType || 'Item'}-${item.ProductId || item.ServiceId || item.BundleId || item.Name || 'line'}-${idx}`">
                <td style="padding: 6px; border: 1px solid #ccc; text-align: center;">{{ idx + 1 }}</td>
                <td style="padding: 6px; border: 1px solid #ccc; font-weight: bold;">
                  {{ item.Name }} {{ item.isFOC ? '(FOC)' : '' }}
                  <div v-if="item.DiscountAmount > 0 && !item.isFOC" :style="{ fontWeight: 'normal', fontSize: receiptFont.tiny + 'px', color: '#dc2626', marginTop: '2px' }">
                    Discount: -{{ formatNumber(item.DiscountAmount) }}
                  </div>
                  <div v-if="item.Note" :style="{ fontWeight: 'normal', fontSize: receiptFont.tiny + 'px', color: '#555', fontStyle: 'italic', marginTop: '2px' }">
                    * {{ item.Note }}
                  </div>
                </td>
                <td style="padding: 6px; border: 1px solid #ccc; text-align: center;">{{ item.Quantity }}</td>
                <td style="padding: 6px; border: 1px solid #ccc; text-align: center;">{{ getUnitName(item.UnitId) }}
                </td>
                <td style="padding: 6px; border: 1px solid #ccc; text-align: right;">{{ item.isFOC ? '0' :
                  formatNumber(item.SelectedPrice) }}</td>
                <td style="padding: 6px; border: 1px solid #ccc; text-align: right;">{{ item.isFOC ? '0' :
                  formatNumber((item.Quantity * item.SelectedPrice) - (item.DiscountAmount || 0)) }}</td>
              </tr>

              <tr v-for="i in calculateEmptyRows()" :key="'empty-' + i">
                <td v-for="n in 6" :key="n" style="padding: 8px; border: 1px solid #eee;"></td>
              </tr>

              <tr>
                <td colspan="5" style="padding: 6px; border: 1px solid #ccc; text-align: right;">
                  {{ t('sale.subtotal') }}:</td>
                <td style="padding: 6px; border: 1px solid #ccc; text-align: right; font-weight: bold;">
                  {{ formatNumber(receiptSubtotal) }}
                </td>
              </tr>
              <tr v-if="invoicePreviewData.discount > 0">
                <td colspan="5" style="padding: 6px; border: 1px solid #ccc; text-align: right; color: #ea580c;">
                  {{ receiptVoucherDiscountLabel }}</td>
                <td style="padding: 6px; border: 1px solid #ccc; text-align: right; color: #ea580c;">{{
                  formatNumber(invoicePreviewData.discount) }}</td>
              </tr>
              <tr v-if="invoicePreviewData.deliveryFee > 0">
                <td colspan="5" style="padding: 6px; border: 1px solid #ccc; text-align: right; color: #2563eb;">
                  Delivery (+):</td>
                <td style="padding: 6px; border: 1px solid #ccc; text-align: right; color: #2563eb;">{{
                  formatNumber(invoicePreviewData.deliveryFee) }}</td>
              </tr>
              <tr v-if="saleForm.Note && saleForm.Note.trim() !== ''">
                <td colspan="6" style="padding: 6px; border: 1px solid #ccc; text-align: left; color: #475569; font-style: italic;">
                  Note: {{ saleForm.Note }}
                </td>
              </tr>
              <tr style="font-weight: bold; background-color: #f8fafc;">
                <td colspan="5" style="padding: 8px; border: 1px solid #ccc; text-align: right;">
                  {{ t('sale.netAmount') }}:</td>
                <td style="padding: 8px; border: 1px solid #ccc; text-align: right;">{{ formatNumber(receiptTotal) }}
                </td>
              </tr>
              <tr v-for="p in receiptData.payments" :key="p.method">
                <td colspan="5" style="padding: 6px; border: 1px solid #ccc; text-align: right;">
                  {{ receiptPaidLabel(p.method) }}</td>
                <td style="padding: 6px; border: 1px solid #ccc; text-align: right;">{{ formatNumber(p.amount) }}</td>
              </tr>
              <tr v-if="balanceAmount !== 0">
                <td colspan="5" style="padding: 6px; border: 1px solid #ccc; text-align: right; font-weight: bold;">
                  {{ receiptBalanceLabel(balanceAmount) }}</td>
                <td style="padding: 6px; border: 1px solid #ccc; text-align: right; font-weight: bold;">{{
                  formatNumber(Math.abs(balanceAmount)) }}</td>
              </tr>
              <tr v-if="showPreviousCredit && receiptTotalCredit > 0">
                <td colspan="5" style="padding: 6px; border: 1px solid #ccc; text-align: right; font-weight: bold;">
                  {{ t('sale.totalCredit') }}:</td>
                <td style="padding: 6px; border: 1px solid #ccc; text-align: right; font-weight: bold; color: #dc2626;">
                  {{ formatNumber(receiptTotalCredit) }}</td>
              </tr>
            </tbody>
          </table>

          <div :style="{ fontSize: receiptFont.small + 'px', fontWeight: 'bold', marginTop: '10px' }">Total Qty: {{ totalQty }}</div>
          <div style="text-align: center; margin-top: 20px; border-top: 1px dashed #ccc; padding-top: 10px;">
            <p v-for="(line, idx) in receiptFooterLines" :key="idx" :style="{ fontWeight: 'bold', fontSize: receiptFont.footer + 'px', margin: '2px 0' }">{{ line }}</p>
          </div>
        </div>
      </div>

      <!-- Quick Sale Modal -->
      <div v-if="showQuickSaleModal"
        class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
        <div
          class="bg-white rounded-3xl max-w-xl w-full shadow-2xl overflow-hidden border border-slate-200 animate-in zoom-in duration-200">
          
          <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
            <h3 class="text-lg font-normal uppercase tracking-tight">{{ t('sale.quickSale') || 'Quick Sale' }}</h3>
            <button @click="showQuickSaleModal = false" class="text-white/60 hover:text-white transition-colors">
              <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>

          <div class="p-8 space-y-6 max-h-[80vh] overflow-y-auto custom-scrollbar">
            
            <div class="space-y-1.5 relative">
              <label class="text-[10px] font-bold text-slate-400 uppercase tracking-widest ml-1">{{ t('sale.supplier')
                || 'Supplier' }} (Optional)</label>
              <div class="relative group">
                <span class="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400">
                  <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
                  </svg>
                </span>
                <input v-model="supplierSearch" @input="handleSupplierSearch"
                  @focus="showSupplierResults = supplierSearch.length > 0"
                  @keydown.down.prevent="moveSearchDown(filteredSuppliers)"
                  @keydown.up.prevent="moveSearchUp(filteredSuppliers)"
                  @keydown.enter.prevent="selectHighlightedProductFromSearch(filteredSuppliers, 'supplier')"
                  type="text"
                  class="w-full pl-9 pr-9 py-3 bg-slate-50 border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-blue-500/20 font-medium transition-all"
                  placeholder="Search or type new supplier...">
                <button v-if="supplierSearch" @click="clearSearch('supplier')"
                  class="absolute right-3 top-1/2 -translate-y-1/2 text-slate-300 hover:text-slate-500">✕</button>
              </div>

              <div v-if="showSupplierResults && filteredSuppliers.length > 0"
                class="absolute z-[110] left-0 right-0 mt-1 bg-white border border-slate-200 rounded-2xl shadow-2xl max-h-48 overflow-y-auto animate-in fade-in slide-in-from-top-2 duration-200 custom-scrollbar">
                <div v-for="(s, idx) in filteredSuppliers" :key="s.supplierId" @click="selectSupplier(s)"
                  :class="['p-3 flex items-center justify-between cursor-pointer border-b border-slate-50 last:border-0 hover:bg-blue-50 transition-colors', idx === highlightedIndex ? 'bg-blue-50' : '']">
                  <div>
                    <div class="text-sm font-medium text-slate-800">{{ s.supplierName }}</div>
                    <div class="text-[10px] text-slate-400">{{ s.phone || 'No phone' }}</div>
                  </div>
                </div>
              </div>
            </div>

            <div class="grid grid-cols-2 gap-4">
              <div class="space-y-1.5">
                <label class="text-[10px] font-bold text-slate-400 uppercase tracking-widest ml-1">{{ t('sale.itemName')
                  || 'Item Name' }}</label>
                <input v-model="quickSaleForm.Name" type="text"
                  class="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-blue-500/20 text-sm font-medium">
              </div>
              <div class="space-y-1.5">
                <label class="text-[10px] font-bold text-slate-400 uppercase tracking-widest ml-1">{{ t('sale.sku') ||
                  'SKU' }} (Optional)</label>
                <input v-model="quickSaleForm.Sku" type="text"
                  class="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-blue-500/20 text-sm font-medium">
              </div>
            </div>

            <div class="grid grid-cols-2 gap-4">
              <div class="space-y-1.5 relative">
                <label class="text-[10px] font-bold text-slate-400 uppercase tracking-widest ml-1">{{ t('sale.category')
                  || 'Category' }}</label>
                <div class="relative group">
                  <span class="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400">
                    <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
                    </svg>
                  </span>
                  <input v-model="categorySearch" @input="handleCategorySearch"
                    @focus="showCategoryResults = categorySearch.length > 0"
                    @keydown.down.prevent="moveSearchDown(filteredCategoriesForSearch)"
                    @keydown.up.prevent="moveSearchUp(filteredCategoriesForSearch)"
                    @keydown.enter.prevent="selectHighlightedProductFromSearch(filteredCategoriesForSearch, 'category')"
                    type="text"
                    class="w-full pl-9 pr-9 py-3 bg-slate-50 border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-blue-500/20 font-medium transition-all"
                    placeholder="Search or type new category...">
                  <button v-if="categorySearch" @click="clearSearch('category')"
                    class="absolute right-3 top-1/2 -translate-y-1/2 text-slate-300 hover:text-slate-500">✕</button>
                </div>
                <div v-if="showCategoryResults && filteredCategoriesForSearch.length > 0"
                  class="absolute z-[110] left-0 right-0 mt-1 bg-white border border-slate-200 rounded-2xl shadow-2xl max-h-48 overflow-y-auto animate-in fade-in slide-in-from-top-2 duration-200 custom-scrollbar">
                  <div v-for="(c, idx) in filteredCategoriesForSearch" :key="c.categoryId"
                    @click="selectCategoryFromSearch(c)"
                    :class="['p-3 flex items-center justify-between cursor-pointer border-b border-slate-50 last:border-0 hover:bg-blue-50 transition-colors', idx === highlightedIndex ? 'bg-blue-50' : '']">
                    <div class="text-sm font-medium text-slate-800">{{ c.categoryName }}</div>
                  </div>
                </div>
              </div>

              <div class="space-y-1.5 relative">
                <label class="text-[10px] font-bold text-slate-400 uppercase tracking-widest ml-1">{{ t('sale.unit') ||
                  'Unit' }}</label>
                <div class="relative group">
                  <span class="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400">
                    <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
                    </svg>
                  </span>
                  <input v-model="unitSearch" @input="handleUnitSearch" @focus="showUnitResults = unitSearch.length > 0"
                    @keydown.down.prevent="moveSearchDown(filteredUnitsForSearch)"
                    @keydown.up.prevent="moveSearchUp(filteredUnitsForSearch)"
                    @keydown.enter.prevent="selectHighlightedProductFromSearch(filteredUnitsForSearch, 'unit')"
                    type="text"
                    class="w-full pl-9 pr-9 py-3 bg-slate-50 border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-blue-500/20 font-medium transition-all"
                    placeholder="Search or type new unit...">
                  <button v-if="unitSearch" @click="clearSearch('unit')"
                    class="absolute right-3 top-1/2 -translate-y-1/2 text-slate-300 hover:text-slate-500">✕</button>
                </div>
                <div v-if="showUnitResults && filteredUnitsForSearch.length > 0"
                  class="absolute z-[110] left-0 right-0 mt-1 bg-white border border-slate-200 rounded-2xl shadow-2xl max-h-48 overflow-y-auto animate-in fade-in slide-in-from-top-2 duration-200 custom-scrollbar">
                  <div v-for="(u, idx) in filteredUnitsForSearch" :key="u.unitId" @click="selectUnitFromSearch(u)"
                    :class="['p-3 flex items-center justify-between cursor-pointer border-b border-slate-50 last:border-0 hover:bg-blue-50 transition-colors', idx === highlightedIndex ? 'bg-blue-50' : '']">
                    <div class="text-sm font-medium text-slate-800">{{ u.unitName }}</div>
                  </div>
                </div>
              </div>
            </div>

            <div class="grid grid-cols-3 gap-4">
              <div class="space-y-1.5">
                <label class="text-[10px] font-bold text-slate-400 uppercase tracking-widest ml-1">{{
                  t('sale.costPrice') || 'Cost Price' }}</label>
                <div class="relative">
                  <input v-model.number="quickSaleForm.CostPrice" type="number"
                    class="w-full p-3 bg-rose-50 border border-rose-100 rounded-xl outline-none text-rose-600 font-bold text-sm">
                  <span class="absolute right-3 top-3 opacity-30">{{ currencySymbol }}</span>
                </div>
              </div>
              <div class="space-y-1.5">
                <label class="text-[10px] font-bold text-slate-400 uppercase tracking-widest ml-1">{{
                  t('sale.salePrice') || 'Sale Price' }}</label>
                <div class="relative">
                  <input v-model.number="quickSaleForm.SalePrice" type="number"
                    class="w-full p-3 bg-emerald-50 border border-emerald-100 rounded-xl outline-none text-emerald-600 font-bold text-sm">
                  <span class="absolute right-3 top-3 opacity-30">{{ currencySymbol }}</span>
                </div>
              </div>
              <div class="space-y-1.5">
                <label class="text-[10px] font-bold text-slate-400 uppercase tracking-widest ml-1">{{
                  t('sale.initialQty') || 'Initial Qty' }}</label>
                <input v-model.number="quickSaleForm.Quantity" type="number"
                  class="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl outline-none text-center font-bold text-sm">
              </div>
            </div>
          </div>

          <div class="p-6 bg-slate-50 border-t flex gap-3">
            <button @click="showQuickSaleModal = false"
              class="flex-1 py-4 bg-white border border-slate-300 text-slate-500 rounded-2xl font-bold uppercase tracking-widest text-[11px] hover:bg-slate-100 transition-all">
              {{ t('common.cancel') }}
            </button>
            <button @click="handleQuickSaleSubmit" :disabled="!quickSaleForm.Name || quickSaleForm.SalePrice <= 0"
              class="flex-[2] py-4 bg-blue-600 text-white rounded-2xl font-bold uppercase tracking-widest text-[11px] hover:bg-blue-700 disabled:bg-slate-300 shadow-xl shadow-blue-200 transition-all active:scale-[0.98]">
              {{ t('sale.addToSaleAndRegister') || 'Add & Register' }}
            </button>
          </div>
        </div>
      </div>

  </div>
</template>

<style scoped>
/* Chrome, Safari, Edge, Opera */
input::-webkit-outer-spin-button,
input::-webkit-inner-spin-button {
  -webkit-appearance: none;
  margin: 0;
}

.no-scrollbar::-webkit-scrollbar {
  display: none;
}

.no-scrollbar {
  -ms-overflow-style: none;
  scrollbar-width: none;
}

.custom-scrollbar::-webkit-scrollbar {
  width: 4px;
}

.custom-scrollbar::-webkit-scrollbar-thumb {
  background: #e5e7eb;
  border-radius: 10px;
}

.payment-modal-overlay {
  padding-top: max(1rem, env(safe-area-inset-top));
  padding-bottom: max(1rem, env(safe-area-inset-bottom));
}

.payment-modal-card {
  max-height: calc(100dvh - max(2rem, env(safe-area-inset-top)) - max(2rem, env(safe-area-inset-bottom)));
}

.payment-modal-body {
  overscroll-behavior: contain;
}

.payment-modal-actions {
  padding-bottom: max(1rem, calc(env(safe-area-inset-bottom) + 0.75rem));
}

@supports not (height: 100dvh) {
  .payment-modal-card {
    max-height: calc(100vh - 4rem);
  }
}

.cart-panel {
  container-type: inline-size;
}

@container (max-width: 380px) {
  .cart-item-fields {
    grid-template-columns: 1fr 1fr;
    align-items: start;
  }

  .cart-item-price {
    grid-column: 1 / -1;
    align-items: stretch;
  }

  .cart-item-qty {
    align-items: flex-end;
  }

  .cart-qty-control button {
    width: 28px;
    height: 28px;
  }

  .cart-qty-control input {
    width: 32px;
    font-size: 12px;
  }
}
</style>

<style>
/* Invoice preview - A4/A5 column headers (non-scoped for v-html content) */
.invoice-preview-wrapper {
  width: min(90mm, 100%);
  max-width: 100%;
  flex-shrink: 1;
  overflow: hidden;
}

.sale-invoice-preview-modal {
  max-height: calc(100dvh - 1rem);
}

.invoice-preview-scroll {
  scrollbar-gutter: stable both-edges;
}

.invoice-preview-actions {
  padding-bottom: max(1rem, calc(1rem + env(safe-area-inset-bottom)));
}

@supports (height: 100dvh) {
  .invoice-preview-scroll {
    max-height: 100dvh;
  }
}

.invoice-preview-wrapper-wide {
  width: max-content;
  min-width: max-content;
  max-width: none;
  flex-shrink: 0;
  overflow: visible;
}

.invoice-preview-content {
  width: 100%;
  max-width: 100%;
  display: flex;
  justify-content: center;
  overflow: hidden;
  font-family: Pyidaungsu, sans-serif;
}

.invoice-preview-content > div {
  max-width: 100%;
}

.invoice-preview-wrapper-wide .invoice-preview-content {
  width: max-content;
  max-width: none;
  justify-content: flex-start;
  overflow: visible;
}

.invoice-preview-wrapper-wide .invoice-preview-content > div {
  max-width: none;
}

.invoice-preview-wrapper #print-area-a4 thead tr {
  background-color: #3f3f46 !important;
}
.invoice-preview-wrapper #print-area-a4 th {
  color: white !important;
}
.invoice-preview-wrapper #print-area thead th {
  color: #111 !important;
  -webkit-text-fill-color: #111 !important;
  opacity: 1 !important;
  text-shadow: none !important;
}
.invoice-preview-wrapper #print-area thead tr {
  background: white !important;
}
.invoice-preview-wrapper > div > div {
  margin-left: auto;
  margin-right: auto;
}
</style>

<script setup>
import { ref, computed, onMounted, onUnmounted, nextTick, watch } from 'vue';
import api from '../services/api';
import { useToast } from '../composables/useToast';
import { useI18n } from '../composables/useI18n';
import { isMobileBrowser, openPrintWindow } from '../utils/print';
import { normalizeReceiptPayments } from '../utils/receiptData';
import androidBridge from '../services/androidBridge';
const { addToast } = useToast();
const { t, locale } = useI18n();

// --- State Variables ---
const products = ref([]);
const services = ref([]);
const bundles = ref([]);
const categories = ref([]);
const customers = ref([]);
const units = ref([]);
const suppliers = ref([]);
const searchQuery = ref('');
const supplierSearch = ref('');
const showSupplierResults = ref(false);
const customerSearch = ref('');
const showCustomerResults = ref(false);
const categorySearch = ref('');
const showCategoryResults = ref(false);
const unitSearch = ref('');
const showUnitResults = ref(false);
const selectedCategory = ref(null);
const showOutOfStockItems = ref(false);
const showAllCategories = ref(false);
const defaultVisibleCategoryCount = 5;
const highlightedIndex = ref(-1);
const cart = ref([]);
const createCartLineId = () =>
  `line-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
const showPaymentModal = ref(false);
const showPreviousCredit = ref(false);
const showHeldSalesModal = ref(false);
const heldSales = ref([]);
const activeHeldSaleId = ref(null);
const showQuickSaleModal = ref(false);
const quickSaleForm = ref({
  Name: '',
  CategoryId: null,
  CategoryName: '',
  UnitId: null,
  UnitName: '',
  CostPrice: 0,
  SalePrice: 0,
  Quantity: 1,
  SupplierId: null,
  SupplierName: '',
  BranchId: 1,
  Sku: ''
});

const getPriceTierLabel = (item) => {
  const selected = parseFloat(item.SelectedPrice);
  const normal = parseFloat(item.CurrentBasePrice);
  if (!Number.isNaN(selected) && !Number.isNaN(normal) && selected === normal) {
    return `${t('sale.normalPrice') || 'Normal'} (${formatNumber(item.CurrentBasePrice)})`;
  }

  const levels = getAvailablePrices(item);
  const match = levels.find(l => parseFloat(l.PriceValue || l.priceValue) === selected);
  if (match) {
    const name = match.PriceName || match.priceName || 'Price';
    const value = match.PriceValue || match.priceValue;
    return `${name} (${formatNumber(value)})`;
  }

  return `${formatNumber(item.SelectedPrice)}`;
};

const selectPriceTier = (item, priceValue, _priceName) => {
  item.SelectedPrice = parseFloat(priceValue) || 0;
  item.showPriceMenu = false;
};

const closeAllPriceMenus = (e) => {
  if (e.target.closest && e.target.closest('[data-price-tier-menu]')) return;
  cart.value.forEach(i => { i.showPriceMenu = false; });
};
const currentPage = ref(1);
const pageSize = ref(20); // တစ်ခါပြရင် အခု ၂၀ စီပြမယ်
const searchInput = ref(null);
const createDefaultSaleForm = () => ({
  CustomerId: null,
  CustomerName: '',
  SaleType: 'Normal',
  SaleDate: new Date().toISOString().split('T')[0], // yyyy-MM-dd
  DiscountAmount: 0,
  DeliveryFee: 0,
  Note: ''
});
const saleForm = ref(createDefaultSaleForm());
const voucherDiscountType = ref('amount');
const voucherDiscountInput = ref(0);
const toBooleanSetting = (value) => value === true || value === 1 || value === '1' || value === 'true' || value === 'True';
const readCachedShopSettings = () => {
  try {
    const settings = JSON.parse(localStorage.getItem('ShopSettings') || '{}');
    return {
      ...settings,
      UseDecimalQuantity: toBooleanSetting(settings.UseDecimalQuantity ?? settings.useDecimalQuantity)
    };
  } catch {
    return {};
  }
};
const hasReceiptSettings = (settings) => !!(
  settings &&
  (settings.ShopName || settings.Name || settings.Address || settings.Phone || settings.LogoData)
);
const applyShopSettings = (settings = {}) => {
  if (!settings || typeof settings !== 'object') return;
  shopInfo.value = {
    ...shopInfo.value,
    ...settings,
    UseDecimalQuantity: toBooleanSetting(settings.UseDecimalQuantity ?? settings.useDecimalQuantity ?? shopInfo.value.UseDecimalQuantity)
  };
  localStorage.setItem('ShopLogo', shopInfo.value.LogoData || '');
  localStorage.setItem('ShopSettings', JSON.stringify(shopInfo.value));
};
const shopInfo = ref(readCachedShopSettings());
const receiptFontSizeMap = { Small: { body: 10, h1: 16, h2: 14, small: 8, tiny: 7, footer: 9, total: 10 }, Medium: { body: 13, h1: 20, h2: 18, small: 11, tiny: 9, footer: 11, total: 12 }, Large: { body: 16, h1: 26, h2: 22, small: 14, tiny: 11, footer: 14, total: 15 } };
const receiptFont = computed(() => receiptFontSizeMap[shopInfo.value.ReceiptFontSize] || receiptFontSizeMap.Medium);
const currencySymbol = computed(() => shopInfo.value.CurrencySymbol || 'Ks');
const useDecimalQuantity = computed(() => toBooleanSetting(shopInfo.value.UseDecimalQuantity ?? shopInfo.value.useDecimalQuantity));
const quantityInputStep = computed(() => useDecimalQuantity.value ? '0.01' : '1');
const quantityInputMin = computed(() => useDecimalQuantity.value ? '0.01' : '1');
const quantityButtonStep = computed(() => useDecimalQuantity.value ? 0.01 : 1);
const lastSaleNo = ref('');
const lastSaleDate = ref('');
const cashierName = ref('');
const normalizeReceiptSize = (value) => ['80mm', 'A4', 'A5'].includes(value) ? value : '80mm';
const preferredReceiptSize = () => normalizeReceiptSize(
  shopInfo.value.PreferredPaperSize ?? shopInfo.value.preferredPaperSize
);
const receiptSize = ref(preferredReceiptSize());
const branchName = ref(localStorage.getItem('BranchName') || 'Main Branch');
const branches = ref([]);
const isExpanded = ref(false);
const isCartOpen = ref(false);
const isProcessing = ref(false);
const isNetworkWaiting = ref(false);
const connectionStatus = ref(navigator.onLine ? 'online' : 'offline');
const connectionNotice = ref('');
let connectionNoticeTimer = null;
const showInvoicePreview = ref(false);
const saveInvoiceAsImage = ref(false);
const invoicePreviewRef = ref(null);
const capturedPrintHtml = ref('');
const capturedReceiptText = ref('');
const invoicePreviewData = ref({
  items: [],
  payments: [],
  balance: 0,
  discount: 0,
  discountType: 'amount',
  discountInput: 0,
  deliveryFee: 0,
  voucherNote: '',
  customerName: '',
  totalCredit: 0,
  branchName: '',
});

const toNonNegativeAmount = (value) => {
  const amount = Number(value);
  return Number.isFinite(amount) ? Math.max(0, amount) : 0;
};

const currentTab = ref('all');
const catalogTabs = computed(() => ([
  { id: 'all', label: t('sale.allItems') },
  { id: 'products', label: t('sale.products') },
  { id: 'services', label: t('sale.services') },
  { id: 'bundles', label: t('sale.bundles') }
]));

const field = (obj, pascal, camel = pascal.charAt(0).toLowerCase() + pascal.slice(1)) => obj?.[pascal] ?? obj?.[camel];
const currentBranchId = computed(() => Number(localStorage.getItem('BranchId') || 1));
const currentBranch = computed(() => branches.value.find(b => Number(field(b, 'BranchId')) === currentBranchId.value) || null);
const receiptBranchName = computed(() => field(currentBranch.value, 'BranchName') || branchName.value || 'Main Branch');
const isMainReceiptBranch = computed(() => {
  const branch = currentBranch.value;
  if (!branch) return currentBranchId.value === 1 || /main/i.test(receiptBranchName.value);
  return Boolean(field(branch, 'IsMainBranch')) || /main/i.test(String(field(branch, 'BranchName') || ''));
});
const receiptContact = computed(() => {
  if (!isMainReceiptBranch.value && currentBranch.value) {
    return {
      Address: field(currentBranch.value, 'Location') || shopInfo.value.Address || '',
      Phone: field(currentBranch.value, 'Phone') || shopInfo.value.Phone || ''
    };
  }
  return {
    Address: shopInfo.value.Address || '',
    Phone: shopInfo.value.Phone || ''
  };
});
const receiptFooterLines = computed(() => {
  const raw = shopInfo.value.ReceiptFooter;
  const footer = String(raw && raw !== 'null' ? raw : (t('common.thanks') || 'Thank you'));
  return footer.split(/\r?\n/).map(line => line.trim()).filter(Boolean);
});

const receiptSubtotal = computed(() =>
  invoicePreviewData.value.items.reduce((sum, i) => {
    if (i.isFOC) return sum;
    return sum + (i.Quantity * (i.SelectedPrice || i.UnitPrice || 0)) - (i.DiscountAmount || 0);
  }, 0)
);
const receiptTotal = computed(() =>
  receiptSubtotal.value - (invoicePreviewData.value.discount || 0) + (invoicePreviewData.value.deliveryFee || 0)
);
const receiptBalance = computed(() => {
  const capturedBalance = Number(invoicePreviewData.value.balance);
  if (Number.isFinite(capturedBalance)) return capturedBalance;

  const paidTotal = invoicePreviewData.value.payments.reduce(
    (sum, payment) => sum + toNonNegativeAmount(payment.amount),
    0
  );
  return paidTotal - receiptTotal.value;
});
const receiptTotalCredit = computed(() => toNonNegativeAmount(invoicePreviewData.value.totalCredit));
const receiptPaidLabel = (method) => `${t('sale.totalPaid')}${method ? ` (${method})` : ''}:`;
const receiptBalanceLabel = (balance) => (
  balance > 0
    ? `${t('sale.change')}:`
    : `${t('sale.dueBalance')}:`
);

const showConnectionNotice = (message, status = 'offline', persist = false) => {
  connectionStatus.value = status;
  connectionNotice.value = message;
  if (connectionNoticeTimer) window.clearTimeout(connectionNoticeTimer);
  if (!persist) {
    connectionNoticeTimer = window.setTimeout(() => {
      connectionNotice.value = '';
      connectionNoticeTimer = null;
    }, 4500);
  }
};

const setOfflineModeNotice = () => {
  showConnectionNotice(
    t('sale.unstableConnection') || 'Connection is unstable. Switching to offline mode.',
    'offline',
    true
  );
};

const setOnlineModeNotice = () => {
  showConnectionNotice(
    t('sale.connectionRestored') || 'Connection restored. Online mode is available.',
    'online'
  );
};

const apiGetWithConnectionFeedback = async (url, options = {}) => {
  let slowTimer = window.setTimeout(() => {
    isNetworkWaiting.value = true;
    showConnectionNotice(
      t('sale.waitingForConnection') || 'Waiting for connection...',
      'online',
      true
    );
  }, options.slowAfterMs ?? 700);

  try {
    return await api.get(url, { ...(options.config || {}), timeout: options.timeout ?? 5000 });
  } catch (err) {
    if (isNetworkSaleFailure(err)) setOfflineModeNotice();
    throw err;
  } finally {
    window.clearTimeout(slowTimer);
    isNetworkWaiting.value = false;
  }
};


// --- Logic ---
const handleSearch = () => {
  if (searchQuery.value) currentTab.value = 'all';
  currentPage.value = 1;
};

const handleBarcodeScan = (e) => {
  if (!searchQuery.value) return;

  const q = searchQuery.value.toLowerCase().trim();
  // Find exact SKU match or exact Name match
  const match = allItems.value.find(i =>
    (i.Sku && i.Sku.toLowerCase() === q) ||
    (i.Name && i.Name.toLowerCase() === q)
  );

  if (match) {
    e.preventDefault();
    handleItemSelect(match);
    searchQuery.value = ''; // Clear search for next scan
    addToast(`Added ${match.Name}`, "success");
  }
};

const normalizeProductStockFields = (product) => {
  const stockQty = Number(product.StockQuantity ?? product.stockQuantity ?? 0);
  const consignmentQty = Number(product.ConsignmentQuantity ?? product.consignmentQuantity ?? 0);
  const ownedQty = Number(product.OwnedQuantity ?? product.ownedQuantity ?? Math.max(0, stockQty - consignmentQty));

  return {
    ...product,
    StockQuantity: stockQty,
    stockQuantity: stockQty,
    ConsignmentQuantity: consignmentQty,
    consignmentQuantity: consignmentQty,
    OwnedQuantity: ownedQty,
    ownedQuantity: ownedQty
  };
};

// အမျိုးအစားအားလုံးကို တစ်နေရာတည်း စုစည်းခြင်း
const allItems = computed(() => {
  const qs = [{ Name: 'Quick Sale', itemType: 'QuickSale', uniqueKey: 'QS0', SalePrice: 0 }];
  const p = products.value.map(x => {
    const stockQty = Number(x.StockQuantity ?? x.stockQuantity ?? 0);
    const consignmentQty = Number(x.ConsignmentQuantity ?? x.consignmentQuantity ?? 0);
    const ownedQty = Number(x.OwnedQuantity ?? x.ownedQuantity ?? Math.max(0, stockQty - consignmentQty));
    // Calculate total quantity in cart for this product (considering unit conversions)
    const cartQty = cart.value
      .filter(i => i.ProductId === x.ProductId && i.ItemType === 'Product')
      .reduce((sum, i) => {
        let m = 1;
        if (i.UnitId !== i.BaseUnitId) {
          const conv = (i.Conversions || []).find(c => (c.UnitId || c.unitId) === i.UnitId);
          m = conv ? (conv.Multiplier || conv.multiplier) : 1;
        }
        return sum + (i.Quantity * m);
      }, 0);
    const consumedFromConsignment = Math.min(cartQty, consignmentQty);
    const consumedFromOwned = Math.max(0, cartQty - consumedFromConsignment);

    return { 
      ...x, 
      itemType: 'Product', 
      uniqueKey: 'P' + x.ProductId, 
      SalePrice: x.SalePrice,
      StockQuantity: stockQty,
      stockQuantity: stockQty,
      ConsignmentQuantity: consignmentQty,
      consignmentQuantity: consignmentQty,
      OwnedQuantity: ownedQty,
      ownedQuantity: ownedQty,
      EffectiveStock: stockQty - cartQty,
      EffectiveConsignmentStock: Math.max(0, consignmentQty - consumedFromConsignment),
      EffectiveOwnedStock: Math.max(0, ownedQty - consumedFromOwned)
    };
  });
  const s = services.value.map(x => ({ ...x, itemType: 'Service', uniqueKey: 'S' + (x.ServiceId || x.serviceId), Name: x.ServiceName || x.serviceName, SalePrice: x.Price || x.price }));
  const b = bundles.value.map(x => ({ ...x, itemType: 'Bundle', uniqueKey: 'B' + (x.BundleId || x.bundleId), Name: x.BundleName || x.bundleName, SalePrice: x.BundlePrice || x.bundlePrice }));
  return [...qs, ...p, ...s, ...b];
});

// Tab & Search & Category အလိုက် Filter လုပ်ခြင်း
const filteredItems = computed(() => {
  let list = allItems.value;

  if (currentTab.value !== 'all') {
    list = list.filter(i => i.itemType.toLowerCase() === currentTab.value.slice(0, -1));
  }

  if (selectedCategory.value && (currentTab.value === 'all' || currentTab.value === 'products')) {
    list = list.filter(i => i.CategoryId === selectedCategory.value);
  }

  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase();
    list = list.filter(i => i.Name.toLowerCase().includes(q) || (i.Sku && i.Sku.toLowerCase().includes(q)));
  }

  if (!showOutOfStockItems.value) {
    list = list.filter(item => item.itemType !== 'Product' || Number(item.EffectiveStock || 0) > 0);
  }

  return list;
});

// Pagination Logic
const totalPages = computed(() => Math.ceil(filteredItems.value.length / pageSize.value));
const paginatedItems = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value;
  return filteredItems.value.slice(start, start + pageSize.value);
});

const getTypeColor = (type) => {
  if (type === 'Product') return 'bg-blue-500';
  if (type === 'Service') return 'bg-orange-500';
  if (type === 'Bundle') return 'bg-emerald-500';
  if (type === 'QuickSale') return 'bg-indigo-600';
  return 'bg-gray-500';
};

const handleItemSelect = (item) => {
  if (item.itemType === 'QuickSale') openQuickSaleModal();
  else if (item.itemType === 'Product') addToCart(item);
  else if (item.itemType === 'Service') addServiceToCart(item);
  else if (item.itemType === 'Bundle') addBundleToCart(item);
};

const openQuickSaleModal = () => {
  quickSaleForm.value = {
    Name: '',
    CategoryId: null,
    CategoryName: '',
    UnitId: null,
    UnitName: '',
    CostPrice: 0,
    SalePrice: 0,
    Quantity: 1,
    SupplierId: null,
    SupplierName: '',
    Sku: '',
    BranchId: parseInt(localStorage.getItem('BranchId') || 1)
  };
  supplierSearch.value = '';
  categorySearch.value = '';
  unitSearch.value = '';
  showQuickSaleModal.value = true;
};

const handleQuickSaleSubmit = async () => {
  try {
    // Resolve IDs from Names if they match existing data
    const cat = categories.value.find(c => c.categoryName === quickSaleForm.value.CategoryName);
    quickSaleForm.value.CategoryId = cat ? cat.categoryId : null;

    const unit = units.value.find(u => u.unitName === quickSaleForm.value.UnitName);
    quickSaleForm.value.UnitId = unit ? unit.unitId : null;

    // Ensure SupplierName is synced from the search input
    quickSaleForm.value.SupplierName = supplierSearch.value;
    const supp = suppliers.value.find(s => s.supplierName === supplierSearch.value);
    if (supp) quickSaleForm.value.SupplierId = supp.supplierId;

    const payload = {
      ...quickSaleForm.value,
      BranchId: parseInt(localStorage.getItem('BranchId') || 1)
    };

    const res = await api.post('/Sales/quick-sale', payload);
    const newProduct = res.data;

    // Refresh products list for the catalog
    await fetchAllData();

    // Add exactly what was returned to the cart
    cart.value.push({
      CartLineId: createCartLineId(),
      ItemType: 'Product',
      ProductId: newProduct.productId || newProduct.ProductId,
      Name: newProduct.name || newProduct.Name,
      UnitId: newProduct.unitId || newProduct.UnitId,
      BaseUnitId: newProduct.unitId || newProduct.UnitId,
      BaseUnitName: getUnitName(newProduct.unitId || newProduct.UnitId),
      Quantity: 1,
      SelectedPrice: newProduct.salePrice || newProduct.SalePrice,
      CostPrice: newProduct.costPrice || newProduct.CostPrice || 0, // Added CostPrice
      CurrentBasePrice: newProduct.salePrice || newProduct.SalePrice,
      OriginalBaseSalePrice: newProduct.salePrice || newProduct.SalePrice,
      OriginalBaseCostPrice: newProduct.costPrice || newProduct.CostPrice || 0, // Keep track of base cost
      DiscountAmount: 0,
      isFOC: false,
      isExpanded: false,
      Note: '',
      Conversions: [],
      Levels: []
    });

    showQuickSaleModal.value = false;
    addToast(`${newProduct.name || newProduct.Name} registered and added to sale!`, "success");
  } catch (err) {
    console.error("Quick Sale Error:", err);
    addToast("Failed to register Quick Sale item", "error");
  }
};

// Split Payment Methods
const availableMethods = computed(() => locale.value === 'mm'
  ? ['Cash', 'KPay', 'CB Pay', 'AYA Pay', 'Wave Money', 'Banking']
  : ['Cash', 'KPay', 'CB Pay', 'AYA Pay', 'Wave Money', 'Banking']);
const paymentDetails = ref([{ method: 'Cash', amount: 0 }]);
const isPaymentAmountAutoFilled = ref(false);

// --- Computed Properties ---
const voucherDiscountBase = computed(() => cart.value.reduce((sum, item) => {
  if (item.isFOC) return sum;
  const price = parseFloat(item.SelectedPrice) || 0;
  const quantity = parseFloat(item.Quantity) || 0;
  const itemDiscount = parseFloat(item.DiscountAmount) || 0;
  return sum + (quantity * price) - itemDiscount;
}, 0));

const syncVoucherDiscount = () => {
  const input = toNonNegativeAmount(voucherDiscountInput.value);
  voucherDiscountInput.value = input;
  saleForm.value.DiscountAmount = voucherDiscountType.value === 'percentage'
    ? voucherDiscountBase.value * Math.min(input, 100) / 100
    : input;
};

watch([voucherDiscountType, voucherDiscountInput, voucherDiscountBase], syncVoucherDiscount);

const formatDiscountPercent = (value) => {
  const percent = Math.min(toNonNegativeAmount(value), 100);
  return Number.isInteger(percent)
    ? String(percent)
    : percent.toFixed(2).replace(/\.?0+$/, '');
};

const voucherDiscountLabel = (type, input) => (
  type === 'percentage' && toNonNegativeAmount(input) > 0
    ? `Discount (${formatDiscountPercent(input)}%):`
    : 'Discount (-):'
);

const currentVoucherDiscountLabel = computed(() =>
  voucherDiscountLabel(voucherDiscountType.value, voucherDiscountInput.value)
);
const receiptVoucherDiscountLabel = computed(() =>
  voucherDiscountLabel(invoicePreviewData.value.discountType, invoicePreviewData.value.discountInput)
);

const totalAmount = computed(() => {
  if (!cart.value) return 0;

  // ၁။ Cart ထဲက item တစ်ခုချင်းစီရဲ့ Subtotal ကို ပေါင်းပါ
  const itemsSubTotal = cart.value.reduce((sum, i) => {
    if (i.isFOC) return sum;
    // Price သို့မဟုတ် Qty မရှိလျှင် 0 ဖြင့် အစားထိုးပါ
    const price = parseFloat(i.SelectedPrice) || 0;
    const qty = parseFloat(i.Quantity) || 0;
    const disc = parseFloat(i.DiscountAmount) || 0;
    return sum + (qty * price) - disc;
  }, 0);

  // ၂။ Voucher Level Discount နှင့် Deli Fee ကို တွက်ချက်ပါ
  const voucherDisc = parseFloat(saleForm.value.DiscountAmount) || 0;
  const deliFee = parseFloat(saleForm.value.DeliveryFee) || 0;

  return Math.max(0, itemsSubTotal - voucherDisc + deliFee);
});
watch(totalAmount, (newTotal) => {
  if (!showPaymentModal.value || !isPaymentAmountAutoFilled.value || paymentDetails.value.length !== 1) return;
  paymentDetails.value[0].amount = newTotal;
});
const totalPaid = computed(() => paymentDetails.value.reduce(
  (sum, payment) => sum + toNonNegativeAmount(payment.amount),
  0
));
const balanceAmount = computed(() => totalPaid.value - totalAmount.value);
const totalQty = computed(() => cart.value.reduce((sum, i) => sum + i.Quantity, 0));
const receiptData = computed(() => ({
  payments: normalizeReceiptPayments(paymentDetails.value, {
    method: paymentDetails.value[0]?.method || 'Cash',
    amount: totalPaid.value
  })
}));

const maxHeldSales = 50;
const cloneDraftValue = (value) => JSON.parse(JSON.stringify(value || null));
const normalizeHeldSale = (row) => {
  const payloadJson = field(row, 'PayloadJson') || '{}';
  let payload = {};
  try {
    payload = typeof payloadJson === 'string' ? JSON.parse(payloadJson) : payloadJson;
  } catch {
    payload = {};
  }

  const heldSaleId = Number(field(row, 'HeldSaleId') || payload.heldSaleId || payload.id || 0);
  return {
    ...payload,
    id: heldSaleId ? `held-${heldSaleId}` : (payload.id || `held-${Date.now()}`),
    heldSaleId,
    createdAt: field(row, 'CreatedAt') || payload.createdAt,
    updatedAt: field(row, 'UpdatedAt') || payload.updatedAt,
    expiresAt: field(row, 'ExpiresAt') || payload.expiresAt,
    branchId: field(row, 'BranchId') || payload.branchId,
    userId: field(row, 'UserId') || payload.userId,
    customerName: field(row, 'CustomerName') || payload.customerName || 'Walkin Customer',
    itemCount: Number(field(row, 'ItemCount') ?? payload.itemCount ?? 0),
    totalAmount: Number(field(row, 'TotalAmount') ?? payload.totalAmount ?? 0),
    preview: field(row, 'Preview') || payload.preview || ''
  };
};

const loadHeldSales = async () => {
  try {
    const res = await api.get('/Sales/held-sales', {
      params: { branchId: parseInt(localStorage.getItem('BranchId') || 1) }
    });
    heldSales.value = (Array.isArray(res.data) ? res.data : []).map(normalizeHeldSale).slice(0, maxHeldSales);
  } catch (err) {
    console.error('Unable to load held sales', err);
    heldSales.value = [];
  }
};

const resetCurrentSaleDraftState = () => {
  cart.value = [];
  saleForm.value = createDefaultSaleForm();
  voucherDiscountType.value = 'amount';
  voucherDiscountInput.value = 0;
  resetCustomerSelection();
  paymentDetails.value = [{ method: availableMethods.value[0], amount: 0 }];
  activeHeldSaleId.value = null;
};

const openHeldSalesModal = async () => {
  await loadHeldSales();
  showHeldSalesModal.value = true;
};

const holdCurrentSale = async () => {
  if (!cart.value.length) {
    addToast(t('sale.cartEmpty') || 'Cart is empty', 'warning');
    return;
  }

  const now = new Date().toISOString();
  const draft = {
    id: activeHeldSaleId.value ? `held-${activeHeldSaleId.value}` : `held-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
    heldSaleId: activeHeldSaleId.value,
    version: 1,
    createdAt: now,
    updatedAt: now,
    branchId: localStorage.getItem('BranchId') || '1',
    userId: localStorage.getItem('UserId') || localStorage.getItem('Username') || localStorage.getItem('UserName') || 'default',
    customerName: saleForm.value.CustomerName || customerSearch.value || 'Walkin Customer',
    itemCount: cart.value.length,
    totalAmount: totalAmount.value,
    preview: cart.value.map(i => `${i.Name} x ${i.Quantity}`).slice(0, 4).join(', '),
    saleForm: cloneDraftValue(saleForm.value),
    voucherDiscountType: voucherDiscountType.value,
    voucherDiscountInput: voucherDiscountInput.value,
    cart: cloneDraftValue(cart.value),
    paymentDetails: cloneDraftValue(paymentDetails.value)
  };

  try {
    const res = await api.post('/Sales/hold-sale', {
      HeldSaleId: activeHeldSaleId.value,
      BranchId: parseInt(localStorage.getItem('BranchId') || 1),
      CustomerId: saleForm.value.CustomerId,
      CustomerName: draft.customerName,
      UserId: parseInt(localStorage.getItem('UserId') || 1),
      TotalAmount: totalAmount.value,
      ItemCount: cart.value.length,
      Preview: draft.preview,
      PayloadJson: JSON.stringify(draft)
    });

    const savedDraft = normalizeHeldSale(res.data);
    heldSales.value = [savedDraft, ...heldSales.value.filter(x => x.heldSaleId !== savedDraft.heldSaleId)].slice(0, maxHeldSales);
    showPaymentModal.value = false;
    resetCurrentSaleDraftState();
    addToast(t('sale.heldSaleSaved'), 'success');
  } catch (err) {
    console.error('Hold sale failed', err);
    addToast(getApiErrorMessage(err, t('sale.checkoutError') || 'Hold sale failed'), 'error');
  }
};

const loadHeldSale = (draft) => {
  if (!draft) return;
  if (cart.value.length > 0 && !window.confirm(t('sale.replaceCurrentSaleConfirm'))) return;

  cart.value = (draft.cart || []).map(item => ({
    ...item,
    CartLineId: item.CartLineId || createCartLineId(),
    isExpanded: false,
    showPriceMenu: false
  }));
  saleForm.value = { ...createDefaultSaleForm(), ...(draft.saleForm || {}) };
  voucherDiscountType.value = draft.voucherDiscountType === 'percentage' ? 'percentage' : 'amount';
  voucherDiscountInput.value = Number(draft.voucherDiscountInput ?? saleForm.value.DiscountAmount ?? 0);
  syncVoucherDiscount();
  paymentDetails.value = Array.isArray(draft.paymentDetails) && draft.paymentDetails.length
    ? cloneDraftValue(draft.paymentDetails)
    : [{ method: availableMethods.value[0], amount: totalAmount.value }];
  customerSearch.value = saleForm.value.CustomerName || '';
  showCustomerResults.value = false;
  showHeldSalesModal.value = false;
  showPaymentModal.value = false;
  activeHeldSaleId.value = draft.heldSaleId || null;
  addToast(t('sale.heldSaleLoaded'), 'success');
};

const deleteHeldSale = async (id) => {
  if (!window.confirm(t('sale.deleteHeldSaleConfirm'))) return;
  const draft = heldSales.value.find(x => x.id === id || x.heldSaleId === id);
  const heldSaleId = draft?.heldSaleId || id;
  try {
    await api.delete(`/Sales/held-sales/${heldSaleId}`);
    heldSales.value = heldSales.value.filter(x => x.heldSaleId !== heldSaleId && x.id !== id);
    if (activeHeldSaleId.value === heldSaleId) activeHeldSaleId.value = null;
  } catch (err) {
    console.error('Delete held sale failed', err);
    addToast(getApiErrorMessage(err, t('sale.checkoutError') || 'Delete failed'), 'error');
  }
};

const clearActiveHeldSaleAfterCommit = async () => {
  const heldSaleId = activeHeldSaleId.value;
  if (!heldSaleId) return;

  try {
    await api.delete(`/Sales/held-sales/${heldSaleId}`);
  } catch (err) {
    console.warn('Could not delete committed held sale', err);
  } finally {
    heldSales.value = heldSales.value.filter(x => x.heldSaleId !== heldSaleId);
    activeHeldSaleId.value = null;
  }
};

const formatHeldSaleDate = (value) => {
  if (!value) return '';
  return new Date(value).toLocaleString([], {
    month: 'short',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  });
};

const filteredProducts = computed(() => {
  return products.value.filter(p => {
    const matchesSearch = p.Name?.toLowerCase().includes(searchQuery.value.toLowerCase()) || p.Sku?.toLowerCase().includes(searchQuery.value.toLowerCase());
    const matchesCategory = selectedCategory.value ? p.CategoryId === selectedCategory.value : true;
    return matchesSearch && matchesCategory;
  });
});

const addServiceToCart = (service) => {
  const existing = cart.value.find(i => i.ServiceId === (service.ServiceId || service.serviceId));
  if (existing) {
    existing.Quantity++;
  } else {
    cart.value.push({
      CartLineId: createCartLineId(),
      ItemType: 'Service',
      ServiceId: service.ServiceId || service.serviceId,
      Name: service.ServiceName || service.serviceName,
      Quantity: 1,
      SelectedPrice: service.Price || service.price,
      CostPrice: service.CostPrice || service.costPrice || 0, // Added CostPrice
      DiscountAmount: 0,
      isFOC: false,
      isExpanded: false,
      Note: '',
      Conversions: [], // Services don't have units
      SubTotal: service.Price || service.price
    });
  }
};

const addBundleToCart = async (bundle) => {
  const existing = cart.value.find(i => i.BundleId === (bundle.BundleId || bundle.bundleId));
  if (existing) {
    existing.Quantity++;
  } else {
    // Bundle Detail ကို API ကနေ တစ်ခါတည်း သွားစစ်ချင်ရင် စစ်လို့ရပါတယ်
    cart.value.push({
      CartLineId: createCartLineId(),
      ItemType: 'Bundle',
      BundleId: bundle.BundleId || bundle.bundleId,
      Name: bundle.BundleName || bundle.bundleName,
      Quantity: 1,
      SelectedPrice: bundle.BundlePrice || bundle.bundlePrice,
      CostPrice: bundle.BundleCost || bundle.bundleCost || 0, // Added CostPrice
      DiscountAmount: 0,
      isFOC: false,
      isExpanded: false,
      Note: '',
      Conversions: [],
      SubTotal: bundle.BundlePrice || bundle.bundlePrice
    });
  }
};

const fetchAllData = async () => {
  try {
    const branchId = localStorage.getItem('BranchId') || 1;
    const [pRes, sRes, bRes, cRes, uRes, supRes, branchRes] = await Promise.all([
      api.get(`/sales/for-sale/${branchId}?t=${Date.now()}`),
      api.get('/services').catch(() => ({ data: [] })),
      api.get('/bundles').catch(() => ({ data: [] })),
      api.get('/categories'),
      api.get('/units'),
      api.get('/suppliers').catch(() => ({ data: [] })),
      api.get('/Branches').catch(() => ({ data: [] }))
    ]);

    products.value = (Array.isArray(pRes.data) ? pRes.data : []).map(normalizeProductStockFields);
    services.value = sRes.data;
    bundles.value = bRes.data;
    categories.value = cRes.data;
    units.value = uRes.data;
    suppliers.value = supRes.data;
    branches.value = Array.isArray(branchRes.data) ? branchRes.data : [];
    localStorage.setItem('VersaBranches', JSON.stringify(branches.value));

    if (androidBridge.isAvailable()) {
      androidBridge.saveCatalog({
        products: products.value,
        services: services.value,
        bundles: bundles.value,
        categories: categories.value,
        units: units.value,
        suppliers: suppliers.value,
        customers: customers.value,
        branches: branches.value,
        shopInfo: shopInfo.value,
        consignmentPolicy: {
          catalogIncludesQuantities: true,
          saleAllocation: 'server_on_sync',
          payoutOnlineOnly: true
        },
        savedAt: new Date().toISOString()
      });
    }
  } catch (err) {
    const offlineCatalog = androidBridge.isAvailable() ? androidBridge.getCatalog() : null;
    if (offlineCatalog?.products?.length) {
      products.value = (offlineCatalog.products || []).map(normalizeProductStockFields);
      services.value = offlineCatalog.services || [];
      bundles.value = offlineCatalog.bundles || [];
      categories.value = offlineCatalog.categories || [];
      units.value = offlineCatalog.units || [];
      suppliers.value = offlineCatalog.suppliers || [];
      branches.value = offlineCatalog.branches || JSON.parse(localStorage.getItem('VersaBranches') || '[]');
      customers.value = offlineCatalog.customers || customers.value || [];
      if (hasReceiptSettings(offlineCatalog.shopInfo)) {
        applyShopSettings(offlineCatalog.shopInfo);
      }
      addToast(t('sale.offlineMode') || 'Offline mode: using saved sale data.', "info");
      return;
    }

    addToast(t('sale.dataLoadFailed'), "error");
  }
};

const selectedCustomerName = computed(() => {
  const selectedId = saleForm.value.CustomerId;
  const c = customers.value.find(x => x.customerId === selectedId);
  if (c?.name) return c.name;

  const typed = (saleForm.value.CustomerName || customerSearch.value || '').trim();
  if (typed) return typed;

  return t('common.walkInCustomer');
});

const selectedCustomerObj = computed(() => {
  const selectedId = saleForm.value.CustomerId;
  return customers.value.find(x => x.customerId === selectedId) || null;
});

const canPreorder = computed(() => (customerSearch.value || '').trim().length > 0);

const displayedCategories = computed(() =>
  showAllCategories.value
    ? categories.value
    : categories.value.slice(0, defaultVisibleCategoryCount)
);

const toggleCategoryExpand = () => {
  showAllCategories.value = !showAllCategories.value;
};

const selectCategory = (categoryId) => {
  selectedCategory.value = categoryId;
  // Collapse after selection so the filter area returns to compact mode.
  showAllCategories.value = false;
};

// --- Methods ---
const formatNumber = (val) => new Intl.NumberFormat().format(val || 0);
const formatMoney = (val) => `${formatNumber(val)} ${currencySymbol.value || 'Ks'}`;
const formatMoneyInput = (val) => {
  const n = Number(val || 0);
  if (!Number.isFinite(n)) return '0';
  return new Intl.NumberFormat('en-US', {
    minimumFractionDigits: 0,
    maximumFractionDigits: 2
  }).format(n);
};
const parseMoneyInput = (value) => {
  const raw = String(value ?? '').replace(/,/g, '');
  const cleaned = raw.replace(/[^\d.]/g, '');
  if (!cleaned) return 0;
  const [intPart, ...rest] = cleaned.split('.');
  const decimalPart = rest.join('').slice(0, 2);
  const normalized = decimalPart ? `${intPart}.${decimalPart}` : intPart;
  const parsed = Number(normalized);
  if (!Number.isFinite(parsed)) return 0;
  return parsed < 0 ? 0 : parsed;
};
const getApiErrorMessage = (err, fallback) => (
  err.response?.data?.Message
  || err.response?.data?.message
  || err.response?.data?.title
  || err.message
  || fallback
);
const onCartPriceInput = (item, event) => {
  item.SelectedPrice = parseMoneyInput(event.target.value);
};

const getUnitName = (id) => {
  const unit = units.value.find(u => (u.unitId || u.UnitId) === id);
  return unit ? (unit.unitName || unit.UnitName) : 'N/A';
};

const getProductStockQuantity = (productId) => {
  const product = products.value.find(p => (p.ProductId || p.productId) === productId);
  return Number(product?.StockQuantity || product?.stockQuantity || 0);
};

const getItemUnitMultiplier = (item) => {
  if (item.UnitId == item.BaseUnitId) return 1;
  const conv = (item.Conversions || []).find(c => (c.UnitId || c.unitId) == item.UnitId);
  return Number(conv?.Multiplier || conv?.multiplier || 1) || 1;
};

const getOtherCartBaseQty = (item) => {
  if (!item?.ProductId) return 0;
  return cart.value
    .filter(i => i !== item && i.ProductId === item.ProductId)
    .reduce((sum, i) => sum + (Number(i.Quantity || 0) * getItemUnitMultiplier(i)), 0);
};

const getMaxSellableQty = (item) => {
  if (!item?.ProductId || item.ItemType !== 'Product') return Number.MAX_SAFE_INTEGER;
  const stockQty = getProductStockQuantity(item.ProductId);
  const remainingBase = stockQty - getOtherCartBaseQty(item);
  const multiplier = getItemUnitMultiplier(item);
  const maxQty = remainingBase / multiplier;
  return useDecimalQuantity.value ? Math.max(0, Number(maxQty.toFixed(2))) : Math.max(0, Math.floor(maxQty));
};

const normalizeCartQty = (item) => {
  if (!item) return;
  const maxQty = getMaxSellableQty(item);
  const minQty = useDecimalQuantity.value ? 0.01 : 1;
  const rawQty = Number(item.Quantity || 0);
  const qty = useDecimalQuantity.value
    ? Math.max(minQty, Number(rawQty.toFixed(2)))
    : Math.max(minQty, Math.floor(rawQty));
  if (qty > maxQty) {
    item.Quantity = maxQty > 0 ? maxQty : minQty;
    addToast(`"${item.Name}" ${t('sale.outOfStock')}`, "error");
    return;
  }
  item.Quantity = qty;
};

const onCartQtyInput = (item, event) => {
  if (!item) return;
  const maxQty = getMaxSellableQty(item);
  const raw = useDecimalQuantity.value
    ? parseFloat(event?.target?.value)
    : parseInt(event?.target?.value, 10);
  if (Number.isNaN(raw)) return;
  const minQty = useDecimalQuantity.value ? 0.01 : 1;
  const qty = useDecimalQuantity.value ? Number(raw.toFixed(2)) : raw;
  item.Quantity = Math.min(Math.max(minQty, qty), maxQty > 0 ? maxQty : minQty);
};

// --- Autocomplete Methods ---
const moveSearchDown = (items) => {
  if (items.length === 0) return;
  highlightedIndex.value = (highlightedIndex.value + 1) % items.length;
};

const moveSearchUp = (items) => {
  if (items.length === 0) return;
  highlightedIndex.value = (highlightedIndex.value - 1 + items.length) % items.length;
};

const selectHighlightedProductFromSearch = (items, type) => {
  if (highlightedIndex.value >= 0 && items[highlightedIndex.value]) {
    const item = items[highlightedIndex.value];
    if (type === 'supplier') selectSupplier(item);
    else if (type === 'customer') selectCustomer(item);
    else if (type === 'category') selectCategoryFromSearch(item);
    else if (type === 'unit') selectUnitFromSearch(item);
  }
};

const handleSupplierSearch = () => {
  showSupplierResults.value = supplierSearch.value.length > 0;
  quickSaleForm.value.SupplierName = supplierSearch.value;
  quickSaleForm.value.SupplierId = null;
  highlightedIndex.value = -1;
};

const selectSupplier = (s) => {
  quickSaleForm.value.SupplierId = s.supplierId;
  quickSaleForm.value.SupplierName = s.supplierName;
  supplierSearch.value = s.supplierName;
  showSupplierResults.value = false;
};

const handleCustomerSearch = () => {
  saleForm.value.CustomerName = customerSearch.value;
  saleForm.value.CustomerId = null;
  showCustomerResults.value = filteredCustomersForSearch.value.length > 0;
  highlightedIndex.value = -1;
};

const openCustomerResults = () => {
  showCustomerResults.value = filteredCustomersForSearch.value.length > 0;
  highlightedIndex.value = filteredCustomersForSearch.value.length > 0 ? 0 : -1;
};

const selectCustomer = (c) => {
  saleForm.value.CustomerId = c.customerId;
  saleForm.value.CustomerName = c.name;
  customerSearch.value = c.name;
  showCustomerResults.value = false;
};

const resetCustomerSelection = () => {
  saleForm.value.CustomerId = null;
  saleForm.value.CustomerName = '';
  customerSearch.value = '';
  showCustomerResults.value = false;
};

const handleCategorySearch = () => {
  showCategoryResults.value = categorySearch.value.length > 0;
  quickSaleForm.value.CategoryName = categorySearch.value;
  quickSaleForm.value.CategoryId = null;
  highlightedIndex.value = -1;
};

const selectCategoryFromSearch = (c) => {
  quickSaleForm.value.CategoryId = c.categoryId;
  quickSaleForm.value.CategoryName = c.categoryName;
  categorySearch.value = c.categoryName;
  showCategoryResults.value = false;
};

const handleUnitSearch = () => {
  showUnitResults.value = unitSearch.value.length > 0;
  quickSaleForm.value.UnitName = unitSearch.value;
  quickSaleForm.value.UnitId = null;
  highlightedIndex.value = -1;
};

const selectUnitFromSearch = (u) => {
  quickSaleForm.value.UnitId = u.unitId;
  quickSaleForm.value.UnitName = u.unitName;
  unitSearch.value = u.unitName;
  showUnitResults.value = false;
};

const clearSearch = (type) => {
  if (type === 'supplier') {
    supplierSearch.value = '';
    quickSaleForm.value.SupplierName = '';
    quickSaleForm.value.SupplierId = null;
    showSupplierResults.value = false;
  } else if (type === 'customer') {
    customerSearch.value = '';
    saleForm.value.CustomerName = '';
    saleForm.value.CustomerId = null;
    showCustomerResults.value = false;
  } else if (type === 'category') {
    categorySearch.value = '';
    quickSaleForm.value.CategoryName = '';
    quickSaleForm.value.CategoryId = null;
    showCategoryResults.value = false;
  } else if (type === 'unit') {
    unitSearch.value = '';
    quickSaleForm.value.UnitName = '';
    quickSaleForm.value.UnitId = null;
    showUnitResults.value = false;
  }
};

const filteredSuppliers = computed(() => {
  if (!supplierSearch.value) return [];
  const q = supplierSearch.value.toLowerCase();
  return suppliers.value.filter(s => s.supplierName.toLowerCase().includes(q));
});

const filteredCustomersForSearch = computed(() => {
  const q = customerSearch.value.trim().toLowerCase();
  if (!q) return customers.value.slice(0, 50);

  return customers.value
    .filter(c => {
      const name = (c.name || '').toLowerCase();
      const phone = (c.phone || '').toLowerCase();
      return name.includes(q) || phone.includes(q);
    })
    .slice(0, 50);
});

const filteredCategoriesForSearch = computed(() => {
  if (!categorySearch.value) return [];
  const q = categorySearch.value.toLowerCase();
  return categories.value.filter(c => c.categoryName.toLowerCase().includes(q));
});

const filteredUnitsForSearch = computed(() => {
  if (!unitSearch.value) return [];
  const q = unitSearch.value.toLowerCase();
  return units.value.filter(u => u.unitName.toLowerCase().includes(q));
});

const markPaymentAmountAsManual = () => {
  isPaymentAmountAutoFilled.value = false;
};

const clampPaymentAmount = (payment) => {
  payment.amount = toNonNegativeAmount(payment.amount);
};

const addPaymentMethod = () => {
  markPaymentAmountAsManual();
  paymentDetails.value.push({ method: availableMethods.value[0], amount: 0 });
};

const removePaymentMethod = (index) => {
  markPaymentAmountAsManual();
  paymentDetails.value.splice(index, 1);
};

const isNetworkSaleFailure = (err) => {
  if (!err) return false;
  return !err.response || err.code === 'ERR_NETWORK' || err.code === 'ECONNABORTED' || err.message === 'Network Error';
};

const createClientSaleId = () => {
  const prefix = androidBridge.isDesktop() ? 'DESKTOP' : 'ANDROID';
  if (window.crypto?.randomUUID) return `${prefix}-${window.crypto.randomUUID()}`;
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
};

const createOfflineVoucherNo = () => {
  const now = new Date();
  const datePart = [
    String(now.getFullYear()).slice(-2),
    String(now.getMonth() + 1).padStart(2, '0'),
    String(now.getDate()).padStart(2, '0')
  ].join('');
  const key = `VersaOfflineVoucherSeq:${datePart}`;
  const next = Number(localStorage.getItem(key) || 0) + 1;
  localStorage.setItem(key, String(next));
  return `OFF-${datePart}-${String(next).padStart(4, '0')}`;
};

const saveCurrentCatalogForAndroid = () => {
  if (!androidBridge.isAvailable()) return;
  androidBridge.saveCatalog({
    products: products.value,
    services: services.value,
    bundles: bundles.value,
    categories: categories.value,
    units: units.value,
    suppliers: suppliers.value,
    customers: customers.value,
    branches: branches.value,
    shopInfo: shopInfo.value,
    consignmentPolicy: {
      catalogIncludesQuantities: true,
      saleAllocation: 'server_on_sync',
      payoutOnlineOnly: true
    },
    savedAt: new Date().toISOString()
  });
};

const applyLocalStockDeduction = (payload) => {
  (payload.Items || []).forEach(item => {
    if (item.ItemType !== 'Product' || !item.ProductId) return;
    const product = products.value.find(p => (p.ProductId || p.productId) === item.ProductId);
    if (!product) return;

    const current = Number(product.StockQuantity ?? product.stockQuantity ?? product.EffectiveStock ?? 0);
    const consignmentQty = Number(product.ConsignmentQuantity ?? product.consignmentQuantity ?? 0);
    const ownedQty = Number(product.OwnedQuantity ?? product.ownedQuantity ?? Math.max(0, current - consignmentQty));
    const deductQty = Number(item.Quantity || 0) * Number(item.Multiplier || 1);
    const nextQty = Math.max(0, current - deductQty);
    const deductConsignmentQty = Math.min(consignmentQty, deductQty);
    const deductOwnedQty = Math.max(0, deductQty - deductConsignmentQty);
    const nextConsignmentQty = Math.max(0, consignmentQty - deductConsignmentQty);
    const nextOwnedQty = Math.max(0, ownedQty - deductOwnedQty);

    product.StockQuantity = nextQty;
    product.stockQuantity = nextQty;
    product.ConsignmentQuantity = nextConsignmentQty;
    product.consignmentQuantity = nextConsignmentQty;
    product.OwnedQuantity = nextOwnedQty;
    product.ownedQuantity = nextOwnedQty;
    product.EffectiveStock = nextQty;
  });

  saveCurrentCatalogForAndroid();
};

const buildReceiptText = (saleNo, options = {}) => {
  const width = Number(options.width || 42);
  const items = options.items || cart.value;
  const payments = options.payments || receiptData.value.payments;
  const discountAmount = Number(options.discountAmount ?? saleForm.value.DiscountAmount ?? 0);
  const discountType = options.discountType ?? voucherDiscountType.value;
  const discountInput = options.discountInput ?? voucherDiscountInput.value;
  const discountLabel = voucherDiscountLabel(discountType, discountInput);
  const deliveryFee = Number(options.deliveryFee ?? saleForm.value.DeliveryFee ?? 0);
  const voucherNote = options.voucherNote ?? saleForm.value.Note ?? '';
  const receiptTotalQty = Number(options.totalQty ?? items.reduce((sum, i) => sum + Number(i.Quantity || 0), 0));
  const subTotal = Number(options.subTotal ?? items.reduce((sum, i) => sum + (i.isFOC ? 0 : Number(i.Quantity || 0) * Number(i.SelectedPrice || 0)), 0));
  const finalTotal = Number(options.totalAmount ?? Math.max(0, subTotal - discountAmount + deliveryFee));
  const paidTotal = Number(options.paidAmount ?? payments.reduce((sum, payment) => sum + Number(payment.amount || 0), 0));
  const balance = paidTotal - finalTotal;
  const totalCredit = toNonNegativeAmount(options.totalCredit ?? invoicePreviewData.value.totalCredit);
  const includePreviousCredit = options.showPreviousCredit ?? showPreviousCredit.value;
  const lines = [];
  const line = '-'.repeat(width);
  const doubleLine = '='.repeat(width);
  const shopName = shopInfo.value.ShopName || shopInfo.value.Name || 'Versa POS';
  const branch = receiptBranchName.value || 'Main Branch';
  const contact = receiptContact.value;
  const customer = selectedCustomerName.value || 'Walk-in';
  const staff = cashierName.value || localStorage.getItem('Username') || 'Admin';

  const textLength = (value) => String(value ?? '').length;
  const clip = (value, max) => String(value ?? '').slice(0, max);
  const center = (value) => {
    const text = String(value ?? '').trim();
    const left = Math.max(0, Math.floor((width - textLength(text)) / 2));
    return `${' '.repeat(left)}${text}`;
  };
  const columns = (left, right) => {
    const rightText = String(right ?? '');
    const leftMax = Math.max(1, width - textLength(rightText) - 1);
    const leftText = clip(left, leftMax);
    return `${leftText}${' '.repeat(Math.max(1, width - textLength(leftText) - textLength(rightText)))}${rightText}`;
  };
  const money = (value) => `${formatNumber(value)} ${currencySymbol.value}`;
  const wrap = (value, max) => {
    const text = String(value ?? '').trim();
    if (!text) return [''];

    const chunks = [];
    let rest = text;
    while (textLength(rest) > max) {
      chunks.push(rest.slice(0, max));
      rest = rest.slice(max);
    }
    chunks.push(rest);
    return chunks;
  };

  lines.push(center(shopName.toUpperCase()));
  lines.push(center(`(${branch})`));
  if (contact.Address) wrap(contact.Address, width).forEach(addressLine => lines.push(center(addressLine)));
  if (contact.Phone) lines.push(center(`Tel: ${contact.Phone}`));
  lines.push(line);
  lines.push(columns(`No: ${saleNo || lastSaleNo.value || ''}`, lastSaleDate.value || new Date().toLocaleString()));
  lines.push(columns(`Cus: ${customer}`, `Staff: ${staff}`));
  if (selectedCustomerObj.value?.phone || selectedCustomerObj.value?.address) {
    lines.push(clip(`${selectedCustomerObj.value.phone || ''} ${selectedCustomerObj.value.address || ''}`.trim(), width));
  }
  lines.push(line);
  const qtyWidth = 5;
  const amountWidth = Math.min(14, Math.max(10, width - 21));
  const nameWidth = Math.max(12, width - qtyWidth - amountWidth);

  lines.push(`${'Name'.padEnd(nameWidth, ' ')}${'Qty'.padStart(qtyWidth, ' ')}${'Amount'.padStart(amountWidth, ' ')}`);
  lines.push(line);

  items.forEach(item => {
    const qty = Number(item.Quantity || 0);
    const price = Number(item.SelectedPrice || 0);
    const total = item.isFOC ? 0 : (qty * price) - Number(item.DiscountAmount || 0);
    const itemName = `${item.Name || ''}${item.isFOC ? ' (FOC)' : ''}`;
    const itemLines = wrap(itemName, nameWidth);
    const qtyText = `${qty}`;
    const totalText = formatNumber(total);

    lines.push(`${itemLines[0].padEnd(nameWidth, ' ')}${qtyText.padStart(qtyWidth, ' ')}${totalText.padStart(amountWidth, ' ')}`);
    itemLines.slice(1).forEach(extra => lines.push(extra));
    lines.push(`  (${getUnitName(item.UnitId)}) @ ${formatNumber(price)}`);
    if (item.DiscountAmount > 0 && !item.isFOC) {
      lines.push(columns('  Discount', `-${formatNumber(item.DiscountAmount)}`));
    }
    if (item.Note) lines.push(`  * ${clip(item.Note, width - 4)}`);
  });

  lines.push(line);
  lines.push(columns('Total Qty', formatNumber(receiptTotalQty)));
  lines.push(columns('Sub-Total:', money(subTotal)));
  if (discountAmount > 0) {
    lines.push(columns(discountLabel, money(discountAmount)));
  }
  if (deliveryFee > 0) {
    lines.push(columns('Delivery (+):', money(deliveryFee)));
  }
  if (voucherNote && String(voucherNote).trim() !== '') {
    lines.push(`Note: ${clip(voucherNote, width - 6)}`);
  }
  lines.push(doubleLine);
  lines.push(columns('TOTAL:', money(finalTotal)));
  payments.forEach(payment => {
    lines.push(columns(receiptPaidLabel(payment.method), money(payment.amount || 0)));
  });
  if (balance !== 0) {
    lines.push(columns(receiptBalanceLabel(balance), money(Math.abs(balance))));
  }
  if (includePreviousCredit && totalCredit > 0) {
    lines.push(columns(`${t('sale.totalCredit')}:`, money(totalCredit)));
  }
  lines.push(doubleLine);
  lines.push('');
  receiptFooterLines.value.forEach(footerLine => lines.push(center(footerLine)));
  return lines.join('\n');
};

const printWithAndroidIfPossible = async (receiptText, showSuccessToast = false) => {
  if (!androidBridge.supportsNativePrint()) return false;
  let result = androidBridge.printReceipt(receiptText, shopInfo.value.LogoData || '');
  if (!result?.ok && shopInfo.value.LogoData) {
    result = androidBridge.printReceipt(receiptText, '');
  }
  if (!result?.ok) {
    addToast(result?.message || 'Bluetooth printing failed.', 'warning');
    return false;
  }
  if (showSuccessToast) {
    addToast(result.message || t('history.printPopupOpened') || 'Receipt sent to printer.', 'success');
  }
  return true;
};

let isSyncingPendingAndroidSales = false;
let pendingSaleSyncTimer = null;

const startPendingSaleSyncTimer = () => {
  if (pendingSaleSyncTimer || !androidBridge.isAvailable()) return;
  const intervalMs = androidBridge.isDesktop() ? 5000 : 15000;
  pendingSaleSyncTimer = window.setInterval(syncPendingAndroidSales, intervalMs);
};

const syncPendingAndroidSales = async () => {
  if (isSyncingPendingAndroidSales) return;
  if (!androidBridge.isAvailable() || !androidBridge.isOnline()) return;

  isSyncingPendingAndroidSales = true;
  let syncedCount = 0;
  try {
    const pendingResult = androidBridge.getPendingSales();
    const pendingSales = pendingResult?.sales || [];
    if (!pendingSales.length) return;

    for (const sale of pendingSales) {
      const res = await api.post('/Sales/create-sale', sale.payload, { timeout: 12000 });
      const serverSaleId = res.data.SaleId || res.data.saleId || '';
      const marked = androidBridge.markSaleSynced(sale.localId, serverSaleId);
      if (marked?.ok) syncedCount++;
    }
  } catch (err) {
    if (!isNetworkSaleFailure(err)) {
      console.error('Offline sale sync failed:', err);
    }
  } finally {
    isSyncingPendingAndroidSales = false;
  }

  if (syncedCount > 0) {
    addToast(`${syncedCount} offline sale(s) synced.`, 'success');
    await fetchAllData();
  }
};


const calculateEmptyRows = () => {

  const maxRows = receiptSize.value === 'A4' ? 10 : 1;
  const emptyCount = maxRows - cart.value.length;
  return emptyCount > 0 ? emptyCount : 0;
};



const addToCart = async (product) => {
  // Check if item with same Product AND same Unit already exists in cart
  const existing = cart.value.find(item => 
    item.ProductId === product.ProductId && 
    (item.UnitId === (product.UnitId || product.unitId)) && 
    !item.isFOC
  );
  
  // Calculate how many base units would be added if we update existing or add new
  let unitsToAdd = 1;
  if (existing) {
    if (existing.UnitId != existing.BaseUnitId) {
      const conv = (existing.Conversions || []).find(c => (c.UnitId || c.unitId) == existing.UnitId);
      unitsToAdd = conv ? (conv.Multiplier || conv.multiplier) : 1;
    }
  }

  if (product.EffectiveStock < unitsToAdd) {
    addToast(`"${product.Name}" ${t('sale.outOfStock')}`, "error");
    return;
  }


  const itemConversions = (product.Conversions || product.conversions || [])
    .filter(u => (u.UnitId || u.unitId) != (product.UnitId || product.unitId));

  if (existing) {
    existing.Quantity++;
  } else {
    const cartItem = {
      CartLineId: createCartLineId(),
      ItemType: 'Product',
      ProductId: product.ProductId,
      Name: product.Name,
      UnitId: product.UnitId || product.unitId,
      BaseUnitId: product.UnitId || product.unitId,
      BaseUnitName: product.UnitName || product.unitName,
      Quantity: 1,
      SelectedPrice: product.SalePrice,
      CostPrice: product.CostPrice || 0, // Added CostPrice
      CurrentBasePrice: product.SalePrice,
      OriginalBaseSalePrice: product.SalePrice,
      OriginalBaseCostPrice: product.CostPrice || 0, // Keep track of base cost
      DiscountAmount: 0,
      isFOC: false,
      isExpanded: false,
      Note: '',
      Conversions: itemConversions,
      Levels: product.AdditionalPrices || [],
      showPriceMenu: false
    };
    cart.value.push(cartItem);
    refreshCartItemConversions(product, cartItem);
  }
};

const refreshCartItemConversions = async (product, cartItem) => {
  try {
    const res = await apiGetWithConnectionFeedback(
      `/products/units/${product.ProductId || product.productId}`,
      { timeout: 3500, slowAfterMs: 600 }
    );

    cartItem.Conversions = (res.data || [])
      .filter(u => (u.UnitId || u.unitId) != (product.UnitId || product.unitId));
  } catch (err) {
    console.error("Units fetch failed:", err);
  }
};

const normalizeCustomerVm = (raw) => ({
  customerId: raw.customerId ?? raw.CustomerId,
  name: raw.name ?? raw.Name,
  phone: raw.phone ?? raw.Phone,
  address: raw.address ?? raw.Address,
  currentBalance: toNonNegativeAmount(raw.currentBalance ?? raw.CurrentBalance)
});

const ensureCustomerForOrder = async ({ requireCustomer }) => {
  const typed = (customerSearch.value || '').trim();

  if (!typed) {
    if (requireCustomer) {
      addToast(t('sale.customerSelection') || 'Please add/select customer first.', 'warning');
      return null;
    }

    saleForm.value.CustomerId = null;
    saleForm.value.CustomerName = 'Walk-in';
    return null;
  }

  const lowered = typed.toLowerCase();
  const matched = customers.value
    .map(normalizeCustomerVm)
    .find(c => (c.name || '').toLowerCase() === lowered);

  if (matched?.customerId) {
    saleForm.value.CustomerId = matched.customerId;
    saleForm.value.CustomerName = matched.name || typed;
    customerSearch.value = saleForm.value.CustomerName;
    return matched.customerId;
  }

  try {
    const res = await api.post('/customers', {
      Name: typed,
      Phone: '',
      Address: '',
      CurrentBalance: 0,
      CreatedAt: new Date().toISOString()
    });

    const created = normalizeCustomerVm(res.data || {});
    if (created.customerId) {
      customers.value.push(created);
      saleForm.value.CustomerId = created.customerId;
      saleForm.value.CustomerName = created.name || typed;
      customerSearch.value = saleForm.value.CustomerName;
      return created.customerId;
    }

    saleForm.value.CustomerId = null;
    saleForm.value.CustomerName = typed;
    return null;
  } catch (err) {
    console.error('Create customer failed:', err);
    addToast('Failed to create customer', 'error');
    saleForm.value.CustomerId = null;
    saleForm.value.CustomerName = typed;
    return null;
  }
};


const toggleFOC = (index) => {
  const item = cart.value[index];


  if (item.Quantity > 1 && !item.isFOC) {
    item.Quantity -= 1;


    cart.value.push({
      ...item,
      CartLineId: createCartLineId(),
      Quantity: 1,
      isFOC: true,
      isExpanded: false,
      DiscountAmount: 0
    });
  } else {

    item.isFOC = !item.isFOC;
    if (item.isFOC) item.DiscountAmount = 0;
  }
};

const removeFromCart = (index) => cart.value.splice(index, 1);
const updateQty = (item, change) => {
  if (item.ItemType === 'Product' && change > 0) {
    const maxQty = getMaxSellableQty(item);
    if (Number(item.Quantity || 0) + change > maxQty) {
      addToast(t('sale.outOfStock'), "error");
      return;
    }
  }
  const minQty = useDecimalQuantity.value ? 0.01 : 1;
  const nextQty = Number(item.Quantity || 0) + change;
  item.Quantity = useDecimalQuantity.value ? Number(nextQty.toFixed(2)) : Math.floor(nextQty);
  if (item.Quantity < minQty) item.Quantity = minQty;
};

const validateCartStock = () => {
  const stockIssues = cart.value
    .filter(item => item.ItemType === 'Product')
    .map(item => ({
      item,
      maxQty: getMaxSellableQty(item)
    }))
    .find(x => Number(x.item.Quantity || 0) > x.maxQty);

  if (stockIssues) {
    const minQty = useDecimalQuantity.value ? 0.01 : 1;
    stockIssues.item.Quantity = stockIssues.maxQty > 0 ? stockIssues.maxQty : minQty;
    addToast(`"${stockIssues.item.Name}" ${t('sale.outOfStock')}`, "error");
    return false;
  }

  return true;
};

const processCheckout = () => {

  if (!cart.value || cart.value.length === 0) {
    addToast(t('sale.cartEmpty'), "warning");
    return;
  }

  try {
    if (!validateCartStock()) return;

    const dueValue = totalAmount.value || 0;


    paymentDetails.value = [{
      method: availableMethods.value[0],
      amount: dueValue
    }];
    isPaymentAmountAutoFilled.value = true;

    // ၄။ Payment Modal ကို ဖွင့်ပါ
    if (!saleForm.value.CustomerId && !saleForm.value.CustomerName) {
      resetCustomerSelection();
    }
    saleForm.value.SaleDate = new Date().toISOString().split('T')[0];
    receiptSize.value = preferredReceiptSize();
    showPreviousCredit.value = false;
    showPaymentModal.value = true;

  } catch (err) {
    console.error("Checkout Logic Error:", err);
    addToast(t('sale.checkoutError'), "error");
  }
};

const shareFromPreview = async () => {
  const mobileFallbackWindow = !androidBridge.isAndroid() && isMobileBrowser()
    ? window.open('', '_blank')
    : null;

  const blob = await capturePreviewElement();
  if (!blob) {
    try { mobileFallbackWindow?.close(); } catch (_) { }
    addToast(t('history.shareCaptureError') || 'Could not capture invoice image.', 'error');
    return;
  }

  const fileName = `invoice-${lastSaleNo.value.replace(/[^a-zA-Z0-9]/g, '_')}.png`;
  const shareTitle = `Invoice - ${lastSaleNo.value}`;
  const shareText = `${shopInfo.value.ShopName} - ${t('sale.shareInvoiceDesc')}`;

  // Save to file if checkbox is checked
  if (saveInvoiceAsImage.value) {
    await saveBlobToFile(blob);
  }

  // Android WebView cannot reliably use browser downloads or Web Share files.
  if (androidBridge.isAndroid()) {
    try {
      const imageData = await blobToDataUrl(blob);
      const nativeResult = androidBridge.shareImage(imageData, fileName, shareTitle, shareText);
      if (nativeResult?.ok) return;
      console.warn('[ShareInvoice] Android native share failed:', nativeResult?.message || nativeResult);
    } catch (err) {
      console.warn('[ShareInvoice] Android native share error:', err);
    }
  }

  const file = new File([blob], fileName, { type: 'image/png' });

  // Try Web Share API (mobile: Facebook, Viber, Messenger, etc.)
  if (navigator.share && navigator.canShare && navigator.canShare({ files: [file] })) {
    try {
      await navigator.share({
        files: [file],
        title: shareTitle,
        text: shareText,
      });
      try { mobileFallbackWindow?.close(); } catch (_) { }
      return;
    } catch (err) {
      if (err.name !== 'AbortError') {
        console.warn('[ShareInvoice] Web Share failed:', err);
      }
    }
  }

  if (mobileFallbackWindow) {
    openMobileInvoiceImageWindow(mobileFallbackWindow, blob, fileName);
    addToast(t('history.shareDownloaded') || 'Invoice image is ready. Use the opened page to save or share it.', 'info');
    return;
  }

  // Fallback: download the image
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);

  addToast(t('history.shareDownloaded') || 'Invoice image downloaded.', 'info');
};

const blobToDataUrl = (blob) => new Promise((resolve, reject) => {
  const reader = new FileReader();
  reader.onload = () => resolve(reader.result || '');
  reader.onerror = () => reject(reader.error || new Error('Could not read invoice image.'));
  reader.readAsDataURL(blob);
});

const openMobileInvoiceImageWindow = (targetWindow, blob, fileName) => {
  const url = URL.createObjectURL(blob);
  targetWindow.document.open();
  targetWindow.document.write(`
    <!DOCTYPE html>
    <html>
      <head>
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <title>${fileName}</title>
        <style>
          body{margin:0;padding:12px;background:#f8fafc;font-family:system-ui,-apple-system,Segoe UI,Roboto,Arial;color:#111827}
          .bar{position:sticky;top:0;display:flex;gap:8px;align-items:center;padding:8px 0 12px;background:#f8fafc}
          a,button{border:0;border-radius:10px;padding:10px 14px;background:#059669;color:white;text-decoration:none;font-size:14px}
          button{background:#111827}
          img{display:block;width:100%;height:auto;background:white;border:1px solid #e5e7eb}
        </style>
      </head>
      <body>
        <div class="bar">
          <a href="${url}" download="${fileName}">Save</a>
          <button id="close-btn">Close</button>
        </div>
        <img src="${url}" alt="Invoice" />
        <script>
          document.getElementById('close-btn').addEventListener('click', function(){ window.close(); });
        <\/script>
      </body>
    </html>
  `);
  targetWindow.document.close();
};

const buildInvoiceHtml = (format = null) => {
  const isThermal = (format || receiptSize.value) === '80mm';
  const data = invoicePreviewData.value;
  const font = receiptFont.value;
  const discountLabel = voucherDiscountLabel(data.discountType, data.discountInput);

  const itemsHtml = isThermal
    ? data.items.map(item => {
        const name = item.Name || item.ItemName || item.ProductName || '';
        const unit = item.UnitName || item.BaseUnitName || '';
        const qty = item.Quantity || 0;
        const price = item.SelectedPrice || item.UnitPrice || 0;
        const itemDiscount = item.isFOC ? 0 : Number(item.DiscountAmount || 0);
        const subtotal = item.isFOC ? 0 : (qty * price) - itemDiscount;
        const discountHtml = itemDiscount > 0
          ? `<div style="font-size:${font.tiny}px;color:#dc2626;margin-top:1px">Discount: -${formatMoney(itemDiscount)}</div>`
          : '';
        return `<tr style="border-bottom:1px dotted #ccc">
          <td style="padding:2px;font-size:${font.small}px">${name}${unit ? ` <span style="font-size:${font.tiny}px;color:#666">(${unit})</span>` : ''}${discountHtml}</td>
          <td style="text-align:center;padding:2px;font-size:${font.small}px">${qty}</td>
          <td style="text-align:right;padding:2px;font-size:${font.small}px">${formatMoney(price)}</td>
          <td style="text-align:right;padding:2px;font-size:${font.small}px">${formatMoney(subtotal)}</td>
        </tr>`;
      }).join('')
    : data.items.map(item => {
        const name = item.Name || item.ItemName || item.ProductName || '';
        const unit = item.UnitName || item.BaseUnitName || '';
        const qty = item.Quantity || 0;
        const price = item.SelectedPrice || item.UnitPrice || 0;
        const itemDiscount = item.isFOC ? 0 : Number(item.DiscountAmount || 0);
        const subtotal = item.isFOC ? 0 : (qty * price) - itemDiscount;
        const discountHtml = itemDiscount > 0
          ? `<div style="font-size:10px;color:#dc2626;margin-top:2px;font-weight:normal">Discount: -${formatMoney(itemDiscount)}</div>`
          : '';
        return `<tr style="border-bottom:1px solid #e5e7eb">
          <td style="padding:6px 4px;font-size:12px">${name}${unit ? ` <span style="font-size:10px;color:#666">(${unit})</span>` : ''}${discountHtml}</td>
          <td style="text-align:center;padding:6px 4px;font-size:12px;width:60px">${qty}</td>
          <td style="text-align:right;padding:6px 4px;font-size:12px;width:90px">${formatMoney(price)}</td>
          <td style="text-align:right;padding:6px 4px;font-size:12px;width:100px">${formatMoney(subtotal)}</td>
        </tr>`;
      }).join('');

  const paymentsHtml = data.payments.map(p =>
    `<div style="display:flex;justify-content:${isThermal ? 'space-between' : 'flex-end'};${isThermal ? '' : 'gap:100px'};margin-top:2px;font-size:${isThermal ? font.small : 12}px"><span>${receiptPaidLabel(p.method || 'Cash')}</span><span>${formatMoney(p.amount || 0)}</span></div>`
  ).join('');

  const footerHtml = receiptFooterLines.value
    .filter(line => line && line !== 'null' && line !== 'undefined')
    .map(line => `<div style="text-align:center;margin-top:4px;font-size:${isThermal ? font.footer : 11}px">${line}</div>`)
    .join('');

  const balance = receiptBalance.value;
  const balanceHtml = (balance !== 0 && Number.isFinite(balance))
    ? `<div style="display:flex;justify-content:${isThermal ? 'space-between' : 'flex-end'};${isThermal ? '' : 'gap:100px'};margin-top:4px;font-weight:bold;font-size:${isThermal ? font.total : 12}px;color:${balance > 0 ? '#16a34a' : '#dc2626'}"><span>${receiptBalanceLabel(balance)}</span><span>${formatMoney(Math.abs(balance))}</span></div>`
    : '';
  const totalCreditHtml = showPreviousCredit.value && receiptTotalCredit.value > 0
    ? `<div style="display:flex;justify-content:${isThermal ? 'space-between' : 'flex-end'};${isThermal ? '' : 'gap:100px'};margin-top:4px;font-weight:bold;font-size:${isThermal ? font.total : 12}px;color:#dc2626"><span>${t('sale.totalCredit')}:</span><span>${formatMoney(receiptTotalCredit.value)}</span></div>`
    : '';

  if (isThermal) {
    return `
      <div style="width:76mm;padding:5px;font-family:Pyidaungsu,sans-serif;color:black;line-height:1.4;background:white">
        <div style="text-align:center;margin-bottom:5px">
          <h2 style="font-size:${font.h2}px;margin:0;text-transform:uppercase;font-weight:bold">${shopInfo.value.ShopName || ''}</h2>
          <p style="margin:2px 0;font-weight:bold">(${receiptBranchName.value || ''})</p>
          ${receiptContact.value.Address ? `<p style="margin:2px 0;font-size:${font.small}px;white-space:pre-line">${receiptContact.value.Address}</p>` : ''}
          ${receiptContact.value.Phone ? `<p style="margin:2px 0;font-size:${font.small}px">Tel: ${receiptContact.value.Phone}</p>` : ''}
        </div>
        <div style="border-bottom:1px dashed black;margin:5px 0"></div>
        <div style="display:flex;justify-content:space-between;font-size:${font.small}px">
          <span>No: ${lastSaleNo.value || ''}</span><span>${lastSaleDate.value || ''}</span>
        </div>
        <div style="display:flex;justify-content:space-between;font-size:${font.small}px;margin-top:2px">
          <span>Cashier: ${cashierName.value || 'Admin'}</span>
          ${data.customerName ? `<span>${data.customerName}</span>` : ''}
        </div>
        <div style="border-bottom:1px dashed black;margin:5px 0"></div>
        <table style="width:100%;border-collapse:collapse;font-size:${font.small}px;line-height:1.25">
          <thead><tr style="border-bottom:1px solid black">
            <th style="text-align:left;padding:2px">Item</th>
            <th style="text-align:center;padding:2px">Qty</th>
            <th style="text-align:right;padding:2px">Price</th>
            <th style="text-align:right;padding:2px">Total</th>
          </tr></thead>
          <tbody>${itemsHtml}</tbody>
        </table>
        <div style="border-bottom:1px dashed black;margin:5px 0"></div>
        <div style="font-size:${font.small}px">
          <div style="display:flex;justify-content:space-between"><span>Subtotal:</span><span>${formatMoney(receiptSubtotal.value)}</span></div>
          ${data.discount > 0 ? `<div style="display:flex;justify-content:space-between;color:#dc2626"><span>${discountLabel}</span><span>- ${formatMoney(data.discount)}</span></div>` : ''}
          ${data.deliveryFee > 0 ? `<div style="display:flex;justify-content:space-between"><span>Delivery:</span><span>${formatMoney(data.deliveryFee)}</span></div>` : ''}
          <div style="display:flex;justify-content:space-between;font-weight:bold;font-size:${font.total}px;margin-top:4px;padding-top:4px;border-top:1px double black"><span>Total:</span><span>${formatMoney(receiptTotal.value)}</span></div>
          ${paymentsHtml}
          ${balanceHtml}
          ${totalCreditHtml}
        </div>
        ${data.voucherNote && data.voucherNote !== 'null' ? `<div style="margin-top:8px;font-size:${font.tiny}px;font-style:italic;text-align:center">Note: ${data.voucherNote}</div>` : ''}
        ${footerHtml}
      </div>`;
  }

  // A4/A5 format
  return `
    <div style="font-family:Pyidaungsu,sans-serif;color:black;background:white">
      <div style="text-align:center;margin-bottom:10px">
        <h1 style="margin:0;font-size:${font.h1}px;font-weight:bold">${shopInfo.value.ShopName || ''}</h1>
        <p style="margin:2px 0;font-size:${font.small}px">(${receiptBranchName.value || ''})</p>
        ${receiptContact.value.Address ? `<p style="margin:2px 0;font-size:${font.small}px">${receiptContact.value.Address}</p>` : ''}
        ${receiptContact.value.Phone ? `<p style="margin:2px 0;font-size:${font.small}px">Tel: ${receiptContact.value.Phone}</p>` : ''}
      </div>
      <div style="display:flex;justify-content:space-between;margin-bottom:5px;font-size:${font.small}px">
        <span>No: ${lastSaleNo.value || ''}</span><span>${lastSaleDate.value || ''}</span>
      </div>
      <div style="display:flex;justify-content:space-between;margin-bottom:5px;font-size:${font.small}px">
        <span>Cashier: ${cashierName.value || 'Admin'}</span>
        ${data.customerName ? `<span>${data.customerName}</span>` : ''}
      </div>
      <table style="width:100%;border-collapse:collapse;table-layout:fixed;border:1px solid #333">
        <thead>
          <tr style="background-color:#3f3f46">
            <th style="color:white;padding:6px 4px;text-align:left;font-size:12px">Item</th>
            <th style="color:white;padding:6px 4px;text-align:center;font-size:12px;width:60px">Qty</th>
            <th style="color:white;padding:6px 4px;text-align:right;font-size:12px;width:90px">Price</th>
            <th style="color:white;padding:6px 4px;text-align:right;font-size:12px;width:100px">Total</th>
          </tr>
        </thead>
        <tbody>${itemsHtml}</tbody>
      </table>
      <div style="margin-top:10px;font-size:${font.small}px;text-align:right">
        <div style="display:flex;justify-content:flex-end;gap:100px"><span>Subtotal:</span><span>${formatMoney(receiptSubtotal.value)}</span></div>
        ${data.discount > 0 ? `<div style="display:flex;justify-content:flex-end;gap:100px;color:#dc2626"><span>${discountLabel}</span><span>- ${formatMoney(data.discount)}</span></div>` : ''}
        ${data.deliveryFee > 0 ? `<div style="display:flex;justify-content:flex-end;gap:100px"><span>Delivery:</span><span>${formatMoney(data.deliveryFee)}</span></div>` : ''}
        <div style="display:flex;justify-content:flex-end;gap:100px;font-weight:bold;font-size:${font.total}px;margin-top:4px;padding-top:4px;border-top:1px double #333"><span>Total:</span><span>${formatMoney(receiptTotal.value)}</span></div>
        ${paymentsHtml}
        ${balanceHtml}
        ${totalCreditHtml}
      </div>
      ${data.voucherNote && data.voucherNote !== 'null' ? `<div style="margin-top:10px;font-size:${font.small}px;font-style:italic;text-align:center">Note: ${data.voucherNote}</div>` : ''}
      ${footerHtml}
    </div>`;
};

const ANDROID_THERMAL_PRINT_SCALE = 0.82;

const capturePreviewElement = async ({ compactAndroidThermal = false } = {}) => {
  try {
    const html = capturedPrintHtml.value;
    if (!html) {
      console.error('[ShareInvoice] No captured HTML available');
      return null;
    }

    const isThermal = receiptSize.value === '80mm';
    const bodyWidth = isThermal ? '76mm' : (receiptSize.value === 'A5' ? '148mm' : '210mm');
    const bodyHeight = isThermal ? 'auto' : (receiptSize.value === 'A5' ? '210mm' : '297mm');
    const capturePaddingBottom = isThermal ? '18px' : '0';
    const captureWidth = bodyWidth;

    // Build logo HTML
    const hasLogo = shopInfo.value.LogoData &&
      shopInfo.value.LogoData !== '' &&
      shopInfo.value.LogoData !== 'null' &&
      shopInfo.value.LogoData !== null;
    const getLogoPx = (size) => {
      if (size === 'Small') return '50px';
      if (size === 'Medium') return '70px';
      return '100px';
    };
    const logoSizePx = getLogoPx(shopInfo.value.LogoSize || 'Large');
    const logoHtml = hasLogo
      ? `<div style="text-align:center;margin-top:2px;margin-bottom:5px;padding-top:0"><img src="${shopInfo.value.LogoData}" style="width:${logoSizePx};height:${logoSizePx};object-fit:contain;border:none"></div>`
      : '';
    const shouldCompactAndroidThermal = compactAndroidThermal && androidBridge.isAndroid() && isThermal;
    const captureContent = shouldCompactAndroidThermal
      ? '<div class="android-thermal-print-scale">' + logoHtml + html + '</div>'
      : logoHtml + html;

    // Build complete document exactly like print
    const docHtml = '<!DOCTYPE html><html><head><meta charset="utf-8"><style>' +
      '@font-face{font-family:"Pyidaungsu";src:local("Pyidaungsu")}' +
      'html,body{margin:0;padding:0;background:white}' +
      'body{font-family:"Pyidaungsu","Noto Sans Myanmar","Myanmar Text",sans-serif;line-height:1.45;-webkit-font-smoothing:antialiased;text-rendering:optimizeLegibility}' +
      '#capture-root{width:' + captureWidth + ';min-height:' + bodyHeight + ';margin:0 auto;padding-bottom:' + capturePaddingBottom + ';background:white}' +
      '#capture-root > div{margin-left:auto!important;margin-right:auto!important}' +
      '*{box-sizing:border-box;word-break:normal;overflow-wrap:anywhere}' +
      '.android-thermal-print-scale{width:calc(100% / ' + ANDROID_THERMAL_PRINT_SCALE + ');transform:scale(' + ANDROID_THERMAL_PRINT_SCALE + ');transform-origin:top left;background:white}' +
      'p{line-height:1.45!important}' +
      '#print-area{line-height:1.45!important;padding-bottom:12px!important}' +
      '#print-area>div{overflow:visible!important}' +
      '#print-area div[style*="border-bottom"]{margin-top:8px!important;margin-bottom:8px!important}' +
      '#print-area div[style*="justify-content:space-between"]{line-height:1.55!important;align-items:flex-start!important;gap:8px!important}' +
      '#print-area div[style*="justify-content:space-between"] span{line-height:1.55!important;min-height:1.55em!important}' +
      '#print-area div[style*="text-align: center"]{line-height:1.45!important}' +
      'table{border-collapse:collapse;table-layout:fixed;width:100%}' +
      'th,td{vertical-align:top}' +
      '#print-area thead tr{background:white!important}' +
      '#print-area thead th{height:34px!important;white-space:nowrap;color:#111!important;-webkit-text-fill-color:#111!important;padding:0!important;vertical-align:middle!important}' +
      '#print-area thead th div{height:34px!important;line-height:1.2!important;display:flex!important;align-items:center!important;font-weight:700!important;transform:none!important}' +
      '#print-area tbody td{padding-top:2px!important;padding-bottom:4px!important;line-height:1.18!important}' +
      '#print-area tbody td div{line-height:1.18!important}' +
      '#print-area tbody tr:last-child td{padding-bottom:8px!important}' +
      '#print-area div:last-child{margin-bottom:8px!important;padding-bottom:8px!important}' +
      '#print-area-a4 thead tr{background-color:#3f3f46!important}' +
      '#print-area-a4 th{color:white!important}' +
      '</style></head><body><div id="capture-root">' + captureContent + '</div></body></html>';

    // Use an iframe for proper isolated rendering
    const iframe = document.createElement('iframe');
    iframe.style.position = 'fixed';
    iframe.style.left = '0';
    iframe.style.top = '0';
    iframe.style.width = captureWidth;
    iframe.style.height = '1px';
    iframe.style.zIndex = '-1';
    iframe.style.border = 'none';
    iframe.style.background = 'white';
    document.body.appendChild(iframe);

    // Write content and wait for render
    const iframeDoc = iframe.contentDocument || iframe.contentWindow.document;
    iframeDoc.open();
    iframeDoc.write(docHtml);
    iframeDoc.close();

    // Wait for images/fonts to load
    await new Promise((resolve) => {
      iframe.onload = () => setTimeout(resolve, 400);
      // Fallback if onload doesn't fire
      setTimeout(resolve, 600);
    });

    const captureRoot = iframeDoc.getElementById('capture-root');
    const targetEl = captureRoot || iframeDoc.body;
    const captureHeight = Math.max(targetEl.scrollHeight, targetEl.offsetHeight);
    const { default: html2canvas } = await import('html2canvas');
    const canvas = await html2canvas(targetEl, {
      scale: androidBridge.isAndroid() && isThermal ? 2 : Math.max(3, window.devicePixelRatio || 2),
      backgroundColor: '#ffffff',
      useCORS: true,
      allowTaint: true,
      logging: false,
      foreignObjectRendering: false,
      width: targetEl.scrollWidth,
      height: captureHeight,
    });

    document.body.removeChild(iframe);

    return new Promise((resolve) => {
      canvas.toBlob((blob) => resolve(blob), 'image/png', 1.0);
    });
  } catch (err) {
    console.error('[ShareInvoice] Capture failed:', err);
    return null;
  }
};

const saveBlobToFile = async (blob) => {
  // Try File System Access API (modern browsers)
  if (window.showSaveFilePicker) {
    try {
      const handle = await window.showSaveFilePicker({
        suggestedName: `invoice-${lastSaleNo.value.replace(/[^a-zA-Z0-9]/g, '_')}.png`,
        types: [{ description: 'PNG Image', accept: { 'image/png': ['.png'] } }],
      });
      const writable = await handle.createWritable();
      await writable.write(blob);
      await writable.close();
      addToast(t('sale.invoiceSaved') || 'Invoice saved successfully.', 'success');
      return;
    } catch (err) {
      if (err.name === 'AbortError') return; // User cancelled
    }
  }

  // Fallback: download via anchor
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `invoice-${lastSaleNo.value.replace(/[^a-zA-Z0-9]/g, '_')}.png`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
  addToast(t('sale.invoiceSaved') || 'Invoice saved successfully.', 'success');
};

const printFromPreview = async () => {
  if (saveInvoiceAsImage.value) {
    const blob = await capturePreviewElement();
    if (blob) {
      await saveBlobToFile(blob);
    } else {
      addToast(t('history.shareCaptureError') || 'Could not capture invoice image.', 'warning');
    }
  }

  const isThermal = receiptSize.value === '80mm';
  const pageSize = isThermal ? '80mm auto' : (receiptSize.value === 'A5' ? '148mm 210mm' : '210mm 297mm');
  const bodyWidth = isThermal ? '76mm' : (receiptSize.value === 'A5' ? '148mm' : '210mm');

  // Build logo HTML
  const hasLogo = shopInfo.value.LogoData &&
    shopInfo.value.LogoData !== '' &&
    shopInfo.value.LogoData !== 'null' &&
    shopInfo.value.LogoData !== null;
  const getLogoPx = (size) => {
    if (size === 'Small') return '50px';
    if (size === 'Medium') return '70px';
    return '100px';
  };
  const logoSizePx = getLogoPx(shopInfo.value.LogoSize || 'Large');
  const logoHtml = hasLogo
    ? `<div style="text-align:center;margin-top:2px;margin-bottom:5px;padding-top:0"><img src="${shopInfo.value.LogoData}" style="width:${logoSizePx};height:${logoSizePx};object-fit:contain;border:none"></div>`
    : '';

  // Use the captured print-area HTML (captured before cart was cleared)
  const content = capturedPrintHtml.value || buildInvoiceHtml(receiptSize.value);
  const fullPrintHtml = logoHtml + content;

  if (androidBridge.isAndroid()) {
    if (isThermal) {
      const blob = await capturePreviewElement({ compactAndroidThermal: true });
      if (blob) {
        const imageData = await blobToDataUrl(blob);
        const imagePrintResult = androidBridge.printImage(imageData);
        if (imagePrintResult?.ok) {
          addToast(imagePrintResult.message || t('history.printPopupOpened') || 'Receipt sent to printer.', 'success');
          return;
        }
        console.warn('[PrintInvoice] Android image print failed:', imagePrintResult?.message || imagePrintResult);
      }

      const receiptText = capturedReceiptText.value || buildReceiptText(lastSaleNo.value, { width: 38 });
      await printWithAndroidIfPossible(receiptText, true);
      return;
    }

    const htmlPrintResult = androidBridge.printHtml(fullPrintHtml, 'Invoice - ' + (lastSaleNo.value || ''));
    if (htmlPrintResult?.ok) {
      addToast(t('history.printPopupOpened') || 'Print preview opened.', 'success');
      return;
    }
  }

  const result = openPrintWindow({
    title: 'Invoice - ' + (lastSaleNo.value || ''),
    pageSize,
    bodyWidth,
    bodyPadding: isThermal ? '2mm' : '0',
    html: fullPrintHtml,
    autoPrint: !isMobileBrowser(),
    extraCss:
      '#print-area-a4 thead tr{background-color:#3f3f46!important}' +
      '#print-area-a4 th{color:white!important;-webkit-print-color-adjust:exact}' +
      '#print-area thead th{color:#111!important;-webkit-text-fill-color:#111!important;opacity:1!important}'
  });

  if (!result.ok) {
    addToast(t('sale.popupBlocked') || 'Popup blocked. Please allow popups to print.', 'warning');
    if (isMobileBrowser()) {
      addToast(t('sale.androidPrintServiceHint') || 'Android: enable a Print Service in system settings.', 'info');
    }
  }
};

const closeInvoicePreview = () => {
  showInvoicePreview.value = false;
};

const onSaleKeydown = (e) => {
  if (e.key === 'F12') {
    e.preventDefault();
    processCheckout();
  }
};

const printReceipt = async (htmlContent = null, preOpenedWindow = null) => {
  await nextTick();
  const isThermal = receiptSize.value === '80mm';
  const elementId = isThermal ? 'print-area' : 'print-area-a4';
  const printArea = document.getElementById(elementId);

  if (!printArea && !htmlContent) return;

  const printWindow = preOpenedWindow || window.open('', '_blank', 'width=900,height=800');
  if (!printWindow) {
    addToast(t('sale.popupBlocked') || 'Popup blocked. Please allow popups to print.', 'warning');
    if (isMobileBrowser()) {
      addToast(t('sale.androidPrintServiceHint') || 'Android: enable a Print Service (Mopria or printer brand service) in system settings.', 'info');
    }
    return;
  }
  let pageSize = isThermal ? '80mm auto' : (receiptSize.value === 'A5' ? '148mm 210mm' : '210mm 297mm');
  let bodyWidth = isThermal ? '76mm' : (receiptSize.value === 'A5' ? '148mm' : '210mm');

  // ပုံစာသား (Base64) တကယ်ရှိမရှိ သေချာစစ်ဆေးခြင်း
  const hasLogo = shopInfo.value.LogoData &&
    shopInfo.value.LogoData !== '' &&
    shopInfo.value.LogoData !== 'null' &&
    shopInfo.value.LogoData !== null;

  const getLogoPx = (size) => {
    if (size === 'Small') return '50px';
    if (size === 'Medium') return '70px';
    return '100px';
  };

  const logoSizePx = getLogoPx(shopInfo.value.LogoSize || 'Large');

  const logoHtml = hasLogo
    ? `<div style="text-align: center; margin-top: 2px; margin-bottom: 5px; padding-top: 0;">
         <img src="${shopInfo.value.LogoData}" 
              style="width: ${logoSizePx}; height: ${logoSizePx}; object-fit: contain; border: none;">
       </div>`
    : ''; // ပုံမရှိရင် <div> ရော <img> ရော လုံးဝ မထွက်လာစေရပါ

  printWindow.document.write(`
    <html>
      <head>
        <title>Invoice - ${lastSaleNo.value}</title>
        <style>
          @font-face { font-family: 'Pyidaungsu'; src: local('Pyidaungsu'); }
          @page { size: ${pageSize}; margin: 0; }
          body { 
            margin: 0; 
            padding: ${isThermal ? '2mm' : '0'}; 
            width: ${bodyWidth}; 
            font-family: 'Pyidaungsu', sans-serif; 
            /* ✅ အရောင်များ အတိအကျ ထွက်စေရန် */
            -webkit-print-color-adjust: exact !important; 
            print-color-adjust: exact !important; 
          }
          * { box-sizing: border-box; color: black !important; }
          
          /* --- A4/A5 Header Styling (Forced) --- */
          #print-area-a4 thead tr { background-color: #3f3f46 !important; }
          #print-area-a4 th { color: white !important; -webkit-print-color-adjust: exact; }
          
          /* --- Misc --- */
          table { border-collapse: collapse; }
        </style>
      </head>
      <body>
      ${logoHtml} ${htmlContent || printArea.innerHTML}
        <script>
          (function () {
            var ua = navigator.userAgent || '';
            var isMobile = /Android|iPhone|iPad|iPod/i.test(ua);

            window.onload = function () {
              if (isMobile) {
                setTimeout(function () { window.print(); }, 150);
                return;
              }
              setTimeout(function () { window.print(); }, 200);
            };
          })();
        <\/script>
      </body>
    </html>
  `);
  printWindow.document.close();
};

const confirmSale = async () => {
  if (cart.value.length === 0 || isProcessing.value) return;

  isProcessing.value = true;
  try {
    if (!validateCartStock()) return;

    await ensureCustomerForOrder({ requireCustomer: false });
    if (totalPaid.value < totalAmount.value && !saleForm.value.CustomerId) {
      addToast(t('sale.customerSelection') || 'Please select or create a customer for credit sale.', 'warning');
      return;
    }

    // ၁။ Items အားလုံး၏ Gross Total (ပစ္စည်းဖိုး သီးသန့်) ကို တွက်ချက်ခြင်း
    const itemsSubTotal = cart.value.reduce((sum, i) => {
      return i.isFOC ? sum : sum + (i.Quantity * i.SelectedPrice);
    }, 0);

    // ၂။ Payload ကို SaleEntryModel နှင့် အတိအကျ ကိုက်ညီအောင် တည်ဆောက်ခြင်း
    const payload = {
      BranchId: parseInt(localStorage.getItem('BranchId') || 1),
      CustomerId: saleForm.value.CustomerId,
      CustomerName: saleForm.value.CustomerName,
      UserId: parseInt(localStorage.getItem('UserId') || 1),
      ClientSaleId: createClientSaleId(),
      SaleDate: saleForm.value.SaleDate
        ? saleForm.value.SaleDate + 'T' + new Date().toTimeString().split(' ')[0]
        : new Date().toLocaleString('sv-SE').replace(' ', 'T'),

      TotalAmount: itemsSubTotal, // ပစ္စည်းဖိုး သီးသန့်
      DiscountAmount: saleForm.value.DiscountAmount || 0,
      DiscountType: voucherDiscountType.value,
      DiscountInput: voucherDiscountInput.value,
      DeliveryFee: saleForm.value.DeliveryFee || 0,
      PaidAmount: totalPaid.value,

      // PaymentMethod ကို Split သို့မဟုတ် ပထမဆုံး method အဖြစ် သတ်မှတ်ခြင်း
      PaymentMethod: paymentDetails.value.length > 1 ? 'Split' : paymentDetails.value[0].method,
      ReferenceNo: "", // လိုအပ်လျှင် Form မှ ယူနိုင်သည်
      Note: saleForm.value.Note || "",
      VoucherNote: saleForm.value.Note || "",
      SaleType: totalPaid.value < totalAmount.value ? 'Credit' : 'Normal',

      // Items Mapping (Product, Service, Bundle ခွဲခြားခြင်း)
      Items: cart.value.map(i => {
        let m = 1;
        if (i.ItemType === 'Product' || !i.ItemType) {
          const conv = i.Conversions?.find(c => (c.UnitId || c.unitId) == i.UnitId);
          m = conv ? (conv.Multiplier || conv.multiplier) : 1;
        }

        return {
          ItemType: i.ItemType || 'Product',
          ProductId: (i.ItemType === 'Product' || !i.ItemType) ? i.ProductId : null,
          ServiceId: i.ItemType === 'Service' ? i.ServiceId : null,
          BundleId: i.ItemType === 'Bundle' ? i.BundleId : null,
          UnitId: i.UnitId || null,
          Quantity: i.Quantity,
          Multiplier: m,
          UnitPrice: i.SelectedPrice,
          DiscountAmount: i.isFOC ? 0 : (i.DiscountAmount || 0),
          IsFOC: i.isFOC,
          CostPrice: i.CostPrice || 0, // အရင်းဈေး ပါဝင်စေရန်
          SubTotal: i.isFOC ? 0 : (i.Quantity * i.SelectedPrice) - (i.DiscountAmount || 0),
          Note: i.Note || ""
        };
      }),

      // ✅ Payments Mapping (Backend မှ PaymentDetailModel အတွက်)
      Payments: paymentDetails.value.map(p => ({
        PaymentMethod: p.method,
        Amount: toNonNegativeAmount(p.amount)
      }))
    };

    let sId = null;
    let offlineLocalId = null;
    let offlineVoucherNo = null;
    let savedOffline = false;

    const saveOfflineSale = () => {
      offlineVoucherNo = createOfflineVoucherNo();
      const offlineResult = androidBridge.saveSale({
        ...payload,
        LocalVoucherNo: offlineVoucherNo,
        LocalCreatedAt: new Date().toISOString(),
        LocalBranchName: branchName.value,
        LocalCashierName: localStorage.getItem('Username') || 'Admin'
      });

      if (!offlineResult?.ok) {
        throw new Error(offlineResult?.message || 'Offline sale could not be saved.');
      }

      offlineLocalId = offlineResult.localId;
      savedOffline = true;
      applyLocalStockDeduction(payload);
      startPendingSaleSyncTimer();
      window.setTimeout(syncPendingAndroidSales, 1000);
      window.setTimeout(syncPendingAndroidSales, 5000);
    };

    try {
      if (androidBridge.isAvailable() && !androidBridge.isOnline()) {
        saveOfflineSale();
      } else {
        const res = await api.post('/Sales/create-sale', payload, { timeout: 7000 });
        sId = res.data.SaleId || res.data.saleId;
      }
    } catch (saleErr) {
      if (!androidBridge.isAvailable() || !isNetworkSaleFailure(saleErr)) {
        throw saleErr;
      }

      saveOfflineSale();
    }

    // ၃။ Voucher Information Setup
    lastSaleNo.value = sId ? "SA" + String(sId).padStart(7, '0') : String(offlineVoucherNo || offlineLocalId || 'OFFLINE');
    const now = new Date();
    lastSaleDate.value = now.toLocaleString('en-US', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false }).replace(',', '');
    cashierName.value = localStorage.getItem('Username') || 'Admin';
    ensureReceiptSettingsForPreview();

    invoicePreviewData.value = {
      items: [...cart.value.map(i => ({ ...i }))],
      payments: paymentDetails.value.map(p => ({
        ...p,
        amount: toNonNegativeAmount(p.amount)
      })),
      balance: totalPaid.value - totalAmount.value,
      discount: saleForm.value.DiscountAmount || 0,
      discountType: voucherDiscountType.value,
      discountInput: voucherDiscountInput.value,
      deliveryFee: saleForm.value.DeliveryFee || 0,
      voucherNote: saleForm.value.Note || '',
      customerName: saleForm.value.CustomerName || t('common.walkInCustomer'),
      totalCredit: toNonNegativeAmount(selectedCustomerObj.value?.currentBalance ?? selectedCustomerObj.value?.CurrentBalance)
        + Math.max(0, totalAmount.value - totalPaid.value),
      branchName: receiptBranchName.value,
    };

    // ၄။ Capture print-area HTML BEFORE clearing cart
    await nextTick();
    const isThermal = receiptSize.value === '80mm';
    const elementId = isThermal ? 'print-area' : 'print-area-a4';
    const printEl = document.getElementById(elementId);
    if (printEl) {
      const origDisplay = printEl.style.display;
      printEl.style.display = 'block';
      capturedPrintHtml.value = printEl.innerHTML;
      printEl.style.display = origDisplay;
    }

    if (!capturedPrintHtml.value) {
      capturedPrintHtml.value = buildInvoiceHtml(receiptSize.value);
    }
    capturedReceiptText.value = buildReceiptText(lastSaleNo.value, {
      width: androidBridge.isAndroid() ? 38 : 42,
      items: cart.value,
      payments: receiptData.value.payments,
      discountAmount: saleForm.value.DiscountAmount || 0,
      discountType: voucherDiscountType.value,
      discountInput: voucherDiscountInput.value,
      deliveryFee: saleForm.value.DeliveryFee || 0,
      voucherNote: saleForm.value.Note || '',
      totalAmount: totalAmount.value,
      paidAmount: totalPaid.value,
      totalCredit: invoicePreviewData.value.totalCredit,
      showPreviousCredit: showPreviousCredit.value,
      totalQty: totalQty.value
    });

    // ၆။ CLEAR UI STATE IMMEDIATELY
    addToast(savedOffline ? (t('sale.offlineSaleSaved') || 'Offline sale saved. It will sync when internet returns.') : t('sale.saleSuccess'), savedOffline ? "info" : "success");
    if (!savedOffline) {
      await clearActiveHeldSaleAfterCommit();
    }
    showPaymentModal.value = false;
    cart.value = [];
    saleForm.value.DiscountAmount = 0;
    voucherDiscountType.value = 'amount';
    voucherDiscountInput.value = 0;
    saleForm.value.DeliveryFee = 0;
    saleForm.value.Note = '';
    resetCustomerSelection();
    paymentDetails.value = [{ method: availableMethods.value[0], amount: 0 }];
    activeHeldSaleId.value = null;

    // ၆။ Sync latest data from server (Update stock quantities)
    setTimeout(async () => {
      if (!savedOffline) {
        await fetchAllData();
      }
    }, 500);

    // ၇။ Show Invoice Preview Modal (with Print & Share buttons)
    await nextTick();
    showInvoicePreview.value = true;

  } catch (err) {
    console.error("Sale Error:", err);
    const errorMsg = getApiErrorMessage(err, "အရောင်းသိမ်းမရပါ");
    addToast("Error: " + errorMsg, "error");
  } finally {
    isProcessing.value = false;
  }
};

const confirmPreorder = async () => {
  if (cart.value.length === 0 || isProcessing.value) return;

  isProcessing.value = true;
  try {
    if (!validateCartStock()) return;
    const ensuredId = await ensureCustomerForOrder({ requireCustomer: true });
    if (!ensuredId) return;

    const itemsSubTotal = cart.value.reduce((sum, i) => {
      return i.isFOC ? sum : sum + (i.Quantity * i.SelectedPrice);
    }, 0);

    const payload = {
      BranchId: parseInt(localStorage.getItem('BranchId') || 1),
      CustomerId: saleForm.value.CustomerId,
      CustomerName: saleForm.value.CustomerName,
      UserId: parseInt(localStorage.getItem('UserId') || 1),
      SaleDate: saleForm.value.SaleDate
        ? saleForm.value.SaleDate + 'T' + new Date().toTimeString().split(' ')[0]
        : new Date().toLocaleString('sv-SE').replace(' ', 'T'),

      TotalAmount: itemsSubTotal,
      DiscountAmount: saleForm.value.DiscountAmount || 0,
      DiscountType: voucherDiscountType.value,
      DiscountInput: voucherDiscountInput.value,
      DeliveryFee: saleForm.value.DeliveryFee || 0,
      PaidAmount: totalPaid.value,

      PaymentMethod: paymentDetails.value.length > 1 ? 'Split' : paymentDetails.value[0].method,
      ReferenceNo: "",
      Note: saleForm.value.Note || "",
      VoucherNote: saleForm.value.Note || "",
      SaleType: totalPaid.value < totalAmount.value ? 'Credit' : 'Normal',

      Items: cart.value.map(i => {
        let m = 1;
        if (i.ItemType === 'Product' || !i.ItemType) {
          const conv = i.Conversions?.find(c => (c.UnitId || c.unitId) == i.UnitId);
          m = conv ? (conv.Multiplier || conv.multiplier) : 1;
        }

        return {
          ItemType: i.ItemType || 'Product',
          ProductId: (i.ItemType === 'Product' || !i.ItemType) ? i.ProductId : null,
          ServiceId: i.ItemType === 'Service' ? i.ServiceId : null,
          BundleId: i.ItemType === 'Bundle' ? i.BundleId : null,
          UnitId: i.UnitId || null,
          Quantity: i.Quantity,
          Multiplier: m,
          UnitPrice: i.SelectedPrice,
          DiscountAmount: i.isFOC ? 0 : (i.DiscountAmount || 0),
          IsFOC: i.isFOC,
          CostPrice: i.CostPrice || 0,
          SubTotal: i.isFOC ? 0 : (i.Quantity * i.SelectedPrice) - (i.DiscountAmount || 0),
          Note: i.Note || ""
        };
      }),

      Payments: paymentDetails.value.map(p => ({
        PaymentMethod: p.method,
        Amount: toNonNegativeAmount(p.amount)
      }))
    };

    await api.post('/Sales/create-preorder', payload);

    addToast(t('sale.preorderSuccess') || 'Pre-order saved', "success");
    await clearActiveHeldSaleAfterCommit();
    showPaymentModal.value = false;
    cart.value = [];
    saleForm.value.DiscountAmount = 0;
    voucherDiscountType.value = 'amount';
    voucherDiscountInput.value = 0;
    saleForm.value.DeliveryFee = 0;
    saleForm.value.Note = '';
    resetCustomerSelection();
    paymentDetails.value = [{ method: availableMethods.value[0], amount: 0 }];
    activeHeldSaleId.value = null;

    setTimeout(async () => {
      await fetchAllData();
    }, 500);
  } catch (err) {
    console.error("Pre-order Error:", err);
    const errorMsg = getApiErrorMessage(err, "Pre-order failed");
    addToast("Error: " + errorMsg, "error");
  } finally {
    isProcessing.value = false;
  }
};

// onUnitChange Logic
const onUnitChange = (item) => {
  const p = allItems.value.find(x => x.uniqueKey === 'P' + item.ProductId);
  
  // ၁။ လက်ရှိရွေးထားတာ Base Unit ဟုတ်မဟုတ် စစ်သည်
  const isBase = item.UnitId == item.BaseUnitId;

  // ၂။ Conversions array ထဲမှာ ရွေးချယ်လိုက်တဲ့ Unit ID နဲ့ တူတာကို ရှာသည်
  const conv = item.Conversions.find(c =>
    (c.UnitId || c.unitId || c.FromUnitId || c.fromUnitId) == item.UnitId
  );

  // Check product stock limit for the new unit
  if (!isBase && conv && p) {
    const totalRequestedQty = item.Quantity * (conv.Multiplier || conv.multiplier || 1);
    // Find how many base units were already in the cart for this specific item (before unit change)
    // Wait, EffectiveStock already has all items subtracted.
    // If I change the unit of this item, EffectiveStock will update.
    // So let's check if EffectiveStock becomes negative.
    // But since it's computed, it's already updated!
    if (p.EffectiveStock < 0) {
        addToast(t('sale.outOfStock'), "error");
        // Revert to base unit or show warning
        item.UnitId = item.BaseUnitId;
        item.SelectedPrice = item.OriginalBaseSalePrice;
        item.CurrentBasePrice = item.SelectedPrice;
        return;
    }
  }

  if (isBase) {
    // မူရင်းယူနစ်ဆိုလျှင် မူရင်းဈေးကို သုံးသည်
    item.SelectedPrice = item.OriginalBaseSalePrice;
    item.CostPrice = item.OriginalBaseCostPrice;
  } else if (conv) {
    // အခြားယူနစ်ဆိုလျှင် Backend SQL က ပေးလိုက်သော SalePrice ကို ယူသုံးသည်
    item.SelectedPrice = conv.SalePrice || conv.salePrice || conv.BaseSalePrice || conv.baseSalePrice || item.OriginalBaseSalePrice;
    
    // Recalculate cost price based on multiplier
    const multiplier = conv.Multiplier || conv.multiplier || 1;
    item.CostPrice = (item.OriginalBaseCostPrice || 0) * multiplier;
  }

  // စုစုပေါင်းတွက်ချက်ရန်အတွက် လက်ရှိဈေးကို update လုပ်သည်
  item.CurrentBasePrice = item.SelectedPrice;
};

const getAvailablePrices = (item) => {
  const levels = item.Levels || [];
  const byUnit = levels.filter(l => (l.UnitId || l.unitId) == item.UnitId);
  return byUnit.length > 0 ? byUnit : levels;
};

// Sale.vue ရဲ့ fetchSettings ကို အခုလို အစားထိုးပါ
const fetchSettings = async () => {
  try {
    const res = await api.get('/Settings');
    if (res.data) {
      applyShopSettings(res.data);
      receiptSize.value = preferredReceiptSize();
      // ✅ ရလာတဲ့ LogoData (Base64) ကို LocalStorage မှာပါ Update လုပ်ပေးခြင်း
      localStorage.setItem('ShopLogo', res.data.LogoData || '');
      localStorage.setItem('ShopSettings', JSON.stringify(shopInfo.value));
      saveCurrentCatalogForAndroid();
    }
  }
  catch (err) { console.warn("Sale settings unavailable; using cached defaults.", err); }
};

const ensureReceiptSettingsForPreview = () => {
  const offlineCatalog = androidBridge.isAvailable() ? androidBridge.getCatalog() : null;
  if (hasReceiptSettings(offlineCatalog?.shopInfo)) {
    applyShopSettings(offlineCatalog.shopInfo);
  }

  if (!shopInfo.value.ShopName && !shopInfo.value.Name) {
    shopInfo.value = {
      ...shopInfo.value,
      ShopName: localStorage.getItem('ShopName') || localStorage.getItem('shopName') || 'Versa POS'
    };
  }
};

onMounted(async () => {
  loadHeldSales();
  // fetchProducts ကို သီးသန့်ခေါ်ပြီး ကျန်တာတွေကို Promise.all ဖြင့် ခေါ်ပါမည်
  await fetchAllData();

  try {
    const [c, cust, u] = await Promise.all([
      api.get('/categories'),
      api.get('/customers'),
      api.get('/units')
    ]);

    categories.value = c.data;
    customers.value = cust.data;
    units.value = u.data;
    saveCurrentCatalogForAndroid();
  } catch (err) {
    const offlineCatalog = androidBridge.isAvailable() ? androidBridge.getCatalog() : null;
    customers.value = offlineCatalog?.customers || [];
  }

  fetchSettings();
  syncPendingAndroidSales();
  startPendingSaleSyncTimer();
  if (!navigator.onLine) setOfflineModeNotice();
  window.addEventListener('online', setOnlineModeNotice);
  window.addEventListener('online', syncPendingAndroidSales);
  window.addEventListener('offline', setOfflineModeNotice);
  window.addEventListener('focus', syncPendingAndroidSales);
  document.addEventListener('visibilitychange', syncPendingAndroidSales);

  window.addEventListener('click', closeAllPriceMenus);
  window.addEventListener('keydown', onSaleKeydown);
});

onUnmounted(() => {
  window.removeEventListener('click', closeAllPriceMenus);
  window.removeEventListener('keydown', onSaleKeydown);
  window.removeEventListener('online', setOnlineModeNotice);
  window.removeEventListener('online', syncPendingAndroidSales);
  window.removeEventListener('offline', setOfflineModeNotice);
  window.removeEventListener('focus', syncPendingAndroidSales);
  document.removeEventListener('visibilitychange', syncPendingAndroidSales);
  if (connectionNoticeTimer) {
    window.clearTimeout(connectionNoticeTimer);
    connectionNoticeTimer = null;
  }
  if (pendingSaleSyncTimer) {
    window.clearInterval(pendingSaleSyncTimer);
    pendingSaleSyncTimer = null;
  }
});
</script>


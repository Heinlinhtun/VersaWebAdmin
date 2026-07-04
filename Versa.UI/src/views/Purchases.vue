<template>
  <div class="px-3 sm:px-5 lg:px-8 pt-2 pb-6 sm:pb-8 bg-gray-50 min-h-screen font-sans">
    <div class="mx-auto">

      <div class="grid grid-cols-12 gap-6">

        <div class="col-span-12 lg:col-span-9 bg-white p-6 rounded-xl shadow-sm border border-gray-100">
          <div class="flex justify-between items-center mb-6">
            <div>
              <h1 class="text-xl font-normal text-gray-800 tracking-tight">{{ tx.purchaseEntry }}</h1>
              <p class="text-[11px] font-normal text-gray-400 uppercase mt-1 tracking-wider flex items-center">
                <span class="w-1.5 h-1.5 bg-blue-600 rounded-full mr-2"></span>
                {{ tx.location }}: {{ currentUser.role === 'Admin' ? tx.allBranches : currentUser.branchName }}
              </p>
            </div>
            <span
              class="bg-blue-50 text-blue-700 px-3 py-1.5 rounded-lg text-[10px] font-normal uppercase border border-blue-100 tracking-tight">
              {{ tx.multiUnitExpirySupport }}
            </span>
          </div>

          <div class="mb-6 grid grid-cols-2 gap-6">
            <div ref="supplierPickerRef" class="space-y-1.5 relative">
              <label class="text-[11px] font-normal text-gray-500 uppercase ml-1 tracking-wide">{{ tx.supplierSource
              }}</label>
              <div class="relative group">
                <input v-model="supplierSearch" @input="handleSupplierSearch" @focus="openSupplierResults"
                  @keydown.down.prevent="moveSupplierDown" @keydown.up.prevent="moveSupplierUp"
                  @keydown.enter.prevent="selectHighlightedSupplier" type="text" :placeholder="tx.selectSupplier"
                  class="w-full p-3 bg-gray-50 border border-gray-200 rounded-xl outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all pl-9" />
                <span class="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400">
                  <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
                  </svg>
                </span>
                <button v-if="supplierSearch" @click="clearSupplierSearch" type="button"
                  class="absolute right-3 top-1/2 -translate-y-1/2 text-slate-300 hover:text-slate-500">x</button>
              </div>
              <div v-if="showSupplierResults && filteredSuppliers.length > 0"
                class="absolute z-[100] left-0 right-0 mt-1 bg-white border border-slate-200 rounded-xl shadow-2xl max-h-64 overflow-y-auto animate-in fade-in slide-in-from-top-2 duration-200">
                <div v-for="(s, index) in filteredSuppliers" :key="s.SupplierId" @click="selectSupplier(s)"
                  :class="['p-3 cursor-pointer border-b border-slate-50 last:border-0 hover:bg-blue-50 transition-colors',
                    supplierHighlightedIndex === index ? 'bg-blue-50' : '']">
                  <p class="text-xs font-normal text-slate-800">{{ s.SupplierName }}</p>
                  <p class="text-[9px] text-slate-400 font-normal uppercase tracking-tighter">
                    {{ s.Phone || s.phone || tx.unknownVendor }}
                  </p>
                </div>
              </div>
            </div>
            <div class="space-y-1.5">
              <label class="text-[11px] font-normal text-gray-500 uppercase ml-1 tracking-wide">{{ tx.purchaseDate
              }}</label>
              <input type="date" v-model="form.PurchaseDate"
                class="w-full p-3 bg-gray-50 border border-gray-200 rounded-xl font-normal text-sm outline-none focus:ring-2 focus:ring-blue-500/20" />
            </div>
          </div>

          <div class="bg-slate-50 p-5 rounded-xl grid grid-cols-12 gap-3 mb-6 border border-slate-200 items-end">
            <div class="col-span-12 md:col-span-4 space-y-1.5 relative">
              <label class="text-[10px] font-normal text-slate-500 uppercase ml-1">{{ tx.searchProduct }}</label>
              <div class="relative group">
                <input v-model="productSearch" @input="handleProductSearch" @keydown.down.prevent="moveSearchDown"
                  @keydown.up.prevent="moveSearchUp" @keydown.enter.prevent="selectHighlightedProduct" type="text"
                  :placeholder="tx.selectProduct"
                  class="w-full p-2.5 bg-white border border-slate-200 rounded-lg font-normal text-sm shadow-sm outline-none focus:ring-2 focus:ring-blue-500/20 pl-9" />
                <span class="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400"><svg class="h-4 w-4" fill="none"
                    stroke="currentColor" viewBox="0 0 24 24">
                    <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
                  </svg></span>
                <button v-if="productSearch" @click="clearSearch"
                  class="absolute right-3 top-1/2 -translate-y-1/2 text-slate-300 hover:text-slate-500">✕</button>
              </div>

              <!-- Search Results Dropdown -->
              <div v-if="showSearchResults && filteredProducts.length > 0"
                class="absolute z-[100] left-0 right-0 mt-1 bg-white border border-slate-200 rounded-xl shadow-2xl max-h-64 overflow-y-auto animate-in fade-in slide-in-from-top-2 duration-200">
                <div v-for="(p, index) in filteredProducts" :key="p.ProductId" @click="selectProduct(p)" :class="['p-3 flex items-center justify-between cursor-pointer border-b border-slate-50 last:border-0 hover:bg-blue-50 transition-colors',
                  highlightedIndex === index ? 'bg-blue-50' : '']">
                  <div class="flex-1">
                    <p class="text-xs font-normal text-slate-800">{{ p.Name }}</p>
                    <p class="text-[9px] text-slate-400 font-normal uppercase tracking-tighter">{{ p.SKU || 'No Barcode'
                      }}</p>
                  </div>
                  <div class="text-right">
                    <p class="text-[10px] font-normal text-blue-600">
                      {{ formatNumber(p.CostPrice ?? p.costPrice ?? 0) }} Ks (Avg)
                    </p>
                    <p v-if="(p.LastPurchasePrice ?? p.lastPurchasePrice) !== undefined && (p.LastPurchasePrice ?? p.lastPurchasePrice) !== null"
                      class="text-[9px] font-normal text-emerald-600 mt-1">
                      {{ formatNumber(p.LastPurchasePrice ?? p.lastPurchasePrice) }} Ks (Latest)
                    </p>
                  </div>
                </div>
              </div>
            </div>

            <div class="col-span-6 md:col-span-2 space-y-1.5">
              <label class="text-[10px] font-normal text-slate-500 uppercase ml-1">{{ tx.unit }}</label>
              <select v-model="selectedUnitId"
                class="w-full p-2.5 bg-white border border-slate-200 rounded-lg font-normal text-sm shadow-sm outline-none">
                <option v-for="u in units" :key="u.UnitId" :value="u.UnitId">{{ u.UnitName }}</option>
              </select>
            </div>

            <div class="col-span-6 md:col-span-2 space-y-1.5">
              <label class="text-[10px] font-normal text-slate-500 uppercase ml-1">{{ tx.expiry }}</label>
              <input v-model="tempExpiry" type="date"
                class="w-full p-2.5 bg-white border border-slate-200 rounded-lg font-normal text-sm shadow-sm" />
            </div>

            <div class="col-span-6 md:col-span-2 space-y-1.5">
              <label class="text-[10px] font-normal text-slate-500 uppercase ml-1">{{ tx.alertDays }}</label>
              <input v-model.number="tempAlertDays" type="number"
                class="w-full p-2.5 bg-white border border-slate-200 rounded-lg font-normal text-sm shadow-sm"
                placeholder="30" />
            </div>

            <div class="col-span-6 md:col-span-2">
              <button @click="addItem"
                class="w-full bg-blue-600 text-white p-2.5 rounded-lg font-normal shadow-md hover:bg-blue-700 active:scale-95 transition-all uppercase text-[11px] tracking-widest">
                + {{ tx.addItem }}
              </button>
            </div>
          </div>

          <div class="overflow-x-auto border border-gray-200 rounded-xl shadow-sm">
            <table class="w-full text-sm">
              <thead class="bg-[#3f3f46] font-normal uppercase text-[10px] text-white tracking-[0.2em]">
                <tr>
                  <th class="p-4 text-left">{{ tx.description }}</th>
                  <th class="p-4 text-center">{{ tx.qty }}</th>
                  <th class="p-4 text-center">{{ tx.foc }}</th>
                  <th class="p-4 text-right">{{ tx.costPrice }}</th>
                  <th class="p-4 text-right bg-blue-800/40">{{ tx.newSale }}</th>
                  <th class="p-4 text-center">{{ tx.expiry }}</th>
                  <th class="p-4 text-right">{{ tx.subtotal }}</th>
                  <th class="p-4 text-center">{{ tx.action }}</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-100">
                <tr v-for="(item, index) in form.Items" :key="index" class="hover:bg-slate-50/50 transition-colors">
                  <td class="p-4 font-normal text-slate-800 text-sm tracking-tight">{{ item.Name }}</td>
                  <td class="p-4 text-center">
                    <div class="flex items-center justify-center gap-2">
                      <input v-model.number="item.Quantity" type="number"
                        class="w-14 p-1.5 bg-gray-50 border border-gray-200 rounded text-center font-normal text-sm" />
                      <span class="text-[9px] font-normal text-slate-400 uppercase tracking-tighter">{{ item.UnitName
                      }}</span>
                    </div>
                  </td>
                  <td class="p-4 text-center">
                    <input v-model.number="item.FOCQuantity" type="number"
                      class="w-14 p-1.5 border border-emerald-200 rounded text-center font-normal text-emerald-600 text-sm outline-none" />
                  </td>
                  <td class="p-4 text-right">
                    <input :value="formatMoneyInput(item.CostPrice)" @input="updateMoneyField(item, 'CostPrice', $event)" type="text" inputmode="decimal"
                      class="w-24 p-1.5 bg-gray-50 border border-gray-200 rounded text-right font-normal text-sm" />
                  </td>
                  <td class="p-4 text-right bg-blue-50/20">
                    <input :value="formatMoneyInput(item.NewSalePrice)" @input="updateMoneyField(item, 'NewSalePrice', $event)" type="text" inputmode="decimal"
                      class="w-24 p-1.5 border border-blue-200 rounded text-right font-normal text-blue-600 text-sm" />
                  </td>
                  <td class="p-4 text-center">
                    <span
                      class="text-[9px] font-normal px-2 py-0.5 bg-orange-50 text-orange-600 rounded border border-orange-100 uppercase italic">
                      {{ item.ExpiryDate || tx.none }}
                    </span>
                  </td>
                  <td class="p-4 text-right font-normal text-slate-900 tabular-nums">
                    {{ formatNumber(item.Quantity * item.CostPrice) }}
                  </td>
                  <td class="p-4 text-center">
                    <button @click="removeItem(index)"
                      class="p-1.5 text-rose-400 hover:text-rose-600 hover:bg-rose-50 rounded transition-all">
                      <svg class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                        <path
                          d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                      </svg>
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <div class="col-span-12 lg:col-span-3">
          <div class="bg-white p-6 rounded-xl shadow-sm border border-gray-100 sticky top-6 space-y-5">

            <div class="text-center pb-4 border-b border-gray-100">
              <p class="text-[10px] font-normal text-gray-400 uppercase tracking-widest mb-1">{{ tx.totalPayable }}</p>
              <p class="text-3xl font-normal text-slate-900 tracking-tighter">
                {{ formatNumber(totalAmount) }}
                <span class="text-xs font-normal text-slate-400">MMK</span>
              </p>
            </div>

            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-gray-400 uppercase ml-1 tracking-widest">{{ tx.paidAmount
              }}</label>
              <input :value="formatMoneyInput(form.PaidAmount)" @input="updateMoneyField(form, 'PaidAmount', $event)" type="text" inputmode="decimal"
                class="w-full p-4 bg-slate-50 border border-slate-200 rounded-xl font-normal text-emerald-600 text-xl outline-none focus:ring-2 focus:ring-emerald-500/20 transition-all" />
            </div>

            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-gray-400 uppercase ml-1 tracking-widest">{{ tx.paymentMethod }}</label>
              <select v-model="form.PaymentMethod" :disabled="Number(form.PaidAmount || 0) <= 0"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl font-normal text-sm outline-none focus:ring-2 focus:ring-blue-500/10 transition-all disabled:opacity-50">
                <option v-for="m in availablePaymentMethods" :key="m" :value="m">{{ m }}</option>
              </select>
              <p v-if="Number(form.PaidAmount || 0) <= 0" class="text-[10px] text-slate-400 ml-1">
                {{ tx.paymentMethodHint }}
              </p>
            </div>

            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-gray-400 uppercase ml-1 tracking-widest">{{ tx.remarks
                }}</label>
              <textarea v-model="form.Remarks" rows="3" :placeholder="tx.notesPlaceholder"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl font-normal text-sm outline-none focus:ring-2 focus:ring-blue-500/10 transition-all resize-none"></textarea>
            </div>

            <button @click="savePurchase"
              class="w-full bg-emerald-600 text-white py-4 rounded-xl font-normal shadow-md hover:bg-emerald-700 transition active:scale-95 uppercase text-xs tracking-widest">
              {{ tx.confirmSave }}
            </button>
            <button @click="saveConsignment"
              class="w-full bg-indigo-600 text-white py-4 rounded-xl font-normal shadow-md hover:bg-indigo-700 transition active:scale-95 uppercase text-xs tracking-widest">
              {{ tx.saveConsignment }}
            </button>
          </div>
        </div>
      </div>

      <div class="mt-4 bg-white p-6 rounded-xl shadow-sm border border-gray-100">
        <div class="flex items-center justify-between mb-6">
          <h3 class="font-normal text-slate-800 uppercase text-sm tracking-tight">{{ tx.recentPurchaseHistory }}</h3>
          <div class="flex items-center gap-4">
            <button @click="handleExportCsv"
              class="text-[10px] font-normal text-blue-600 uppercase hover:underline">EXPORT CSV</button>
            <button @click="navigateToHistory"
              class="text-xs font-normal text-blue-600 hover:text-blue-700 transition-colors uppercase tracking-widest">{{
                tx.viewAll }}</button>
            <button @click="fetchPurchases" class="p-2 text-slate-400 hover:text-blue-600 transition">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path
                  d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"
                  stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
              </svg>
            </button>
          </div>
        </div>

        <div class="overflow-x-auto rounded-xl border border-gray-100">
          <table class="w-full text-sm">
            <thead
              class="bg-[#3f3f46] font-normal uppercase text-[10px] text-white tracking-[0.15em] border-b border-gray-600">
              <tr>
                <th class="p-4 text-left">{{ tx.orderDate }}</th>
                <th class="p-4 text-left">{{ tx.supplier }}</th>
                <th class="p-4 text-right">{{ tx.total }}</th>
                <th class="p-4 text-right">{{ tx.paid }}</th>
                <th class="p-4 text-center">{{ tx.status }}</th>
                <th class="p-4 text-center">{{ tx.action }}</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-100">
              <tr v-for="p in paginatedPurchases" :key="p.POId" class="hover:bg-slate-50 transition-colors">
                <td class="p-4 text-slate-500 font-normal text-xs">{{ new Date(p.OrderDate ||
                  p.orderDate).toLocaleDateString() }}</td>
                <td class="p-4 font-normal text-slate-800">{{ p.SupplierName || tx.unknown }}</td>
                <td class="p-4 text-right font-normal text-sm text-blue-600 tabular-nums">{{ formatNumber(p.TotalAmount) }}</td>
                <td class="p-4 text-right text-emerald-600 font-normal tabular-nums">{{ formatNumber(p.PaidAmount) }}
                </td>
                <td class="p-4 text-center">
                  <span :class="{
                    /* Paid (သို့မဟုတ်) Completed ဆိုလျှင် အစိမ်းရောင် */
                    'bg-emerald-50 text-emerald-600 border-emerald-200': (p.Status || p.status) === 'Paid' || (p.Status || p.status) === 'Completed',

                    /* Partial (တစ်စိတ်တစ်ပိုင်းပေးပြီး) ဆိုလျှင် အဝါရောင် */
                    'bg-amber-50 text-amber-600 border-amber-200': (p.Status || p.status) === 'Partial',

                    /* Unpaid (လုံးဝမပေးရသေး) ဆိုလျှင် အနီရောင် */
                    'bg-rose-50 text-rose-600 border-rose-200': (p.Status || p.status) === 'Unpaid'
                  }"
                    class="px-3 py-1 rounded-lg text-[9px] font-normal uppercase tracking-widest border shadow-sm transition-all inline-block min-w-[70px]">
                    {{ getStatusLabel(p.Status || p.status) }}
                  </span>
                </td>
                <td class="p-4 text-center">
                  <div class="flex items-center justify-center gap-2">
                  <button @click="viewDetail(p.POId || p.poId)"
                    class="p-2 bg-slate-50 text-slate-500 rounded-lg hover:text-blue-600 hover:bg-blue-50 transition-all"
                    :title="tx.detail">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                      <path d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                      <path
                        d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                    </svg>
                  </button>
                  <button @click="openEditPurchaseModal(p)"
                    class="p-2 bg-slate-50 text-slate-500 rounded-lg hover:text-amber-600 hover:bg-amber-50 transition-all"
                    :title="tx.edit">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round"
                        d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5" />
                      <path stroke-linecap="round" stroke-linejoin="round"
                        d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z" />
                    </svg>
                  </button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <div v-if="purchasesList.length > 0"
          class="flex flex-col sm:flex-row justify-between items-center gap-3 px-4 py-4 bg-slate-50 border border-t-0 border-gray-100 rounded-b-xl">
          <div class="text-[10px] font-normal text-slate-400 uppercase tracking-widest">
            {{ tx.recentPurchaseHistory }}: {{ purchasesList.length }}
          </div>
          <div class="flex items-center gap-1.5">
            <button @click="purchaseHistoryPage--" :disabled="purchaseHistoryPage === 1"
              class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">
              {{ tx.prev }}
            </button>
            <span class="px-3 text-xs font-normal text-slate-400">
              {{ tx.page }} {{ purchaseHistoryPage }} / {{ purchaseHistoryTotalPages }}
            </span>
            <button @click="purchaseHistoryPage++" :disabled="purchaseHistoryPage === purchaseHistoryTotalPages"
              class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">
              {{ tx.next }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <div v-if="showEditPurchaseModal"
      class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
      <div class="bg-white w-full max-w-lg rounded-2xl shadow-2xl overflow-hidden border border-slate-200">
        <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
          <div>
            <h3 class="text-lg font-normal uppercase tracking-tight">{{ tx.editPurchase }}</h3>
            <p class="text-blue-300 text-[10px] font-normal uppercase mt-1 tracking-[0.2em]">
              PU-{{ String(editPurchaseForm.POId || '').padStart(7, '0') }}
            </p>
          </div>
          <button @click="closeEditPurchaseModal" class="text-white/60 hover:text-white transition-colors">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        <div class="p-6 space-y-4">
          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ tx.purchaseDate }}</label>
            <input v-model="editPurchaseForm.PurchaseDate" type="date"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-blue-500/20 text-sm">
          </div>
          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ tx.supplier }}</label>
            <select v-model.number="editPurchaseForm.SupplierId"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-blue-500/20 text-sm">
              <option v-for="s in suppliers" :key="s.SupplierId" :value="s.SupplierId">{{ s.SupplierName }}</option>
            </select>
          </div>
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ tx.paidAmount }}</label>
              <input :value="formatMoneyInput(editPurchaseForm.PaidAmount)"
                @input="updateMoneyField(editPurchaseForm, 'PaidAmount', $event)" type="text" inputmode="decimal"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-emerald-500/20 text-sm text-emerald-600">
            </div>
            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{ tx.paymentMethod }}</label>
              <select v-model="editPurchaseForm.PaymentMethod" :disabled="Number(editPurchaseForm.PaidAmount || 0) <= 0"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-blue-500/20 text-sm disabled:opacity-50">
                <option v-for="m in availablePaymentMethods" :key="m" :value="m">{{ m }}</option>
              </select>
            </div>
          </div>
          <div class="flex justify-end gap-3 pt-4">
            <button @click="closeEditPurchaseModal"
              class="px-5 py-3 rounded-xl border border-slate-200 text-slate-500 text-xs uppercase tracking-widest">
              {{ tx.cancel }}
            </button>
            <button @click="savePurchaseHeader"
              class="px-6 py-3 rounded-xl bg-blue-600 text-white text-xs uppercase tracking-widest hover:bg-blue-700">
              {{ tx.saveChanges }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <div v-if="showDetailModal"
      class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
      <div
        class="bg-white w-full max-w-2xl rounded-2xl shadow-2xl overflow-hidden animate-in fade-in zoom-in duration-200 border border-slate-200">

        <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
          <div>
            <h3 class="text-lg font-normal uppercase tracking-tight">{{ tx.purchaseInventoryDetail }}</h3>
            <p class="text-blue-300 text-[10px] font-normal uppercase mt-1 tracking-[0.2em] italic">
              Voucher: PU-{{ String(selectedPurchase.master?.POId || selectedPurchase.master?.poId).padStart(7, '0') }}
            </p>
          </div>
          <button @click="showDetailModal = false" class="text-white/60 hover:text-white transition-colors">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <div class="p-6">
          <div class="overflow-x-auto border border-slate-100 rounded-xl mb-6 shadow-sm">
            <table class="w-full text-left">
              <thead
                class="bg-[#3f3f46] text-[10px] font-normal text-white uppercase tracking-widest border-b border-[#3f3f46]">
                <tr>
                  <th class="p-4">{{ tx.productDescription }}</th>
                  <th class="p-4 text-center">Qty</th>
                  <th class="p-4 text-right">{{ tx.unitCost }}</th>
                  <th class="p-4 text-right">{{ tx.subtotal }}</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-50 text-sm">
                <tr v-for="item in selectedPurchase.items" :key="item.Id || item.id">
                  <td class="p-4 font-normal text-slate-800">
                    {{ item.ProductName || item.productName }}
                    <span class="block text-[10px] font-normal text-slate-400 mt-0.5 uppercase tracking-tighter">
                      Unit: {{ item.UnitName || item.unitName }}
                    </span>
                  </td>
                  <td class="p-4 text-center tabular-nums">
                    <span class="font-normal text-slate-700">{{ item.Quantity || item.quantity }}</span>
                    <span v-if="(item.FOCQuantity || item.focQuantity) > 0"
                      class="block text-[10px] font-normal text-emerald-500 uppercase tracking-tighter">
                      +{{ item.FOCQuantity || item.focQuantity }} {{ tx.focSuffix }}
                    </span>
                  </td>
                  <td class="p-4 text-right text-slate-500 tabular-nums">
                    {{ formatNumber(item.CostPrice || item.costPrice) }}
                  </td>
                  <td class="p-4 text-right font-normal text-slate-900 tabular-nums">
                    {{ formatNumber((item.Quantity || item.quantity) * (item.CostPrice || item.costPrice)) }}
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-8 items-start border-t border-slate-100 pt-6">

            <div class="space-y-4">
              <div>
                <p class="text-[10px] font-normal text-slate-400 uppercase tracking-widest mb-1">{{
                  tx.supplierSourceLabel }}</p>
                <p class="text-sm font-normal text-blue-600 italic">@{{ selectedPurchase.master?.SupplierName ||
                  selectedPurchase.master?.supplierName || tx.unknownVendor }}</p>
              </div>
              <div v-if="selectedPurchase.master?.Remarks || selectedPurchase.master?.remarks"
                class="p-3 bg-amber-50/50 border border-amber-100 rounded-lg">
                <p class="text-[9px] font-normal text-amber-600 uppercase tracking-widest mb-1">{{
                  tx.internalNoteRemarks
                  }}
                </p>
                <p class="text-xs text-slate-600 font-normal leading-relaxed">{{ selectedPurchase.master?.Remarks ||
                  selectedPurchase.master?.remarks }}</p>
              </div>
            </div>

            <div class="bg-slate-50 p-5 rounded-2xl space-y-3 shadow-inner">
              <div class="flex justify-between items-center text-xs">
                <span class="text-slate-400 font-normal uppercase tracking-tighter">{{ tx.netCostLabel }}</span>
                <span class="font-normal text-slate-800 tabular-nums">{{
                  formatNumber(selectedPurchase.master?.TotalAmount
                    || selectedPurchase.master?.totalAmount) }} Ks</span>
              </div>
              <div class="flex justify-between items-center text-xs">
                <span class="text-slate-400 font-normal uppercase tracking-tighter">{{ tx.amountPaidLabel }}</span>
                <span class="font-normal text-emerald-600 tabular-nums">{{
                  formatNumber(selectedPurchase.master?.PaidAmount || selectedPurchase.master?.paidAmount) }} Ks</span>
              </div>

              <div class="flex justify-between items-center pt-3 border-t border-slate-200">
                <span class="text-[10px] font-normal uppercase text-slate-500 tracking-widest">{{ tx.balanceDueLabel
                }}</span>
                <span class="text-xl font-normal tabular-nums"
                  :class="((selectedPurchase.master?.TotalAmount || selectedPurchase.master?.totalAmount) - (selectedPurchase.master?.PaidAmount || selectedPurchase.master?.paidAmount)) > 0 ? 'text-rose-600' : 'text-slate-900'">
                  {{ formatNumber((selectedPurchase.master?.TotalAmount || selectedPurchase.master?.totalAmount) -
                    (selectedPurchase.master?.PaidAmount || selectedPurchase.master?.paidAmount)) }} Ks
                </span>
              </div>
            </div>
          </div>

          <div class="mt-8">
            <button @click="showDetailModal = false"
              class="w-full py-4 bg-[#3f3f46] text-white rounded-xl font-normal uppercase text-xs tracking-[0.2em] shadow-lg hover:bg-black transition active:scale-[0.98]">
              {{ tx.closeDetailView }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { useRouter } from 'vue-router';
import api from '../services/api';
import { useToast } from '../composables/useToast';
import { useI18n } from '../composables/useI18n';
import { useCsvExport } from '../composables/useCsvExport';
const { addToast } = useToast();
const { t } = useI18n();
const { exportToCsv } = useCsvExport();
const router = useRouter();

const suppliers = ref([]);
const products = ref([]);
const units = ref([]);
const selectedProduct = ref(null);
const supplierSearch = ref('');
const filteredSuppliers = ref([]);
const showSupplierResults = ref(false);
const supplierHighlightedIndex = ref(-1);
const supplierPickerRef = ref(null);
const productSearch = ref('');
const filteredProducts = ref([]);
const showSearchResults = ref(false);
const highlightedIndex = ref(-1);

const handleSupplierSearch = () => {
  const query = supplierSearch.value.toLowerCase().trim();
  form.value.SupplierId = 0;

  if (!query) {
    filteredSuppliers.value = suppliers.value.slice(0, 50);
    showSupplierResults.value = filteredSuppliers.value.length > 0;
    supplierHighlightedIndex.value = filteredSuppliers.value.length > 0 ? 0 : -1;
    return;
  }

  filteredSuppliers.value = suppliers.value.filter(s => {
    const name = s.SupplierName || s.supplierName || '';
    const phone = s.Phone || s.phone || '';
    return name.toLowerCase().includes(query) || String(phone).toLowerCase().includes(query);
  }).slice(0, 50);
  showSupplierResults.value = true;
  supplierHighlightedIndex.value = filteredSuppliers.value.length > 0 ? 0 : -1;
};

const openSupplierResults = () => {
  const query = supplierSearch.value.toLowerCase().trim();
  filteredSuppliers.value = (query
    ? suppliers.value.filter(s => {
      const name = s.SupplierName || s.supplierName || '';
      const phone = s.Phone || s.phone || '';
      return name.toLowerCase().includes(query) || String(phone).toLowerCase().includes(query);
    })
    : suppliers.value).slice(0, 50);
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

const selectSupplier = (supplier) => {
  form.value.SupplierId = Number(supplier.SupplierId || supplier.supplierId || 0);
  supplierSearch.value = supplier.SupplierName || supplier.supplierName || '';
  filteredSuppliers.value = [];
  showSupplierResults.value = false;
  supplierHighlightedIndex.value = -1;
};

const clearSupplierSearch = () => {
  supplierSearch.value = '';
  form.value.SupplierId = 0;
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

const handleProductSearch = () => {
  if (!productSearch.value || productSearch.value.length < 1) {
    filteredProducts.value = [];
    showSearchResults.value = false;
    highlightedIndex.value = -1;
    return;
  }

  const query = productSearch.value.toLowerCase().trim();

  // Barcode Lookup (Exact Match)
  const exactMatch = products.value.find(p => p.SKU && p.SKU.toLowerCase() === query);
  if (exactMatch) {
    selectProduct(exactMatch);
    return;
  }

  filteredProducts.value = products.value.filter(p => {
    const name = p.Name || p.name || '';
    const sku = p.SKU || p.Sku || p.sku || '';
    return name.toLowerCase().includes(query) || sku.toLowerCase().includes(query);
  }).slice(0, 50);

  showSearchResults.value = true;
  highlightedIndex.value = 0;
};

const selectProduct = (p) => {
  selectedProduct.value = p;
  productSearch.value = p.Name || p.name;
  showSearchResults.value = false;
  loadUnits();
};

const clearSearch = () => {
  productSearch.value = '';
  selectedProduct.value = null;
  filteredProducts.value = [];
  showSearchResults.value = false;
};

const moveSearchDown = () => {
  if (highlightedIndex.value < filteredProducts.value.length - 1) highlightedIndex.value++;
};

const moveSearchUp = () => {
  if (highlightedIndex.value > 0) highlightedIndex.value--;
};

const selectHighlightedProduct = () => {
  if (highlightedIndex.value >= 0 && highlightedIndex.value < filteredProducts.value.length) {
    selectProduct(filteredProducts.value[highlightedIndex.value]);
  }
};

const selectedUnitId = ref(0);
const tempExpiry = ref('');
const tempAlertDays = ref(30);
const purchasesList = ref([]);
const purchaseHistoryPage = ref(1);
const purchaseHistoryPageSize = 10;
const showDetailModal = ref(false);
const showEditPurchaseModal = ref(false);
const selectedPurchase = ref({ master: {}, items: [] });
const editPurchaseForm = ref({
  POId: 0,
  SupplierId: 0,
  PurchaseDate: '',
  PaidAmount: 0,
  PaymentMethod: 'Cash'
});
const tx = computed(() => ({
  purchaseEntry: t('purchases.purchaseEntry'),
  location: t('purchases.location'),
  allBranches: t('purchases.allBranches'),
  supplierSource: t('purchases.supplierSource'),
  selectSupplier: t('purchases.selectSupplier'),
  purchaseDate: t('purchases.purchaseDate'),
  searchProduct: t('purchases.searchProduct'),
  selectProduct: t('purchases.selectProduct'),
  unit: t('purchases.unit'),
  expiry: t('purchases.expiry'),
  alertDays: t('purchases.alertDays'),
  addItem: t('purchases.addItem'),
  multiUnitExpirySupport: t('purchases.multiUnitExpirySupport'),
  description: t('purchases.description'),
  qty: t('purchases.qty'),
  foc: t('purchases.foc'),
  costPrice: t('purchases.costPrice'),
  newSale: t('purchases.newSale'),
  subtotal: t('purchases.subtotal'),
  action: t('purchases.action'),
  totalPayable: t('purchases.totalPayable'),
  paidAmount: t('purchases.paidAmount'),
  paymentMethod: t('purchases.paymentMethod'),
  paymentMethodHint: t('purchases.paymentMethodHint'),
  notesPlaceholder: t('purchases.notesPlaceholder'),
  remarks: t('purchases.remarks'),
  confirmSave: t('purchases.confirmSave'),
  recentPurchaseHistory: t('purchases.recentPurchaseHistory'),
  orderDate: t('purchases.orderDate'),
  supplier: t('purchases.supplier'),
  total: t('purchases.total'),
  paid: t('purchases.paid'),
  status: t('purchases.status'),
  paidStatus: t('purchases.paidStatus'),
  partialStatus: t('purchases.partialStatus'),
  unpaidStatus: t('purchases.unpaidStatus'),
  detail: t('purchases.detail'),
  edit: t('common.edit') || 'Edit',
  editPurchase: t('purchases.editPurchase') || 'Edit Purchase',
  saveChanges: t('common.saveChanges') || 'Save Changes',
  cancel: t('common.cancel') || 'Cancel',
  unknown: t('purchases.unknown'),
  none: t('purchases.none'),
  purchaseInventoryDetail: t('purchases.purchaseInventoryDetail'),
  productDescription: t('purchases.productDescription'),
  unitCost: t('purchases.unitCost'),
  supplierSourceLabel: t('purchases.supplierSourceLabel'),
  internalNoteRemarks: t('purchases.internalNoteRemarks'),
  netCostLabel: t('purchases.netCostLabel'),
  amountPaidLabel: t('purchases.amountPaidLabel'),
  balanceDueLabel: t('purchases.balanceDueLabel'),
  focSuffix: t('purchases.focSuffix'),
  closeDetailView: t('purchases.closeDetailView'),
  unknownVendor: t('purchases.unknownVendor'),
  missingSupplierOrItems: t('purchases.missingSupplierOrItems'),
  selectProductAndUnit: t('purchases.selectProductAndUnit'),
  purchaseRecorded: t('purchases.purchaseRecorded'),
  saveConsignment: t('purchases.saveConsignment'),
  consignmentReceived: t('purchases.consignmentReceived'),
  consignmentSaveFailed: t('purchases.consignmentSaveFailed'),
  saveFailed: t('purchases.saveFailed'),
  loadDetailsFailed: t('purchases.loadDetailsFailed'),
  viewAll: t('purchases.viewAll'),
  page: t('common.page'),
  prev: t('common.prev'),
  next: t('common.next')
}));

const currentUser = ref({
  role: localStorage.getItem('Role') || 'Staff',
  branchId: parseInt(localStorage.getItem('BranchId')) || 1,
  branchName: localStorage.getItem('BranchName') || 'Main Branch'
});

const form = ref({
  SupplierId: 0,
  BranchId: currentUser.value.branchId,
  TotalAmount: 0,
  PaidAmount: 0,
  PaymentMethod: 'Cash',
  Remarks: '',
  PurchaseDate: new Date().toISOString().substr(0, 10),
  Items: []
});

const availablePaymentMethods = ['Cash', 'KPay', 'CB Pay', 'AYA Pay', 'Wave Money', 'Banking'];

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

const totalAmount = computed(() => form.value.Items.reduce((sum, i) => sum + (i.Quantity * i.CostPrice), 0));
const purchaseHistoryTotalPages = computed(() => Math.max(1, Math.ceil(purchasesList.value.length / purchaseHistoryPageSize)));
const paginatedPurchases = computed(() => {
  const start = (purchaseHistoryPage.value - 1) * purchaseHistoryPageSize;
  return purchasesList.value.slice(start, start + purchaseHistoryPageSize);
});

const fetchPurchases = async () => {
  try {
    const res = await api.get('/purchases/history', {
      params: { branchId: currentUser.value.branchId, role: currentUser.value.role }
    });
    purchasesList.value = Array.isArray(res.data) ? res.data : [];
    if (purchaseHistoryPage.value > purchaseHistoryTotalPages.value) {
      purchaseHistoryPage.value = purchaseHistoryTotalPages.value;
    }
  } catch (err) {
    console.error(err);
  }
};

const handleExportCsv = () => {
  const headers = [
    tx.value.orderDate,
    tx.value.supplier,
    tx.value.total,
    tx.value.paid,
    tx.value.status
  ];

  const rows = purchasesList.value.map(p => [
    new Date(p.OrderDate || p.orderDate).toLocaleDateString(),
    p.SupplierName || tx.value.unknown,
    p.TotalAmount,
    p.PaidAmount,
    getStatusLabel(p.Status || p.status)
  ]);

  const dateStr = new Date().toISOString().split('T')[0];
  exportToCsv(`Recent_Purchases_${dateStr}`, headers, rows);
};

const viewDetail = async (id) => {
  if (!id) return;
  try {
    const res = await api.get(`/purchases/details/${id}`);
    selectedPurchase.value = res.data;
    showDetailModal.value = true;
  } catch (err) {
    addToast(tx.value.loadDetailsFailed, "error");
  }
};

const toDateInputValue = (value) => {
  if (!value) return new Date().toISOString().slice(0, 10);
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return new Date().toISOString().slice(0, 10);
  const offsetDate = new Date(date.getTime() - (date.getTimezoneOffset() * 60000));
  return offsetDate.toISOString().slice(0, 10);
};

const openEditPurchaseModal = (purchase) => {
  editPurchaseForm.value = {
    POId: purchase.POId || purchase.poId,
    SupplierId: Number(purchase.SupplierId || purchase.supplierId || 0),
    PurchaseDate: toDateInputValue(purchase.OrderDate || purchase.orderDate),
    PaidAmount: Number(purchase.PaidAmount ?? purchase.paidAmount ?? 0),
    PaymentMethod: purchase.PaymentMethod || purchase.paymentMethod || 'Cash'
  };
  showEditPurchaseModal.value = true;
};

const closeEditPurchaseModal = () => {
  showEditPurchaseModal.value = false;
};

const getApiErrorMessage = (err, fallback) => (
  err.response?.data?.Message
  || err.response?.data?.message
  || err.response?.data?.title
  || fallback
);

const savePurchaseHeader = async () => {
  const payload = {
    SupplierId: Number(editPurchaseForm.value.SupplierId),
    PurchaseDate: editPurchaseForm.value.PurchaseDate,
    PaidAmount: Number(editPurchaseForm.value.PaidAmount || 0),
    PaymentMethod: Number(editPurchaseForm.value.PaidAmount || 0) > 0 ? editPurchaseForm.value.PaymentMethod : ''
  };

  try {
    await api.put(`/purchases/${editPurchaseForm.value.POId}/header`, payload);
    addToast(tx.value.purchaseRecorded || 'Purchase updated', 'success');
    showEditPurchaseModal.value = false;
    fetchPurchases();
  } catch (err) {
    addToast(getApiErrorMessage(err, tx.value.saveFailed), 'error');
  }
};

const loadUnits = async () => {
  if (!selectedProduct.value) return;
  const productId = selectedProduct.value.ProductId || selectedProduct.value.productId;
  try {
    const res = await api.get(`/products/units/${productId}`);
    units.value = res.data;
    if (units.value.length > 0) selectedUnitId.value = units.value[0].UnitId || units.value[0].unitId;
  } catch (err) {
    console.error(err);
  }
};

const addItem = () => {
  if (!selectedProduct.value || !selectedUnitId.value) {
    addToast(tx.value.selectProductAndUnit, "warning");
    return;
  }
  const unit = units.value.find(u => u.UnitId === selectedUnitId.value);
  const p = selectedProduct.value;

  // Detect if it's a scaling unit (not the base unit)
  const isBaseUnit = selectedUnitId.value === (p.UnitId || p.unitId);
  const multiplier = unit.Multiplier || unit.multiplier || 1;

  // Calculate cost based on multiplier for scaling units
  let cost = (p.LastPurchasePrice ?? p.lastPurchasePrice) ?? (p.CostPrice ?? p.costPrice) ?? 0;
  if (!isBaseUnit) {
    cost = cost * multiplier;
  }

  // Use specific sale price for scaling units if available
  let sale = p.SalePrice ?? p.salePrice ?? 0;
  if (!isBaseUnit) {
    sale = unit.SalePrice || unit.salePrice || unit.BaseSalePrice || unit.baseSalePrice || sale;
  }

  form.value.Items.push({
    ProductId: p.ProductId || p.productId,
    UnitId: selectedUnitId.value,
    Name: p.Name || p.name,
    UnitName: unit.UnitName || unit.unitName,
    Quantity: 1,
    FOCQuantity: 0,
    CostPrice: cost,
    NewSalePrice: sale,
    ExpiryDate: tempExpiry.value,
    AlertDays: tempAlertDays.value
  });

  // Clear search for next item
  clearSearch();
};

const isSaving = ref(false);
const savePurchase = async () => {
  if (isSaving.value) return;
  if (form.value.Items.length === 0 || form.value.SupplierId === 0) {
    addToast(tx.value.missingSupplierOrItems, "warning");
    return;
  }
  isSaving.value = true;
  const payload = {
    SupplierId: Number(form.value.SupplierId),
    BranchId: Number(form.value.BranchId),
    TotalAmount: Number(totalAmount.value),
    PaidAmount: Number(form.value.PaidAmount),
    PaymentMethod: form.value.PaymentMethod,
    PurchaseDate: form.value.PurchaseDate
      ? new Date(form.value.PurchaseDate + 'T' + new Date().toTimeString().split(' ')[0]).toISOString()
      : new Date().toISOString(),
    Remarks: form.value.Remarks,
    Items: form.value.Items.map(i => ({
      ProductId: i.ProductId,
      UnitId: i.UnitId,
      Quantity: Number(i.Quantity),
      // ✅ ဒီနေရာမှာ အက္ခရာ အကြီး/အသေး မှန်ပါစေ။ i.FOCQuantity ပါရပါမယ်။
      FOCQuantity: Number(i.FOCQuantity || 0),
      CostPrice: Number(i.CostPrice),
      NewSalePrice: Number(i.NewSalePrice || 0),
      ExpiryDate: i.ExpiryDate || null,
      AlertDays: Number(i.AlertDays || 30)
    }))
  };
  try {
    await api.post('/purchases', payload);
    addToast(tx.value.purchaseRecorded, "success");
    fetchPurchases();
    // Refetch products to get latest average cost and last purchase price
    const pRes = await api.get('/products');
    products.value = pRes.data;

    form.value.Items = [];
    form.value.PaidAmount = 0;
    form.value.PaymentMethod = 'Cash';
    form.value.Remarks = '';
  } catch (err) {
    addToast(tx.value.saveFailed, "error");
  } finally {
    isSaving.value = false;
  }
};

const saveConsignment = async () => {
  if (isSaving.value) return;
  if (form.value.Items.length === 0 || form.value.SupplierId === 0) {
    addToast(tx.value.missingSupplierOrItems, "warning");
    return;
  }

  isSaving.value = true;
  const payload = {
    SupplierId: Number(form.value.SupplierId),
    BranchId: Number(form.value.BranchId),
    TotalAmount: Number(totalAmount.value),
    PaidAmount: 0,
    PaymentMethod: '',
    PurchaseDate: form.value.PurchaseDate
      ? new Date(form.value.PurchaseDate + 'T' + new Date().toTimeString().split(' ')[0]).toISOString()
      : new Date().toISOString(),
    Remarks: form.value.Remarks,
    Items: form.value.Items.map(i => ({
      ProductId: i.ProductId,
      UnitId: i.UnitId,
      Quantity: Number(i.Quantity),
      FOCQuantity: Number(i.FOCQuantity || 0),
      CostPrice: Number(i.CostPrice),
      NewSalePrice: Number(i.NewSalePrice || 0),
      ExpiryDate: i.ExpiryDate || null,
      AlertDays: Number(i.AlertDays || 30)
    }))
  };

  try {
    await api.post('/consignments', payload);
    addToast(tx.value.consignmentReceived, "success");
    const pRes = await api.get('/products');
    products.value = pRes.data;
    form.value.Items = [];
    form.value.PaidAmount = 0;
    form.value.PaymentMethod = 'Cash';
    form.value.Remarks = '';
  } catch (err) {
    addToast(err?.response?.data?.Message || tx.value.consignmentSaveFailed, "error");
  } finally {
    isSaving.value = false;
  }
};

const removeItem = (index) => form.value.Items.splice(index, 1);
const navigateToHistory = () => {
  router.push('/HistoryCenter');
};
const getStatusLabel = (status) => {
  const normalized = status === 'Completed' ? 'Paid' : (status || 'Unpaid');
  if (normalized === 'Paid') return tx.value.paidStatus;
  if (normalized === 'Partial') return tx.value.partialStatus;
  return tx.value.unpaidStatus;
};
const formatNumber = (val) => new Intl.NumberFormat().format(val || 0);

onMounted(async () => {
  document.addEventListener('mousedown', handleSupplierOutsideClick);
  document.addEventListener('touchstart', handleSupplierOutsideClick);
  try {
    const [sRes, pRes] = await Promise.all([api.get('/suppliers'), api.get('/products')]);
    suppliers.value = sRes.data.map(s => ({
      SupplierId: s.SupplierId ?? s.supplierId,
      SupplierName: s.SupplierName ?? s.supplierName,
      Phone: s.Phone ?? s.phone ?? ''
    }));
    products.value = pRes.data;
    fetchPurchases();
  } catch (err) { console.error(err); }
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
</style>

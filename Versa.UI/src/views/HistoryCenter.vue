<template>
  <div class="px-3 sm:px-5 lg:px-8 pt-2 pb-6 sm:pb-8 bg-[#f8fafc] min-h-screen font-sans">

    <div class="mx-auto mb-8 flex flex-col md:flex-row justify-between items-start md:items-end gap-6">
      <div>
        <h1 class="text-2xl font-normal text-slate-900 tracking-tight">{{ t('history.title') }}</h1>
        <p class="text-[11px] font-normal text-slate-400 uppercase tracking-widest mt-1 flex items-center gap-4">
          <span class="flex items-center">
            <span class="w-2 h-2 bg-blue-500 rounded-full mr-2 shadow-[0_0_8px_rgba(59,130,246,0.5)]"></span>
            {{ t('history.subtitle') }}
          </span>
          <button @click="handleExportCsv" :disabled="isExportingCsv"
            class="text-[10px] font-normal text-blue-600 uppercase hover:underline disabled:opacity-50 disabled:cursor-not-allowed">
            {{ isExportingCsv ? 'EXPORTING...' : 'EXPORT CSV' }}
          </button>
        </p>
      </div>

      <div class="flex flex-wrap gap-3 bg-white p-2.5 rounded-xl shadow-sm border border-slate-200 items-end">
        <div v-if="isAdmin" class="flex flex-col">
          <label class="text-[9px] font-normal uppercase text-slate-400 ml-2 mb-1">{{ t('history.branchLocation')
          }}</label>
          <select v-model="filters.branchId"
            class="p-2 bg-slate-50 border border-slate-200 rounded-lg font-normal text-xs outline-none w-44 focus:ring-2 focus:ring-blue-500/20 transition-all">
            <option :value="null">{{ t('common.allBranches') }}</option>
            <option v-for="b in branches" :key="b.BranchId || b.branchId" :value="b.BranchId || b.branchId">
              {{ b.BranchName || b.branchName }}
            </option>
          </select>
        </div>
        <div class="flex flex-col">
          <label class="text-[9px] font-normal uppercase text-slate-400 ml-2 mb-1">{{ t('history.transactionId')
          }}</label>
          <div class="relative">
            <span class="absolute inset-y-0 left-0 pl-3 flex items-center text-slate-400">
              <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
              </svg>
            </span>
            <input v-model="filters.query" type="text" :placeholder="t('sale.searchPlaceholder')"
              class="p-2 pl-7 bg-slate-50 border border-slate-200 rounded-lg font-normal text-xs outline-none w-40 focus:ring-2 focus:ring-blue-500/20">

          </div>
        </div>
        <div v-if="currentTab === 'sale'" ref="customerFilterRef" class="flex flex-col relative">
          <label class="text-[9px] font-normal uppercase text-slate-400 ml-2 mb-1">{{ t('history.customer') }}</label>
          <div class="relative">
            <span class="absolute inset-y-0 left-0 pl-3 flex items-center text-slate-400">
              <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
              </svg>
            </span>
            <input v-model="filters.customerName" @input="handleCustomerFilterSearch" @focus="openCustomerFilterResults"
              @keydown.down.prevent="moveCustomerFilterDown" @keydown.up.prevent="moveCustomerFilterUp"
              @keydown.enter.prevent="selectHighlightedCustomerFilter" type="text" :placeholder="t('history.customer')"
              class="p-2 pl-7 pr-7 bg-slate-50 border border-slate-200 rounded-lg font-normal text-xs outline-none w-44 focus:ring-2 focus:ring-blue-500/20">
            <button v-if="filters.customerName" @click="clearCustomerFilter" type="button"
              class="absolute right-2 top-1/2 -translate-y-1/2 text-slate-300 hover:text-slate-500">x</button>
          </div>
          <div v-if="showCustomerFilterResults && filteredCustomerOptions.length > 0"
            class="absolute z-[100] left-0 right-0 top-full mt-1 bg-white border border-slate-200 rounded-xl shadow-2xl max-h-64 overflow-y-auto animate-in fade-in slide-in-from-top-2 duration-200">
            <div v-for="(customer, index) in filteredCustomerOptions" :key="customer.customerId || customer.CustomerId || customer.name"
              @click="selectCustomerFilter(customer)"
              :class="['p-3 cursor-pointer border-b border-slate-50 last:border-0 hover:bg-blue-50 transition-colors',
                customerFilterHighlightedIndex === index ? 'bg-blue-50' : '']">
              <p class="text-xs font-normal text-slate-800">{{ customer.name }}</p>
              <p class="text-[9px] text-slate-400 font-normal uppercase tracking-tighter">{{ customer.phone || '-' }}</p>
            </div>
          </div>
        </div>
        <div v-if="currentTab === 'purchase'" ref="supplierFilterRef" class="flex flex-col relative">
          <label class="text-[9px] font-normal uppercase text-slate-400 ml-2 mb-1">{{ t('history.supplierName') }}</label>
          <div class="relative">
            <span class="absolute inset-y-0 left-0 pl-3 flex items-center text-slate-400">
              <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
              </svg>
            </span>
            <input v-model="filters.supplierName" @input="handleSupplierFilterSearch" @focus="openSupplierFilterResults"
              @keydown.down.prevent="moveSupplierFilterDown" @keydown.up.prevent="moveSupplierFilterUp"
              @keydown.enter.prevent="selectHighlightedSupplierFilter" type="text" :placeholder="t('history.supplierName')"
              class="p-2 pl-7 pr-7 bg-slate-50 border border-slate-200 rounded-lg font-normal text-xs outline-none w-44 focus:ring-2 focus:ring-blue-500/20">
            <button v-if="filters.supplierName" @click="clearSupplierFilter" type="button"
              class="absolute right-2 top-1/2 -translate-y-1/2 text-slate-300 hover:text-slate-500">x</button>
          </div>
          <div v-if="showSupplierFilterResults && filteredSupplierOptions.length > 0"
            class="absolute z-[100] left-0 right-0 top-full mt-1 bg-white border border-slate-200 rounded-xl shadow-2xl max-h-64 overflow-y-auto animate-in fade-in slide-in-from-top-2 duration-200">
            <div v-for="(supplier, index) in filteredSupplierOptions" :key="supplier.supplierId || supplier.SupplierId || supplier.name"
              @click="selectSupplierFilter(supplier)"
              :class="['p-3 cursor-pointer border-b border-slate-50 last:border-0 hover:bg-blue-50 transition-colors',
                supplierFilterHighlightedIndex === index ? 'bg-blue-50' : '']">
              <p class="text-xs font-normal text-slate-800">{{ supplier.name }}</p>
              <p class="text-[9px] text-slate-400 font-normal uppercase tracking-tighter">{{ supplier.phone || '-' }}</p>
            </div>
          </div>
        </div>
        <div v-if="currentTab !== 'inventory-ledger'" class="flex gap-2">
          <div class="flex flex-col">
            <label class="text-[9px] font-normal uppercase text-slate-400 ml-2 mb-1">{{ t('history.startDate') }}</label>
            <input v-model="filters.startDate" type="date"
              class="p-2 bg-slate-50 border border-slate-200 rounded-lg font-normal text-xs outline-none w-36">
          </div>
          <div class="flex flex-col">
            <label class="text-[9px] font-normal uppercase text-slate-400 ml-2 mb-1">{{ t('history.endDate') }}</label>
            <input v-model="filters.endDate" type="date"
              class="p-2 bg-slate-50 border border-slate-200 rounded-lg font-normal text-xs outline-none w-36">
          </div>
        </div>
        <div v-if="currentTab === 'sale'" class="flex flex-col">
          <label class="text-[9px] font-normal uppercase text-slate-400 ml-2 mb-1">{{ t('history.status') }}</label>
          <select v-model="filters.orderStatus"
            class="p-2 bg-slate-50 border border-slate-200 rounded-lg font-normal text-xs outline-none w-40 focus:ring-2 focus:ring-blue-500/20 transition-all">
            <option value="">{{ t('common.all') || 'All' }}</option>
            <option value="Completed">Completed</option>
            <option value="Preordered">Preordered</option>
            <option value="Delivering">Delivering</option>
            <option value="Cancelled">Cancelled</option>
          </select>
        </div>
        <button @click="resetAndLoad"
          class="bg-slate-900 text-white px-6 py-2 rounded-lg font-normal uppercase text-[10px] tracking-widest hover:bg-black shadow-md transition-all active:scale-95">
          {{ t('history.filter') }}
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

    <div class="mx-auto bg-white rounded-xl shadow-sm border border-slate-200 table-responsive">
      <table class="w-full text-left border-collapse">
        <thead class="bg-[#3f3f46] text-white">
          <tr class="text-[10px] font-normal uppercase tracking-[0.15em]">
            <template v-if="currentTab === 'sale'">
              <th class="p-5">{{ t('history.invoiceNo') }}</th>
              <th class="p-5">{{ t('history.dateTime') }}</th>
              <th class="p-5">{{ t('history.customer') }}</th>
              <th class="p-5 text-right">{{ t('history.netAmount') }}</th>
              <th class="p-5">{{ t('history.saleType') }}</th>
              <th class="p-5">{{ t('history.status') }}</th>
              <th class="p-5">{{ t('history.notes') }}</th>
              <th class="p-5 text-center">{{ t('history.action') }}</th>
            </template>
            <template v-else-if="currentTab === 'purchase'">
              <th class="p-5">{{ t('history.poNumber') }}</th>
              <th class="p-5">{{ t('history.orderDate') }}</th>
              <th class="p-5">{{ t('history.supplierName') }}</th>
              <th class="p-5">{{ t('history.paymentMethod') || 'Payment' }}</th>
              <th class="p-5 text-right">{{ t('history.totalCost') }}</th>
              <th class="p-5 text-center">{{ t('history.detail') }}</th>
            </template>
            <template v-else-if="currentTab === 'return'">
              <th class="p-5">{{ t('history.dateTime') }}</th>
              <th class="p-5">{{ t('history.voucherNo') || 'Voucher No' }}</th>
              <th class="p-5">{{ t('history.category') }}</th>
              <th class="p-5">{{ t('history.productName') }}</th>
              <th class="p-5 text-center">{{ t('history.qty') }}</th>
              <th class="p-5 text-right">{{ t('history.refund') }}</th>
              <th class="p-5">{{ t('history.reason') }}</th>
            </template>
            <template v-else-if="currentTab === 'adjustment'">
              <th class="p-5">{{ t('history.dateTime') }}</th>
              <th class="p-5">{{ t('history.product') }}</th>
              <th class="p-5">{{ t('history.type') }}</th>
              <th class="p-5 text-center">{{ t('history.qty') }}</th>
              <th class="p-5">{{ t('history.reason') }}</th>
            </template>
            <template v-else-if="currentTab === 'transfer'">
              <th class="p-5">{{ t('history.dateTime') }}</th>
              <th class="p-5 text-left">{{ t('history.branchFlow') }}</th>
              <th class="p-5 text-center">{{ t('history.items') }}</th>
              <th class="p-5 text-center">{{ t('history.status') }}</th>
              <th class="p-5 text-center">{{ t('history.action') }}</th>
            </template>
            <template v-else-if="currentTab === 'inventory-ledger'">
              <th class="p-4 text-center">{{ t('common.no') }}</th>
              <th class="p-4">{{ t('history.barcode') }}</th>
              <th class="p-4">{{ t('history.itemName') }}</th>
              <th class="p-4">{{ t('history.unit') }}</th>
              <th class="p-4 text-right">{{ t('history.latestPurchasePrice') }}</th>
              <th class="p-4 text-right">{{ t('history.latestSalePrice') }}</th>
              <th class="p-4 text-right">{{ t('history.totalPurchased') }}</th>
              <th class="p-4 text-right">{{ t('history.totalSold') }}</th>
              <th class="p-4 text-right">{{ t('history.totalReturned') }}</th>
              <th class="p-4 text-right">{{ t('history.totalStockRemains') }}</th>
              <th class="p-4 text-center">{{ t('history.action') }}</th>
            </template>
          </tr>
        </thead>

        <tbody class="divide-y divide-slate-100">
          <tr v-for="(item, index) in historyData"
            :key="item.saleId || item.adjustmentId || item.returnId || item.poId || item.transferId || item.productId"
            class="hover:bg-slate-50/80 transition-colors group">

            <template v-if="currentTab === 'sale'">
            <td class="p-5 text-blue-600 font-normal tracking-tight text-sm">SA{{ String(item.saleId).padStart(7, '0') }}</td>
              <td class="p-5 text-slate-500 font-normal text-[11px]">{{ formatDateTime(item.saleDate) }}</td>
              <td class="p-5">
                <div class="font-normal text-slate-800 text-sm tracking-tight">{{ item.customerName || t('common.walkInCustomer') }}</div>
                <div class="text-[10px] text-slate-500 mt-0.5 flex items-center gap-1">
                  <svg class="w-3 h-3 text-slate-400" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z"></path></svg>
                  {{ item.username || item.Username || item.cashierName || item.CashierName || 'System' }}
                </div>
                <div class="text-[10px] text-slate-400 truncate max-w-xs italic font-normal mt-0.5">
                  {{ item.itemsSummary || t('common.loadingDetails') }}
                </div>
                <div v-if="item.voucherNote" class="text-[10px] text-slate-500 truncate max-w-xs italic font-normal mt-0.5">
                  Note: {{ item.voucherNote }}
                </div>
              </td>
              <td class="p-4 text-right font-normal text-sm text-blue-600 tabular-nums">
                {{ formatNumber(getSaleNetAmount(item)) }}
              </td>
              <td class="p-5">
                <span
                  :class="{
                    'text-rose-700 bg-rose-50 border-rose-100': (item.saleType || '').toLowerCase() === 'credit',
                    'text-amber-700 bg-amber-50 border-amber-100': (item.saleType || '').toLowerCase() === 'partial',
                    'text-emerald-700 bg-emerald-50 border-emerald-100': !['credit', 'partial'].includes((item.saleType || '').toLowerCase())
                  }"
                  class="px-2 py-0.5 rounded-md text-[9px] font-normal border uppercase tracking-wider">
                  {{ item.saleType || 'Normal' }}
                </span>
              </td>
              <td class="p-5">
                <span
                  :class="{
                    'text-emerald-700 bg-emerald-50 border-emerald-100': (item.orderStatus || 'Completed') === 'Completed',
                    'text-amber-700 bg-amber-50 border-amber-100': (item.orderStatus || '') === 'Preordered',
                    'text-indigo-700 bg-indigo-50 border-indigo-100': (item.orderStatus || '') === 'Delivering',
                    'text-rose-700 bg-rose-50 border-rose-100': (item.orderStatus || '') === 'Cancelled'
                  }"
                  class="px-2 py-0.5 rounded-md text-[9px] font-normal border uppercase tracking-wider">
                  {{ item.orderStatus || 'Completed' }}
                </span>
              </td>
              <td class="p-5 text-slate-500 font-normal text-[11px]">
                <span v-if="item.voucherNote || item.VoucherNote" class="truncate block max-w-xs">
                  {{ item.voucherNote || item.VoucherNote }}
                </span>
                <span v-else class="text-slate-300">-</span>
              </td>
              <td class="p-5">
                <div class="flex justify-center gap-2">
                  <button @click="viewDetail(item.saleId)"
                    class="p-2 bg-slate-50 text-slate-400 hover:text-blue-600 transition-all shadow-sm">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                        d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                        d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                    </svg>
                  </button>
                  <button @click="reprint(item.saleId || item.SaleId)"
                    class="p-2 bg-slate-50 text-slate-400 hover:text-emerald-600 transition-all shadow-sm">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                        d="M17 17h2a2 2 0 002-2v-4a2 2 0 00-2-2H5a2 2 0 00-2 2v4a2 2 0 002 2h2m2 4h6a2 2 0 002-2v-4a2 2 0 00-2-2H9a2 2 0 00-2 2v4a2 2 0 002 2zm8-12V5a2 2 0 00-2-2H9a2 2 0 00-2 2v4h10z" />
                    </svg>
                  </button>
                  <button @click="printDeliForm(item.saleId || item.SaleId)"
                    class="p-2 bg-slate-50 text-slate-400 hover:text-amber-600 transition-all shadow-sm" title="Print Deli Form">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                        d="M9 17a2 2 0 11-4 0 2 2 0 014 0zM19 17a2 2 0 11-4 0 2 2 0 014 0z" />
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                        d="M13 16V6a1 1 0 00-1-1H4a1 1 0 00-1 1v10a1 1 0 001 1h1m8-1a1 1 0 01-1 1H9m4-1V8a1 1 0 011-1h2.586a1 1 0 01.707.293l3.414 3.414a1 1 0 01.293.707V16a1 1 0 01-1 1h-1m-6-1a1 1 0 001 1h1M5 17a2 2 0 104 0m-4 0a2 2 0 114 0m6 0a2 2 0 104 0m-4 0a2 2 0 114 0" />
                    </svg>
                  </button>
                </div>
              </td>
            </template>

            <template v-if="currentTab === 'purchase'">
              <td class="p-5 text-orange-600 font-normal tracking-tight uppercase text-xs">PO-{{ item.poId }}</td>
              <td class="p-5 text-slate-500 font-normal text-[11px]">{{ formatDateTime(item.orderDate) }}</td>
              <td class="p-5 font-normal text-slate-800 text-sm tracking-tight">{{ item.supplierName || t('purchases.unknownVendor') }}</td>
              <td class="p-5 text-slate-700 font-normal text-[11px] tabular-nums">{{ item.paymentMethod || '-' }}</td>
              <td class="p-4 text-right font-normal text-sm text-blue-600 tabular-nums">{{ (item.totalAmount ||
                0).toLocaleString() }}</td>
              <td class="p-5 text-center">
                <button @click="viewPurchaseDetail(item.poId)"
                  class="p-2 bg-slate-50 text-slate-400 hover:text-orange-600 transition-all shadow-sm">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
                  </svg>
                </button>
              </td>
            </template>

            <template v-else-if="currentTab === 'return'">
              <td class="p-5 text-slate-500 text-[11px]">{{ formatDateTime(item.returnDate) }}</td>
              <td class="p-5 font-normal text-blue-600 text-xs tabular-nums">
                {{ item.returnType === 'Sale'
                  ? `SA-${String(item.saleId || 0).padStart(7, '0')}`
                  : `PO-${String(item.poId || 0).padStart(7, '0')}` }}
              </td>
              <td class="p-5">
                <span
                  :class="item.returnType === 'Sale' ? 'text-indigo-600 bg-indigo-50 border-indigo-100' : 'text-orange-600 bg-orange-50 border-orange-100'"
                  class="px-2 py-0.5 rounded-md text-[9px] font-normal border uppercase tracking-tighter">
                  {{ item.returnType }}
                </span>
              </td>
              <td class="p-5 font-normal text-slate-800 text-sm tracking-tight">
                {{ item.productName }}
                <span class="text-[10px] text-slate-400 ml-1 font-normal italic">({{ item.unitName }})</span>
              </td>
              <td class="p-5 text-center font-normal text-slate-900 tabular-nums text-sm">{{ formatQuantity(item.quantity) }}</td>
              <td class="p-5 text-right font-normal text-rose-600 tabular-nums text-sm">{{ (item.refundAmount ||
                0).toLocaleString() }}</td>
              <td class="p-5 text-[11px] text-slate-400 font-normal italic truncate max-w-[200px]">{{ item.reason }}</td>
            </template>

            <template v-else-if="currentTab === 'adjustment'">
              <td class="p-5 text-slate-500 text-[11px]">{{ formatDateTime(item.adjustmentDate) }}</td>
              <td class="p-5 font-normal text-slate-800 text-sm tracking-tight">{{ item.productName }}</td>
              <td class="p-5">
                <span
                  :class="item.type === 'Addition' ? 'text-emerald-600 bg-emerald-50 border-emerald-100' : 'text-rose-600 bg-rose-50 border-rose-100'"
                  class="px-2 py-0.5 rounded-md text-[9px] font-normal border uppercase">
                  {{ item.type }}
                </span>
              </td>
              <td class="p-5 text-center font-normal tabular-nums text-emerald-600 text-sm">{{ formatQuantity(item.quantity) }}</td>
              <td class="p-5 text-[11px] text-slate-400 font-normal italic">{{ item.reason }}</td>
            </template>

            <template v-else-if="currentTab === 'transfer'">
              <td class="p-5 text-slate-500 text-[11px]">{{ formatDateTime(item.transferDate) }}</td>
              <td class="p-5">
                <div class="flex items-center gap-2">
                  <span class="font-normal text-slate-800 text-sm tracking-tight">{{ item.fromBranchName }}</span>
                  <svg class="w-2.5 h-2.5 text-slate-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M14 5l7 7m0 0l-7 7m7-7H3" />
                  </svg>
                  <span class="font-normal text-slate-800 text-sm tracking-tight">{{ item.toBranchName }}</span>
                </div>
                <div v-if="item.username" class="text-[9px] text-slate-400 mt-1 font-normal">
                  Created by: <span class="text-slate-500 font-normal uppercase tracking-tighter">{{ item.username
                  }}</span>
                </div>
              </td>
              <td class="p-5 text-center">
                <span class="text-[10px] font-normal text-slate-600 bg-slate-100 px-2 py-1 rounded-md">
                  {{ item.itemCount }} {{ t('history.items') }}
                </span>
              </td>
              <td class="p-5 text-center">
                <span :class="{
                  'text-blue-600 bg-blue-50 border-blue-100': item.status === 'Pending',
                  'text-emerald-600 bg-emerald-50 border-emerald-100': item.status === 'Completed',
                  'text-rose-600 bg-rose-50 border-rose-100': item.status === 'Cancelled'
                }" class="px-2 py-0.5 rounded-md text-[9px] font-normal border uppercase tracking-wider">
                  {{ item.status }}
                </span>
              </td>
              <td class="p-5 text-center">
                <button @click="viewTransferDetail(item.transferId)"
                  class="p-2 bg-slate-50 text-slate-400 hover:text-blue-600 transition-all shadow-sm">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                  </svg>
                </button>
              </td>
            </template>

            <template v-else-if="currentTab === 'inventory-ledger'">
              <td class="p-4 text-center text-slate-400 text-xs tabular-nums">{{ ((filters.page - 1) * filters.limit) + index + 1 }}</td>
              <td class="p-4 text-blue-600 text-xs tabular-nums">{{ item.barcode || '-' }}</td>
              <td class="p-4 font-normal text-slate-800 text-sm tracking-tight">{{ item.itemName }}</td>
              <td class="p-4 text-slate-500 text-xs">{{ item.unitName || '-' }}</td>
              <td class="p-4 text-right text-slate-700 text-xs tabular-nums">{{ formatNumber(item.latestPurchasePrice) }}</td>
              <td class="p-4 text-right text-slate-700 text-xs tabular-nums">{{ formatNumber(item.latestSalePrice) }}</td>
              <td class="p-4 text-right text-emerald-700 text-xs tabular-nums">{{ formatNumber(item.totalPurchased) }}</td>
              <td class="p-4 text-right text-rose-700 text-xs tabular-nums">{{ formatNumber(item.totalSold) }}</td>
              <td class="p-4 text-right text-orange-700 text-xs tabular-nums">{{ formatNumber(item.totalReturned) }}</td>
              <td class="p-4 text-right text-blue-700 text-xs tabular-nums">{{ formatNumber(item.totalStockRemains) }}</td>
              <td class="p-4 text-center">
                <button @click="viewProductBatches(item)"
                  class="p-2 bg-slate-50 text-slate-400 hover:text-cyan-700 transition-all shadow-sm"
                  title="View stock batches">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4" />
                  </svg>
                </button>
              </td>
            </template>

          </tr>

          <tr v-if="historyData.length === 0">
            <td :colspan="tableColspan" class="p-24 text-center">
              <div class="flex flex-col items-center justify-center opacity-20 grayscale">
                <svg class="w-12 h-12 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path
                    d="M20 13V6a2 2 0 00-2-2H6a2 2 0 00-2 2v7m16 0v5a2 2 0 01-2 2H6a2 2 0 01-2-2v-5m16 0h-2.586a1 1 0 00-.707.293l-2.414 2.414a1 1 0 01-.707.293h-3.172a1 1 0 01-.707-.293l-2.414-2.414A1 1 0 006.586 13H4" />
                </svg>
                <div class="text-sm font-normal uppercase tracking-[0.3em] text-slate-900">{{ t('history.noData') }}</div>
              </div>
            </td>
          </tr>
        </tbody>
      </table>

      <div v-if="historyData.length > 0"
        class="flex justify-between items-center px-6 py-4 bg-slate-50 border-t border-slate-200">
        <div class="text-[10px] font-normal text-slate-400 uppercase tracking-widest text-left">
          {{ t('history.title') }}: {{ totalItems }}
        </div>

        <div class="flex items-center gap-1.5">
          <button @click="prevPage" :disabled="filters.page <= 1"
            class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">{{
              t('common.prev') }}</button>
          <span class="px-3 text-xs font-normal text-slate-400">{{ t('common.page') }} {{ filters.page }} / {{ totalPages
          }}</span>
          <button @click="nextPage" :disabled="historyData.length < filters.limit"
            class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">{{
              t('common.next') }}</button>
        </div>
      </div>
    </div>

    <div v-if="showDeliFormModal"
      class="fixed inset-0 z-[130] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
      <div class="bg-white w-full max-w-md rounded-2xl shadow-2xl overflow-hidden border border-slate-200 animate-in zoom-in duration-200">
        <div class="bg-[#3f3f46] p-5 text-white flex justify-between items-center">
          <div>
            <h3 class="text-sm font-normal uppercase tracking-tight">Edit Deli Form</h3>
            <p class="text-amber-300 text-[10px] font-normal uppercase mt-1 tracking-widest">
              SA{{ String(deliForm.saleId || '').padStart(7, '0') }}
            </p>
          </div>
          <button @click="closeDeliFormModal" class="text-white/60 hover:text-white transition-colors">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <div class="p-5 space-y-4">
          <div>
            <label class="text-[9px] font-normal uppercase text-slate-400 ml-1 mb-1 block">Customer Name</label>
            <input v-model="deliForm.customerName" type="text"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg text-sm outline-none focus:ring-2 focus:ring-amber-500/20">
          </div>
          <div>
            <label class="text-[9px] font-normal uppercase text-slate-400 ml-1 mb-1 block">Phone</label>
            <input v-model="deliForm.customerPhone" type="text"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg text-sm outline-none focus:ring-2 focus:ring-amber-500/20">
          </div>
          <div>
            <label class="text-[9px] font-normal uppercase text-slate-400 ml-1 mb-1 block">Address</label>
            <textarea v-model="deliForm.customerAddress" rows="3"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg text-sm outline-none resize-none focus:ring-2 focus:ring-amber-500/20"></textarea>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="text-[9px] font-normal uppercase text-slate-400 ml-1 mb-1 block">Amount</label>
              <input v-model.number="deliForm.amount" type="number" min="0"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg text-sm outline-none text-right focus:ring-2 focus:ring-amber-500/20">
            </div>
            <div>
              <label class="text-[9px] font-normal uppercase text-slate-400 ml-1 mb-1 block">Deli Fee</label>
              <input v-model.number="deliForm.deliveryFee" type="number" min="0"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg text-sm outline-none text-right focus:ring-2 focus:ring-amber-500/20">
            </div>
          </div>
          <div class="flex justify-between items-center bg-amber-50 border border-amber-100 rounded-lg px-3 py-2">
            <span class="text-[10px] font-normal uppercase text-amber-700">Total</span>
            <span class="text-lg font-normal text-amber-900 tabular-nums">{{ formatNumber(deliFormTotal) }} Ks</span>
          </div>
          <div>
            <label class="text-[9px] font-normal uppercase text-slate-400 ml-1 mb-1 block">Note</label>
            <textarea v-model="deliForm.note" rows="2"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg text-sm outline-none resize-none focus:ring-2 focus:ring-amber-500/20"></textarea>
          </div>
        </div>

        <div class="p-5 border-t border-slate-100 flex gap-3">
          <button @click="closeDeliFormModal"
            class="flex-1 py-3 bg-slate-100 text-slate-600 rounded-lg font-normal uppercase text-[10px] hover:bg-slate-200 transition-all">
            {{ t('common.cancel') }}
          </button>
          <button @click="printEditedDeliForm"
            class="flex-1 py-3 bg-amber-600 text-white rounded-lg font-normal uppercase text-[10px] shadow-lg shadow-amber-100 hover:bg-amber-700 transition-all">
            Print
          </button>
        </div>
      </div>
    </div>

    <div v-if="showDetailModal"
      class="fixed inset-0 bg-slate-900/40 backdrop-blur-sm flex items-stretch sm:items-center justify-center p-2 sm:p-4 z-[100]">
      <div
        class="history-detail-modal-panel bg-white rounded-xl max-w-2xl w-full shadow-2xl animate-in zoom-in duration-200 overflow-hidden border border-slate-200 max-h-[95vh] min-h-0 flex flex-col">

        <div class="bg-[#3f3f46] p-4 sm:p-6 text-white flex justify-between items-center shrink-0">
          <div>
            <h3 class="text-lg font-normal uppercase tracking-tight">{{ t('history.invoiceDetails') }}</h3>
            <p class="text-blue-300 text-[10px] font-normal uppercase mt-0.5 tracking-widest italic">ID: SA{{
              String(selectedSale.sale.SaleId).padStart(7, '0') }}</p>
          </div>
          <button @click="showDetailModal = false" class="text-white/60 hover:text-white transition-colors">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <div class="history-detail-modal-body p-4 sm:p-6 overflow-y-auto min-h-0">
          <div class="grid grid-cols-2 gap-4 sm:gap-6 mb-4 sm:mb-6 bg-slate-50 p-4 rounded-xl border border-slate-100">
            <div>
              <label class="text-[9px] font-normal text-slate-400 uppercase block mb-1">{{ t('history.billedTo')
              }}</label>
              <p class="font-normal text-slate-800">{{ selectedSale.sale.CustomerName || t('common.walkInCustomer') }}</p>
            </div>
            <div class="text-right">
              <label class="text-[9px] font-normal text-slate-400 uppercase block mb-1">{{ t('history.transactionDate')
              }}</label>
              <p class="font-normal text-slate-800">{{ formatDateTime(selectedSale.sale.SaleDate) }}</p>
            </div>
          </div>
          <div v-if="selectedSale.sale.VoucherNote" class="mb-4 sm:mb-6 bg-amber-50 border border-amber-100 rounded-xl p-4">
            <label class="text-[9px] font-normal text-amber-500 uppercase block mb-1">Voucher Note</label>
            <p class="font-normal text-amber-900 italic text-sm">{{ selectedSale.sale.VoucherNote }}</p>
          </div>

          <div class="max-h-[300px] overflow-auto border border-slate-100 rounded-xl mb-4 sm:mb-6">
            <table class="w-full text-left">
              <thead
                class="bg-[#3f3f46] text-[10px] font-normal text-white uppercase tracking-widest sticky top-0 border-b border-[#3f3f46]">
                <tr>
                  <th class="p-4">{{ t('history.itemName') }}</th>
                  <th class="p-4 text-center">{{ t('history.qty') }}</th>
                  <th class="p-4 text-right">{{ t('history.unitPrice') }}</th>
                  <th class="p-4 text-right">{{ t('history.subtotal') }}</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-50 text-sm">
                <tr v-for="item in selectedSale.items" :key="item.SaleItemId">
                  <td class="p-4">
                    <div class="font-normal text-slate-800">
                      {{ item.ItemName || item.ProductName }}
                    </div>
                    <div class="flex flex-col gap-1 mt-1">
                      <div class="flex items-center gap-2">
                        <span class="text-[10px] text-blue-500 font-normal italic">({{ item.UnitName || 'Fixed' }})</span>
  
                        <span v-if="item.ItemType && item.ItemType !== 'Product'" :class="['text-[8px] px-1.5 py-0.5 rounded font-normal uppercase text-white',
                          item.ItemType === 'Service' ? 'bg-orange-500' : 'bg-emerald-500']">
                          {{ item.ItemType }}
                        </span>
                      </div>
                      <div v-if="item.Note || item.note" class="text-[10px] text-slate-500 font-normal bg-blue-50/50 px-2 py-1 rounded inline-block w-fit">
                        Note: {{ item.Note || item.note }}
                      </div>
                    </div>
                  </td>
                  <td class="p-4 text-center font-normal text-slate-900 tabular-nums">{{ formatQuantity(item.Quantity) }}</td>
                  <td class="p-4 text-right text-slate-500 tabular-nums">{{ formatNumber(item.UnitPrice) }}</td>
                  <td class="p-4 text-right font-normal text-slate-900 tabular-nums">{{ formatNumber(lineSubtotal(item)) }}
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <div class="space-y-2 pt-4 border-t border-slate-100">
            <div class="flex justify-between text-[10px] font-normal text-slate-400 uppercase">
              <span>Items Subtotal</span>
              <span class="tabular-nums">{{ formatNumber(itemsSubtotalDetail) }} Ks</span>
            </div>
            <div v-if="receiptDeliveryFeeDetail > 0"
              class="flex justify-between text-[10px] font-normal text-blue-400 uppercase">
              <span>Delivery Fee</span>
              <span class="tabular-nums">+ {{ formatNumber(receiptDeliveryFeeDetail) }} Ks</span>
            </div>
            <div v-if="receiptDiscountDetail > 0"
              class="flex justify-between text-[10px] font-normal text-rose-500 uppercase">
              <span>{{ receiptDiscountSummaryLabel }}</span>
              <span class="tabular-nums">- {{ formatNumber(receiptDiscountDetail) }} Ks</span>
            </div>
            <div
              class="flex justify-between text-2xl font-normal text-slate-900 pt-2 tracking-tighter border-t border-dashed mt-2">
              <span>Final Total</span>
              <span class="tabular-nums text-blue-700">{{ formatNumber(receiptTotalDetail) }} <small
                  class="text-xs font-normal text-slate-400">MMK</small></span>
            </div>
          </div>

          <div class="mt-6 sm:mt-8 space-y-3">
            <div v-if="supportsEpsonAssistant && receiptSize === '80mm'" class="space-y-2">
              <label class="block text-xs text-slate-600">
                {{ t('sale.printMethod') }}
                <select v-model="applePrintMethod" class="mt-1 w-full p-2 border border-slate-200 rounded-lg bg-white">
                  <option value="standard">{{ t('sale.standardPrint') }}</option>
                  <option value="epson">Epson TM Print Assistant</option>
                </select>
              </label>
              <p v-if="applePrintMethod === 'epson'" class="text-xs text-slate-500">{{ t('sale.epsonSetup') }}</p>
              <a v-if="epsonReceiptUrl && applePrintMethod === 'epson'" :href="epsonReceiptUrl" target="_blank" rel="noopener"
                class="block rounded-lg bg-blue-600 p-3 text-center text-white text-xs">{{ t('sale.openEpson') }}</a>
              <p v-if="epsonReceiptUrl && applePrintMethod === 'epson'" class="text-xs text-slate-500">{{ t('sale.epsonReturn') }}</p>
            </div>
            <!-- Save as image checkbox -->
            <label class="flex items-center gap-2 text-xs text-slate-600 cursor-pointer select-none">
              <input
                v-model="saveInvoiceAsImage"
                type="checkbox"
                class="h-4 w-4 rounded border-gray-300 text-emerald-600 focus:ring-emerald-500"
              >
              <span>{{ t('sale.saveInvoiceAsImage') }}</span>
            </label>
            <label class="flex items-center gap-2 text-xs text-slate-600 cursor-pointer select-none">
              <input v-model="showPreviousCredit" type="checkbox"
                class="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500">
              <span>{{ t('sale.showPreviousCredit') }}</span>
            </label>

            <div class="flex flex-col sm:flex-row gap-3">
              <div class="flex-1">
                <label class="text-[9px] font-normal text-slate-400 uppercase block mb-1.5 ml-1">{{
                  t('history.receiptFormat')
                }}</label>
                <select v-model="receiptSize"
                  class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg text-xs font-normal outline-none focus:ring-2 focus:ring-blue-500/20">
                  <option value="80mm">{{ t('common.thermal80') }}</option>
                  <option value="A4">{{ t('common.a4') }}</option>
                  <option value="A5">{{ t('common.a5') }}</option>
                </select>
              </div>
              <div class="flex flex-col sm:flex-row sm:items-end gap-3 flex-[1.5]">
                <button @click="showDetailModal = false"
                  class="flex-1 py-3 bg-slate-100 text-slate-600 rounded-lg font-normal uppercase text-[10px] hover:bg-slate-200 transition-all">{{
                    t('common.cancel') }}</button>
                <button @click="() => printFromDetail()" :disabled="preparingEpson"
                  class="flex-1 py-3 bg-blue-600 text-white rounded-lg font-normal uppercase text-[10px] shadow-lg shadow-blue-200 hover:bg-blue-700 transition-all">{{
                    preparingEpson ? t('sale.processing') : t('history.printReceipt') }}</button>
                <button @click="shareFromDetail"
                  :title="t('history.shareReceiptDesc')"
                  class="flex-1 py-3 bg-emerald-600 text-white rounded-lg font-normal uppercase text-[10px] shadow-lg shadow-emerald-200 hover:bg-emerald-700 transition-all flex items-center justify-center gap-1.5">
                  <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8.684 13.342C8.886 12.938 9 12.482 9 12c0-.482-.114-.938-.316-1.342m0 2.684a3 3 0 110-2.684m0 2.684l6.632 3.316m-6.632-6l6.632-3.316m0 0a3 3 0 105.367-2.684 3 3 0 00-5.367 2.684zm0 9.316a3 3 0 105.368 2.684 3 3 0 00-5.368-2.684z" />
                  </svg>
                  {{ t('history.shareReceipt') }}
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div v-if="showPurchaseModal"
      class="fixed inset-0 z-[110] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
      <div
        class="bg-white w-full max-w-2xl rounded-2xl shadow-2xl overflow-hidden animate-in zoom-in duration-200 border border-slate-200">

        <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
          <div>
            <h3 class="text-lg font-normal uppercase tracking-tight">{{ t('history.purchaseInventoryDetail') }}</h3>
            <p class="text-orange-400 text-[10px] font-normal uppercase mt-1 tracking-widest">
              PO Number: PO-{{ selectedPurchase.master?.POId || selectedPurchase.master?.poId }}
            </p>
          </div>
          <button @click="showPurchaseModal = false" class="text-white/60 hover:text-white">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <div class="p-6">
          <div class="overflow-x-auto border border-slate-100 rounded-xl mb-6 max-h-[300px] overflow-y-auto">
            <table class="w-full text-left border-collapse">
              <thead
                class="bg-[#3f3f46] text-[10px] font-normal text-white uppercase tracking-widest sticky top-0 border-b border-[#3f3f46]">
                <tr>
                  <th class="p-4">{{ t('history.product') }}</th>
                  <th class="p-4 text-center">{{ t('history.qty') }}</th>
                  <th class="p-4 text-right">{{ t('history.unitCost') }}</th>
                  <th class="p-4 text-right">{{ t('history.total') }}</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-50 text-sm">
                <tr v-for="item in selectedPurchase.items" :key="item.Id || item.id || item.POItemId || item.poItemId">
                  <td class="p-4 font-normal text-slate-800">
                    {{ item.ProductName || item.productName }}
                    <span class="block text-[10px] text-slate-400 font-normal">Unit: {{ item.UnitName || item.unitName
                    }}</span>
                  </td>
                  <td class="p-4 text-center tabular-nums font-normal text-slate-700">{{ formatQuantity(item.Quantity || item.quantity) }}
                  </td>
                  <td class="p-4 text-right tabular-nums text-slate-500">{{ formatNumber(item.CostPrice ||
                    item.costPrice)
                  }}</td>
                  <td class="p-4 text-right tabular-nums font-normal text-slate-900">
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
                  t('history.supplierInfo')
                }}</p>
                <p class="text-sm font-normal text-orange-600">@{{ selectedPurchase.master?.SupplierName ||
                  selectedPurchase.master?.supplierName || t('purchases.unknownVendor') }}</p>
              </div>
              <div>
                <p class="text-[10px] font-normal text-slate-400 uppercase tracking-widest mb-1">
                  {{ t('history.paymentMethod') || 'Payment Method' }}
                </p>
                <p class="text-xs font-normal text-slate-700">
                  {{ selectedPurchase.master?.PaymentMethod || selectedPurchase.master?.paymentMethod || '-' }}
                </p>
              </div>
              <div v-if="selectedPurchase.master?.Remarks || selectedPurchase.master?.remarks"
                class="p-3 bg-amber-50 border border-amber-100 rounded-lg">
                <p class="text-[9px] font-normal text-amber-600 uppercase mb-1">{{ t('history.remarks') }}</p>
                <p class="text-xs text-slate-600 leading-relaxed">{{ selectedPurchase.master?.Remarks ||
                  selectedPurchase.master?.remarks }}</p>
              </div>
            </div>

            <div class="bg-slate-50 p-5 rounded-2xl space-y-3 shadow-inner">
              <div class="flex justify-between text-xs font-normal">
                <span class="text-slate-400 uppercase">{{ t('history.netCost') }}:</span>
                <span class="text-slate-800 tabular-nums">{{ formatNumber(selectedPurchase.master?.TotalAmount ||
                  selectedPurchase.master?.totalAmount) }} Ks</span>
              </div>
              <div class="flex justify-between text-xs font-normal">
                <span class="text-slate-400 uppercase">{{ t('history.amountPaid') }}:</span>
                <span class="text-emerald-600 tabular-nums">{{ formatNumber(selectedPurchase.master?.PaidAmount ||
                  selectedPurchase.master?.paidAmount) }} Ks</span>
              </div>
              <div class="flex justify-between items-center pt-3 border-t border-slate-200">
                <span class="text-[10px] font-normal uppercase text-slate-500">{{ t('history.balanceDue') }}:</span>
                <span class="text-xl font-normal tabular-nums text-rose-600">
                  {{ formatNumber((selectedPurchase.master?.TotalAmount || 0) - (selectedPurchase.master?.PaidAmount ||
                    0))
                  }} Ks
                </span>
              </div>
            </div>
          </div>

          <div class="mt-8">
            <button @click="showPurchaseModal = false"
              class="w-full py-4 bg-[#3f3f46] text-white rounded-xl font-normal uppercase text-xs tracking-widest shadow-lg transition active:scale-[0.98]">
              {{ t('history.closeDetailView') }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <div v-if="showBatchModal"
      class="fixed inset-0 z-[115] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-2 sm:p-4">
      <div
        class="bg-white w-full max-w-4xl rounded-2xl shadow-2xl overflow-hidden animate-in zoom-in duration-200 border border-slate-200 max-h-[95dvh] flex flex-col">

        <div class="bg-[#3f3f46] p-4 sm:p-6 text-white flex justify-between items-center shrink-0">
          <div>
            <h3 class="text-lg font-normal uppercase tracking-tight">Stock Batches</h3>
            <p class="text-cyan-300 text-[10px] font-normal uppercase mt-1 tracking-widest">
              {{ selectedBatchProduct.itemName || '-' }}
            </p>
          </div>
          <button @click="showBatchModal = false" class="text-white/60 hover:text-white">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <div class="p-4 sm:p-6 overflow-y-auto min-h-0">
          <div class="grid grid-cols-1 sm:grid-cols-3 gap-3 mb-5">
            <div class="bg-slate-50 border border-slate-100 rounded-xl p-3">
              <div class="text-[9px] uppercase tracking-widest text-slate-400">Barcode</div>
              <div class="text-sm text-slate-800 tabular-nums">{{ selectedBatchProduct.barcode || '-' }}</div>
            </div>
            <div class="bg-slate-50 border border-slate-100 rounded-xl p-3">
              <div class="text-[9px] uppercase tracking-widest text-slate-400">Unit</div>
              <div class="text-sm text-slate-800">{{ selectedBatchProduct.unitName || '-' }}</div>
            </div>
            <div class="bg-slate-50 border border-slate-100 rounded-xl p-3">
              <div class="text-[9px] uppercase tracking-widest text-slate-400">Batches</div>
              <div class="text-sm text-slate-800 tabular-nums">{{ purchaseBatches.length }}</div>
            </div>
          </div>

          <div class="overflow-auto border border-slate-100 rounded-xl">
            <table class="w-full min-w-[1180px] text-left border-collapse">
              <thead class="bg-[#3f3f46] text-white">
                <tr class="text-[10px] uppercase tracking-[0.15em]">
                  <th class="p-4">Ref</th>
                  <th class="p-4">Source</th>
                  <th class="p-4">Date</th>
                  <th class="p-4">Supplier</th>
                  <th class="p-4">Branch</th>
                  <th class="p-4 text-right">Qty</th>
                  <th class="p-4 text-right">FOC</th>
                  <th class="p-4 text-right">Sold</th>
                  <th class="p-4 text-right">Returned</th>
                  <th class="p-4 text-right">Remaining</th>
                  <th class="p-4">Unit</th>
                  <th class="p-4 text-right">Cost</th>
                  <th class="p-4">Expiry Date</th>
                  <th class="p-4 text-center">Status</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100 text-xs">
                <tr v-if="isBatchLoading">
                  <td colspan="13" class="p-10 text-center text-slate-400 uppercase tracking-widest">
                    Loading batches...
                  </td>
                </tr>
                <tr v-else-if="purchaseBatches.length === 0">
                  <td colspan="13" class="p-10 text-center text-slate-400 uppercase tracking-widest">
                    No stock batches found
                  </td>
                </tr>
                <tr v-for="batch in purchaseBatches" :key="`${batch.sourceType || 'Purchase'}-${batch.batchId || batch.poItemId}`" class="hover:bg-slate-50">
                  <td class="p-4 text-orange-600 tabular-nums">{{ formatBatchReference(batch) }}</td>
                  <td class="p-4">
                    <div class="flex flex-col gap-1">
                      <span class="w-fit px-2 py-0.5 rounded-md border text-[9px] uppercase tracking-wider"
                        :class="getBatchSourceClass(batch)">
                        {{ batch.sourceType || 'Purchase' }}
                      </span>
                      <span v-if="batch.batchStatus" class="text-[10px] text-slate-400">{{ batch.batchStatus }}</span>
                    </div>
                  </td>
                  <td class="p-4 text-slate-500 tabular-nums">{{ formatDateTime(batch.orderDate) }}</td>
                  <td class="p-4 text-slate-800">{{ batch.supplierName || '-' }}</td>
                  <td class="p-4 text-slate-600">{{ batch.branchName || '-' }}</td>
                  <td class="p-4 text-right tabular-nums">{{ formatQuantity(batch.quantity) }}</td>
                  <td class="p-4 text-right tabular-nums">{{ formatQuantity(batch.focQuantity) }}</td>
                  <td class="p-4 text-right tabular-nums">{{ formatNullableNumber(batch.soldQuantity) }}</td>
                  <td class="p-4 text-right tabular-nums">{{ formatNullableNumber(batch.returnedQuantity) }}</td>
                  <td class="p-4 text-right tabular-nums text-emerald-700">{{ formatQuantity(batch.remainingQuantity) }}</td>
                  <td class="p-4 text-slate-500">{{ batch.unitName || '-' }}</td>
                  <td class="p-4 text-right tabular-nums">{{ formatNumber(batch.costPrice) }}</td>
                  <td class="p-4 tabular-nums" :class="getBatchExpiryStatus(batch).className">
                    {{ formatDateOnlySafe(batch.expiryDate) }}
                  </td>
                  <td class="p-4 text-center">
                    <span :class="getBatchExpiryStatus(batch).badgeClass"
                      class="px-2 py-0.5 rounded-md border text-[9px] uppercase tracking-wider">
                      {{ getBatchExpiryStatus(batch).label }}
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <div class="p-4 border-t border-slate-100 bg-slate-50 shrink-0">
          <button @click="showBatchModal = false"
            class="w-full py-3 bg-slate-900 text-white rounded-xl font-normal uppercase text-xs tracking-widest shadow-lg transition active:scale-[0.98]">
            {{ t('common.close') || 'Close' }}
          </button>
        </div>
      </div>
    </div>

    <!-- Transfer Detail Modal -->
    <div v-if="showTransferModal"
      class="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-in fade-in duration-300">
      <div class="bg-white rounded-[2rem] shadow-2xl w-full max-w-2xl overflow-hidden border border-white/20">
        <div class="p-8">
          <div class="flex justify-between items-start mb-8">
            <div>
              <div class="flex items-center gap-3 mb-2">
                <span
                  class="px-3 py-1 bg-blue-50 text-blue-600 rounded-full text-[10px] font-normal uppercase tracking-widest border border-blue-100">
                  Transfer Detail
                </span>
                <span class="text-slate-300">/</span>
                <span class="text-slate-400 font-normal text-xs uppercase tracking-widest">#{{
                  selectedTransfer.transfer?.TransferId }}</span>
              </div>
              <h2 class="text-2xl font-normal text-slate-900 tracking-tight flex items-center gap-3">
                {{ selectedTransfer.transfer?.FromBranchName }}
                <svg class="w-5 h-5 text-slate-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14 5l7 7m0 0l-7 7m7-7H3" />
                </svg>
                {{ selectedTransfer.transfer?.ToBranchName }}
              </h2>
              <div class="flex items-center gap-4 mt-1">
                <p class="text-slate-400 font-normal text-xs">{{ formatDateTime(selectedTransfer.transfer?.TransferDate)
                }}
                </p>
                <div v-if="selectedTransfer.transfer?.CreatedBy"
                  class="flex items-center gap-1.5 px-2 py-0.5 bg-slate-50 border border-slate-100 rounded text-[10px]">
                  <span class="text-slate-400 font-normal uppercase tracking-widest">By:</span>
                  <span class="text-slate-600 font-normal uppercase tracking-tight">{{
                    selectedTransfer.transfer?.CreatedBy
                  }}</span>
                </div>
              </div>
            </div>
            <button @click="showTransferModal = false"
              class="p-2 hover:bg-slate-100 rounded-xl transition-colors text-slate-400">
              <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>

          <div class="bg-slate-50/50 rounded-2xl border border-slate-100 overflow-hidden mb-6">
            <div class="max-h-[350px] overflow-y-auto">
              <table class="w-full text-left">
                <thead class="sticky top-0 bg-[#3f3f46] text-white backdrop-blur border-b border-[#3f3f46]">
                  <tr>
                    <th class="p-4 text-[10px] font-normal uppercase tracking-widest">Product</th>
                    <th class="p-4 text-[10px] font-normal uppercase tracking-widest text-center">Unit
                    </th>
                    <th class="p-4 text-[10px] font-normal uppercase tracking-widest text-center">Quantity
                    </th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-100 bg-white/30 text-sm">
                  <tr v-for="item in selectedTransfer.items" :key="item.TransferId + '-' + item.ProductId">
                    <td class="p-4 font-normal text-slate-800">{{ item.ProductName }}</td>
                    <td class="p-4 text-center font-normal text-slate-500 italic">{{ item.UnitName || '-' }}</td>
                    <td class="p-4 text-center font-normal text-slate-900 tabular-nums text-base">{{ formatQuantity(item.Quantity) }}
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <div v-if="selectedTransfer.transfer?.Note"
            class="bg-blue-50/50 p-4 rounded-xl border border-blue-100/50 mb-8">
            <p class="text-[10px] font-normal text-blue-400 uppercase tracking-widest mb-1">{{ t('history.note') }}</p>
            <p class="text-slate-600 text-sm font-normal">{{ selectedTransfer.transfer.Note }}</p>
          </div>

          <button @click="showTransferModal = false"
            class="w-full py-4 bg-slate-900 text-white rounded-xl font-normal uppercase text-xs tracking-widest shadow-lg shadow-slate-200 transition active:scale-[0.98]">
            {{ t('history.closeDetailView') }}
          </button>
        </div>
      </div>
    </div>
    <div id="print-area" style="display: none;">
      <div
        :style="{ width: '76mm', padding: '5px', fontFamily: 'Pyidaungsu, Segoe UI, sans-serif', fontSize: receiptFont.body + 'px', color: 'black', lineHeight: '1.4' }">
        <div style="text-align: center; margin-bottom: 5px;">
          <h2 :style="{ fontSize: receiptFont.h2 + 'px', margin: 0, textTransform: 'uppercase', fontWeight: 'bold' }">{{ shopInfo.ShopName }}
          </h2>
          <p style="margin: 2px 0; font-weight: bold;">({{ receiptBranchName }})</p>
          <p v-if="receiptContact.Address" :style="{ margin: '2px 0', fontSize: receiptFont.small + 'px', whiteSpace: 'pre-line' }">{{ receiptContact.Address }}</p>
          <p v-if="receiptContact.Phone" :style="{ margin: '2px 0', fontSize: receiptFont.small + 'px' }">Tel: {{ receiptContact.Phone }}</p>
        </div>

        <div style="border-bottom: 1px dashed black; margin: 5px 0;"></div>

        <div style="display: flex; justify-content: space-between;">
          <span>No: SA{{ String(selectedSale.sale.SaleId).padStart(7, '0') }}</span>
          <span>{{ formatDateTime(selectedSale.sale.SaleDate) }}</span>
        </div>
        <div :style="{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: '8px', fontSize: receiptFont.small + 'px' }">
          <span style="flex: 1 1 auto; min-width: 0; white-space: normal; word-break: break-word; line-height: 1.35;">Cus: {{
            selectedSale.sale.CustomerName || 'Walk-in' }}</span>
          <span style="flex: 0 0 auto; text-align: right; white-space: nowrap;">Staff: {{ selectedSale.sale.CashierName || 'System' }}</span>
        </div>
        <div v-if="selectedSale.sale.CustomerPhone || selectedSale.sale.CustomerAddress" :style="{ fontSize: receiptFont.tiny + 'px', color: '#444', marginBottom: '5px' }">
          {{ selectedSale.sale.CustomerPhone || '' }} {{ selectedSale.sale.CustomerPhone && selectedSale.sale.CustomerAddress ? '|' : '' }} {{ selectedSale.sale.CustomerAddress || '' }}
        </div>
        <div v-if="selectedSale.sale.VoucherNote" :style="{ fontSize: receiptFont.tiny + 'px', color: '#444', marginBottom: '5px', fontStyle: 'italic' }">
          Note: {{ selectedSale.sale.VoucherNote }}
        </div>

        <div style="border-bottom: 1px dashed black; margin: 5px 0;"></div>

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
            <tr v-for="(item, idx) in selectedSale.items" :key="idx">
              <td style="padding: 2px 0 4px 0; vertical-align: top; line-height: 1.18;">
                <div style="font-weight: bold; line-height: 1.18;">{{ item.ItemName || item.ProductName }}</div>
                <div :style="{ fontSize: receiptFont.tiny + 'px', color: '#444' }">
                  ({{ receiptUnitName(item, item.UnitName || 'N/A') }})
                  <span v-if="item.ItemType && item.ItemType !== 'Product' && item.ItemType !== 'Bundle'">[{{ item.ItemType }}]</span>
                  @ {{ formatNumber(item.UnitPrice) }}
                </div>
                <div v-if="lineDiscount(item) > 0" :style="{ fontSize: receiptFont.tiny + 'px', color: '#dc2626', marginTop: '1px' }">
                  Discount: -{{ formatNumber(lineDiscount(item)) }}
                </div>
                <div v-if="item.Note || item.note" :style="{ fontSize: receiptFont.tiny + 'px', color: '#555', fontStyle: 'italic', marginTop: '2px' }">
                  * {{ item.Note || item.note }}
                </div>
              </td>
              <td style="text-align: center; vertical-align: top; padding: 2px 0 4px 0; line-height: 1.18;">{{ formatQuantity(item.Quantity) }}</td>
              <td style="text-align: right; vertical-align: top; padding: 2px 0 4px 0; line-height: 1.18;">
                {{ formatNumber(lineSubtotal(item)) }}
              </td>
            </tr>
          </tbody>

          <tfoot :style="{ fontSize: receiptFont.small + 'px', lineHeight: '1.25' }">
            <tr>
              <td colspan="3" style="border-top: 1px solid black; height: 12px; padding: 0;"></td>
            </tr>
            <tr>
              <td></td>
              <td style="text-align: center; padding: 0 0 8px 0; font-weight: bold; border-bottom: 1px solid black;">
                {{ selectedSale.items.reduce((sum, i) => sum + i.Quantity, 0) }}
              </td>
              <td></td>
            </tr>

            <tr>
              <td colspan="2" style="text-align: left; padding-top: 6px;">Sub-Total:</td>
              <td style="text-align: right; padding-top: 6px;">
                {{ formatNumber(itemsSubtotalDetail) }}
              </td>
            </tr>

            <tr v-if="receiptDiscountDetail > 0">
              <td colspan="2" style="text-align: left; color: #444;">{{ receiptDiscountPrintLabel }}</td>
              <td style="text-align: right; color: #444;">{{ formatNumber(receiptDiscountDetail) }}</td>
            </tr>

            <tr v-if="receiptDeliveryFeeDetail > 0">
              <td colspan="2" style="text-align: left;">Delivery (+):</td>
              <td style="text-align: right;">{{ formatNumber(receiptDeliveryFeeDetail) }}</td>
            </tr>

            <tr>
              <td colspan="2" :style="{ textAlign: 'left', paddingTop: '5px', fontWeight: 'bold', fontSize: receiptFont.total + 'px' }">TOTAL:
              </td>
              <td :style="{ textAlign: 'right', paddingTop: '5px', fontWeight: 'bold', fontSize: receiptFont.total + 'px' }">{{
                formatNumber(receiptTotalDetail) }}</td>
            </tr>

            <tr v-for="payment in selectedSalePayments" :key="payment.method">
              <td colspan="2" style="text-align: left; padding-top: 5px; font-weight: bold;">{{ receiptPaidLabel(payment.method) }}</td>
              <td style="text-align: right; padding-top: 5px; font-weight: bold;">{{
                formatNumber(payment.amount) }}</td>
            </tr>

            <tr v-if="receiptBalanceDetail !== 0">
              <td colspan="2" style="text-align: left; padding-top: 5px; font-weight: bold;">
                {{ receiptBalanceLabel(receiptBalanceDetail) }}
              </td>
              <td style="text-align: right; padding-top: 5px; font-weight: bold;">{{
                formatNumber(Math.abs(receiptBalanceDetail)) }}</td>
            </tr>
            <tr v-if="showPreviousCredit && receiptTotalCreditDetail > 0">
              <td colspan="2" style="text-align: left; padding-top: 5px; font-weight: bold;">{{ t('sale.totalCredit') }}:</td>
              <td style="text-align: right; padding-top: 5px; font-weight: bold; color: #dc2626;">{{ formatNumber(receiptTotalCreditDetail) }}</td>
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
          <span>Voucher: SA{{ String(selectedSale.sale.SaleId).padStart(7, '0') }}</span>
          <span>Date: {{ formatDateTime(selectedSale.sale.SaleDate) }}</span>
        </div>
        <div :style="{ display: 'flex', justifyContent: 'space-between', fontSize: receiptFont.small + 'px', marginBottom: '10px' }">
          <span>Customer: {{ selectedSale.sale.CustomerName || 'Walk-in' }}
            <br v-if="selectedSale.sale.CustomerPhone || selectedSale.sale.CustomerAddress">
            <span v-if="selectedSale.sale.CustomerPhone || selectedSale.sale.CustomerAddress" :style="{ fontSize: receiptFont.tiny + 'px', color: '#555' }">
              {{ selectedSale.sale.CustomerPhone || '' }} {{ selectedSale.sale.CustomerPhone && selectedSale.sale.CustomerAddress ? '|' : '' }} {{ selectedSale.sale.CustomerAddress || '' }}
            </span>
          </span>
          <span>Staff: {{ selectedSale.sale.CashierName || 'System' }}</span>
        </div>
        <div v-if="selectedSale.sale.VoucherNote" :style="{ fontSize: receiptFont.small + 'px', color: '#334155', marginBottom: '8px', fontStyle: 'italic' }">
          Note: {{ selectedSale.sale.VoucherNote }}
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
                :style="{ width: '55px', padding: '6px', border: '1px solid #333', color: 'white !important', fontSize: receiptFont.small + 'px', textAlign: 'center' }">
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
            <tr v-for="(item, idx) in selectedSale.items" :key="idx">
              <td style="padding: 6px; border: 1px solid #ccc; text-align: center;">{{ idx + 1 }}</td>
              <td style="padding: 6px; border: 1px solid #ccc; font-weight: bold;">
                {{ item.ItemName || item.ProductName }} {{ item.IsFOC ? '(FOC)' : '' }}
                <div v-if="lineDiscount(item) > 0" :style="{ fontSize: receiptFont.tiny + 'px', color: '#dc2626', fontWeight: 'normal', marginTop: '2px' }">
                  Discount: -{{ formatNumber(lineDiscount(item)) }}
                </div>
                <div v-if="item.Note || item.note" :style="{ fontSize: receiptFont.tiny + 'px', color: '#666', fontWeight: 'normal', fontStyle: 'italic', marginTop: '2px' }">
                  Note: {{ item.Note || item.note }}
                </div>
              </td>
              <td style="padding: 6px; border: 1px solid #ccc; text-align: center;">{{ formatQuantity(item.Quantity) }}</td>
              <td style="padding: 6px; border: 1px solid #ccc; text-align: center;">{{ receiptUnitName(item, item.UnitName || 'N/A') }}</td>
              <td style="padding: 6px; border: 1px solid #ccc; text-align: right;">{{ formatNumber(item.UnitPrice) }}
              </td>
              <td style="padding: 6px; border: 1px solid #ccc; text-align: right;">{{ formatNumber(lineSubtotal(item)) }}</td>
            </tr>

            <tr v-for="i in calculateEmptyRowsDetail()" :key="'empty-' + i">
              <td v-for="n in 6" :key="n" style="padding: 8px; border: 1px solid #eee;"></td>
            </tr>

            <tr>
              <td colspan="5" style="padding: 6px; border: 1px solid #ccc; text-align: right;">
                ပမာဏကျသင့်ငွေ:</td>
              <td style="padding: 6px; border: 1px solid #ccc; text-align: right; font-weight: bold;">
                {{ formatNumber(itemsSubtotalDetail) }}
              </td>
            </tr>
            <tr v-if="receiptDiscountDetail > 0">
              <td colspan="5" style="padding: 6px; border: 1px solid #ccc; text-align: right; color: #ea580c;">{{
                receiptDiscountPrintLabel }}</td>
              <td style="padding: 6px; border: 1px solid #ccc; text-align: right; color: #ea580c;">{{
                formatNumber(receiptDiscountDetail) }}</td>
            </tr>
            <tr v-if="receiptDeliveryFeeDetail > 0">
              <td colspan="5" style="padding: 6px; border: 1px solid #ccc; text-align: right; color: #2563eb;">
                Delivery (+):
              </td>
              <td style="padding: 6px; border: 1px solid #ccc; text-align: right; color: #2563eb;">{{
                formatNumber(receiptDeliveryFeeDetail) }}</td>
            </tr>
            <tr style="font-weight: bold; background-color: #f8fafc;">
              <td colspan="5" style="padding: 8px; border: 1px solid #ccc; text-align: right;">
                အသားတင်ကျသင့်ငွေ:</td>
              <td style="padding: 8px; border: 1px solid #ccc; text-align: right;">{{
                formatNumber(receiptTotalDetail) }}</td>
            </tr>
            <tr v-for="payment in selectedSalePayments" :key="payment.method">
              <td colspan="5" style="padding: 6px; border: 1px solid #ccc; text-align: right;">
                {{ receiptPaidLabel(payment.method) }}</td>
              <td style="padding: 6px; border: 1px solid #ccc; text-align: right;">{{ formatNumber(payment.amount) }}</td>
            </tr>
            <tr v-if="receiptBalanceDetail !== 0">
              <td colspan="5" style="padding: 6px; border: 1px solid #ccc; text-align: right; font-weight: bold;">
                {{ receiptBalanceLabel(receiptBalanceDetail) }}</td>
              <td style="padding: 6px; border: 1px solid #ccc; text-align: right; font-weight: bold;">{{
                formatNumber(Math.abs(receiptBalanceDetail)) }}</td>
            </tr>
            <tr v-if="showPreviousCredit && receiptTotalCreditDetail > 0">
              <td colspan="5" style="padding: 6px; border: 1px solid #ccc; text-align: right; font-weight: bold;">
                {{ t('sale.totalCredit') }}:</td>
              <td style="padding: 6px; border: 1px solid #ccc; text-align: right; font-weight: bold; color: #dc2626;">
                {{ formatNumber(receiptTotalCreditDetail) }}</td>
            </tr>
          </tbody>
        </table>

        <div :style="{ fontSize: receiptFont.small + 'px', fontWeight: 'bold', marginTop: '10px' }">
          Total Qty: {{ totalQtyDetail }}
        </div>
        <div style="text-align: center; margin-top: 20px; border-top: 1px dashed #ccc; padding-top: 10px;">
          <p v-for="(line, idx) in receiptFooterLines" :key="idx" :style="{ fontWeight: 'bold', fontSize: receiptFont.footer + 'px', margin: '2px 0' }">{{ line }}</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { useQuantitySettings } from '../composables/useQuantitySettings';
const { formatQuantity } = useQuantitySettings();
import { ref, onMounted, onUnmounted, watch, computed } from 'vue';
import { useRoute } from 'vue-router'; // ✅ သေချာအောင် ပြန်စစ်ပါ
import api from '../services/api';
import { nextTick } from 'vue';
import { useToast } from '../composables/useToast';
import { useI18n } from '../composables/useI18n';
import { useCsvExport } from '../composables/useCsvExport';
import { normalizeReceiptPayments, receiptUnitName } from '../utils/receiptData';
import { isMobileBrowser, openPrintWindow } from '../utils/print';
import { isAppleMobile, prepareEpsonReceipt } from '../utils/epsonPrint';
import androidBridge from '../services/androidBridge';

const { addToast } = useToast();
const { t } = useI18n();
const { exportToCsv } = useCsvExport();
const supportsEpsonAssistant = isAppleMobile() && !androidBridge.isAvailable();
const applePrintMethod = ref(localStorage.getItem('AppleReceiptPrintMethod') === 'epson' ? 'epson' : 'standard');
const preparingEpson = ref(false);
const epsonReceiptUrl = ref('');
let epsonPreparationVersion = 0;
watch(applePrintMethod, (method) => {
  localStorage.setItem('AppleReceiptPrintMethod', method);
});

// selectedSale.value.items ထဲက အရေအတွက်အားလုံးကို ပေါင်းပေးမည့် computed
// HistoryCenter.vue ရဲ့ script setup ထဲမှာ ရှာပြင်ပါ
const totalQtyDetail = computed(() => {
  // selectedSale သို့မဟုတ် items မရှိရင် 0 ပဲ ပြန်ခိုင်းထားပါတယ်
  const items = selectedSale.value?.items || [];
  return items.reduce((sum, i) => sum + (parseFloat(i.Quantity) || 0), 0);
});

const isFocLine = (item) => Boolean(item?.IsFOC ?? item?.isFOC);
const lineDiscount = (item) => {
  if (!item || isFocLine(item)) return 0;
  return Math.max(0, Number(item.DiscountAmount ?? item.discountAmount ?? 0));
};
const lineSubtotal = (item) => {
  if (!item || isFocLine(item)) return 0;

  const stored = Number(item.SubTotal ?? item.subTotal);
  const quantity = Number(item.Quantity ?? item.quantity ?? 0);
  const unitPrice = Number(item.UnitPrice ?? item.unitPrice ?? 0);
  const discount = lineDiscount(item);
  const current = Math.max(0, (quantity * unitPrice) - discount);

  if (!Number.isFinite(stored) || stored <= 0) return current;
  return Math.min(stored, current);
};

const itemsSubtotalDetail = computed(() => {
  const items = selectedSale.value?.items || [];
  return items.reduce((sum, i) => sum + lineSubtotal(i), 0);
});

const receiptDiscountDetail = computed(() =>
  Number(selectedSale.value?.sale?.DiscountAmount ?? selectedSale.value?.sale?.discountAmount ?? 0)
);
const toNonNegativeAmount = (value) => {
  const amount = Number(value);
  return Number.isFinite(amount) ? Math.max(0, amount) : 0;
};
const formatDiscountPercent = (value) => {
  const percent = Math.min(toNonNegativeAmount(value), 100);
  return Number.isInteger(percent)
    ? String(percent)
    : percent.toFixed(2).replace(/\.?0+$/, '');
};
const saleDiscountType = computed(() =>
  selectedSale.value?.sale?.DiscountType ?? selectedSale.value?.sale?.discountType ?? 'amount'
);
const saleDiscountInput = computed(() =>
  selectedSale.value?.sale?.DiscountInput ?? selectedSale.value?.sale?.discountInput ?? 0
);
const receiptDiscountPrintLabel = computed(() =>
  saleDiscountType.value === 'percentage' && toNonNegativeAmount(saleDiscountInput.value) > 0
    ? `Discount (${formatDiscountPercent(saleDiscountInput.value)}%):`
    : 'Discount (-):'
);
const receiptDiscountSummaryLabel = computed(() =>
  saleDiscountType.value === 'percentage' && toNonNegativeAmount(saleDiscountInput.value) > 0
    ? `Voucher Discount (${formatDiscountPercent(saleDiscountInput.value)}%)`
    : 'Voucher Discount'
);
const receiptDeliveryFeeDetail = computed(() =>
  Number(selectedSale.value?.sale?.DeliveryFee ?? selectedSale.value?.sale?.deliveryFee ?? 0)
);
const receiptTotalDetail = computed(() =>
  Math.max(0, itemsSubtotalDetail.value - receiptDiscountDetail.value + receiptDeliveryFeeDetail.value)
);
const receiptBalanceDetail = computed(() =>
  Number(selectedSale.value?.sale?.DueAmount ?? selectedSale.value?.sale?.dueAmount ?? 0)
);
const receiptTotalCreditDetail = computed(() => toNonNegativeAmount(
  selectedSale.value?.sale?.CustomerCurrentBalance ?? selectedSale.value?.sale?.customerCurrentBalance
));
const receiptPaidLabel = (method) => `${t('history.amountPaid')}${method ? ` (${method})` : ''}:`;
const receiptBalanceLabel = (balance) => (
  balance > 0
    ? `${t('history.balanceDue')}:`
    : `${t('sale.change')}:`
);

const getSaleNetAmount = (sale) => {
  const suppliedNet = sale?.netAmount ?? sale?.NetAmount;
  if (suppliedNet != null) return Number(suppliedNet) || 0;

  const subtotal = Number(sale?.totalAmount ?? sale?.TotalAmount ?? 0);
  const discount = Number(sale?.discountAmount ?? sale?.DiscountAmount ?? 0);
  const deliveryFee = Number(sale?.deliveryFee ?? sale?.DeliveryFee ?? 0);
  return Math.max(0, subtotal - discount + deliveryFee);
};

const shopInfo = ref({});
const receiptFontSizeMap = { Small: { body: 10, h1: 16, h2: 14, small: 8, tiny: 7, footer: 9, total: 10 }, Medium: { body: 13, h1: 20, h2: 18, small: 11, tiny: 9, footer: 11, total: 12 }, Large: { body: 16, h1: 26, h2: 22, small: 14, tiny: 11, footer: 14, total: 15 } };
const receiptFont = computed(() => receiptFontSizeMap[shopInfo.value.ReceiptFontSize] || receiptFontSizeMap.Medium);
const normalizeReceiptSize = (value) => ['80mm', 'A4', 'A5'].includes(value) ? value : '80mm';
const preferredReceiptSize = () => normalizeReceiptSize(
  shopInfo.value.PreferredPaperSize ?? shopInfo.value.preferredPaperSize
);
const receiptSize = ref('80mm');
const saveInvoiceAsImage = ref(true);
const showPreviousCredit = ref(false);

// ၁။ Settings (Shop Info) ကို ယူခြင်း
const fetchSettings = async () => {
  try {
    const res = await api.get('/Settings');
    shopInfo.value = res.data;
    receiptSize.value = preferredReceiptSize();
  } catch (err) { console.error(err); }
};

const calculateEmptyRowsDetail = () => {
  // ✅ Sale.vue ရှိ logic အတိုင်း အတိအကျ ပြန်ပြင်လိုက်ပါသည်
  const maxRows = receiptSize.value === 'A4' ? 10 : 1;
  const emptyCount = maxRows - (selectedSale.value.items?.length || 0);
  return emptyCount > 0 ? emptyCount : 0;
};

// နံပတ်တွေကို ကော်မာ (,) ခံပြီး ပြပေးမည့် function
const formatNumber = (val) => new Intl.NumberFormat().format(val || 0);

const buildAndroidHistoryReceiptText = () => {
  // The Android bridge renders text at 24px on a 576-dot 80mm canvas.
  // 38 monospace characters fit safely; 42 wraps on common Bluetooth printers.
  const width = 38;
  const sale = selectedSale.value?.sale || {};
  const items = selectedSale.value?.items || [];
  const line = '-'.repeat(width);
  const doubleLine = '='.repeat(width);
  const shopName = shopInfo.value.ShopName || shopInfo.value.Name || 'Versa POS';
  const contact = receiptContact.value;
  const receiptNo = `SA${String(sale.SaleId || sale.saleId || '').padStart(7, '0')}`;
  const textLength = (value) => String(value ?? '').length;
  const clip = (value, max) => String(value ?? '').slice(0, max);
  const center = (value) => {
    const text = String(value ?? '').trim();
    return `${' '.repeat(Math.max(0, Math.floor((width - textLength(text)) / 2)))}${text}`;
  };
  const columns = (left, right) => {
    const rightText = String(right ?? '');
    const leftText = clip(left, Math.max(1, width - textLength(rightText) - 1));
    return `${leftText}${' '.repeat(Math.max(1, width - textLength(leftText) - textLength(rightText)))}${rightText}`;
  };
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

  const lines = [
    center(shopName.toUpperCase()),
    center(`(${receiptBranchName.value || 'Main Branch'})`)
  ];
  if (contact.Address) wrap(contact.Address, width).forEach(addressLine => lines.push(center(addressLine)));
  if (contact.Phone) lines.push(center(`Tel: ${contact.Phone}`));
  lines.push(line);
  lines.push(columns(`No: ${receiptNo}`, formatDateTime(sale.SaleDate || sale.saleDate)));
  lines.push(columns(`Cus: ${sale.CustomerName || sale.customerName || 'Walk-in'}`, `Staff: ${sale.CashierName || sale.cashierName || 'System'}`));
  if (sale.CustomerPhone || sale.CustomerAddress) lines.push(clip(`${sale.CustomerPhone || ''} ${sale.CustomerAddress || ''}`.trim(), width));
  if (sale.VoucherNote || sale.voucherNote) lines.push(`Note: ${clip(sale.VoucherNote || sale.voucherNote, width - 6)}`);
  lines.push(line);
  const qtyWidth = 9;
  const amountWidth = Math.min(14, Math.max(10, width - 21));
  const nameWidth = Math.max(12, width - qtyWidth - amountWidth);
  lines.push(`${'Name'.padEnd(nameWidth, ' ')}${'Qty'.padStart(qtyWidth, ' ')}${'Amount'.padStart(amountWidth, ' ')}`);
  lines.push(line);

  items.forEach(item => {
    const qty = Number(item.Quantity || item.quantity || 0);
    const amount = lineSubtotal(item);
    const itemLines = wrap(`${item.ItemName || item.ProductName || item.productName || 'Item'}${item.IsFOC ? ' (FOC)' : ''}`, nameWidth);
    lines.push(`${itemLines[0].padEnd(nameWidth, ' ')}${formatQuantity(qty).padStart(qtyWidth, ' ')}${formatNumber(amount).padStart(amountWidth, ' ')}`);
    itemLines.slice(1).forEach(extra => lines.push(extra));
    lines.push(`  (${receiptUnitName(item, item.UnitName || item.unitName || 'N/A')}) @ ${formatNumber(item.UnitPrice || item.unitPrice || 0)}`);
    const discount = lineDiscount(item);
    if (discount > 0) lines.push(columns('  Discount', `-${formatNumber(discount)}`));
    if (item.Note || item.note) lines.push(`  * ${clip(item.Note || item.note, width - 4)}`);
  });

  const subtotal = items.reduce((sum, item) => sum + lineSubtotal(item), 0);
  const totalQty = items.reduce((sum, item) => sum + Number(item.Quantity || item.quantity || 0), 0);
  const discount = Number(sale.DiscountAmount ?? sale.discountAmount ?? 0);
  const deliveryFee = Number(sale.DeliveryFee ?? sale.deliveryFee ?? 0);
  const finalTotal = Math.max(0, subtotal - discount + deliveryFee);
  lines.push(line);
  lines.push(columns('Total Qty', formatQuantity(totalQty)));
  lines.push(columns('Sub-Total:', formatNumber(subtotal)));
  if (discount > 0) lines.push(columns(receiptDiscountPrintLabel.value, formatNumber(discount)));
  if (deliveryFee > 0) lines.push(columns('Delivery (+):', formatNumber(deliveryFee)));
  lines.push(doubleLine);
  lines.push(columns('TOTAL:', formatNumber(finalTotal)));
  selectedSalePayments.value.forEach(payment => lines.push(columns(receiptPaidLabel(payment.method), formatNumber(payment.amount || 0))));
  if (receiptBalanceDetail.value !== 0) {
    lines.push(columns(receiptBalanceLabel(receiptBalanceDetail.value), formatNumber(Math.abs(receiptBalanceDetail.value))));
  }
  if (showPreviousCredit.value && receiptTotalCreditDetail.value > 0) {
    lines.push(columns(`${t('sale.totalCredit')}:`, formatNumber(receiptTotalCreditDetail.value)));
  }
  lines.push(doubleLine, '');
  receiptFooterLines.value.forEach(footerLine => lines.push(center(footerLine)));
  return lines.join('\n');
};

const printWithAndroidIfPossible = () => {
  if (!androidBridge.supportsNativePrint()) return false;
  const receiptText = buildAndroidHistoryReceiptText();
  let result = androidBridge.printReceipt(receiptText, shopInfo.value.LogoData || '');
  if (!result?.ok && shopInfo.value.LogoData) {
    result = androidBridge.printReceipt(receiptText, '');
  }
  if (!result?.ok) {
    addToast(result?.message || 'Bluetooth printing failed.', 'warning');
    return false;
  }
  addToast(t('history.printPopupOpened') || 'Receipt sent to printer.', 'success');
  return true;
};

const printAndroidDetailImageIfPossible = async () => {
  if (!androidBridge.supportsNativePrint()) return false;

  await nextTick();
  const elementId = receiptSize.value === '80mm' ? 'print-area' : 'print-area-a4';
  const el = document.getElementById(elementId);
  if (!el || typeof androidBridge.printImage !== 'function') return false;

  const blob = await captureDetailInvoiceElement(el);
  if (!blob) return false;

  const dataUrl = await blobToDataUrl(blob);
  const result = androidBridge.printImage(dataUrl);
  if (!result?.ok) {
    console.warn('[HistoryPrint] Android image print failed:', result?.message || result);
    return false;
  }

  addToast(result.message || t('history.printPopupOpened') || 'Receipt sent to printer.', 'success');
  return true;
};

const printFromDetail = async (preOpenedWindow = null) => {
  const saleId = selectedSale.value?.sale?.SaleId || selectedSale.value?.sale?.saleId;
  if (!saleId) return;

  const isThermal = receiptSize.value === '80mm';
  if (supportsEpsonAssistant && isThermal && applePrintMethod.value === 'epson') {
    if (preparingEpson.value) return;
    preparingEpson.value = true;
    epsonReceiptUrl.value = '';
    const version = epsonPreparationVersion;
    try {
      await nextTick();
      const element = document.getElementById('print-area');
      if (!element) throw new Error('Receipt not found');
      const blob = await captureDetailInvoiceElement(element);
      if (!blob) throw new Error('Receipt capture failed');
      const url = await prepareEpsonReceipt(blob);
      if (version !== epsonPreparationVersion || !showDetailModal.value) return;
      epsonReceiptUrl.value = url;
      if (saveInvoiceAsImage.value) downloadBlob(blob, `invoice-${saleId}.png`);
    } catch (err) {
      addToast(t(err.message === 'EPSON_RECEIPT_TOO_LARGE' ? 'sale.epsonTooLarge' : 'sale.epsonPrepareFailed'), 'error');
    } finally {
      preparingEpson.value = false;
    }
    return;
  }
  if (isThermal && await printAndroidDetailImageIfPossible()) return;
  if (isThermal && printWithAndroidIfPossible()) return;

  const hasPrintWindow = preOpenedWindow?.document && typeof preOpenedWindow.document.write === 'function';
  const printWindow = hasPrintWindow ? preOpenedWindow : window.open('', '_blank', 'width=900,height=800');
  if (!printWindow) {
    addToast(t('history.popupBlocked') || 'Popup blocked. Please allow popups to print.', 'warning');
    return;
  }

  await nextTick();

  const elementId = isThermal ? 'print-area' : 'print-area-a4';
  const printArea = document.getElementById(elementId);
  if (!printArea) {
    console.error('Print Area not found! Looking for ID:', elementId);
    try { printWindow.close(); } catch (_) { }
    return;
  }

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
    ? `<div style="text-align: center; margin-top: 2px; margin-bottom: 5px; padding-top: ${isThermal ? '0' : '10mm'};">
         <img src="${shopInfo.value.LogoData}" style="width: ${logoSizePx}; height: ${logoSizePx}; object-fit: contain; border: none;">
       </div>`
    : '';

  const pageSize = isThermal ? '80mm auto' : (receiptSize.value === 'A5' ? '148mm 210mm' : '210mm 297mm');
  const bodyWidth = isThermal ? '76mm' : (receiptSize.value === 'A5' ? '148mm' : '210mm');

  printWindow.document.write(`
    <html>
      <head>
        <title>Invoice - ${saleId}</title>
        <style>
          @font-face { font-family: 'Pyidaungsu'; src: local('Pyidaungsu'); }
          @page { size: ${pageSize}; margin: 0; }
          body {
            margin: 0;
            padding: ${isThermal ? '2mm' : '0'};
            width: ${bodyWidth};
            font-family: 'Pyidaungsu', sans-serif;
            -webkit-print-color-adjust: exact !important;
            print-color-adjust: exact !important;
          }
          * { box-sizing: border-box; color: black !important; }
          #history-print-controls {
            position: sticky; top: 0; z-index: 9999;
            display: flex; gap: 8px; padding: 10px;
            background: white; border-bottom: 1px solid #d1d5db;
          }
          #history-print-controls button {
            cursor: pointer; padding: 8px 16px; border: 1px solid #9ca3af;
            border-radius: 6px; background: #f3f4f6; font: 14px sans-serif;
          }
          @media print { #history-print-controls { display: none !important; } }
          #print-area-a4 thead tr { background-color: #3f3f46 !important; }
          #print-area-a4 th { color: white !important; -webkit-print-color-adjust: exact; }
          #print-area th {
            background-color: transparent !important;
            color: black !important;
            border-bottom: 1px solid black;
          }
        </style>
      </head>
      <body>
        <div id="history-print-controls">
          <button id="history-print-button" type="button">Print</button>
          <button id="history-close-button" type="button">Close</button>
        </div>
        ${logoHtml} ${printArea.innerHTML}
        <script>
          (function () {
            var autoPrintTimer;
            function returnToApp() {
              try {
                if (window.opener && !window.opener.closed) window.opener.focus();
              } catch (_) { }
            }
            function closePrintWindow() {
              returnToApp();
              window.close();
            }
            function printReceipt() {
              clearTimeout(autoPrintTimer);
              window.focus();
              window.print();
            }
            document.getElementById('history-print-button').addEventListener('click', printReceipt);
            document.getElementById('history-close-button').addEventListener('click', closePrintWindow);
            // Chromium fires afterprint whether the user prints or cancels the preview.
            // Closing this child window releases its modal relationship with the desktop app.
            window.addEventListener('afterprint', closePrintWindow);
            window.addEventListener('beforeunload', returnToApp);
            window.onload = function () {
              autoPrintTimer = setTimeout(printReceipt, 200);
            };
          })();
        <\/script>
      </body>
    </html>
  `);
  printWindow.document.close();
};

const shareFromDetail = async () => {
  const saleId = selectedSale.value?.sale?.SaleId || selectedSale.value?.sale?.saleId;
  if (!saleId) return;

  await nextTick();

  const isThermal = receiptSize.value === '80mm';
  const elementId = isThermal ? 'print-area' : 'print-area-a4';
  const el = document.getElementById(elementId);
  if (!el) {
    addToast(t('history.shareCaptureError') || 'Could not capture invoice image.', 'error');
    return;
  }

  const blob = await captureDetailInvoiceElement(el);

  if (!blob) {
    addToast(t('history.shareCaptureError') || 'Could not capture invoice image.', 'error');
    return;
  }

  const fileName = `invoice-SA${String(saleId).padStart(7, '0')}.png`;

  if (androidBridge.isAndroid()) {
    const dataUrl = await blobToDataUrl(blob);
    const result = androidBridge.shareImage(
      dataUrl,
      fileName,
      `Invoice - SA${String(saleId).padStart(7, '0')}`,
      `${shopInfo.value.ShopName} - ${t('history.shareReceiptDesc')}`
    );

    if (result?.ok) return;
    console.warn('[ShareInvoice] Android native share failed:', result?.message || result);
  }

  // Save to file if checkbox is checked
  if (saveInvoiceAsImage.value) {
    if (window.showSaveFilePicker) {
      try {
        const handle = await window.showSaveFilePicker({
          suggestedName: fileName,
          types: [{ description: 'PNG Image', accept: { 'image/png': ['.png'] } }],
        });
        const writable = await handle.createWritable();
        await writable.write(blob);
        await writable.close();
        addToast(t('sale.invoiceSaved') || 'Invoice saved successfully.', 'success');
      } catch (err) {
        if (err.name !== 'AbortError') {
          // Fallback download
          downloadBlob(blob, fileName);
        }
      }
    } else {
      downloadBlob(blob, fileName);
    }
  }

  const file = new File([blob], fileName, { type: 'image/png' });

  // Try Web Share API
  if (navigator.share && navigator.canShare && navigator.canShare({ files: [file] })) {
    try {
      await navigator.share({
        files: [file],
        title: `Invoice - SA${String(saleId).padStart(7, '0')}`,
        text: `${shopInfo.value.ShopName} - ${t('history.shareReceiptDesc')}`,
      });
      return;
    } catch (err) {
      if (err.name !== 'AbortError') {
        console.warn('[ShareInvoice] Web Share failed:', err);
      }
    }
  }

  // Fallback: download
  if (!saveInvoiceAsImage.value) {
    downloadBlob(blob, fileName);
  }
  addToast(t('history.shareDownloaded') || 'Invoice image downloaded.', 'info');
};

const captureDetailInvoiceElement = async (el) => {
  let iframe = null;
  try {
    const isThermal = receiptSize.value === '80mm';
    const bodyWidth = isThermal ? '76mm' : (receiptSize.value === 'A5' ? '148mm' : '210mm');
    const bodyHeight = isThermal ? 'auto' : (receiptSize.value === 'A5' ? '210mm' : '297mm');
    const capturePaddingBottom = isThermal ? '18px' : '0';
    const captureWidth = bodyWidth;
    const contentHtml = el.outerHTML.replace('style="display: none;"', 'style="display: block;"');

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
      ? `<div style="text-align:center;margin-top:2px;margin-bottom:5px;padding-top:${isThermal ? '0' : '10mm'}"><img src="${shopInfo.value.LogoData}" style="width:${logoSizePx};height:${logoSizePx};object-fit:contain;border:none"></div>`
      : '';
    const captureContent = logoHtml + contentHtml;

    const docHtml = '<!DOCTYPE html><html><head><meta charset="utf-8"><style>' +
      '@font-face{font-family:"Pyidaungsu";src:local("Pyidaungsu")}' +
      'html,body{margin:0;padding:0;background:white}' +
      'body{font-family:"Pyidaungsu","Noto Sans Myanmar","Myanmar Text",sans-serif;line-height:1.45;-webkit-font-smoothing:antialiased;text-rendering:optimizeLegibility}' +
      '#capture-root{width:' + captureWidth + ';min-height:' + bodyHeight + ';margin:0 auto;padding-bottom:' + capturePaddingBottom + ';background:white}' +
      '#capture-root > div{margin-left:auto!important;margin-right:auto!important}' +
      '*{box-sizing:border-box;word-break:normal;overflow-wrap:anywhere}' +
      'p{line-height:1.45!important}' +
      '#print-area{line-height:1.45!important;padding-bottom:12px!important}' +
      '#print-area>div{overflow:visible!important}' +
      '#print-area div[style*="border-bottom"]{margin-top:8px!important;margin-bottom:8px!important}' +
      '#print-area div[style*="justify-content:space-between"]{line-height:1.6!important;align-items:flex-start!important;gap:8px!important;flex-wrap:wrap!important;overflow:visible!important}' +
      '#print-area div[style*="justify-content:space-between"] span{line-height:1.6!important;min-height:1.6em!important;white-space:normal!important;overflow:visible!important;text-overflow:clip!important}' +
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

    iframe = document.createElement('iframe');
    iframe.style.position = 'fixed';
    iframe.style.left = '0';
    iframe.style.top = '0';
    iframe.style.width = captureWidth;
    iframe.style.height = '1px';
    iframe.style.zIndex = '-1';
    iframe.style.border = 'none';
    iframe.style.background = 'white';
    document.body.appendChild(iframe);

    const iframeDoc = iframe.contentDocument || iframe.contentWindow.document;
    iframeDoc.open();
    iframeDoc.write(docHtml);
    iframeDoc.close();

    await new Promise((resolve) => {
      iframe.onload = () => setTimeout(resolve, 400);
      setTimeout(resolve, 600);
    });

    const captureRoot = iframeDoc.getElementById('capture-root') || iframeDoc.body;
    const captureHeight = Math.max(captureRoot.scrollHeight, captureRoot.offsetHeight);
    const { default: html2canvas } = await import('html2canvas');
    const canvas = await html2canvas(captureRoot, {
      scale: androidBridge.isAndroid() && isThermal ? 2 : Math.max(3, window.devicePixelRatio || 2),
      backgroundColor: '#ffffff',
      useCORS: true,
      allowTaint: true,
      logging: false,
      foreignObjectRendering: false,
      width: captureRoot.scrollWidth,
      height: captureHeight,
    });

    return await new Promise((resolve) => {
      canvas.toBlob((b) => resolve(b), 'image/png', 1.0);
    });
  } catch (err) {
    console.error('[ShareInvoice] Capture failed:', err);
    return null;
  } finally {
    if (iframe?.parentNode) {
      iframe.parentNode.removeChild(iframe);
    }
  }
};

const blobToDataUrl = (blob) => new Promise((resolve, reject) => {
  const reader = new FileReader();
  reader.onload = () => resolve(reader.result || '');
  reader.onerror = () => reject(reader.error || new Error('Could not read invoice image.'));
  reader.readAsDataURL(blob);
});

const downloadBlob = (blob, fileName) => {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
};

// Helper function for date display in print
const formatDateOnly = (d) => new Date(d).toLocaleDateString('en-GB');
const totalQty = computed(() => selectedSale.value.items?.reduce((sum, i) => sum + i.Quantity, 0) || 0);
const route = useRoute(); // ✅ ဒီကောင် define မလုပ်ထားရင် white screen ဖြစ်တတ်ပါတယ်
const isAdmin = ref(localStorage.getItem('Role') === 'Admin');
const currentTab = ref('inventory-ledger');
const historyData = ref([]);
const totalItems = ref(0);
const isExportingCsv = ref(false);
const branches = ref([]);
const showDeliFormModal = ref(false);
const deliForm = ref({
  saleId: null,
  customerName: '',
  customerPhone: '',
  customerAddress: '',
  amount: 0,
  deliveryFee: 0,
  note: ''
});
const deliFormTotal = computed(() =>
  Math.max(0, Number(deliForm.value.amount || 0) + Number(deliForm.value.deliveryFee || 0))
);
const selectedCustomerName = computed(() => selectedSale.value.sale?.CustomerName || 'Walk-in');
const branchName = ref(localStorage.getItem('BranchName') || 'Main Branch');
const field = (obj, pascal, camel = pascal.charAt(0).toLowerCase() + pascal.slice(1)) => obj?.[pascal] ?? obj?.[camel];
const selectedSaleBranchId = computed(() => Number(field(selectedSale.value?.sale, 'BranchId') || localStorage.getItem('BranchId') || 1));
const selectedReceiptBranch = computed(() => branches.value.find(b => Number(field(b, 'BranchId')) === selectedSaleBranchId.value) || null);
const receiptBranchName = computed(() => field(selectedReceiptBranch.value, 'BranchName') || branchName.value || 'Main Branch');
const isMainReceiptBranch = computed(() => {
  const branch = selectedReceiptBranch.value;
  if (!branch) return selectedSaleBranchId.value === 1 || /main/i.test(receiptBranchName.value);
  return Boolean(field(branch, 'IsMainBranch')) || /main/i.test(String(field(branch, 'BranchName') || ''));
});
const receiptContact = computed(() => {
  if (!isMainReceiptBranch.value && selectedReceiptBranch.value) {
    return {
      Address: field(selectedReceiptBranch.value, 'Location') || shopInfo.value.Address || '',
      Phone: field(selectedReceiptBranch.value, 'Phone') || shopInfo.value.Phone || ''
    };
  }
  return {
    Address: shopInfo.value.Address || '',
    Phone: shopInfo.value.Phone || ''
  };
});
const receiptFooterLines = computed(() => {
  const footer = String(shopInfo.value.ReceiptFooter || t('common.thanks') || 'Thank you');
  return footer.split(/\r?\n/).map(line => line.trim()).filter(Boolean);
});

const filters = ref({
  page: 1, limit: 15,
  branchId: localStorage.getItem('BranchId'),
  query: '', customerName: '', supplierName: '', startDate: '', endDate: '', orderStatus: ''
});

const customerOptions = ref([]);
const supplierOptions = ref([]);
const filteredCustomerOptions = ref([]);
const filteredSupplierOptions = ref([]);
const showCustomerFilterResults = ref(false);
const showSupplierFilterResults = ref(false);
const customerFilterHighlightedIndex = ref(-1);
const supplierFilterHighlightedIndex = ref(-1);
const customerFilterRef = ref(null);
const supplierFilterRef = ref(null);

const normalizeCustomerOption = (customer) => ({
  customerId: customer.CustomerId ?? customer.customerId ?? customer.Id ?? customer.id ?? null,
  name: customer.Name ?? customer.name ?? customer.CustomerName ?? customer.customerName ?? '',
  phone: customer.Phone ?? customer.phone ?? ''
});

const normalizeSupplierOption = (supplier) => ({
  supplierId: supplier.SupplierId ?? supplier.supplierId ?? supplier.Id ?? supplier.id ?? null,
  name: supplier.SupplierName ?? supplier.supplierName ?? supplier.Name ?? supplier.name ?? '',
  phone: supplier.Phone ?? supplier.phone ?? ''
});

const filterNamedOptions = (options, query) => {
  const q = String(query || '').toLowerCase().trim();
  return (q
    ? options.filter(option =>
      option.name.toLowerCase().includes(q) ||
      String(option.phone || '').toLowerCase().includes(q)
    )
    : options).slice(0, 50);
};

const handleCustomerFilterSearch = () => {
  filteredCustomerOptions.value = filterNamedOptions(customerOptions.value, filters.value.customerName);
  showCustomerFilterResults.value = filteredCustomerOptions.value.length > 0;
  customerFilterHighlightedIndex.value = filteredCustomerOptions.value.length > 0 ? 0 : -1;
};

const openCustomerFilterResults = () => {
  handleCustomerFilterSearch();
};

const selectCustomerFilter = (customer) => {
  filters.value.customerName = customer.name || '';
  showCustomerFilterResults.value = false;
  filteredCustomerOptions.value = [];
  customerFilterHighlightedIndex.value = -1;
};

const clearCustomerFilter = () => {
  filters.value.customerName = '';
  showCustomerFilterResults.value = false;
  filteredCustomerOptions.value = [];
  customerFilterHighlightedIndex.value = -1;
};

const moveCustomerFilterDown = () => {
  if (customerFilterHighlightedIndex.value < filteredCustomerOptions.value.length - 1) customerFilterHighlightedIndex.value++;
};

const moveCustomerFilterUp = () => {
  if (customerFilterHighlightedIndex.value > 0) customerFilterHighlightedIndex.value--;
};

const selectHighlightedCustomerFilter = () => {
  const index = customerFilterHighlightedIndex.value;
  if (index >= 0 && index < filteredCustomerOptions.value.length) selectCustomerFilter(filteredCustomerOptions.value[index]);
};

const handleSupplierFilterSearch = () => {
  filteredSupplierOptions.value = filterNamedOptions(supplierOptions.value, filters.value.supplierName);
  showSupplierFilterResults.value = filteredSupplierOptions.value.length > 0;
  supplierFilterHighlightedIndex.value = filteredSupplierOptions.value.length > 0 ? 0 : -1;
};

const openSupplierFilterResults = () => {
  handleSupplierFilterSearch();
};

const selectSupplierFilter = (supplier) => {
  filters.value.supplierName = supplier.name || '';
  showSupplierFilterResults.value = false;
  filteredSupplierOptions.value = [];
  supplierFilterHighlightedIndex.value = -1;
};

const clearSupplierFilter = () => {
  filters.value.supplierName = '';
  showSupplierFilterResults.value = false;
  filteredSupplierOptions.value = [];
  supplierFilterHighlightedIndex.value = -1;
};

const moveSupplierFilterDown = () => {
  if (supplierFilterHighlightedIndex.value < filteredSupplierOptions.value.length - 1) supplierFilterHighlightedIndex.value++;
};

const moveSupplierFilterUp = () => {
  if (supplierFilterHighlightedIndex.value > 0) supplierFilterHighlightedIndex.value--;
};

const selectHighlightedSupplierFilter = () => {
  const index = supplierFilterHighlightedIndex.value;
  if (index >= 0 && index < filteredSupplierOptions.value.length) selectSupplierFilter(filteredSupplierOptions.value[index]);
};

const closeNameFilterResults = () => {
  showCustomerFilterResults.value = false;
  showSupplierFilterResults.value = false;
  customerFilterHighlightedIndex.value = -1;
  supplierFilterHighlightedIndex.value = -1;
};

const handleNameFilterOutsideClick = (event) => {
  const target = event.target;
  if (customerFilterRef.value?.contains(target) || supplierFilterRef.value?.contains(target)) return;
  closeNameFilterResults();
};

const totalPages = computed(() => Math.ceil(totalItems.value / filters.value.limit) || 1);
const tableColspan = computed(() => {
  if (currentTab.value === 'inventory-ledger') return 11;
  if (currentTab.value === 'sale') return 8;
  if (currentTab.value === 'purchase') return 6;
  if (currentTab.value === 'return') return 7;
  if (currentTab.value === 'transfer') return 5;
  return 5;
});

const showDetailModal = ref(false);
const selectedSale = ref({ sale: {}, items: [], payments: [] });
watch([selectedSale, showDetailModal, receiptSize, showPreviousCredit, applePrintMethod, shopInfo], () => {
  epsonPreparationVersion++;
  epsonReceiptUrl.value = '';
});

const selectedSalePayments = computed(() => normalizeReceiptPayments(selectedSale.value?.payments || [], {
  method: selectedSale.value?.sale?.PaymentMethod || selectedSale.value?.sale?.paymentMethod || 'Cash',
  amount: selectedSale.value?.sale?.PaidAmount || selectedSale.value?.sale?.paidAmount || 0
}));

const fetchSalePayments = async (saleId) => {
  try {
    const res = await api.get(`/Inventory/sale-payment-methods/${saleId}`);
    return res.data || [];
  } catch (err) {
    console.error('Sale payment methods load error:', err);
    return [];
  }
};

const viewDetail = async (saleId) => {
  try {
    receiptSize.value = preferredReceiptSize();
    showPreviousCredit.value = false;
    const [detailRes, payments] = await Promise.all([
      api.get(`/Sales/details/${saleId}`),
      fetchSalePayments(saleId)
    ]);
    selectedSale.value = { ...detailRes.data, payments };
    showDetailModal.value = true;
  } catch (err) {
    console.error("Detail load error:", err);
    alert("အရောင်းအသေးစိတ် ပြန်ဖတ်၍မရပါ");
  }
};

const openSaleDetailFromQuery = async () => {
  const saleId = Number(route.query.detailSaleId || 0);
  if (!saleId || currentTab.value !== 'sale') return;
  await viewDetail(saleId);
};

const tabs = computed(() => ([
  { id: 'inventory-ledger', label: t('history.inventoryLedgerTab'), activeClass: 'bg-cyan-700 text-white shadow-lg shadow-cyan-100 font-normal' },
  { id: 'sale', label: t('history.saleTab'), activeClass: 'bg-indigo-600 text-white shadow-lg shadow-indigo-100 font-normal' },
  { id: 'purchase', label: t('history.purchaseTab'), activeClass: 'bg-orange-600 text-white shadow-lg shadow-orange-100 font-normal' },
  { id: 'return', label: t('history.returnTab'), activeClass: 'bg-emerald-600 text-white shadow-lg shadow-emerald-100 font-normal' },
  { id: 'adjustment', label: t('history.adjustmentTab'), activeClass: 'bg-gray-800 text-white shadow-lg shadow-gray-200 font-normal' },
  { id: 'transfer', label: t('history.transferTab'), activeClass: 'bg-blue-600 text-white shadow-lg shadow-blue-100 font-normal' }
]));

// ✅ URL ကလာတဲ့ query တွေကို filter ထဲ အရင်ဆုံး ထည့်ပေးမယ့် logic
const syncQueryToFilters = () => {
  if (route.query.tab) {
    currentTab.value = route.query.tab.toString();
  }

  if (route.query.search) {
    // String ဖြစ်ကြောင်း သေချာမှ replace လုပ်ပါ (Null safe)
    let q = route.query.search.toString();
    filters.value.query = q.replace('INV-', '').replace('PUR-', '');
  } else {
    filters.value.query = '';
  }
};

// ✅ Purchase Detail အတွက် State များ
const showPurchaseModal = ref(false);
const selectedPurchase = ref({ master: {}, items: [] });
const showBatchModal = ref(false);
const selectedBatchProduct = ref({});
const purchaseBatches = ref([]);
const isBatchLoading = ref(false);

// ✅ Purchase Detail ကို API မှ ခေါ်ယူပြသသည့် Function
const viewPurchaseDetail = async (id) => {
  if (!id) return;
  try {
    const res = await api.get(`/Purchases/details/${id}`);
    selectedPurchase.value = {
      master: res.data?.master || res.data?.Master || {},
      items: res.data?.items || res.data?.Items || []
    };

    // ✅ data အဝင်မှန်အောင် တစ်ချက် စစ်ဆေးခြင်း
    if (selectedPurchase.value.master) {
      showPurchaseModal.value = true;
    } else {
      alert("ဒေတာ ရှာမတွေ့ပါ");
    }
  } catch (err) {
    console.error("Purchase detail load error:", err);
    alert("အဝယ်အသေးစိတ် ပြန်ဖတ်၍မရပါ");
  }
};

// ✅ Transfer Detail State & Logic
const formatDateOnlySafe = (value) => {
  if (!value) return '-';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '-';
  return date.toLocaleDateString('en-US', { year: 'numeric', month: '2-digit', day: '2-digit' });
};

const getBatchExpiryStatus = (batch) => {
  if (!batch?.expiryDate) {
    return {
      label: 'No Expiry',
      className: 'text-slate-500',
      badgeClass: 'text-slate-600 bg-slate-50 border-slate-200'
    };
  }

  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const expiry = new Date(batch.expiryDate);
  expiry.setHours(0, 0, 0, 0);
  const daysRemaining = Math.ceil((expiry - today) / 86400000);
  const alertDays = Number(batch.alertDays ?? 30);

  if (daysRemaining < 0) {
    return {
      label: 'Expired',
      className: 'text-rose-600',
      badgeClass: 'text-rose-700 bg-rose-50 border-rose-100'
    };
  }

  if (daysRemaining <= alertDays) {
    return {
      label: `${daysRemaining} days`,
      className: 'text-amber-600',
      badgeClass: 'text-amber-700 bg-amber-50 border-amber-100'
    };
  }

  return {
    label: 'Valid',
    className: 'text-emerald-600',
    badgeClass: 'text-emerald-700 bg-emerald-50 border-emerald-100'
  };
};

const formatNullableNumber = (value) => {
  if (value === null || value === undefined || value === '') return '-';
  return formatQuantity(value);
};

const isConsignmentBatch = (batch) => String(batch?.sourceType || '').toLowerCase() === 'consignment';

const formatBatchReference = (batch) => {
  const prefix = isConsignmentBatch(batch) ? 'CON' : 'PO';
  return `${prefix}-${batch?.poId || '-'}`;
};

const getBatchSourceClass = (batch) => {
  if (isConsignmentBatch(batch)) return 'text-emerald-700 bg-emerald-50 border-emerald-100';
  return 'text-orange-700 bg-orange-50 border-orange-100';
};

const viewProductBatches = async (product) => {
  const productId = field(product, 'ProductId', 'productId');
  if (!productId) return;

  selectedBatchProduct.value = {
    productId,
    itemName: field(product, 'ItemName', 'itemName') || '-',
    barcode: field(product, 'Barcode', 'barcode') || '-',
    unitName: field(product, 'UnitName', 'unitName') || '-'
  };
  purchaseBatches.value = [];
  showBatchModal.value = true;
  isBatchLoading.value = true;

  try {
    const res = await api.get(`/History/product-purchase-batches/${productId}`, {
      params: { branchId: filters.value.branchId || null }
    });
    purchaseBatches.value = Array.isArray(res.data) ? res.data : [];
  } catch (err) {
    console.error('Stock batches load error:', err);
    addToast('Stock batches could not be loaded.', 'error');
  } finally {
    isBatchLoading.value = false;
  }
};

const showTransferModal = ref(false);
const selectedTransfer = ref({ transfer: {}, items: [] });

const viewTransferDetail = async (id) => {
  if (!id) return;
  try {
    const res = await api.get(`/History/transfer-detail/${id}`);
    selectedTransfer.value = res.data;
    showTransferModal.value = true;
  } catch (err) {
    console.error("Transfer detail load error:", err);
    addToast(t('history.transferDetailNotFound'), "error");
  }
};

const reprint = async (saleId) => {
  receiptSize.value = preferredReceiptSize();
  showPreviousCredit.value = false;
  const useAndroidNativePrint = androidBridge.supportsNativePrint() && receiptSize.value === '80mm';
  const useAppleReceiptPreview = supportsEpsonAssistant && receiptSize.value === '80mm';
  const preOpenedPrintWindow = useAndroidNativePrint || useAppleReceiptPreview ? null : window.open('', '_blank', 'width=900,height=800');
  if (!useAndroidNativePrint && !useAppleReceiptPreview && !preOpenedPrintWindow) {
    addToast(t('history.popupBlocked') || 'Popup blocked. Please allow popups to print.', 'warning');
    return;
  }

  try {
    // ၁။ API Endpoint ကို သေချာအောင် စစ်ပါ (မိတ်ဆွေ၏ controller အတိုင်း /Sales/details/${saleId} ဖြစ်နိုင်သည်)
    const [detailRes, payments] = await Promise.all([
      api.get(`/Sales/details/${saleId}`),
      fetchSalePayments(saleId)
    ]);

    // ၂။ Data Assign လုပ်ခြင်း
    // Backend က { sale: ..., items: ... } ပုံစံပို့လျှင် ဤအတိုင်း အဆင်ပြေပါသည်
    selectedSale.value = { ...detailRes.data, payments };

    if (useAppleReceiptPreview) {
      showDetailModal.value = true;
      await nextTick();
      if (applePrintMethod.value === 'epson') await printFromDetail();
      return;
    }

    // ၃။ DOM ထဲမှာ v-if guard တွေပွင့်ပြီး id="print-area" တွေ ပေါ်လာအောင် သေချာစောင့်ခြင်း
    // တစ်ခါတလေ nextTick တစ်ခုတည်းနှင့် မလုံလောက်တတ်၍ setTimeout လေး အနည်းငယ် ခံပေးပါမည်
    await nextTick();
    await new Promise(resolve => setTimeout(resolve, 100));
    await printFromDetail(preOpenedPrintWindow);
    if (!useAndroidNativePrint) {
      addToast(t('history.printPopupOpened') || "Voucher opened. Tap Print in the popup window.", "info");
    }
    return;

    setTimeout(() => {
      printFromDetail(preOpenedPrintWindow);
      addToast(t('history.printPopupOpened') || "Voucher opened. Tap Print in the popup window.", "info");
    }, 100); // 0.1 စက္ကန့်ခန့် စောင့်ပေးခြင်းက DOM ပေါ်လာဖို့ ပိုသေချာစေပါသည်

  } catch (err) {
    try { preOpenedPrintWindow?.close(); } catch (_) { }
    console.error("Reprint Error:", err);
    // Error တက်လျှင် မည်သည့်နေရာမှတက်သည်ကို သိနိုင်ရန် log ထုတ်ကြည့်ပါ
    addToast("Voucher ပြန်ထုတ်၍ မရပါ - " + (err.response?.data?.Message || err.message), "error");
  }
};

const escapeHtml = (value) => String(value ?? '')
  .replace(/&/g, '&amp;')
  .replace(/</g, '&lt;')
  .replace(/>/g, '&gt;')
  .replace(/"/g, '&quot;')
  .replace(/'/g, '&#39;');

const closeDeliFormModal = () => {
  showDeliFormModal.value = false;
};

const printDeliForm = async (saleId) => {
  try {
    const detailRes = await api.get(`/Sales/details/${saleId}`);
    selectedSale.value = { ...detailRes.data, payments: detailRes.data?.payments || [] };
    const sale = detailRes.data?.sale || {};
    const items = detailRes.data?.items || [];

    const customerName = sale.CustomerName || sale.customerName || 'Walk-in';
    const customerPhone = sale.CustomerPhone || sale.customerPhone || '';
    const customerAddress = sale.CustomerAddress || sale.customerAddress || '';

    const subtotal = items.reduce((sum, item) => {
      const qty = Number(item.Quantity ?? item.quantity ?? 0);
      const price = Number(item.UnitPrice ?? item.unitPrice ?? 0);
      const lineDisc = Number(item.DiscountAmount ?? item.discountAmount ?? 0);
      return sum + Math.max(0, (qty * price) - lineDisc);
    }, 0);

    const discount = Number(sale.DiscountAmount ?? sale.discountAmount ?? 0);
    const deliveryFee = Number(sale.DeliveryFee ?? sale.deliveryFee ?? 0);
    const amount = Math.max(0, subtotal - discount);

    deliForm.value = {
      saleId,
      customerName,
      customerPhone,
      customerAddress,
      amount,
      deliveryFee,
      note: sale.VoucherNote || sale.voucherNote || ''
    };
    showDeliFormModal.value = true;
  } catch (err) {
    console.error("Deli Form Load Error:", err);
    addToast("Deli Form ဖတ်၍ မရပါ - " + (err.response?.data?.Message || err.message), "error");
  }
};

const printEditedDeliForm = () => {
  try {
    const form = deliForm.value;
    const saleId = form.saleId;
    const shopName = escapeHtml(shopInfo.value?.ShopName || '');
    const branchName = escapeHtml(receiptBranchName.value || '');
    const contact = receiptContact.value || {};
    const footerLines = receiptFooterLines.value || [];

    const formatMoney = (val) => Number(val || 0).toLocaleString();
    const amount = Math.max(0, Number(form.amount || 0));
    const deliveryFee = Math.max(0, Number(form.deliveryFee || 0));
    const total = amount + deliveryFee;
    const note = String(form.note || '').trim();

    const html = `
      <div style="width:76mm;padding:4mm;font-family:'Pyidaungsu','Segoe UI',sans-serif;font-size:11px;color:black;line-height:1.4;">
        <div style="text-align:center;margin-bottom:4mm;">
          <div style="font-size:14px;font-weight:bold;text-transform:uppercase;">${shopName}</div>
          <div style="font-size:11px;font-weight:bold;">(${branchName})</div>
          ${contact.Address ? `<div style="font-size:9px;">${escapeHtml(contact.Address)}</div>` : ''}
          ${contact.Phone ? `<div style="font-size:9px;">Tel: ${escapeHtml(contact.Phone)}</div>` : ''}
        </div>
        <div style="border-bottom:1px dashed black;margin:3mm 0;"></div>
        <div style="text-align:center;font-size:13px;font-weight:bold;margin:3mm 0;">DELI FORM</div>
        <div style="border-bottom:1px dashed black;margin:3mm 0;"></div>
        <div style="margin:2mm 0;">
          <div style="display:flex;justify-content:space-between;margin-bottom:1mm;">
            <span>Name:</span>
            <span style="font-weight:bold;text-align:right;">${escapeHtml(form.customerName || '-')}</span>
          </div>
          <div style="display:flex;justify-content:space-between;margin-bottom:1mm;">
            <span>Ph No:</span>
            <span style="text-align:right;">${escapeHtml(form.customerPhone || '-')}</span>
          </div>
          <div style="display:flex;justify-content:space-between;margin-bottom:1mm;">
            <span>Address:</span>
            <span style="text-align:right;max-width:48mm;white-space:pre-line;">${escapeHtml(form.customerAddress || '-')}</span>
          </div>
        </div>
        <div style="border-bottom:1px dashed black;margin:3mm 0;"></div>
        <div style="margin:2mm 0;">
          <div style="display:flex;justify-content:space-between;margin-bottom:1mm;">
            <span>Amount:</span>
            <span>${formatMoney(amount)} Ks</span>
          </div>
          <div style="display:flex;justify-content:space-between;margin-bottom:1mm;">
            <span>Deli Fee:</span>
            <span>${formatMoney(deliveryFee)} Ks</span>
          </div>
          <div style="border-bottom:1px solid black;margin:2mm 0;"></div>
          <div style="display:flex;justify-content:space-between;font-size:13px;font-weight:bold;">
            <span>Total:</span>
            <span>${formatMoney(total)} Ks</span>
          </div>
        </div>
        <div style="border-bottom:1px dashed black;margin:3mm 0;"></div>
        ${note ? `<div style="font-size:10px;font-style:italic;margin-top:2mm;white-space:pre-line;">Note: ${escapeHtml(note)}</div><div style="border-bottom:1px dashed black;margin:3mm 0;"></div>` : ''}
        <div style="text-align:center;margin-top:4mm;">
          ${footerLines.map(line => `<div style="font-size:10px;font-weight:bold;margin:1mm 0;">${escapeHtml(line)}</div>`).join('')}
        </div>
      </div>
    `;

    const result = openPrintWindow({
      title: 'Deli Form - SA' + String(saleId).padStart(7, '0'),
      pageSize: '80mm auto',
      bodyWidth: '76mm',
      bodyPadding: '0',
      html: html,
      autoPrint: !isMobileBrowser()
    });

    if (!result.ok) {
      addToast(t('history.popupBlocked') || 'Popup blocked. Please allow popups to print.', 'warning');
      return;
    }
    showDeliFormModal.value = false;
  } catch (err) {
    console.error("Deli Form Print Error:", err);
    addToast("Deli Form ထုတ်၍ မရပါ - " + (err.response?.data?.Message || err.message), "error");
  }
};

const loadData = async () => {
  try {
    const res = await api.get(`/History/${currentTab.value}-history`, { params: filters.value });
    historyData.value = res.data.list || [];
    totalItems.value = res.data.total || 0;
  } catch (err) {
    console.error("History Load Error:", err);
    historyData.value = [];
  }
};

const csvValueOf = (item, pascal, camel = pascal.charAt(0).toLowerCase() + pascal.slice(1), fallback = '') =>
  item?.[camel] ?? item?.[pascal] ?? fallback;

const buildCsvPayload = (tab, sourceData) => {
  let headers = [];
  let rows = [];

  if (tab === 'sale') {
    headers = [t('history.invoiceNo'), t('history.dateTime'), t('history.customer'), t('history.netAmount'), t('history.saleType'), t('history.status'), t('history.notes')];
    rows = sourceData.map(item => {
      const saleId = csvValueOf(item, 'SaleId', 'saleId', '');
      return [
        `SA${String(saleId).padStart(7, '0')}`,
        formatDateTime(saleId ? csvValueOf(item, 'SaleDate', 'saleDate', null) : null), // Use safe date check
        csvValueOf(item, 'CustomerName', 'customerName', t('common.walkInCustomer')) || t('common.walkInCustomer'),
        getSaleNetAmount(item),
        csvValueOf(item, 'SaleType', 'saleType', 'Normal') || 'Normal',
        csvValueOf(item, 'OrderStatus', 'orderStatus', 'Completed') || 'Completed',
        csvValueOf(item, 'VoucherNote', 'voucherNote', '')
      ];
    });
  } else if (tab === 'purchase') {
    headers = [t('history.poNumber'), t('history.orderDate'), t('history.supplierName'), (t('history.paymentMethod') || 'Payment'), t('history.totalCost')];
    rows = sourceData.map(item => [
      `PO-${csvValueOf(item, 'POId', 'poId', '')}`,
      formatDateTime(csvValueOf(item, 'OrderDate', 'orderDate')),
      csvValueOf(item, 'SupplierName', 'supplierName', 'General Supplier') || 'General Supplier',
      csvValueOf(item, 'PaymentMethod', 'paymentMethod', ''),
      csvValueOf(item, 'TotalAmount', 'totalAmount', 0)
    ]);
  } else if (tab === 'return') {
    headers = [t('history.dateTime'), t('history.voucherNo') || 'Voucher No', t('history.category'), t('history.productName'), t('history.qty'), t('history.refund'), t('history.reason')];
    rows = sourceData.map(item => {
      const returnType = csvValueOf(item, 'ReturnType', 'returnType');
      const voucherNo = returnType === 'Sale'
        ? `SA-${String(csvValueOf(item, 'SaleId', 'saleId', 0)).padStart(7, '0')}`
        : `PO-${String(csvValueOf(item, 'POId', 'poId', 0)).padStart(7, '0')}`;

      return [
        formatDateTime(csvValueOf(item, 'ReturnDate', 'returnDate')),
        voucherNo,
        returnType,
        csvValueOf(item, 'ProductName', 'productName'),
        csvValueOf(item, 'Quantity', 'quantity', 0),
        csvValueOf(item, 'RefundAmount', 'refundAmount', 0),
        csvValueOf(item, 'Reason', 'reason')
      ];
    });
  } else if (tab === 'adjustment') {
    headers = [t('history.dateTime'), t('history.product'), t('history.type'), t('history.qty'), t('history.reason')];
    rows = sourceData.map(item => [
      formatDateTime(csvValueOf(item, 'AdjustmentDate', 'adjustmentDate')),
      csvValueOf(item, 'ProductName', 'productName'),
      csvValueOf(item, 'Type', 'type'),
      csvValueOf(item, 'Quantity', 'quantity', 0),
      csvValueOf(item, 'Reason', 'reason')
    ]);
  } else if (tab === 'transfer') {
    headers = [t('history.dateTime'), t('history.branchFlow'), t('history.items'), t('history.status')];
    rows = sourceData.map(item => [
      formatDateTime(csvValueOf(item, 'TransferDate', 'transferDate')),
      `${csvValueOf(item, 'FromBranchName', 'fromBranchName')} -> ${csvValueOf(item, 'ToBranchName', 'toBranchName')}`,
      csvValueOf(item, 'ItemCount', 'itemCount', 0),
      csvValueOf(item, 'Status', 'status')
    ]);
  } else if (tab === 'inventory-ledger') {
    headers = [
      t('common.no'),
      t('history.barcode'),
      t('history.itemName'),
      t('history.category'),
      t('history.unit'),
      t('history.latestPurchasePrice'),
      t('history.latestSalePrice'),
      t('history.totalPurchased'),
      t('history.totalSold'),
      t('history.totalReturned'),
      t('history.totalStockRemains')
    ];
    rows = sourceData.map((item, index) => [
      index + 1,
      csvValueOf(item, 'Barcode', 'barcode'),
      csvValueOf(item, 'ItemName', 'itemName'),
      csvValueOf(item, 'CategoryName', 'categoryName', 'Uncategorized'),
      csvValueOf(item, 'UnitName', 'unitName'),
      csvValueOf(item, 'LatestPurchasePrice', 'latestPurchasePrice', 0),
      csvValueOf(item, 'LatestSalePrice', 'latestSalePrice', 0),
      csvValueOf(item, 'TotalPurchased', 'totalPurchased', 0),
      csvValueOf(item, 'TotalSold', 'totalSold', 0),
      csvValueOf(item, 'TotalReturned', 'totalReturned', 0),
      csvValueOf(item, 'TotalStockRemains', 'totalStockRemains', 0)
    ]);
  }

  return { headers, rows };
};

const fetchFilteredExportData = async (tab) => {
  const exportLimit = Math.max(Number(totalItems.value) || 0, 100000);
  const params = {
    ...filters.value,
    page: 1,
    limit: exportLimit
  };
  const res = await api.get(`/History/${tab}-history`, { params });
  return Array.isArray(res.data?.list) ? res.data.list : [];
};

const handleExportCsv = async () => {
  if (isExportingCsv.value) return;
  isExportingCsv.value = true;

  try {
    const tab = currentTab.value;
    const exportData = await fetchFilteredExportData(tab);
    const { headers, rows } = buildCsvPayload(tab, exportData);
    const dateStr = new Date().toISOString().split('T')[0];
    const tabLabel = tabs.value.find(t => t.id === tab)?.label || tab;
    exportToCsv(`${tabLabel}_History_${dateStr}`, headers, rows);
  } catch (err) {
    console.error("History Export Error:", err);
    addToast("CSV export failed", "error");
  } finally {
    isExportingCsv.value = false;
  }
};

function formatDateTime(dateStr) {
  if (!dateStr) return "-";

  const raw = String(dateStr).trim();
  const match = raw.match(/^(\d{4})-(\d{2})-(\d{2})(?:[T\s](\d{2}):(\d{2})(?::(\d{2})(?:\.\d+)?)?(?:Z|[+-]\d{2}:?\d{2})?)?$/);

  if (match) {
    const [, year, month, day, hour = '00', minute = '00'] = match;
    const hourNumber = Number(hour);
    const hour12 = hourNumber % 12 || 12;
    const period = hourNumber >= 12 ? 'PM' : 'AM';
    return `${month}/${day}/${year}, ${String(hour12).padStart(2, '0')}:${minute} ${period}`;
  }

  const date = new Date(dateStr);
  if (isNaN(date.getTime())) return dateStr;
  return date.toLocaleString('en-US', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: true });
}



const fetchBranches = async () => {
  try {
    const res = await api.get('/Branches');
    branches.value = Array.isArray(res.data) ? res.data : [];
    localStorage.setItem('VersaBranches', JSON.stringify(branches.value));
  } catch (err) {
    console.error(err);
    branches.value = JSON.parse(localStorage.getItem('VersaBranches') || '[]');
  }
};

const fetchNameFilterOptions = async () => {
  const [customerRes, supplierRes] = await Promise.all([
    api.get('/customers').catch(() => ({ data: [] })),
    api.get('/suppliers').catch(() => ({ data: [] }))
  ]);

  customerOptions.value = (Array.isArray(customerRes.data) ? customerRes.data : [])
    .map(normalizeCustomerOption)
    .filter(customer => customer.name);
  supplierOptions.value = (Array.isArray(supplierRes.data) ? supplierRes.data : [])
    .map(normalizeSupplierOption)
    .filter(supplier => supplier.name);
};

const resetAndLoad = () => { filters.value.page = 1; loadData(); };
const prevPage = () => { if (filters.value.page > 1) { filters.value.page--; loadData(); } };
const nextPage = () => { filters.value.page++; loadData(); };

onMounted(async () => {
  document.addEventListener('mousedown', handleNameFilterOutsideClick);
  document.addEventListener('touchstart', handleNameFilterOutsideClick);
  syncQueryToFilters(); // ၁။ Filter တွေကို အရင်ပြင်ပါ
  await Promise.all([
    fetchBranches(), // ၂။ Branch တွေယူပါ
    fetchSettings(),
    fetchNameFilterOptions()
  ]);
  await loadData();      // ၃။ ဒေတာ စတင်ခေါ်ပါ
  await openSaleDetailFromQuery();
});

onUnmounted(() => {
  document.removeEventListener('mousedown', handleNameFilterOutsideClick);
  document.removeEventListener('touchstart', handleNameFilterOutsideClick);
});

// ✅ စာမျက်နှာပွင့်နေတုန်း နောက်ထပ် Link တစ်ခု ထပ်နှိပ်ရင်လည်း အလုပ်လုပ်အောင် စောင့်ကြည့်ပါ
watch(() => route.query, () => {
  syncQueryToFilters();
  loadData().then(openSaleDetailFromQuery);
}, { deep: true });

watch(currentTab, () => {
  filters.value.page = 1;
  filters.value.customerName = currentTab.value === 'sale' ? filters.value.customerName : '';
  filters.value.supplierName = currentTab.value === 'purchase' ? filters.value.supplierName : '';
  closeNameFilterResults();
  loadData();
});
</script>
<style scoped>
.tabular-nums {
  font-variant-numeric: tabular-nums;
}

.history-detail-modal-body {
  padding-bottom: max(1rem, calc(1rem + env(safe-area-inset-bottom)));
}

.history-detail-modal-panel {
  max-height: calc(100dvh - 1rem);
}

/* Clean custom scrollbar for the table and modal */
::-webkit-scrollbar {
  width: 5px;
  height: 5px;
}

::-webkit-scrollbar-track {
  background: transparent;
}

::-webkit-scrollbar-thumb {
  background: #e2e8f0;
  border-radius: 10px;
}

::-webkit-scrollbar-thumb:hover {
  background: #cbd5e1;
}
</style>




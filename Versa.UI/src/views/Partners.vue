<template>
    <div class="px-3 sm:px-5 lg:px-8 pt-2 pb-6 sm:pb-8 bg-[#f8fafc] min-h-screen font-sans text-left">

        <!-- Page Header -->
        <div class="flex flex-col md:flex-row justify-between items-start md:items-end mb-6 gap-4">
            <div>
                <h1 class="text-2xl font-normal text-slate-900 tracking-tight">{{ t('partners.title') }}</h1>
                <p class="text-[11px] font-normal text-slate-400 uppercase tracking-widest mt-1 flex items-center">
                    <span class="w-2 h-2 bg-blue-500 rounded-full mr-2 shadow-[0_0_8px_rgba(59,130,246,0.5)]"></span>
                    {{ t('partners.subtitle') }}
                </p>
            </div>
        </div>

        <!-- Tab Bar -->
        <div
            class="flex flex-wrap gap-1 mb-8 bg-slate-200/60 p-1.5 rounded-xl border border-slate-200 w-fit transition-all">
            <button @click="activeTab = 'customers'"
                :class="activeTab === 'customers' ? 'bg-white text-indigo-600 shadow-sm' : 'text-slate-500 hover:text-slate-700'"
                class="px-8 py-2.5 rounded-lg font-normal text-[11px] uppercase tracking-wide transition-all duration-300">
                {{ t('customers.title') }}
            </button>
            <button @click="activeTab = 'suppliers'"
                :class="activeTab === 'suppliers' ? 'bg-white text-orange-600 shadow-sm' : 'text-slate-500 hover:text-slate-700'"
                class="px-8 py-2.5 rounded-lg font-normal text-[11px] uppercase tracking-wide transition-all duration-300">
                {{ t('suppliers.title') }}
            </button>
            <button @click="activeTab = 'staff'"
                :class="activeTab === 'staff' ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500 hover:text-slate-700'"
                class="px-8 py-2.5 rounded-lg font-normal text-[11px] uppercase tracking-wide transition-all duration-300">
                {{ t('users.title') }}
            </button>
        </div>

        <!-- ===================== CUSTOMERS TAB ===================== -->
        <div v-if="activeTab === 'customers'">
            <div class="flex flex-col md:flex-row justify-between items-start md:items-end mb-6 gap-4">
                <div class="relative flex-1 md:w-80">
                    <span class="absolute inset-y-0 left-0 pl-3 flex items-center text-slate-400">
                        <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
                        </svg>
                    </span>
                    <input v-model="cust.searchQuery" type="text" :placeholder="t('customers.searchPlaceholder')"
                        class="w-full pl-10 pr-4 py-2.5 bg-white border border-slate-200 rounded-xl text-xs font-normal outline-none focus:ring-2 focus:ring-blue-500/20 transition-all">
                </div>
                <button @click="cust.openModal()"
                    class="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2.5 rounded-xl font-normal shadow-lg shadow-blue-100 transition-all active:scale-95 uppercase text-xs tracking-widest flex items-center gap-2 whitespace-nowrap">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
                        <path fill-rule="evenodd"
                            d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z"
                            clip-rule="evenodd" />
                    </svg>
                    {{ t('customers.addCustomer') }}
                </button>
            </div>

            <div class="bg-white rounded-xl shadow-sm border border-slate-200 table-responsive">
                <table class="w-full text-left border-collapse">
                    <thead class="bg-[#3f3f46] text-white">
                        <tr class="text-[10px] font-normal uppercase tracking-[0.2em]">
                            <th class="p-5 w-20 text-center opacity-70">{{ t('customers.tableNo') }}</th>
                            <th class="p-5">{{ t('customers.tableName') }}</th>
                            <th class="p-5">{{ t('customers.tablePhone') }}</th>
                            <th class="p-5 text-right">{{ t('customers.tableDebt') }}</th>
                            <th class="p-5 text-right px-8">{{ t('customers.tableActions') }}</th>
                        </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-50">
                        <tr v-for="(item, index) in cust.paginated" :key="index"
                            class="hover:bg-slate-50/80 transition-colors group">
                            <td class="p-5 text-center text-slate-400 font-normal tabular-nums">{{ ((cust.currentPage -
                                1) * cust.pageSize) + index + 1 }}</td>
                            <td class="p-5">
                                <div class="flex items-center gap-4">
                                    <div
                                        class="w-9 h-9 rounded-lg bg-blue-50 flex items-center justify-center text-blue-600 font-normal uppercase text-xs border border-blue-100 transition-transform group-hover:scale-110">
                                        {{ (item.Name || item.name || 'C').charAt(0) }}
                                    </div>
                                    <div class="font-normal text-slate-800 text-sm tracking-tight">{{ item.Name ||
                                        item.name }}</div>
                                </div>
                            </td>
                            <td class="p-5">
                                <span
                                    class="bg-slate-100 text-slate-500 font-mono text-[11px] px-2.5 py-1 rounded-md border border-slate-200/50 tabular-nums">{{
                                        item.Phone || item.phone || '-' }}</span>
                            </td>
                            <td class="p-5 text-right font-normal tabular-nums text-sm">
                                <span
                                    :class="(item.CurrentBalance || item.currentBalance) > 0 ? 'text-rose-600' : 'text-emerald-600'">
                                    {{ (item.CurrentBalance || item.currentBalance || 0).toLocaleString() }} <small
                                        class="text-[10px] font-normal opacity-60">KS</small>
                                </span>
                            </td>
                            <td class="p-5 text-right px-8">
                                <div class="flex justify-end gap-2">
                                    <button v-if="(item.CurrentBalance || item.currentBalance) > 0"
                                        @click="cust.openPayment(item)"
                                        class="bg-emerald-600 hover:bg-emerald-700 text-white px-3 py-1.5 rounded-lg text-[10px] font-normal uppercase tracking-wider transition-all shadow-sm active:scale-95">
                                        {{ t('customers.collectPayment') }}
                                    </button>
                                    <button @click="cust.openOrders(item)"
                                        class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-amber-600 hover:bg-amber-50 transition-all shadow-sm"
                                        :title="t('sale.preorder') || 'Orders'">
                                        <svg class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                                            <path stroke-linecap="round" stroke-linejoin="round" d="M9 12h6m-6 4h6m-7 4h8a2 2 0 002-2V6a2 2 0 00-2-2H8a2 2 0 00-2 2v12a2 2 0 002 2z" />
                                        </svg>
                                    </button>
                                    <button @click="cust.openDetail(item)"
                                        class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-indigo-600 hover:bg-indigo-50 transition-all shadow-sm">
                                        <svg class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                                            <path stroke-linecap="round" stroke-linejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                                            <path stroke-linecap="round" stroke-linejoin="round" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                                        </svg>
                                    </button>
                                    <button @click="cust.openModal(item)"
                                        class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-blue-600 hover:bg-blue-50 transition-all shadow-sm">
                                        <svg class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2"
                                            viewBox="0 0 24 24">
                                            <path
                                                d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                                        </svg>
                                    </button>
                                    <button @click="cust.deleteItem(item.CustomerId || item.customerId)"
                                        class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-rose-600 hover:bg-rose-50 transition-all shadow-sm">
                                        <svg class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2"
                                            viewBox="0 0 24 24">
                                            <path
                                                d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                                        </svg>
                                    </button>
                                </div>
                            </td>
                        </tr>
                        <tr v-if="cust.list.length === 0">
                            <td colspan="5" class="p-24 text-center">
                                <div class="flex flex-col items-center opacity-20 grayscale">
                                    <svg class="w-12 h-12 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                        <path
                                            d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z"
                                            stroke-width="2" />
                                    </svg>
                                    <p class="text-xs font-normal uppercase tracking-[0.3em] text-slate-900">{{
                                        t('customers.noRecords') }}</p>
                                </div>
                            </td>
                        </tr>
                    </tbody>
                </table>
                <div v-if="cust.filtered.length > 0"
                    class="flex justify-between items-center px-6 py-4 bg-slate-50 border-t border-slate-200">
                    <div class="text-[10px] font-normal text-slate-400 uppercase tracking-widest">{{ t('customers.title')
                        }}: {{ cust.filtered.length }}</div>
                    <div class="flex items-center gap-1.5">
                        <button @click="cust.currentPage--" :disabled="cust.currentPage === 1"
                            class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">{{
                                t('common.prev') }}</button>
                        <span class="px-3 text-xs font-normal text-slate-400">{{ t('common.page') }} {{ cust.currentPage
                            }} / {{ cust.totalPages }}</span>
                        <button @click="cust.currentPage++" :disabled="cust.currentPage === cust.totalPages"
                            class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">{{
                                t('common.next') }}</button>
                    </div>
                </div>
            </div>

            <!-- Customer Modal -->
            <div v-if="cust.showModal"
                class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
                <div class="bg-white w-full max-w-md rounded-xl shadow-2xl overflow-hidden border border-slate-200">
                    <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
                        <h3 class="text-lg font-normal uppercase tracking-tight">{{ cust.form.CustomerId ?
                            t('customers.modalTitleEdit') : t('customers.modalTitleAdd') }}</h3>
                        <button @click="cust.showModal = false"
                            class="text-white/60 hover:text-white transition-colors text-2xl">×</button>
                    </div>
                    <div class="p-8 space-y-5">
                        <div class="space-y-1.5">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                t('customers.labelName') }}</label>
                            <input v-model="cust.form.Name" type="text" :placeholder="t('customers.placeholderName')"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all">
                        </div>
                        <div class="space-y-1.5">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                t('customers.labelPhone') }}</label>
                            <input v-model="cust.form.Phone" type="text" :placeholder="t('customers.placeholderPhone')"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm tracking-widest transition-all">
                        </div>
                        <div class="space-y-1.5">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                t('customers.labelAddress') }}</label>
                            <textarea v-model="cust.form.Address" rows="3"
                                :placeholder="t('customers.placeholderAddress')"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all resize-none"></textarea>
                        </div>
                        <button @click="cust.save()"
                            class="w-full bg-blue-600 text-white py-4 rounded-lg font-normal text-xs shadow-lg shadow-blue-100 hover:bg-blue-700 transition active:scale-95 mt-4 uppercase tracking-widest">{{
                                t('customers.btnSave') }}</button>
                    </div>
                </div>
            </div>

            <!-- Payment Modal -->
            <div v-if="cust.showPaymentModal"
                class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
                <div class="bg-white w-full max-w-md rounded-xl shadow-2xl overflow-hidden border border-slate-200">
                    <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
                        <h3 class="text-lg font-normal uppercase tracking-tight">{{ t('customers.paymentTitle') }}</h3>
                        <button @click="cust.showPaymentModal = false"
                            class="text-white/60 hover:text-white transition-colors text-2xl">×</button>
                    </div>
                    <div class="p-8 space-y-5">
                        <div class="bg-blue-50 border border-blue-100 rounded-xl p-4 flex items-center gap-3">
                            <div
                                class="w-10 h-10 bg-white rounded-lg flex items-center justify-center text-blue-600 shadow-sm text-lg">
                                👤</div>
                            <div>
                                <p class="text-[10px] text-blue-400 font-normal uppercase leading-none mb-1">Customer
                                    Account</p>
                                <p class="text-sm font-normal text-blue-800">{{ cust.selectedCustomer?.Name ||
                                    cust.selectedCustomer?.name }}</p>
                            </div>
                        </div>
                        <div class="space-y-1.5">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                t('customers.hDate') }}</label>
                            <input v-model="cust.paymentForm.PaymentDate" type="date"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all">
                        </div>
                        <div class="space-y-1.5">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                t('customers.paymentAmount') }} (Ks)</label>
                            <input v-model.number="cust.paymentForm.Amount" type="number"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-emerald-500/20 font-normal text-xl text-emerald-600 transition-all tabular-nums">
                        </div>
                        <div class="space-y-1.5">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                t('customers.paymentMethod') }}</label>
                            <select v-model="cust.paymentForm.PaymentMethod"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm">
                                <option v-for="method in availablePaymentMethods" :key="method" :value="method">
                                    {{ method }}
                                </option>
                            </select>
                        </div>
                        <button @click="cust.submitPayment()"
                            class="w-full bg-emerald-600 text-white py-4 rounded-xl font-normal uppercase text-xs tracking-widest shadow-lg shadow-emerald-100 hover:bg-emerald-700 transition active:scale-95 mt-4">{{
                                t('customers.paymentTitle') }}</button>
                    </div>
                </div>
            </div>

            <!-- Customer Orders Modal -->
            <div v-if="cust.showOrdersModal"
                class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
                <div class="bg-white w-full max-w-4xl rounded-2xl shadow-2xl overflow-hidden animate-in zoom-in duration-200 border border-slate-200">
                    <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
                        <div>
                            <h3 class="text-lg font-normal uppercase tracking-tight">{{ t('sale.preorder') || 'Orders' }}</h3>
                            <p class="text-blue-300 text-[10px] font-normal uppercase mt-0.5 tracking-widest italic">
                                {{ cust.ordersCustomer?.Name || cust.ordersCustomer?.name || '-' }}
                            </p>
                        </div>
                        <button @click="cust.showOrdersModal = false" class="text-white/60 hover:text-white transition-colors">
                            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                            </svg>
                        </button>
                    </div>

                    <div class="p-6 bg-white">
                        <div class="border border-slate-100 rounded-xl overflow-hidden">
                            <div class="bg-slate-50 px-4 py-3 border-b border-slate-100 flex items-center justify-between">
                                <h4 class="text-[10px] font-normal uppercase tracking-widest text-slate-500">
                                    {{ t('customers.customerDetail') || 'Order History' }}
                                </h4>
                                <button @click="cust.refreshOrders()"
                                    class="text-[10px] font-normal text-blue-600 uppercase tracking-widest hover:text-blue-800 transition-colors">
                                    {{ t('common.refresh') || 'Refresh' }}
                                </button>
                            </div>
                            <div class="max-h-[65vh] overflow-y-auto">
                                <table class="w-full text-left text-xs">
                                    <thead class="bg-slate-50 sticky top-0 text-[10px] uppercase text-slate-600 border-b border-slate-200">
                                        <tr>
                                            <th class="py-2 px-4 font-normal text-slate-600">Date</th>
                                            <th class="py-2 px-4 font-normal text-slate-600">Voucher No</th>
                                            <th class="py-2 px-4 font-normal text-right text-slate-600">Amount</th>
                                            <th class="py-2 px-4 font-normal text-right text-slate-600">Paid</th>
                                            <th class="py-2 px-4 font-normal text-right text-slate-600">Remain</th>
                                            <th class="py-2 px-4 font-normal text-slate-600">Method</th>
                                            <th class="py-2 px-4 font-normal text-slate-600">Status</th>
                                            <th class="py-2 px-4 font-normal text-right text-slate-600">Action</th>
                                        </tr>
                                    </thead>
                                    <tbody class="divide-y divide-slate-50">
                                        <tr v-for="sale in cust.orders" :key="sale.SaleId || sale.saleId" class="hover:bg-slate-50/50">
                                            <td class="py-2 px-4 text-slate-500">{{ formatDateOnly(sale.SaleDate || sale.saleDate) }}</td>
                                            <td class="py-2 px-4 text-blue-600 font-normal">{{ getSaleNo(sale) }}</td>
                                            <td class="py-2 px-4 text-right tabular-nums text-slate-700 font-normal">{{ (sale.TotalAmount || sale.totalAmount || 0).toLocaleString() }}</td>
                                            <td class="py-2 px-4 text-right tabular-nums text-emerald-600 font-normal">{{ (sale.PaidAmount || sale.paidAmount || 0).toLocaleString() }}</td>
                                            <td class="py-2 px-4 text-right tabular-nums text-rose-600 font-normal">{{ (sale.DueAmount || sale.dueAmount || 0).toLocaleString() }}</td>
                                            <td class="py-2 px-4 text-slate-500">{{ sale.PaymentMethod || sale.paymentMethod || '-' }}</td>
                                            <td class="py-2 px-4">
                                                <span :class="cust.statusClass(sale.OrderStatus || sale.orderStatus)" class="text-[9px] font-normal uppercase tracking-wider px-2 py-1 rounded-full border">
                                                    {{ sale.OrderStatus || sale.orderStatus || 'Completed' }}
                                                </span>
                                            </td>
                                            <td class="py-2 px-4 text-right">
                                                <div class="flex justify-end gap-2">
                                                    <button v-if="(sale.DueAmount || sale.dueAmount || 0) > 0" @click="cust.openOrderPayment(sale)"
                                                        class="px-3 py-1.5 rounded-lg text-[10px] font-normal uppercase tracking-wider bg-emerald-600 text-white hover:bg-emerald-700 transition-all active:scale-95">
                                                        {{ t('customers.collectPayment') || 'Pay' }}
                                                    </button>
                                                    <button @click="cust.viewOrderDetail(sale)"
                                                        class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-blue-600 hover:bg-blue-50 transition-all shadow-sm active:scale-95"
                                                        title="View voucher detail">
                                                        <svg class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                                                            <path stroke-linecap="round" stroke-linejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                                                            <path stroke-linecap="round" stroke-linejoin="round" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                                                        </svg>
                                                    </button>
                                                    <button v-if="cust.canDeliver(sale)" @click="cust.setOrderStatus(sale, 'Completed')"
                                                        class="px-3 py-1.5 rounded-lg text-[10px] font-normal uppercase tracking-wider bg-slate-900 text-white hover:bg-black transition-all active:scale-95">
                                                        {{ t('sale.confirm') || 'Deliver' }}
                                                    </button>
                                                    <span v-else-if="((sale.OrderStatus || sale.orderStatus) || 'Completed') === 'Preordered' && ((sale.DueAmount || sale.dueAmount || 0) > 0)"
                                                        class="text-[9px] text-rose-600 font-normal uppercase tracking-wider">
                                                        {{ t('sale.dueBalance') || 'Due Balance' }}
                                                    </span>
                                                    <button v-if="cust.canCancel(sale)" @click="cust.setOrderStatus(sale, 'Cancelled')"
                                                        class="px-3 py-1.5 rounded-lg text-[10px] font-normal uppercase tracking-wider bg-rose-600 text-white hover:bg-rose-700 transition-all active:scale-95">
                                                        {{ t('common.cancel') || 'Cancel' }}
                                                    </button>
                                                </div>
                                            </td>
                                        </tr>
                                        <tr v-if="!cust.orders?.length">
                                            <td colspan="8" class="py-10 text-center text-emerald-600 text-[10px] uppercase">
                                                No Orders
                                            </td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>
                        </div>

                        <div class="mt-6">
                            <button @click="cust.showOrdersModal = false"
                                class="w-full py-4 bg-[#3f3f46] text-white rounded-xl font-normal uppercase text-xs tracking-widest shadow-lg transition active:scale-[0.98]">
                                {{ t('common.close') || 'Close View' }}
                            </button>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Order Payment Modal (per-order remaining balance) -->
            <div v-if="cust.showOrderPaymentModal"
                class="fixed inset-0 z-[110] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
                <div class="bg-white w-full max-w-lg rounded-2xl shadow-2xl overflow-hidden animate-in zoom-in duration-200 border border-slate-200">
                    <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
                        <div>
                            <h3 class="text-lg font-normal uppercase tracking-tight">{{ t('customers.collectPayment') || 'Collect Payment' }}</h3>
                            <p class="text-blue-300 text-[10px] font-normal uppercase mt-0.5 tracking-widest italic">
                                {{ getSaleNo(cust.selectedOrder) }}
                            </p>
                        </div>
                        <button @click="cust.showOrderPaymentModal = false" class="text-white/60 hover:text-white transition-colors">
                            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                            </svg>
                        </button>
                    </div>
                    <div class="p-8 space-y-5">
                        <div class="space-y-1.5">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">
                                {{ t('customers.hDate') || 'Date' }}
                            </label>
                            <input v-model="cust.orderPaymentForm.PaymentDate" type="date"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all">
                        </div>
                        <div class="space-y-1.5">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">
                                {{ t('customers.paymentAmount') || 'Amount' }} (Ks)
                            </label>
                            <input v-model.number="cust.orderPaymentForm.Amount" type="number"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-emerald-500/20 font-normal text-xl text-emerald-600 transition-all tabular-nums">
                        </div>
                        <div class="space-y-1.5">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">
                                {{ t('customers.paymentMethod') || 'Method' }}
                            </label>
                            <select v-model="cust.orderPaymentForm.PaymentMethod"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm">
                                <option v-for="method in availablePaymentMethods" :key="method" :value="method">
                                    {{ method }}
                                </option>
                            </select>
                        </div>
                        <div class="space-y-1.5">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">Reference</label>
                            <input v-model="cust.orderPaymentForm.ReferenceNo" type="text"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all">
                        </div>
                        <div class="space-y-1.5">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">Note</label>
                            <input v-model="cust.orderPaymentForm.Note" type="text"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all">
                        </div>

                        <button @click="cust.submitOrderPayment()"
                            class="w-full bg-emerald-600 text-white py-4 rounded-xl font-normal uppercase text-xs tracking-widest shadow-lg shadow-emerald-100 hover:bg-emerald-700 transition active:scale-95 mt-2">
                            {{ t('customers.collectPayment') || 'Collect Payment' }}
                        </button>
                    </div>
                </div>
            </div>

            <!-- Customer Detail Modal -->
            <div v-if="cust.showDetailModal"
                class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
                <div class="bg-white w-full max-w-2xl rounded-2xl shadow-2xl overflow-hidden animate-in zoom-in duration-200 border border-slate-200">
                    <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
                        <div>
                            <h3 class="text-lg font-normal uppercase tracking-tight">{{ t('customers.customerDetail') || 'Customer Detail' }}</h3>
                            <p class="text-blue-300 text-[10px] font-normal uppercase mt-0.5 tracking-widest italic">ID: CUS-{{ String(cust.selectedCustomer?.CustomerId || cust.selectedCustomer?.customerId || '').padStart(5, '0') }}</p>
                        </div>
                        <button @click="cust.showDetailModal = false" class="text-white/60 hover:text-white transition-colors">
                            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                            </svg>
                        </button>
                    </div>
                    <div class="p-6">
                        <div class="grid grid-cols-2 gap-6 mb-6 bg-slate-50 p-4 rounded-xl border border-slate-100">
                            <div>
                                <label class="text-[9px] font-normal text-slate-400 uppercase block mb-1">{{ t('customers.labelName') }}</label>
                                <p class="font-normal text-slate-800 text-lg">{{ cust.selectedCustomer?.Name || cust.selectedCustomer?.name || '-' }}</p>
                            </div>
                            <div class="text-right">
                                <label class="text-[9px] font-normal text-slate-400 uppercase block mb-1">{{ t('customers.labelPhone') }}</label>
                                <p class="font-normal text-slate-800">{{ cust.selectedCustomer?.Phone || cust.selectedCustomer?.phone || '-' }}</p>
                            </div>
                        </div>
                        <div class="mb-6 bg-slate-50 p-4 rounded-xl border border-slate-100">
                            <label class="text-[9px] font-normal text-slate-400 uppercase block mb-1">{{ t('customers.labelAddress') }}</label>
                            <p class="font-normal text-slate-800">{{ cust.selectedCustomer?.Address || cust.selectedCustomer?.address || '-' }}</p>
                        </div>
                        <div class="bg-blue-50/50 p-5 rounded-2xl space-y-3 border border-blue-100">
                            <div class="flex justify-between items-center">
                                <span class="text-[10px] font-normal uppercase text-slate-500">{{ t('customers.tableDebt') }}:</span>
                                <span :class="(cust.selectedCustomer?.CurrentBalance || cust.selectedCustomer?.currentBalance) > 0 ? 'text-rose-600' : 'text-emerald-600'" class="text-2xl font-normal tabular-nums">
                                    {{ (cust.selectedCustomer?.CurrentBalance || cust.selectedCustomer?.currentBalance || 0).toLocaleString() }} Ks
                                </span>
                            </div>
                        </div>
                        <div class="mt-6 border border-slate-100 rounded-xl overflow-hidden">
                            <div class="bg-slate-50 px-4 py-3 border-b border-slate-100">
                                <h4 class="text-[10px] font-normal uppercase tracking-widest text-slate-500">Debt Detail</h4>
                            </div>
                            <div class="max-h-56 overflow-y-auto">
                                <table class="w-full text-left text-xs">
                                    <thead class="bg-slate-50 sticky top-0 text-[10px] uppercase text-slate-600 border-b border-slate-200">
                                        <tr>
                                            <th class="py-2 px-4 font-normal text-slate-600">Date</th>
                                            <th class="py-2 px-4 font-normal text-slate-600">Voucher No</th>
                                            <th class="py-2 px-4 font-normal text-right text-slate-600">Amount</th>
                                            <th class="py-2 px-4 font-normal text-right text-slate-600">Paid</th>
                                            <th class="py-2 px-4 font-normal text-right text-slate-600">Remain</th>
                                            <th class="py-2 px-4 font-normal text-slate-600">Method</th>
                                        </tr>
                                    </thead>
                                    <tbody class="divide-y divide-slate-50">
                                        <tr v-for="sale in cust.debtSources" :key="sale.SaleId || sale.saleId" class="hover:bg-slate-50/50">
                                            <td class="py-2 px-4 text-slate-500">{{ formatDateOnly(sale.SaleDate || sale.saleDate) }}</td>
                                            <td class="py-2 px-4 text-blue-600 font-normal">{{ getSaleNo(sale) }}</td>
                                            <td class="py-2 px-4 text-right tabular-nums text-slate-700 font-normal">{{ (sale.TotalAmount || sale.totalAmount || 0).toLocaleString() }}</td>
                                            <td class="py-2 px-4 text-right tabular-nums text-emerald-600 font-normal">{{ (sale.PaidAmount || sale.paidAmount || 0).toLocaleString() }}</td>
                                            <td class="py-2 px-4 text-right tabular-nums text-rose-600 font-normal">{{ (sale.DueAmount || sale.dueAmount || 0).toLocaleString() }}</td>
                                            <td class="py-2 px-4 text-slate-500">{{ sale.PaymentMethod || sale.paymentMethod || '-' }}</td>
                                        </tr>
                                        <tr v-if="!cust.debtSources?.length">
                                            <td colspan="6" class="py-4 text-center text-emerald-600 text-[10px] uppercase">No Outstanding Credit Sales</td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>
                        </div>
                        <div class="mt-8">
                            <button @click="cust.showDetailModal = false"
                                class="w-full py-4 bg-[#3f3f46] text-white rounded-xl font-normal uppercase text-xs tracking-widest shadow-lg transition active:scale-[0.98]">
                                {{ t('common.close') || 'Close View' }}
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- ===================== SUPPLIERS TAB ===================== -->
        <div v-if="activeTab === 'suppliers'">
            <div class="flex flex-col md:flex-row justify-between items-start md:items-end mb-6 gap-4">
                <div class="relative flex-1 md:w-80">
                    <span class="absolute inset-y-0 left-0 pl-3 flex items-center text-slate-400">
                        <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
                        </svg>
                    </span>
                    <input v-model="supp.searchQuery" type="text" :placeholder="t('suppliers.searchPlaceholder')"
                        class="w-full pl-10 pr-4 py-2.5 bg-white border border-slate-200 rounded-xl text-xs font-normal outline-none focus:ring-2 focus:ring-blue-500/20 transition-all">
                </div>
                <button @click="supp.openModal()"
                    class="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2.5 rounded-xl font-normal shadow-lg shadow-blue-100 transition-all active:scale-95 uppercase text-xs tracking-widest flex items-center gap-2 whitespace-nowrap">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
                        <path fill-rule="evenodd"
                            d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z"
                            clip-rule="evenodd" />
                    </svg>
                    {{ t('suppliers.addSupplier') }}
                </button>
            </div>

            <div class="bg-white rounded-xl shadow-sm border border-slate-200 table-responsive">
                <table class="w-full text-left border-collapse">
                    <thead class="bg-[#3f3f46] text-white">
                        <tr class="text-[10px] font-normal uppercase tracking-[0.2em]">
                            <th class="p-5 w-20 text-center opacity-70">{{ t('suppliers.tableNo') }}</th>
                            <th class="p-5">{{ t('suppliers.tableName') }}</th>
                            <th class="p-5">{{ t('suppliers.tablePhone') }}</th>
                            <th class="p-5 text-right">{{ t('suppliers.tableDebt') }}</th>
                            <th class="p-5 text-right px-8">{{ t('suppliers.tableActions') }}</th>
                        </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-50">
                        <tr v-for="(item, index) in supp.paginated" :key="item.SupplierId || item.supplierId"
                            class="hover:bg-slate-50/80 transition-colors group">
                            <td class="p-5 text-center text-slate-400 font-normal tabular-nums">{{ ((supp.currentPage -
                                1) * supp.pageSize) + index + 1 }}</td>
                            <td class="p-5">
                                <div class="flex items-center gap-4">
                                    <div
                                        class="w-9 h-9 rounded-lg bg-blue-50 flex items-center justify-center text-blue-600 font-normal uppercase text-xs border border-blue-100 transition-transform group-hover:scale-110">
                                        {{ (item.SupplierName || item.supplierName || 'S').charAt(0) }}
                                    </div>
                                    <div>
                                        <div class="font-normal text-slate-800 text-sm tracking-tight">{{
                                            item.SupplierName || item.supplierName }}</div>
                                        <div class="text-[10px] text-slate-400 font-normal">{{ item.ContactPerson ||
                                            item.contactPerson || '' }}</div>
                                    </div>
                                </div>
                            </td>
                            <td class="p-5">
                                <span
                                    class="bg-slate-100 text-slate-500 font-mono text-[11px] px-2.5 py-1 rounded-md border border-slate-200/50 tabular-nums">{{
                                        item.Phone || item.phone || '-' }}</span>
                            </td>
                            <td class="p-5 text-right font-normal tabular-nums text-sm">
                                <span
                                    :class="(item.CurrentDebt || item.currentDebt) > 0 ? 'text-rose-600' : 'text-emerald-600'">
                                    {{ (item.CurrentDebt || item.currentDebt || 0).toLocaleString() }} <small
                                        class="text-[10px] font-normal opacity-60">KS</small>
                                </span>
                            </td>
                            <td class="p-5 text-right px-8">
                                <div class="flex justify-end gap-2">
                                    <button v-if="(item.CurrentDebt || item.currentDebt) > 0"
                                        @click="supp.openPayment(item)"
                                        class="bg-blue-600 hover:bg-blue-700 text-white px-3 py-1.5 rounded-lg text-[10px] font-normal uppercase tracking-wider transition-all shadow-sm active:scale-95">
                                        {{ t('suppliers.makePayment') }}
                                    </button>
                                    <button @click="supp.openDetail(item)"
                                        class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-orange-600 hover:bg-orange-50 transition-all shadow-sm">
                                        <svg class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                                            <path stroke-linecap="round" stroke-linejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                                            <path stroke-linecap="round" stroke-linejoin="round" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                                        </svg>
                                    </button>
                                    <button @click="supp.openModal(item)"
                                        class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-blue-600 hover:bg-blue-50 transition-all shadow-sm">
                                        <svg class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2"
                                            viewBox="0 0 24 24">
                                            <path
                                                d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                                        </svg>
                                    </button>
                                    <button @click="supp.deleteItem(item.SupplierId || item.supplierId)"
                                        class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-rose-600 hover:bg-rose-50 transition-all shadow-sm">
                                        <svg class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2"
                                            viewBox="0 0 24 24">
                                            <path
                                                d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                                        </svg>
                                    </button>
                                </div>
                            </td>
                        </tr>
                        <tr v-if="supp.list.length === 0">
                            <td colspan="5" class="p-24 text-center opacity-20 grayscale">
                                <p class="text-xs font-normal uppercase tracking-[0.3em]">{{ t('suppliers.noRecords') }}
                                </p>
                            </td>
                        </tr>
                    </tbody>
                </table>
                <div v-if="supp.filtered.length > 0"
                    class="flex justify-between items-center px-6 py-4 bg-slate-50 border-t border-slate-200">
                    <div class="text-[10px] font-normal text-slate-400 uppercase tracking-widest">{{ t('suppliers.title')
                        }}: {{ supp.filtered.length }}</div>
                    <div class="flex items-center gap-1.5">
                        <button @click="supp.currentPage--" :disabled="supp.currentPage === 1"
                            class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">{{
                                t('common.prev') }}</button>
                        <span class="px-3 text-xs font-normal text-slate-400">{{ t('common.page') }} {{ supp.currentPage
                            }} / {{ supp.totalPages }}</span>
                        <button @click="supp.currentPage++" :disabled="supp.currentPage === supp.totalPages"
                            class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">{{
                                t('common.next') }}</button>
                    </div>
                </div>
            </div>

            <!-- Supplier Modal -->
            <div v-if="supp.showModal"
                class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
                <div class="bg-white w-full max-w-lg rounded-xl shadow-2xl overflow-hidden border border-slate-200">
                    <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
                        <h3 class="text-lg font-normal uppercase tracking-tight">{{ supp.isEdit ?
                            t('suppliers.modalTitleEdit') : t('suppliers.modalTitleAdd') }}</h3>
                        <button @click="supp.showModal = false"
                            class="text-white/60 hover:text-white transition-colors text-2xl">×</button>
                    </div>
                    <div class="p-8 space-y-5">
                        <div class="grid grid-cols-1 md:grid-cols-2 gap-5">
                            <div class="md:col-span-2 space-y-1.5">
                                <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                    t('suppliers.labelName') }}</label>
                                <input v-model="supp.form.SupplierName" type="text"
                                    :placeholder="t('suppliers.placeholderName')"
                                    class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all" />
                            </div>
                            <div class="space-y-1.5">
                                <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                    t('suppliers.labelContact') }}</label>
                                <input v-model="supp.form.ContactPerson" type="text"
                                    :placeholder="t('suppliers.placeholderContact')"
                                    class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm" />
                            </div>
                            <div class="space-y-1.5">
                                <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                    t('suppliers.labelPhone') }}</label>
                                <input v-model="supp.form.Phone" type="text"
                                    :placeholder="t('suppliers.placeholderPhone')"
                                    class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm" />
                            </div>
                            <div class="md:col-span-2 space-y-1.5">
                                <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                    t('suppliers.labelAddress') }}</label>
                                <textarea v-model="supp.form.Address" rows="3"
                                    :placeholder="t('suppliers.placeholderAddress')"
                                    class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm resize-none"></textarea>
                            </div>
                        </div>
                        <div class="pt-4 flex gap-3">
                            <button @click="supp.showModal = false"
                                class="flex-1 py-3 bg-slate-100 text-slate-600 rounded-lg font-normal uppercase text-[10px] hover:bg-slate-200 transition-all">{{
                                    t('suppliers.btnCancel') }}</button>
                            <button @click="supp.save()"
                                class="flex-1 py-3 bg-blue-600 text-white rounded-lg font-normal uppercase text-[10px] shadow-lg shadow-blue-200 hover:bg-blue-700 transition active:scale-95">{{
                                    t('suppliers.btnSave') }}</button>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Supplier Payment Modal -->
            <div v-if="supp.showPaymentModal"
                class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
                <div class="bg-white w-full max-w-md rounded-xl shadow-2xl overflow-hidden border border-slate-200">
                    <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
                        <h3 class="text-lg font-normal uppercase tracking-tight">{{ t('suppliers.paymentTitle') }}</h3>
                        <button @click="supp.showPaymentModal = false"
                            class="text-white/60 hover:text-white transition-colors text-2xl">×</button>
                    </div>
                    <div class="p-8 space-y-5">
                        <div class="bg-blue-50 border border-blue-100 rounded-xl p-4">
                            <p class="text-[10px] text-blue-400 font-normal uppercase leading-none mb-1">Paying To Vendor
                            </p>
                            <p class="text-sm font-normal text-blue-800">{{ supp.selectedSupplier?.SupplierName }}</p>
                        </div>
                        <div class="space-y-1.5">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                t('suppliers.hDate') }}</label>
                            <input v-model="supp.paymentForm.PaymentDate" type="date"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm transition-all">
                        </div>
                        <div class="space-y-1.5">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                t('suppliers.paymentAmount') }} (Ks)</label>
                            <input v-model.number="supp.paymentForm.Amount" type="number"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-xl text-blue-600 transition-all tabular-nums">
                        </div>
                        <div class="space-y-1.5">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                t('suppliers.paymentChannel') }}</label>
                            <select v-model="supp.paymentForm.PaymentMethod"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg font-normal text-sm outline-none">
                                <option value="Cash">Cash Disbursement</option>
                                <option value="KPay">KPay / Wave</option>
                                <option value="Bank Transfer">Direct Bank Transfer</option>
                            </select>
                        </div>
                        <button @click="supp.submitPayment()"
                            class="w-full bg-blue-600 text-white py-4 rounded-xl font-normal uppercase text-xs tracking-widest shadow-lg shadow-blue-100 hover:bg-blue-700 transition active:scale-95 mt-4">{{
                                t('suppliers.paymentTitle') }}</button>
                    </div>
                </div>
            </div>

            <!-- Supplier Detail Modal -->
            <div v-if="supp.showDetailModal"
                class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
                <div class="bg-white w-full max-w-2xl rounded-2xl shadow-2xl overflow-hidden animate-in zoom-in duration-200 border border-slate-200">
                    <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
                        <div>
                            <h3 class="text-lg font-normal uppercase tracking-tight">{{ t('suppliers.supplierDetail') || 'Supplier Detail' }}</h3>
                            <p class="text-orange-400 text-[10px] font-normal uppercase mt-1 tracking-widest">
                                ID: SUP-{{ String(supp.selectedSupplier?.SupplierId || supp.selectedSupplier?.supplierId || '').padStart(5, '0') }}
                            </p>
                        </div>
                        <button @click="supp.showDetailModal = false" class="text-white/60 hover:text-white transition-colors">
                            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                            </svg>
                        </button>
                    </div>
                    <div class="p-6">
                        <div class="grid grid-cols-2 gap-6 mb-6 bg-slate-50 p-4 rounded-xl border border-slate-100">
                            <div>
                                <label class="text-[9px] font-normal text-slate-400 uppercase block mb-1">{{ t('suppliers.labelName') }}</label>
                                <p class="font-normal text-slate-800 text-lg">{{ supp.selectedSupplier?.SupplierName || supp.selectedSupplier?.supplierName || '-' }}</p>
                            </div>
                            <div class="text-right">
                                <label class="text-[9px] font-normal text-slate-400 uppercase block mb-1">{{ t('suppliers.labelContact') }}</label>
                                <p class="font-normal text-slate-800">{{ supp.selectedSupplier?.ContactPerson || supp.selectedSupplier?.contactPerson || '-' }}</p>
                            </div>
                        </div>
                        <div class="grid grid-cols-2 gap-6 mb-6 bg-slate-50 p-4 rounded-xl border border-slate-100">
                            <div class="col-span-2">
                                <label class="text-[9px] font-normal text-slate-400 uppercase block mb-1">{{ t('suppliers.labelPhone') }}</label>
                                <p class="font-normal text-slate-800">{{ supp.selectedSupplier?.Phone || supp.selectedSupplier?.phone || '-' }}</p>
                            </div>
                            <div class="col-span-2">
                                <label class="text-[9px] font-normal text-slate-400 uppercase block mb-1">{{ t('suppliers.labelAddress') }}</label>
                                <p class="font-normal text-slate-800">{{ supp.selectedSupplier?.Address || supp.selectedSupplier?.address || '-' }}</p>
                            </div>
                        </div>
                        <div class="bg-orange-50/50 p-5 rounded-2xl space-y-3 border border-orange-100">
                            <div class="flex justify-between items-center">
                                <span class="text-[10px] font-normal uppercase text-slate-500">{{ t('suppliers.tableDebt') }}:</span>
                                <span :class="(supp.selectedSupplier?.CurrentDebt || supp.selectedSupplier?.currentDebt) > 0 ? 'text-rose-600' : 'text-emerald-600'" class="text-2xl font-normal tabular-nums">
                                    {{ (supp.selectedSupplier?.CurrentDebt || supp.selectedSupplier?.currentDebt || 0).toLocaleString() }} Ks
                                </span>
                            </div>
                        </div>
                        <div class="mt-8">
                            <button @click="supp.showDetailModal = false"
                                class="w-full py-4 bg-[#3f3f46] text-white rounded-xl font-normal uppercase text-xs tracking-widest shadow-lg transition active:scale-[0.98]">
                                {{ t('common.close') || 'Close View' }}
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- ===================== STAFF TAB ===================== -->
        <div v-if="activeTab === 'staff'">
            <div class="flex flex-col md:flex-row justify-between items-start md:items-end mb-6 gap-4">
                <div class="relative flex-1 md:w-80">
                    <span class="absolute inset-y-0 left-0 pl-3 flex items-center text-slate-400">
                        <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
                        </svg>
                    </span>
                    <input v-model="staff.searchQuery" type="text" :placeholder="t('users.searchPlaceholder')"
                        class="w-full pl-10 pr-4 py-2.5 bg-white border border-slate-200 rounded-xl text-xs font-normal outline-none focus:ring-2 focus:ring-blue-500/20 transition-all">
                </div>
                <button @click="showAccessModal = true"
                    class="px-4 py-2.5 bg-white border border-slate-200 text-slate-600 rounded-xl font-normal shadow-sm hover:bg-slate-50 hover:text-blue-600 transition-all active:scale-95 uppercase text-xs tracking-widest flex items-center gap-2 whitespace-nowrap">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24"
                        stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                            d="M13 16h-1v-4h-1m1-4h.01M12 3a9 9 0 110 18 9 9 0 010-18z" />
                    </svg>
                    {{ t('users.accessInfo') }}
                </button>
                <button v-if="can('canManageUsers')" @click="staff.openCreate()"
                    class="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2.5 rounded-xl font-normal shadow-lg shadow-blue-100 transition-all active:scale-95 uppercase text-xs tracking-widest flex items-center gap-2 whitespace-nowrap">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
                        <path fill-rule="evenodd"
                            d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z"
                            clip-rule="evenodd" />
                    </svg>
                    {{ t('users.addStaff') }}
                </button>
            </div>

            <div v-if="showAccessModal"
                class="fixed inset-0 z-[110] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4 text-left">
                <div class="bg-white w-full max-w-5xl rounded-xl shadow-2xl overflow-hidden border border-slate-200">
                    <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
                        <div>
                            <h3 class="text-lg font-normal uppercase tracking-tight">{{ t('users.accessInfoTitle') }}</h3>
                            <p class="text-[10px] text-white/50 uppercase tracking-widest mt-1">{{ t('users.accessInfoSubtitle') }}</p>
                        </div>
                        <button @click="showAccessModal = false"
                            class="text-white/60 hover:text-white transition-colors text-2xl leading-none">&times;</button>
                    </div>

                    <div class="p-6 bg-slate-50 border-b border-slate-200">
                        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
                            <div v-for="role in accessRoles" :key="role"
                                class="bg-white border border-slate-200 rounded-lg px-4 py-3">
                                <div class="text-[10px] font-normal text-slate-400 uppercase tracking-widest">{{ role }}</div>
                                <div class="text-xs text-slate-600 mt-1">{{ t(roleDescriptions[role]) }}</div>
                            </div>
                        </div>
                    </div>

                    <div class="p-6 max-h-[65vh] overflow-auto">
                        <div v-for="section in accessSections" :key="section.title" class="mb-8 last:mb-0">
                            <h4 class="text-[11px] font-normal text-slate-500 uppercase tracking-widest mb-3">{{ t(section.title) }}</h4>
                            <div class="overflow-x-auto border border-slate-200 rounded-xl bg-white">
                                <table class="w-full text-left border-collapse min-w-[720px]">
                                    <thead class="bg-[#3f3f46] text-white">
                                        <tr class="text-[10px] font-normal uppercase tracking-[0.18em]">
                                            <th class="p-4">{{ t('users.accessFeature') }}</th>
                                            <th v-for="role in accessRoles" :key="role" class="p-4 text-center">{{ role }}</th>
                                        </tr>
                                    </thead>
                                    <tbody class="divide-y divide-slate-100">
                                        <tr v-for="permission in section.items" :key="permission.key" class="hover:bg-slate-50">
                                            <td class="p-4">
                                                <div class="text-sm text-slate-800">{{ t(permission.labelKey) }}</div>
                                                <div class="text-[10px] text-slate-400 uppercase tracking-wider mt-0.5">{{ permission.key }}</div>
                                            </td>
                                            <td v-for="role in accessRoles" :key="`${permission.key}-${role}`" class="p-4 text-center">
                                                <span v-if="roleHasPermission(permission.key, role)"
                                                    class="inline-flex items-center justify-center min-w-12 px-2 py-1 rounded-md bg-emerald-50 text-emerald-600 border border-emerald-100 text-[10px] uppercase tracking-widest">
                                                    {{ t('users.accessYes') }}
                                                </span>
                                                <span v-else
                                                    class="inline-flex items-center justify-center min-w-12 px-2 py-1 rounded-md bg-slate-50 text-slate-300 border border-slate-100 text-[10px] uppercase tracking-widest">
                                                    {{ t('users.accessNo') }}
                                                </span>
                                            </td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>
                        </div>
                    </div>

                    <div class="px-6 py-4 bg-white border-t border-slate-200 flex justify-end">
                        <button @click="showAccessModal = false"
                            class="px-6 py-2.5 bg-slate-900 text-white rounded-lg font-normal uppercase text-[10px] tracking-widest">
                            Close
                        </button>
                    </div>
                </div>
            </div>

            <div class="bg-white rounded-xl shadow-sm border border-slate-200 table-responsive">
                <table class="w-full text-left border-collapse">
                    <thead class="bg-[#3f3f46] text-white">
                        <tr class="text-[10px] font-normal uppercase tracking-[0.2em]">
                            <th class="p-5 w-16 text-center opacity-70">{{ t('common.no') }}</th>
                            <th class="p-5">{{ t('users.tableName') }}</th>
                            <th class="p-5">{{ t('users.tableBranch') }}</th>
                            <th class="p-5 text-center">{{ t('users.tableRole') }}</th>
                            <th class="p-5 text-center">{{ t('users.tableStatus') }}</th>
                            <th class="p-5 text-right px-8">{{ t('users.tableActions') }}</th>
                        </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-50">
                        <tr v-for="(user, idx) in staff.paginated" :key="user.UserId"
                            class="hover:bg-slate-50/80 transition-colors group">
                            <td class="p-5 text-center text-slate-400 font-normal tabular-nums">{{ ((staff.currentPage -
                                1) * staff.pageSize) + idx + 1 }}</td>
                            <td class="p-5">
                                <div class="flex items-center gap-4">
                                    <div
                                        class="w-10 h-10 rounded-lg bg-blue-50 flex items-center justify-center text-blue-600 font-normal uppercase text-sm border border-blue-100 transition-transform group-hover:scale-110">
                                        {{ (user.Username || 'U').charAt(0) }}
                                    </div>
                                    <div class="font-normal text-slate-800 text-sm tracking-tight">{{ user.Username }}
                                    </div>
                                </div>
                            </td>
                            <td class="p-5">
                                <span
                                    class="inline-flex items-center px-3 py-1 bg-slate-100 text-slate-600 rounded-md text-[10px] font-normal uppercase tracking-wide border border-slate-200/50">
                                    <span class="mr-1.5 opacity-50">📍</span> {{ user.BranchName ||
                                        t('users.unassigned') }}
                                </span>
                            </td>
                            <td class="p-5 text-center">
                                <span
                                    :class="user.Role === 'Admin' ? 'text-purple-600 bg-purple-50 border-purple-100' : 'text-blue-600 bg-blue-50 border-blue-100'"
                                    class="px-2.5 py-1 rounded-lg text-[10px] font-normal uppercase border shadow-sm tracking-tighter">{{
                                        user.Role }}</span>
                            </td>
                            <td class="p-5 text-center">
                                <button @click="staff.toggleStatus(user)" :disabled="user.Role === 'Admin'"
                                    :class="[user.IsActive ? 'bg-emerald-500' : 'bg-slate-300', user.Role === 'Admin' ? 'opacity-40 cursor-not-allowed' : 'cursor-pointer hover:shadow-md']"
                                    class="relative inline-flex h-5 w-10 items-center rounded-full transition-all duration-300">
                                    <span :class="user.IsActive ? 'translate-x-5' : 'translate-x-1'"
                                        class="inline-block h-3 w-3 transform rounded-full bg-white transition-transform duration-300"></span>
                                </button>
                            </td>
                            <td class="p-5 text-right px-8">
                                <div class="flex justify-end gap-2">
                                    <button v-if="can('canManageUsers')" @click="staff.openEdit(user)"
                                        class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-blue-600 hover:bg-blue-50 transition-all shadow-sm">
                                        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none"
                                            viewBox="0 0 24 24" stroke="currentColor">
                                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                                                d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                                        </svg>
                                    </button>
                                    <button v-if="can('canManageUsers')" @click="staff.deleteItem(user)"
                                        :disabled="user.Role === 'Admin'"
                                        class="p-2 rounded-lg bg-slate-50 text-slate-400 hover:text-rose-600 hover:bg-rose-50 transition-all shadow-sm disabled:opacity-20">
                                        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none"
                                            viewBox="0 0 24 24" stroke="currentColor">
                                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                                                d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                                        </svg>
                                    </button>
                                </div>
                            </td>
                        </tr>
                        <tr v-if="staff.filtered.length === 0">
                            <td colspan="6" class="p-24 text-center opacity-30 grayscale">
                                <p class="text-xs font-normal uppercase tracking-[0.3em]">{{ t('users.noRecords') }}</p>
                            </td>
                        </tr>
                    </tbody>
                </table>
                <div v-if="staff.filtered.length > 0"
                    class="flex justify-between items-center px-6 py-4 bg-slate-50 border-t border-slate-200">
                    <div class="text-[10px] font-normal text-slate-400 uppercase tracking-widest">{{ t('users.title') }}:
                        {{ staff.filtered.length }}</div>
                    <div class="flex items-center gap-1.5">
                        <button @click="staff.currentPage--" :disabled="staff.currentPage === 1"
                            class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">{{
                                t('common.prev') }}</button>
                        <span class="px-3 text-xs font-normal text-slate-400">{{ t('common.page') }} {{ staff.currentPage
                            }} / {{ staff.totalPages }}</span>
                        <button @click="staff.currentPage++" :disabled="staff.currentPage === staff.totalPages"
                            class="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-600 text-[10px] font-normal uppercase disabled:opacity-30 hover:bg-slate-50 transition-all">{{
                                t('common.next') }}</button>
                    </div>
                </div>
            </div>

            <!-- Staff Modal -->
            <div v-if="staff.showModal"
                class="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
                <div class="bg-white w-full max-w-md rounded-xl shadow-2xl overflow-hidden border border-slate-200">
                    <div class="bg-[#3f3f46] p-6 text-white flex justify-between items-center">
                        <h3 class="text-lg font-normal uppercase tracking-tight">{{ staff.isEdit ?
                            t('users.modalTitleEdit') : t('users.modalTitleAdd') }}</h3>
                        <button @click="staff.showModal = false"
                            class="text-white/60 hover:text-white transition-colors text-2xl">×</button>
                    </div>
                    <div class="p-8 space-y-5">
                        <div class="space-y-1.5">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                t('users.labelUser') }}</label>
                            <input v-model="staff.form.Username" type="text" :placeholder="t('users.placeholderUser')"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm">
                        </div>
                        <div class="space-y-1.5">
                            <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">Secure
                                Password {{ staff.isEdit ? '(Optional)' : '' }}</label>
                            <input v-model="staff.form.Password" type="password"
                                :placeholder="staff.isEdit ? 'Leave blank to keep current' : '••••••••'"
                                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-blue-500/20 font-normal text-sm">
                        </div>
                        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                            <div class="space-y-1.5">
                                <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                    t('users.labelRole') }}</label>
                                <select v-model="staff.form.Role"
                                    class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg font-normal text-sm outline-none">
                                    <option value="Admin">{{ t('users.administrator') }}</option>
                                    <option value="Manager">Manager</option>
                                    <option value="Staff">{{ t('users.staffMember') }}</option>
                                    <option value="Cashier">Cashier</option>
                                </select>
                            </div>
                            <div class="space-y-1.5">
                                <label class="text-[10px] font-normal text-slate-400 uppercase tracking-widest ml-1">{{
                                    t('users.labelBranch') }}</label>
                                <select v-model="staff.form.BranchId"
                                    class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg font-normal text-sm outline-none">
                                    <option v-for="b in branches" :key="b.branchId || b.BranchId"
                                        :value="b.branchId || b.BranchId">{{ b.branchName || b.BranchName }}</option>
                                </select>
                            </div>
                        </div>
                        <div class="pt-4 flex gap-3">
                            <button @click="staff.showModal = false"
                                class="flex-1 py-3 bg-slate-100 text-slate-600 rounded-lg font-normal uppercase text-[10px]">{{
                                    t('users.btnCancel') }}</button>
                            <button @click="staff.save()"
                                class="flex-1 py-3 bg-blue-600 text-white rounded-lg font-normal uppercase text-[10px] shadow-lg shadow-blue-200">{{
                                    t('users.btnSave') }}</button>
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
import { usePermissions } from '../composables/usePermissions';
import { useRouter } from 'vue-router';

const { addToast } = useToast();
const { t } = useI18n();
const { can, PERMISSIONS } = usePermissions();
const router = useRouter();
const availablePaymentMethods = ['Cash', 'KPay', 'CB Pay', 'AYA Pay', 'Wave Money', 'Banking'];

const activeTab = ref('customers');
const branches = ref([]);
const showAccessModal = ref(false);

const accessRoles = ['Admin', 'Manager', 'Staff', 'Cashier'];
const roleDescriptions = {
    Admin: 'users.accessAdminDesc',
    Manager: 'users.accessManagerDesc',
    Staff: 'users.accessStaffDesc',
    Cashier: 'users.accessCashierDesc'
};

const accessSections = [
    {
        title: 'users.accessSectionPages',
        items: [
            { key: 'canViewDashboard', labelKey: 'users.accessDashboard' },
            { key: 'canViewSale', labelKey: 'users.accessSaleTerminal' },
            { key: 'canViewProducts', labelKey: 'users.accessProducts' },
            { key: 'canViewPurchases', labelKey: 'users.accessPurchases' },
            { key: 'canViewInventory', labelKey: 'users.accessInventory' },
            { key: 'canViewAdjustments', labelKey: 'users.accessAdjustments' },
            { key: 'canViewHistory', labelKey: 'users.accessHistory' },
            { key: 'canViewExpenses', labelKey: 'users.accessExpenses' },
            { key: 'canViewMasterData', labelKey: 'users.accessMasterData' },
            { key: 'canViewPartners', labelKey: 'users.accessPartners' },
            { key: 'canViewReports', labelKey: 'users.accessReports' },
            { key: 'canViewSettings', labelKey: 'users.accessSettings' }
        ]
    },
    {
        title: 'users.accessSectionActions',
        items: [
            { key: 'canEditProducts', labelKey: 'users.accessEditProducts' },
            { key: 'canEditPurchases', labelKey: 'users.accessEditPurchases' },
            { key: 'canEditExpenses', labelKey: 'users.accessEditExpenses' },
            { key: 'canEditMasterData', labelKey: 'users.accessEditMasterData' },
            { key: 'canEditCustomers', labelKey: 'users.accessEditCustomers' },
            { key: 'canEditSuppliers', labelKey: 'users.accessEditSuppliers' },
            { key: 'canManageUsers', labelKey: 'users.accessManageUsers' },
            { key: 'canViewBusinessIntel', labelKey: 'users.accessBusinessIntel' }
        ]
    }
];

const roleHasPermission = (permissionKey, role) => {
    const allowedRoles = PERMISSIONS[permissionKey] || [];
    return allowedRoles.includes(role);
};

const tabs = computed(() => [
    { id: 'customers', label: t('customers.title') },
    { id: 'suppliers', label: t('suppliers.title') },
    { id: 'staff', label: t('users.title') },
]);

const formatDateOnly = (value) => {
    if (!value) return '-';
    return new Date(value).toLocaleDateString('en-GB');
};

const getSaleNo = (item) => {
    const saleNo = item?.SaleNo || item?.saleNo;
    if (saleNo) return saleNo;

    const saleId = item?.SaleId || item?.saleId;
    if (!saleId) return '-';
    return `SA${String(saleId).padStart(7, '0')}`;
};

// ── CUSTOMERS ──────────────────────────────────────────
const cust = reactive({
    list: [], showModal: false, showDetailModal: false, showPaymentModal: false, showOrdersModal: false, searchQuery: '', currentPage: 1, pageSize: 10,
    form: { CustomerId: null, Name: '', Phone: '', Address: '' },
    selectedCustomer: null,
    ordersCustomer: null,
    orders: [],
    showOrderPaymentModal: false,
    selectedOrder: null,
    orderPaymentForm: { Amount: 0, PaymentMethod: 'Cash', ReferenceNo: '', Note: '', PaymentDate: new Date().toISOString().substr(0, 10) },
    paymentForm: { CustomerId: 0, Amount: 0, PaymentMethod: 'Cash', ReferenceNo: '', Note: '', PaymentDate: new Date().toISOString().substr(0, 10) },
    debtSources: [],
    filtered: computed(() => {
        const q = cust.searchQuery.toLowerCase();
        return q ? cust.list.filter(c => (c.Name || c.name || '').toLowerCase().includes(q) || (c.Phone || c.phone || '').includes(q)) : cust.list;
    }),
    totalPages: computed(() => Math.max(1, Math.ceil(cust.filtered.length / cust.pageSize))),
    paginated: computed(() => { const s = (cust.currentPage - 1) * cust.pageSize; return cust.filtered.slice(s, s + cust.pageSize); }),
    async openDetail(item) {
        cust.selectedCustomer = item;
        const customerId = item.CustomerId || item.customerId;
        try {
            const debtSourcesRes = await api.get(`/customers/${customerId}/debt-sources`);
            cust.debtSources = debtSourcesRes.data || [];
        } catch (e) {
            cust.debtSources = [];
        }
        cust.showDetailModal = true;
    },
    statusClass(status) {
        const s = (status || 'Completed').toLowerCase();
        if (s === 'preordered') return 'bg-amber-50 text-amber-700 border-amber-200';
        if (s === 'delivering') return 'bg-indigo-50 text-indigo-700 border-indigo-200';
        if (s === 'cancelled') return 'bg-rose-50 text-rose-700 border-rose-200';
        return 'bg-emerald-50 text-emerald-700 border-emerald-200';
    },
    canDeliver(sale) {
        const s = ((sale?.OrderStatus || sale?.orderStatus) || 'Completed').toLowerCase();
        const remain = (sale?.DueAmount ?? sale?.dueAmount ?? 0) || 0;
        return (s === 'preordered' || s === 'delivering') && Number(remain) <= 0;
    },
    canCancel(sale) {
        const s = ((sale?.OrderStatus || sale?.orderStatus) || 'Completed').toLowerCase();
        return s === 'preordered' || s === 'delivering';
    },
    async openOrders(customer) {
        cust.ordersCustomer = customer;
        await cust.refreshOrders();
        cust.showOrdersModal = true;
    },
    async refreshOrders() {
        const customerId = cust.ordersCustomer?.CustomerId || cust.ordersCustomer?.customerId;
        if (!customerId) {
            cust.orders = [];
            return;
        }
        try {
            const res = await api.get(`/customers/${customerId}/orders`);
            cust.orders = Array.isArray(res.data) ? res.data : [];
        } catch (err) {
            cust.orders = [];
            addToast(err.response?.data?.Message || 'Failed to load orders', 'error');
        }
    },
    async setOrderStatus(sale, status) {
        const saleId = sale?.SaleId || sale?.saleId;
        if (!saleId) return;
        if (status === 'Cancelled' && !confirm(t('common.confirmChange') || 'Confirm?')) return;

        try {
            await api.put(`/Sales/orders/${saleId}/status`, { OrderStatus: status });
            addToast('Updated', 'success');
            await cust.refreshOrders();
        } catch (err) {
            addToast(err.response?.data?.Message || 'Failed to update', 'error');
        }
    },
    openOrderPayment(sale) {
        cust.selectedOrder = sale;
        const due = Number(sale?.DueAmount ?? sale?.dueAmount ?? 0) || 0;
        cust.orderPaymentForm = {
            Amount: due,
            PaymentMethod: 'Cash',
            ReferenceNo: '',
            Note: '',
            PaymentDate: new Date().toISOString().substr(0, 10),
        };
        cust.showOrderPaymentModal = true;
    },
    viewOrderDetail(sale) {
        const saleId = sale?.SaleId || sale?.saleId;
        if (!saleId) return;
        router.push({
            path: '/historycenter',
            query: {
                tab: 'sale',
                search: String(saleId),
                detailSaleId: String(saleId)
            }
        });
    },
    async submitOrderPayment() {
        const saleId = cust.selectedOrder?.SaleId || cust.selectedOrder?.saleId;
        if (!saleId) return;
        if (cust.orderPaymentForm.Amount <= 0) return addToast(t('customers.invalidAmount') || 'Invalid amount', 'warning');

        try {
            await api.post(`/Sales/${saleId}/collect-payment`, cust.orderPaymentForm);
            addToast(t('customers.paymentSuccess') || 'Payment success', 'success');
            cust.showOrderPaymentModal = false;
            await cust.refreshOrders();
        } catch (err) {
            addToast(err.response?.data?.Message || 'Payment failed', 'error');
        }
    },
    openModal(item = null) {
        if (item) { cust.form = { CustomerId: item.CustomerId || item.customerId, Name: item.Name || item.name, Phone: item.Phone || item.phone, Address: item.Address || item.address }; }
        else { cust.form = { CustomerId: null, Name: '', Phone: '', Address: '' }; }
        cust.showModal = true;
    },
    async save() {
        if (!cust.form.Name) return addToast(t('customers.identityRequired'), 'warning');
        try {
            if (cust.form.CustomerId) await api.put(`/customers/${cust.form.CustomerId}`, cust.form);
            else await api.post('/customers', cust.form);
            cust.showModal = false;
            addToast(t('customers.toastSuccess'), 'success');
            await fetchCustomers();
        } catch { addToast(t('customers.toastError'), 'error'); }
    },
    async deleteItem(id) {
        if (!confirm(t('customers.deleteConfirm'))) return;
        try { await api.delete(`/customers/${id}`); addToast(t('customers.removed'), 'success'); await fetchCustomers(); }
        catch { addToast(t('customers.deleteFailed'), 'error'); }
    },
    openPayment(customer) {
        cust.selectedCustomer = customer;
        cust.paymentForm = { CustomerId: customer.CustomerId || customer.customerId, Amount: customer.CurrentBalance || customer.currentBalance || 0, PaymentMethod: 'Cash', ReferenceNo: '', Note: '', PaymentDate: new Date().toISOString().substr(0, 10) };
        cust.showPaymentModal = true;
    },
    async submitPayment() {
        try {
            await api.post('/customers/pay-debt', cust.paymentForm);
            addToast(t('customers.paymentSuccess'), 'success');
            cust.showPaymentModal = false;
            await fetchCustomers();
            if (cust.showDetailModal && cust.selectedCustomer) {
                await cust.openDetail(cust.selectedCustomer);
            }
        }
        catch (err) { addToast(err.response?.data?.Message || err.response?.data?.message || t('customers.errorOccurred'), 'error'); }
    },
});
watch(() => cust.searchQuery, () => { cust.currentPage = 1; });

// ── SUPPLIERS ──────────────────────────────────────────
const supp = reactive({
    list: [], showModal: false, showDetailModal: false, showPaymentModal: false, isEdit: false, searchQuery: '', currentPage: 1, pageSize: 10,
    form: { SupplierId: 0, SupplierName: '', ContactPerson: '', Phone: '', Address: '' },
    selectedSupplier: null,
    paymentForm: { SupplierId: 0, Amount: 0, PaymentMethod: 'Cash', ReferenceNo: '', Note: '', PaymentDate: new Date().toISOString().split('T')[0] },
    filtered: computed(() => {
        const q = supp.searchQuery.toLowerCase();
        return q ? supp.list.filter(s => (s.SupplierName || s.supplierName || '').toLowerCase().includes(q) || (s.Phone || s.phone || '').includes(q)) : supp.list;
    }),
    totalPages: computed(() => Math.max(1, Math.ceil(supp.filtered.length / supp.pageSize))),
    paginated: computed(() => { const s = (supp.currentPage - 1) * supp.pageSize; return supp.filtered.slice(s, s + supp.pageSize); }),
    openDetail(item) {
        supp.selectedSupplier = item;
        supp.showDetailModal = true;
    },
    openModal(item = null) {
        if (item) { supp.isEdit = true; supp.form = { SupplierId: item.SupplierId || item.supplierId, SupplierName: item.SupplierName || item.supplierName, ContactPerson: item.ContactPerson || item.contactPerson, Phone: item.Phone || item.phone, Address: item.Address || item.address }; }
        else { supp.isEdit = false; supp.form = { SupplierId: 0, SupplierName: '', ContactPerson: '', Phone: '', Address: '' }; }
        supp.showModal = true;
    },
    async save() {
        try {
            if (supp.isEdit) await api.put(`/suppliers/${supp.form.SupplierId}`, supp.form);
            else await api.post('/suppliers', supp.form);
            supp.showModal = false; addToast(t('suppliers.toastSuccess'), 'success'); await fetchSuppliers();
        } catch { addToast(t('suppliers.toastError'), 'error'); }
    },
    async deleteItem(id) {
        if (confirm(t('suppliers.deleteConfirm'))) {
            try { await api.delete(`/suppliers/${id}`); await fetchSuppliers(); addToast(t('suppliers.deleted'), 'success'); }
            catch { addToast(t('suppliers.deleteError'), 'error'); }
        }
    },
    openPayment(supplier) {
        supp.selectedSupplier = supplier;
        supp.paymentForm = { SupplierId: supplier.SupplierId || supplier.supplierId, Amount: supplier.CurrentDebt || supplier.currentDebt || 0, PaymentMethod: 'Cash', ReferenceNo: '', Note: '', PaymentDate: new Date().toISOString().split('T')[0] };
        supp.showPaymentModal = true;
    },
    async submitPayment() {
        if (supp.paymentForm.Amount <= 0) return addToast(t('suppliers.invalidAmount'), 'warning');
        try {
            const supplierId = supp.paymentForm.SupplierId;
            await api.post('/suppliers/pay-debt', supp.paymentForm);
            await fetchSuppliers();
            const refreshed = supp.list.find(s => (s.SupplierId || s.supplierId) === supplierId);
            if (refreshed) supp.selectedSupplier = refreshed;
            addToast(t('suppliers.toastSuccess'), 'success');
            supp.showPaymentModal = false;
        }
        catch (err) { addToast(err.response?.data?.message || t('suppliers.toastError'), 'error'); }
    },
});
watch(() => supp.searchQuery, () => { supp.currentPage = 1; });

// ── STAFF ──────────────────────────────────────────────
const staff = reactive({
    list: [], showModal: false, isEdit: false, searchQuery: '', currentPage: 1, pageSize: 10,
    form: { UserId: 0, Username: '', Password: '', Role: 'Staff', BranchId: 1, IsActive: true },
    filtered: computed(() => {
        const q = staff.searchQuery.toLowerCase();
        return q ? staff.list.filter(u => (u.Username || '').toLowerCase().includes(q) || (u.BranchName || '').toLowerCase().includes(q)) : staff.list;
    }),
    totalPages: computed(() => Math.max(1, Math.ceil(staff.filtered.length / staff.pageSize))),
    paginated: computed(() => { const s = (staff.currentPage - 1) * staff.pageSize; return staff.filtered.slice(s, s + staff.pageSize); }),
    openCreate() {
        staff.isEdit = false;
        staff.form = { UserId: 0, Username: '', Password: '', Role: 'Staff', BranchId: branches.value[0]?.branchId || 1, IsActive: true };
        staff.showModal = true;
    },
    openEdit(user) {
        staff.isEdit = true;
        staff.form = { UserId: user.UserId, Username: user.Username, Role: user.Role, BranchId: user.BranchId, IsActive: user.IsActive, Password: '' };
        staff.showModal = true;
    },
    async save() {
        try {
            const { BranchName, ...payload } = staff.form;
            if (staff.isEdit) await api.put(`/ShopUsers/${staff.form.UserId}`, payload);
            else await api.post('/ShopUsers', payload);
            staff.showModal = false; addToast(t('users.toastSuccess'), 'success'); await fetchStaff();
        } catch (err) { addToast(err.response?.data?.Message || t('users.toastError'), 'error'); }
    },
    async deleteItem(user) {
        if (user.Role === 'Admin') return addToast(t('users.adminProtected'), 'warning');
        if (confirm(t('users.deleteConfirm'))) {
            try { await api.delete(`/ShopUsers/${user.UserId}`); await fetchStaff(); addToast(t('users.deleted'), 'success'); }
            catch { addToast(t('users.toastError'), 'error'); }
        }
    },
    async toggleStatus(user) {
        if (user.Role === 'Admin') return addToast(t('users.adminProtected'), 'warning');
        if (confirm(t('common.confirmChange'))) {
            try { await api.put(`/ShopUsers/${user.UserId}`, { ...user, IsActive: !user.IsActive }); await fetchStaff(); }
            catch { addToast(t('users.toastError'), 'error'); }
        }
    },
});
watch(() => staff.searchQuery, () => { staff.currentPage = 1; });

const fetchCustomers = async () => { try { const r = await api.get('/customers'); cust.list = r.data; } catch { } };
const fetchSuppliers = async () => {
    try {
        const r = await api.get('/suppliers');
        supp.list = (Array.isArray(r.data) ? r.data : []).map(s => ({
            ...s,
            SupplierId: s.SupplierId ?? s.supplierId,
            SupplierName: s.SupplierName ?? s.supplierName,
            ContactPerson: s.ContactPerson ?? s.contactPerson,
            Phone: s.Phone ?? s.phone,
            Address: s.Address ?? s.address,
            CurrentDebt: s.CurrentDebt ?? s.currentDebt ?? 0
        }));
        if (supp.currentPage > supp.totalPages) supp.currentPage = supp.totalPages;
    } catch { }
};
const fetchStaff = async () => { try { const r = await api.get('/ShopUsers'); staff.list = Array.isArray(r.data) ? r.data : []; } catch { } };
const fetchBranches = async () => { try { const r = await api.get('/Branches'); branches.value = r.data; } catch { } };

onMounted(() => { fetchCustomers(); fetchSuppliers(); fetchStaff(); fetchBranches(); });
</script>

<template>
  <div class="px-3 sm:px-5 lg:px-8 pt-2 pb-6 sm:pb-8 bg-gray-50 min-h-screen font-sans">
    <div class="mb-6">
      <h1 class="text-2xl font-normal text-slate-900 tracking-tight">
        {{ tx.title }}
      </h1>
      <p class="text-[11px] font-normal text-slate-400 uppercase tracking-widest mt-1 flex items-center">
        <span class="w-2 h-2 bg-slate-300 rounded-full mr-2"></span>
        {{ tx.subtitle }}
      </p>
    </div>

    <div
      class="flex flex-wrap gap-1 mb-8 bg-slate-200/60 p-1.5 rounded-xl border border-slate-200 w-fit transition-all">
      <button @click="activeTab = 'sale'" :class="activeTab === 'sale'
        ? 'bg-white text-indigo-600 shadow-sm'
        : 'text-slate-500 hover:text-slate-700'
        " class="px-8 py-2.5 rounded-lg font-normal text-[11px] uppercase tracking-wide transition-all duration-300">
        {{ tx.saleReturn }}
      </button>
      <button @click="activeTab = 'purchase'" :class="activeTab === 'purchase'
        ? 'bg-white text-orange-600 shadow-sm'
        : 'text-slate-500 hover:text-slate-700'
        " class="px-8 py-2.5 rounded-lg font-normal text-[11px] uppercase tracking-wide transition-all duration-300">
        {{ tx.purchaseReturn }}
      </button>
      <button @click="activeTab = 'adjustment'" :class="activeTab === 'adjustment'
        ? 'bg-white text-slate-900 shadow-sm'
        : 'text-slate-500 hover:text-slate-700'
        " class="px-8 py-2.5 rounded-lg font-normal text-[11px] uppercase tracking-wide transition-all duration-300">
        {{ tx.adjustment }}
      </button>
      <button @click="activeTab = 'transfer'" :class="activeTab === 'transfer'
        ? 'bg-white text-blue-600 shadow-sm'
        : 'text-slate-500 hover:text-slate-700'
        " class="px-8 py-2.5 rounded-lg font-normal text-[11px] uppercase tracking-wide transition-all duration-300">
        {{ tx.stockTransfer }}
      </button>
    </div>

    <div class=" mx-auto">
      <div v-if="activeTab === 'sale'" class="space-y-6 animate-in fade-in duration-300">
        <div class="bg-white p-6 rounded-xl shadow-sm border border-slate-100">
          <h2 class="text-sm font-normal uppercase text-indigo-600 mb-6 flex items-center gap-2">{{ tx.saleVoucherReturn
            }}</h2>
          <div class="flex gap-4">
            <input v-model="searchId" type="number" :placeholder="tx.enterVoucherId"
              class="flex-1 p-3 bg-slate-50 border border-slate-200 rounded-xl font-normal outline-none focus:ring-2 focus:ring-indigo-500/20 transition-all text-sm" />
            <button @click="searchVoucher"
              class="bg-indigo-600 text-white px-10 rounded-xl font-normal uppercase text-xs tracking-widest hover:bg-indigo-700 transition shadow-md active:scale-95">
              {{ tx.search }}
            </button>
          </div>

          <div v-if="voucherItems.length > 0"
            class="mt-6 pt-6 border-t border-slate-100 flex justify-between items-center">
            <div>
              <p class="text-[10px] text-slate-400 uppercase tracking-widest italic font-normal">
                {{ tx.actionMode }}
              </p>
              <p class="text-xs text-indigo-600 font-normal">
                Voucher #{{ searchId }} - {{ voucherItems.length }} {{ tx.items }}
              </p>
            </div>
            <button @click="handleFullReturn" :disabled="isFullReturning"
              class="bg-rose-600 text-white px-8 py-3 rounded-xl font-normal uppercase text-[10px] tracking-widest hover:bg-rose-700 transition shadow-md active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed">
              {{ tx.returnFullVoucher }}
            </button>
          </div>
        </div>

        <div v-if="voucherItems.length > 0"
          class="bg-white rounded-xl shadow-sm border border-slate-100 overflow-x-auto">
          <table class="w-full text-left">
            <thead class="bg-[#3f3f46] text-white text-[10px] font-normal uppercase tracking-widest">
              <tr>
                <th class="p-4">{{ tx.productName }}</th>
                <th class="p-4 text-center">{{ tx.qtyUnit }}</th>
                <th class="p-4 text-right">{{ tx.action }}</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-50">
              <tr v-for="item in voucherItems" :key="item.ProductId" class="hover:bg-slate-50/50 transition-colors">
                <td class="p-5 font-normal text-slate-700">{{ item.ProductName }}</td>
                <td class="p-5 text-center text-blue-600 font-normal">
                  {{ item.Quantity }} {{ item.UnitName }}
                </td>
                <td class="p-5 text-right">
                  <button @click="processSaleReturn(item)" :disabled="isSaleReturning"
                    class="bg-rose-50 text-rose-600 px-5 py-2 rounded-lg font-normal text-[10px] uppercase hover:bg-rose-100 transition disabled:opacity-50 disabled:cursor-not-allowed">
                    {{ tx.return }}
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div v-if="activeTab === 'purchase'" class="space-y-6 animate-in fade-in duration-300">
        <!-- Sub-tabs for Purchase Return -->
        <div class="flex flex-wrap gap-1 mb-2 bg-slate-200/60 p-1.5 rounded-xl border border-slate-200 w-fit transition-all">
          <button @click="purchaseSubTab = 'voucher'" :class="purchaseSubTab === 'voucher'
            ? 'bg-white text-orange-600 shadow-sm'
            : 'text-slate-500 hover:text-slate-700'
            " class="px-6 py-2 rounded-lg font-normal text-[11px] uppercase tracking-wide transition-all duration-300">
            {{ tx.byVoucher }}
          </button>
          <button @click="purchaseSubTab = 'item'" :class="purchaseSubTab === 'item'
            ? 'bg-white text-orange-600 shadow-sm'
            : 'text-slate-500 hover:text-slate-700'
            " class="px-6 py-2 rounded-lg font-normal text-[11px] uppercase tracking-wide transition-all duration-300">
            {{ tx.byItem }}
          </button>
        </div>

        <!-- By Voucher sub-tab -->
        <div v-if="purchaseSubTab === 'voucher'" class="bg-white p-6 rounded-xl shadow-sm border border-slate-100">
          <h2 class="text-sm font-normal uppercase text-orange-600 mb-6 flex items-center gap-2">{{
            tx.purchaseSupplierReturn }}</h2>
          <div class="flex gap-4">
            <input v-model="searchPOId" type="number" :placeholder="tx.enterPoId"
              class="flex-1 p-3 bg-slate-50 border border-slate-200 rounded-xl font-normal outline-none focus:ring-2 focus:ring-orange-500/20 transition-all text-sm" />
            <button @click="searchPO"
              class="bg-orange-600 text-white px-10 rounded-xl font-normal uppercase text-xs tracking-widest hover:bg-orange-700 transition">
              {{ tx.findVoucher }}
            </button>
          </div>
        </div>

        <!-- By Item sub-tab -->
        <div v-if="purchaseSubTab === 'item'" class="bg-white p-6 rounded-xl shadow-sm border border-slate-100">
          <h2 class="text-sm font-normal uppercase text-orange-600 mb-6 flex items-center gap-2">{{ tx.returnByItemTitle }}</h2>
          <p class="text-[10px] font-normal text-slate-400 uppercase tracking-widest mb-4">{{ tx.returnByItemDesc }}</p>
          <div ref="poProductSearchWrap" class="relative">
            <input v-model="poProductSearch" @input="handlePoProductSearch"
              @focus="showPoProductResults = filteredPoProducts.length > 0"
              @keydown.down.prevent="movePoProductDown" @keydown.up.prevent="movePoProductUp"
              @keydown.enter.prevent="selectPoHighlightedProduct"
              type="text" :placeholder="tx.searchProductToFindPO"
              class="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl font-normal outline-none focus:ring-2 focus:ring-orange-500/20 transition-all text-sm pl-9 pr-9" />
            <span class="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400">
              <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
              </svg>
            </span>
            <button v-if="poProductSearch" @click="clearPoProductSearch"
              class="absolute right-3 top-1/2 -translate-y-1/2 text-slate-300 hover:text-slate-500">x</button>

            <div v-if="showPoProductResults && filteredPoProducts.length > 0"
              class="absolute z-[120] left-0 right-0 mt-1 bg-white border border-slate-200 rounded-xl shadow-2xl max-h-64 overflow-y-auto">
              <div v-for="(p, idx) in filteredPoProducts" :key="p.ProductId || p.productId"
                @click="selectPoProduct(p)"
                :class="['p-3 flex items-center justify-between cursor-pointer border-b border-slate-50 last:border-0 hover:bg-orange-50 transition-colors',
                  idx === poHighlightedIndex ? 'bg-orange-50' : '']">
                <div class="flex-1 min-w-0">
                  <p class="text-xs font-normal text-slate-800 truncate">{{ p.ProductName || p.Name || p.productName || '' }}</p>
                  <p class="text-[9px] text-slate-400 font-normal uppercase tracking-tighter">{{ (p.SKU || p.Sku || p.sku || '').toString() || 'No SKU' }}</p>
                </div>
                <div class="text-right pl-3">
                  <p class="text-[10px] font-normal text-slate-600 tabular-nums">
                    {{ tx.stock }}: {{ p.BaseQuantity || p.Quantity || 0 }}
                  </p>
                </div>
              </div>
            </div>
          </div>
        </div>
        <div v-if="poItems.length > 0" class="bg-white rounded-xl shadow-sm border border-slate-100 overflow-x-auto">
          <table class="w-full text-left">
            <thead class="bg-[#3f3f46] font-normal uppercase text-[10px] text-white tracking-widest">
              <tr>
                <th class="p-5">{{ tx.productName }}</th>
                <th class="p-5 text-center">{{ tx.returnable }}</th>
                <th class="p-5 text-center w-32">{{ tx.returnQty }}</th>
                <th class="p-5 text-center">{{ tx.unit }}</th>
                <th class="p-5 text-right">{{ tx.unitCost }}</th>
                <th class="p-5 text-right">{{ tx.action }}</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-50 text-slate-700">
              <tr v-for="item in poItems" :key="item.ProductId" class="hover:bg-slate-50 transition-colors">
                <td class="p-5 font-normal text-sm">{{ item.ProductName }}</td>
                <td class="p-5 text-center font-normal text-blue-600 bg-blue-50/20">
                  {{ formatQty(item.ReturnableQty ?? item.returnableQty ?? item.StockQty) }} {{ item.UnitName }}
                </td>
                <td class="p-5 text-center">
                  <input v-model.number="item.returnQty" type="number"
                    :max="item.ReturnableQty ?? item.returnableQty ?? item.StockQty"
                    min="0"
                    class="w-full p-2 border border-slate-200 bg-slate-50 rounded-lg text-center font-normal outline-none focus:ring-2 focus:ring-orange-500/20" />
                </td>
                <td class="p-5 text-center">
                  <select v-model="item.UnitId"
                    class="p-2 border border-slate-200 rounded-lg font-normal bg-white text-[11px] outline-none">
                    <option v-for="u in item.availableUnits" :key="u.UnitId" :value="u.UnitId">
                      {{ u.UnitName }}
                    </option>
                  </select>
                </td>
                <td class="p-5 text-right font-normal tabular-nums text-sm">
                  {{ (item.CostPrice || item.UnitPrice || 0).toLocaleString() }}
                </td>
                <td class="p-5 text-right">
                  <button @click="processPurchaseReturn(item)"
                    class="bg-orange-600 text-white px-5 py-2 rounded-lg font-normal text-[10px] uppercase hover:bg-orange-700 transition">
                    {{ tx.return }}
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div v-if="activeTab === 'adjustment'"
        class="mx-auto bg-white p-8 rounded-xl shadow-sm border border-slate-100 animate-in zoom-in duration-300">
        <h2 class="text-sm font-normal uppercase text-slate-800 mb-8 border-b border-slate-100 pb-4">{{ tx.adjustmentTitle
          }}</h2>
        <div class="space-y-6">
          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase ml-1 block">{{ tx.selectProduct }}</label>
            <div ref="adjProductSearchWrap" class="relative">
              <input v-model="adjProductSearch" @input="handleAdjProductSearch" @focus="showAdjProductResults = filteredAdjProducts.length > 0"
                @keydown.down.prevent="moveAdjSearchDown" @keydown.up.prevent="moveAdjSearchUp" @keydown.enter.prevent="selectAdjHighlightedProduct"
                type="text" :placeholder="tx.selectProduct"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl font-normal outline-none focus:ring-2 focus:ring-slate-900/10 transition-all text-sm pl-9 pr-9" />
              <span class="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400">
                <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
                </svg>
              </span>
              <button v-if="adjProductSearch" @click="clearAdjProductSearch"
                class="absolute right-3 top-1/2 -translate-y-1/2 text-slate-300 hover:text-slate-500">x</button>

              <div v-if="showAdjProductResults && filteredAdjProducts.length > 0"
                class="absolute z-[120] left-0 right-0 mt-1 bg-white border border-slate-200 rounded-xl shadow-2xl max-h-64 overflow-y-auto">
                <div v-for="(p, idx) in filteredAdjProducts" :key="p.ProductId || p.productId" @click="selectAdjProduct(p)"
                  :class="['p-3 flex items-center justify-between cursor-pointer border-b border-slate-50 last:border-0 hover:bg-slate-50 transition-colors',
                    idx === adjHighlightedIndex ? 'bg-slate-50' : '']">
                  <div class="flex-1 min-w-0">
                    <p class="text-xs font-normal text-slate-800 truncate">{{ p.ProductName || p.Name || p.productName || '' }}</p>
                    <p class="text-[9px] text-slate-400 font-normal uppercase tracking-tighter">{{ (p.SKU || p.Sku || p.sku || '').toString() || 'No SKU' }}</p>
                  </div>
                  <div class="text-right pl-3">
                    <p class="text-[10px] font-normal text-slate-600 tabular-nums">
                      {{ tx.current }}: {{ p.BaseQuantity || p.Quantity || 0 }}
                    </p>
                  </div>
                </div>
              </div>
            </div>
          </div>
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <div class="col-span-2 space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase ml-1 block">{{ tx.type }}</label>
              <select v-model="adjForm.Type"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl font-normal outline-none text-sm">
                <option value="Addition">{{ tx.addition }}</option>
                <option value="Subtraction">{{ tx.subtraction }}</option>
              </select>
            </div>

            <div class="col-span-1 space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase ml-1 block">{{ tx.quantity }}</label>
              <input v-model.number="adjForm.Quantity" type="number"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl font-normal text-lg outline-none focus:ring-2 focus:ring-slate-900/10" />
            </div>

            <div class="col-span-1 space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase ml-1 block">Unit</label>
              <select v-model="adjForm.UnitId"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl font-normal outline-none text-xs">
                <option v-for="u in currentProductUnits" :key="u.UnitId" :value="u.UnitId">
                  {{ u.UnitName }}
                </option>
              </select>
            </div>
          </div>
          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase ml-1 block">{{ tx.reason }}</label>
            <textarea v-model="adjForm.Reason" rows="3"
              class="w-full p-4 bg-slate-50 border border-slate-200 rounded-xl outline-none font-normal text-xs"></textarea>
          </div>
          <button @click="submitAdjustment"
            class="w-full bg-slate-800 text-white py-4 rounded-xl font-normal uppercase text-[11px] tracking-widest shadow-md hover:bg-black transition">
            {{ tx.confirmAdjustment }}
          </button>
        </div>
      </div>

      <div v-if="activeTab === 'transfer'"
        class=" mx-auto bg-white p-8 rounded-xl shadow-sm border border-slate-100 animate-in slide-in-from-bottom-6 duration-400">
        <h2 class="text-sm font-normal uppercase text-blue-600 mb-8 border-b border-slate-100 pb-4">{{ tx.transferTitle }}
        </h2>
        <div class="space-y-6">
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase ml-1 block">{{ tx.fromBranch }}</label>
              <input :value="branchName" disabled
                class="w-full p-3 bg-slate-100 border border-slate-100 rounded-xl font-normal text-slate-400 italic cursor-not-allowed text-sm" />
            </div>
            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase ml-1 block font-normal">{{ tx.toBranchTarget }}
                (Target)</label>
              <select v-model="transferForm.ToBranchId"
                class="w-full p-3 bg-blue-50/50 border border-blue-100 rounded-xl font-normal outline-none focus:ring-2 focus:ring-blue-500/20 transition-all text-sm">
                <option :value="null" disabled>{{ tx.selectTargetBranch }}</option>
                <option v-for="b in branches" :key="b.BranchId || b.branchId" :value="Number(b.BranchId || b.branchId)">
                  {{ b.BranchName || b.branchName }}
                </option>
              </select>
            </div>
          </div>

          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase ml-1 block font-normal">{{ tx.selectProduct
              }}</label>
            <div ref="transferProductSearchWrap" class="relative">
              <input v-model="transferProductSearch" @input="handleTransferProductSearch"
                @focus="showTransferProductResults = filteredTransferProducts.length > 0"
                @keydown.down.prevent="moveTransferSearchDown" @keydown.up.prevent="moveTransferSearchUp"
                @keydown.enter.prevent="selectTransferHighlightedProduct" type="text" :placeholder="tx.selectProduct"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl font-normal outline-none text-sm pl-9 pr-9" />
              <span class="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400">
                <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" stroke-width="2" />
                </svg>
              </span>
              <button v-if="transferProductSearch" @click="clearTransferProductSearch"
                class="absolute right-3 top-1/2 -translate-y-1/2 text-slate-300 hover:text-slate-500">x</button>

              <div v-if="showTransferProductResults && filteredTransferProducts.length > 0"
                class="absolute z-[120] left-0 right-0 mt-1 bg-white border border-slate-200 rounded-xl shadow-2xl max-h-64 overflow-y-auto">
                <div v-for="(p, idx) in filteredTransferProducts" :key="p.ProductId || p.productId"
                  @click="selectTransferProduct(p)"
                  :class="['p-3 flex items-center justify-between cursor-pointer border-b border-slate-50 last:border-0 hover:bg-slate-50 transition-colors',
                    idx === transferHighlightedIndex ? 'bg-slate-50' : '']">
                  <div class="flex-1 min-w-0">
                    <p class="text-xs font-normal text-slate-800 truncate">{{ p.ProductName || p.Name || p.productName || '' }}</p>
                    <p class="text-[9px] text-slate-400 font-normal uppercase tracking-tighter">{{ (p.SKU || p.Sku || p.sku || '').toString() || 'No SKU' }}</p>
                  </div>
                  <div class="text-right pl-3">
                    <p class="text-[10px] font-normal text-slate-600 tabular-nums">
                      {{ tx.stock }}: {{ p.BaseQuantity || p.Quantity || 0 }}
                    </p>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase ml-1 block font-normal">{{ tx.transferQuantity
                }}</label>
              <input v-model.number="transferForm.Quantity" type="number"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl font-normal text-xl text-blue-600 outline-none" />
            </div>

            <div class="space-y-1.5">
              <label class="text-[10px] font-normal text-slate-400 uppercase ml-1 block font-normal">{{ tx.transferUnit
                }}</label>
              <select v-model="transferForm.UnitId"
                class="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl font-normal text-sm text-blue-600 outline-none">
                <option v-for="u in availableUnits" :key="u.UnitId" :value="u.UnitId">
                  {{ u.UnitName }}
                </option>
              </select>
            </div>
          </div>

          <div class="space-y-1.5">
            <label class="text-[10px] font-normal text-slate-400 uppercase ml-1 block font-normal">{{ tx.note }}</label>
            <input v-model="transferForm.Note" type="text" :placeholder="tx.reasonPlaceholder"
              class="w-full p-3 bg-slate-50 border border-slate-100 rounded-xl font-normal outline-none text-sm" />
          </div>

          <button @click="submitTransfer"
            class="w-full bg-blue-600 text-white py-4 rounded-xl font-normal uppercase text-[11px] tracking-widest shadow-md hover:bg-blue-700 transition active:scale-95">
            {{ tx.confirmTransfer }}
          </button>
        </div>
      </div>
    </div>
  </div>

  <div v-if="showSaleReturnModal" class="fixed inset-0 bg-slate-900/40 backdrop-blur-sm flex items-center justify-center p-4 z-[120]">
    <div class="bg-white rounded-xl border border-slate-200 shadow-2xl w-full max-w-md overflow-hidden">
      <div class="bg-[#3f3f46] px-5 py-4 flex items-center justify-between text-white">
        <h3 class="text-xs uppercase tracking-widest">{{ tx.selectReturnPaymentMethod }}</h3>
        <button @click="closeSaleReturnModal" class="text-white/70 hover:text-white">✕</button>
      </div>
      <div class="p-5 space-y-3">
        <p class="text-[11px] text-slate-500">{{ tx.chooseDeductionChannel }}</p>
        <select v-model="selectedReturnPaymentMethod"
          class="w-full p-3 bg-slate-50 border border-slate-200 rounded-lg text-sm outline-none focus:ring-2 focus:ring-indigo-500/20">
          <option v-for="m in salePaymentMethods" :key="m.method" :value="m.method">
            {{ m.method }} - {{ Number(m.amount || 0).toLocaleString() }}
          </option>
        </select>
        <div class="flex gap-2 pt-2">
          <button @click="closeSaleReturnModal"
            class="flex-1 py-2.5 rounded-lg bg-slate-100 text-slate-700 text-[11px] uppercase">{{ tx.cancel }}</button>
          <button @click="confirmSaleReturn" :disabled="isSaleReturning"
            class="flex-1 py-2.5 rounded-lg bg-rose-600 text-white text-[11px] uppercase hover:bg-rose-700 disabled:opacity-50 disabled:cursor-not-allowed">{{ tx.return }}</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted, watch, computed } from "vue";
import api from "../services/api";
import { useToast } from "../composables/useToast";
import { useI18n } from "../composables/useI18n";
const { addToast } = useToast();
const { t } = useI18n();
const currentProductUnits = ref([]);

const activeTab = ref("sale");
const purchaseSubTab = ref("voucher"); // "voucher" or "item"
const branchName = ref(localStorage.getItem("BranchName") || "Current Branch");

const products = ref([]);
const branches = ref([]);
const voucherItems = ref([]);
const poItems = ref([]);
const searchId = ref(null);
const searchPOId = ref(null);
const showSaleReturnModal = ref(false);
const pendingSaleReturnItem = ref(null);
const isSaleReturning = ref(false);
const isFullReturning = ref(false);
const salePaymentMethods = ref([]);
const selectedReturnPaymentMethod = ref("");

const adjProductSearchWrap = ref(null);
const adjProductSearch = ref("");
const showAdjProductResults = ref(false);
const adjHighlightedIndex = ref(-1);

const transferProductSearchWrap = ref(null);
const transferProductSearch = ref("");
const showTransferProductResults = ref(false);
const transferHighlightedIndex = ref(-1);

const poProductSearchWrap = ref(null);
const poProductSearch = ref("");
const showPoProductResults = ref(false);
const poHighlightedIndex = ref(-1);

const adjForm = ref({
  BranchId: localStorage.getItem("BranchId"),
  ProductId: null,
  Type: "Subtraction",
  Quantity: 0,
  Reason: "",
  UserId: localStorage.getItem("UserId"),
});
const availableUnits = ref([]);
const transferForm = ref({
  FromBranchId: Number(localStorage.getItem("BranchId")),
  ToBranchId: null,
  ProductId: null,
  Quantity: 1,
  UnitId: null,
  Note: "Manual Stock Transfer",
});
const tx = computed(() => ({
  title: t('inventoryAdjustments.title'),
  subtitle: t('inventoryAdjustments.subtitle'),
  saleReturn: t('inventoryAdjustments.saleReturn'),
  purchaseReturn: t('inventoryAdjustments.purchaseReturn'),
  adjustment: t('inventoryAdjustments.adjustment'),
  stockTransfer: t('inventoryAdjustments.stockTransfer'),
  saleVoucherReturn: t('inventoryAdjustments.saleVoucherReturn'),
  enterVoucherId: t('inventoryAdjustments.enterVoucherId'),
  actionMode: t('inventoryAdjustments.actionMode'),
  items: t('inventoryAdjustments.items'),
  returnFullVoucher: t('inventoryAdjustments.returnFullVoucher'),
  productName: t('inventoryAdjustments.productName'),
  qtyUnit: t('inventoryAdjustments.qtyUnit'),
  action: t('inventoryAdjustments.action'),
  purchaseSupplierReturn: t('inventoryAdjustments.purchaseSupplierReturn'),
  enterPoId: t('inventoryAdjustments.enterPoId'),
  findVoucher: t('inventoryAdjustments.findVoucher'),
  byVoucher: t('inventoryAdjustments.byVoucher'),
  byItem: t('inventoryAdjustments.byItem'),
  returnByItemTitle: t('inventoryAdjustments.returnByItemTitle'),
  returnByItemDesc: t('inventoryAdjustments.returnByItemDesc'),
  orSearchByItem: t('inventoryAdjustments.orSearchByItem'),
  searchProductToFindPO: t('inventoryAdjustments.searchProductToFindPO'),
  latestPO: t('inventoryAdjustments.latestPO'),
  poFound: t('inventoryAdjustments.poFound'),
  returnable: t('inventoryAdjustments.returnable'),
  returnQty: t('inventoryAdjustments.returnQty'),
  unit: t('inventoryAdjustments.unit'),
  unitCost: t('inventoryAdjustments.unitCost'),
  type: t('inventoryAdjustments.type'),
  addition: t('inventoryAdjustments.addition'),
  subtraction: t('inventoryAdjustments.subtraction'),
  quantity: t('inventoryAdjustments.quantity'),
  reason: t('inventoryAdjustments.reason'),
  adjustmentTitle: t('inventoryAdjustments.adjustmentTitle'),
  transferTitle: t('inventoryAdjustments.transferTitle'),
  fromBranch: t('inventoryAdjustments.fromBranch'),
  toBranchTarget: t('inventoryAdjustments.toBranchTarget'),
  selectTargetBranch: t('inventoryAdjustments.selectTargetBranch'),
  selectProduct: t('inventoryAdjustments.selectProduct'),
  current: t('inventoryAdjustments.current'),
  stock: t('inventoryAdjustments.stock'),
  transferQuantity: t('inventoryAdjustments.transferQuantity'),
  transferUnit: t('inventoryAdjustments.transferUnit'),
  note: t('inventoryAdjustments.note'),
  reasonPlaceholder: t('inventoryAdjustments.reasonPlaceholder'),
  search: t('inventoryAdjustments.search'),
  return: t('inventoryAdjustments.return'),
  confirmAdjustment: t('inventoryAdjustments.confirmAdjustment'),
  confirmTransfer: t('inventoryAdjustments.confirmTransfer'),
  couldNotLoadUnits: t('inventoryAdjustments.couldNotLoadUnits'),
  success: t('inventoryAdjustments.success'),
  fullReturnFailed: t('inventoryAdjustments.fullReturnFailed'),
  voucherNotFound: t('inventoryAdjustments.voucherNotFound'),
  poNotFound: t('inventoryAdjustments.poNotFound'),
  invalidQty: t('inventoryAdjustments.invalidQty'),
  returnSuccess: t('inventoryAdjustments.returnSuccess'),
  returnFailed: t('inventoryAdjustments.returnFailed'),
  qtyRequired: t('inventoryAdjustments.qtyRequired'),
  adjustmentSuccess: t('inventoryAdjustments.adjustmentSuccess'),
  failed: t('inventoryAdjustments.failed'),
  requiredFields: t('inventoryAdjustments.requiredFields'),
  transferFailed: t('inventoryAdjustments.transferFailed'),
  selectReturnPaymentMethod: 'Select Payment Method',
  chooseDeductionChannel: 'Choose which payment method should be deducted in reports.',
  cancel: t('common.cancel')
}));

const formatQty = (value) => Number(value || 0).toLocaleString(undefined, {
  minimumFractionDigits: 0,
  maximumFractionDigits: 2
});

const getApiErrorMessage = (err, fallback) => (
  err.response?.data?.Message
  || err.response?.data?.message
  || err.response?.data?.title
  || fallback
);

const fetchAvailableUnits = async () => {
  if (!transferForm.value.ProductId) {
    availableUnits.value = [];
    return;
  }

  try {
    const res = await api.get(`/Products/units/${transferForm.value.ProductId}`);
    availableUnits.value = res.data;
    if (res.data.length > 0) {
      const firstUnitId = res.data[0].UnitId || res.data[0].unitId;
      transferForm.value.UnitId = res.data[0].UnitId || res.data[0].unitId;
    }
  } catch (err) {
    console.error("Units load error:", err);
    addToast(tx.value.couldNotLoadUnits, "error");
  }
};

const fetchProducts = async () => {
  try {
    const res = await api.get(`/Inventory/branch/${localStorage.getItem("BranchId")}`);
    products.value = res.data;
    // Don't auto-select — leave search boxes empty like supplier picker
  } catch (err) {
    console.error("Load Error:", err);
  }
};

const filteredAdjProducts = computed(() => {
  const q = (adjProductSearch.value || "").trim().toLowerCase();
  if (!q) return products.value.slice(0, 30);
  return products.value
    .filter((p) => {
      const name = (p.ProductName || p.Name || p.productName || "").toString().toLowerCase();
      const sku = (p.SKU || p.Sku || p.sku || "").toString().toLowerCase();
      return name.includes(q) || sku.includes(q);
    })
    .slice(0, 30);
});

const filteredTransferProducts = computed(() => {
  const q = (transferProductSearch.value || "").trim().toLowerCase();
  if (!q) return products.value.slice(0, 30);
  return products.value
    .filter((p) => {
      const name = (p.ProductName || p.Name || p.productName || "").toString().toLowerCase();
      const sku = (p.SKU || p.Sku || p.sku || "").toString().toLowerCase();
      return name.includes(q) || sku.includes(q);
    })
    .slice(0, 30);
});

const formatTransferProductLabel = (p) => {
  const name = p?.ProductName || p?.Name || p?.productName || "";
  const qty = p?.BaseQuantity ?? p?.Quantity ?? p?.baseQuantity ?? 0;
  const stockText = tx.value?.stock || "Stock";
  return `${name} (${stockText}: ${qty})`;
};

const formatAdjProductLabel = (p) => {
  const name = p?.ProductName || p?.Name || p?.productName || "";
  const qty = p?.BaseQuantity ?? p?.Quantity ?? p?.baseQuantity ?? 0;
  const currentText = tx.value?.current || "Current";
  return `${name} (${currentText}: ${qty})`;
};

const filteredPoProducts = computed(() => {
  const q = (poProductSearch.value || "").trim().toLowerCase();
  if (!q) return [];
  return products.value
    .filter((p) => {
      const name = (p.ProductName || p.Name || p.productName || "").toString().toLowerCase();
      const sku = (p.SKU || p.Sku || p.sku || "").toString().toLowerCase();
      return name.includes(q) || sku.includes(q);
    })
    .slice(0, 30);
});

const handlePoProductSearch = () => {
  showPoProductResults.value = filteredPoProducts.value.length > 0;
  poHighlightedIndex.value = filteredPoProducts.value.length > 0 ? 0 : -1;
};

const selectPoProduct = async (p) => {
  const id = p.ProductId || p.productId;
  poProductSearch.value = (p.ProductName || p.Name || p.productName || '');
  showPoProductResults.value = false;
  poHighlightedIndex.value = -1;
  // Search for the latest PO containing this product for this branch
  try {
    const branchId = localStorage.getItem('BranchId');
    const res = await api.get(`/Inventory/find-po-by-product/${id}?branchId=${branchId}`);
    if (res.data && Array.isArray(res.data) && res.data.length > 0) {
      const mappedItems = await Promise.all(
        res.data.map(async (item) => {
          const hasProductId = item.ProductId !== null && item.ProductId !== undefined;
          const unitRes = hasProductId
            ? await api.get(`/Products/units/${item.ProductId}`)
            : { data: [] };
          return {
            ...item,
            returnQty: 1,
            UnitId: item.UnitId,
            availableUnits: unitRes.data,
          };
        }),
      );
      poItems.value = mappedItems;
      searchPOId.value = mappedItems[0]?.POId || mappedItems[0]?.poId || null;
      addToast(tx.value.poFound || 'Purchase order found.', 'success');
    } else {
      addToast(tx.value.poNotFound, 'warning');
    }
  } catch (err) {
    addToast(tx.value.poNotFound, 'error');
  }
};

const clearPoProductSearch = () => {
  poProductSearch.value = "";
  showPoProductResults.value = false;
  poHighlightedIndex.value = -1;
};

const movePoProductDown = () => {
  if (filteredPoProducts.value.length === 0) return;
  poHighlightedIndex.value = (poHighlightedIndex.value + 1) % filteredPoProducts.value.length;
};

const movePoProductUp = () => {
  if (filteredPoProducts.value.length === 0) return;
  poHighlightedIndex.value =
    (poHighlightedIndex.value - 1 + filteredPoProducts.value.length) % filteredPoProducts.value.length;
};

const selectPoHighlightedProduct = () => {
  if (poHighlightedIndex.value < 0) return;
  const p = filteredPoProducts.value[poHighlightedIndex.value];
  if (p) selectPoProduct(p);
};

const handlePoClickOutside = (e) => {
  const el = poProductSearchWrap.value;
  if (!el) return;
  if (el.contains(e.target)) return;
  showPoProductResults.value = false;
  poHighlightedIndex.value = -1;
};

const handleAdjProductSearch = () => {
  showAdjProductResults.value = filteredAdjProducts.value.length > 0;
  adjHighlightedIndex.value = filteredAdjProducts.value.length > 0 ? 0 : -1;
  adjForm.value.ProductId = null;
};

const selectAdjProduct = (p) => {
  const id = p.ProductId || p.productId;
  adjForm.value.ProductId = id;
  adjProductSearch.value = formatAdjProductLabel(p);
  showAdjProductResults.value = false;
  adjHighlightedIndex.value = -1;
};

const clearAdjProductSearch = () => {
  adjProductSearch.value = "";
  showAdjProductResults.value = false;
  adjHighlightedIndex.value = -1;
  adjForm.value.ProductId = null;
};

const moveAdjSearchDown = () => {
  if (filteredAdjProducts.value.length === 0) return;
  adjHighlightedIndex.value = (adjHighlightedIndex.value + 1) % filteredAdjProducts.value.length;
};

const moveAdjSearchUp = () => {
  if (filteredAdjProducts.value.length === 0) return;
  adjHighlightedIndex.value =
    (adjHighlightedIndex.value - 1 + filteredAdjProducts.value.length) % filteredAdjProducts.value.length;
};

const selectAdjHighlightedProduct = () => {
  if (adjHighlightedIndex.value < 0) return;
  const p = filteredAdjProducts.value[adjHighlightedIndex.value];
  if (p) selectAdjProduct(p);
};

const handleAdjClickOutside = (e) => {
  const el = adjProductSearchWrap.value;
  if (!el) return;
  if (el.contains(e.target)) return;
  showAdjProductResults.value = false;
  adjHighlightedIndex.value = -1;
};

const handleTransferProductSearch = () => {
  showTransferProductResults.value = filteredTransferProducts.value.length > 0;
  transferHighlightedIndex.value = filteredTransferProducts.value.length > 0 ? 0 : -1;
  transferForm.value.ProductId = null;
  availableUnits.value = [];
  transferForm.value.UnitId = null;
};

const selectTransferProduct = async (p) => {
  const id = p.ProductId || p.productId;
  transferForm.value.ProductId = id;
  transferProductSearch.value = formatTransferProductLabel(p);
  showTransferProductResults.value = false;
  transferHighlightedIndex.value = -1;
  await fetchAvailableUnits();
};

const clearTransferProductSearch = () => {
  transferProductSearch.value = "";
  showTransferProductResults.value = false;
  transferHighlightedIndex.value = -1;
  transferForm.value.ProductId = null;
  availableUnits.value = [];
  transferForm.value.UnitId = null;
};

const moveTransferSearchDown = () => {
  if (filteredTransferProducts.value.length === 0) return;
  transferHighlightedIndex.value = (transferHighlightedIndex.value + 1) % filteredTransferProducts.value.length;
};

const moveTransferSearchUp = () => {
  if (filteredTransferProducts.value.length === 0) return;
  transferHighlightedIndex.value =
    (transferHighlightedIndex.value - 1 + filteredTransferProducts.value.length) % filteredTransferProducts.value.length;
};

const selectTransferHighlightedProduct = () => {
  if (transferHighlightedIndex.value < 0) return;
  const p = filteredTransferProducts.value[transferHighlightedIndex.value];
  if (p) selectTransferProduct(p);
};

const handleTransferClickOutside = (e) => {
  const el = transferProductSearchWrap.value;
  if (!el) return;
  if (el.contains(e.target)) return;
  showTransferProductResults.value = false;
  transferHighlightedIndex.value = -1;
};

const fetchBranches = async () => {
  try {
    const res = await api.get("/Branches");
    const currentId = localStorage.getItem("BranchId");
    branches.value = res.data.filter((b) => (b.BranchId || b.branchId) != currentId);
    if (branches.value.length > 0)
      transferForm.value.ToBranchId = branches.value[0].BranchId || branches.value[0].branchId;
  } catch (err) {
    console.error("Load Error:", err);
  }
};

const handleFullReturn = async () => {
  if (isFullReturning.value) return;
  if (!confirm(`ဤဘောက်ချာ (#${searchId.value}) ပါ ပစ္စည်းအားလုံးကို Stock ထဲ ပြန်သွင်းမှာ သေချာပါသလား?`))
    return;
  try {
    isFullReturning.value = true;
    const res = await api.post(`/Inventory/full-sale-return/${searchId.value}`);
    addToast(tx.value.success, "success");
    voucherItems.value = [];
    searchId.value = null;
    fetchProducts();
  } catch (err) {
    addToast(getApiErrorMessage(err, tx.value.fullReturnFailed), "error");
  } finally {
    isFullReturning.value = false;
  }
};

const searchVoucher = async () => {
  if (!searchId.value) return;
  try {
    const branchId = localStorage.getItem('BranchId');
    const res = await api.get(`/Sales/find-voucher/${searchId.value}?branchId=${branchId}`);
    const mappedItems = await Promise.all(
      res.data.map(async (item) => {
        const hasProductId = item.ProductId !== null && item.ProductId !== undefined;
        const unitRes = hasProductId
          ? await api.get(`/Products/units/${item.ProductId}`)
          : { data: [] };
        return {
          ...item,
          returnQty: 1,
          UnitId: item.UnitId,
          availableUnits: unitRes.data,
        };
      }),
    );
    voucherItems.value = mappedItems;
  } catch (err) {
    addToast(tx.value.voucherNotFound, "error");
    voucherItems.value = [];
  }
};

const searchPO = async () => {
  if (!searchPOId.value) return;
  try {
    const branchId = localStorage.getItem('BranchId');
    const res = await api.get(`/Inventory/find-po/${searchPOId.value}?branchId=${branchId}`);
    const mappedItems = await Promise.all(
      res.data.map(async (item) => {
        const hasProductId = item.ProductId !== null && item.ProductId !== undefined;
        const unitRes = hasProductId
          ? await api.get(`/Products/units/${item.ProductId}`)
          : { data: [] };
        return {
          ...item,
          returnQty: 1,
          UnitId: item.UnitId,
          availableUnits: unitRes.data,
        };
      }),
    );
    poItems.value = mappedItems;
  } catch (err) {
    addToast(tx.value.poNotFound, "error");
    poItems.value = [];
  }
};

const closeSaleReturnModal = () => {
  showSaleReturnModal.value = false;
  pendingSaleReturnItem.value = null;
  salePaymentMethods.value = [];
  selectedReturnPaymentMethod.value = "";
};

const processSaleReturn = async (item) => {
  if (isSaleReturning.value) return;
  if (!item.returnQty || item.returnQty <= 0)
    return addToast(tx.value.invalidQty, "error");

  try {
    const resMethods = await api.get(`/Inventory/sale-payment-methods/${item.SaleId}`);
    const methods = Array.isArray(resMethods.data) ? resMethods.data : [];
    salePaymentMethods.value = methods.map(x => ({
      method: x.Method ?? x.method ?? "Unknown",
      amount: Number(x.Amount ?? x.amount ?? 0)
    }));
    if (salePaymentMethods.value.length === 0) {
      salePaymentMethods.value = [{ method: "Unknown", amount: 0 }];
    }
    selectedReturnPaymentMethod.value = salePaymentMethods.value[0].method;
    pendingSaleReturnItem.value = item;
    showSaleReturnModal.value = true;
  } catch (err) {
    addToast(getApiErrorMessage(err, tx.value.returnFailed), "error");
  }
};

const confirmSaleReturn = async () => {
  if (isSaleReturning.value) return;

  const item = pendingSaleReturnItem.value;
  if (!item) return;

  const payload = {
    SaleId: item.SaleId, ProductId: item.ProductId, BranchId: localStorage.getItem("BranchId"),
    Quantity: item.returnQty, UnitId: item.UnitId, RefundAmount: (item.UnitPrice || 0) * item.returnQty, Reason: "Customer Return",
    PreferredPaymentMethod: selectedReturnPaymentMethod.value || "Unknown"
  };
  try {
    isSaleReturning.value = true;
    await api.post("/Inventory/sale-return", payload);
    addToast(tx.value.returnSuccess, "success");
    closeSaleReturnModal();
    searchVoucher();
  } catch (err) {
    addToast(getApiErrorMessage(err, tx.value.returnFailed), "error");
  } finally {
    isSaleReturning.value = false;
  }
};

const processPurchaseReturn = async (item) => {
  const qty = parseFloat(item.returnQty || 0);
  const maxReturnable = Number(item.ReturnableQty ?? item.returnableQty ?? item.StockQty ?? 0);
  if (!qty || qty <= 0) return addToast(tx.value.invalidQty, "error");
  if (qty > maxReturnable) return addToast(`${tx.value.returnQty} > ${tx.value.returnable} (${formatQty(maxReturnable)})`, "error");
  if (qty > item.StockQty) return addToast("လက်ကျန်ထက် ပိုပို့၍မရပါ", "error");
  const payload = {
    POId: item.POId, ProductId: item.ProductId, BranchId: localStorage.getItem("BranchId"),
    Quantity: qty, UnitId: item.UnitId, RefundAmount: (item.CostPrice || 0) * qty, Reason: "Supplier Return",
  };
  try {
    await api.post("/Inventory/purchase-return", payload);
    addToast(tx.value.returnSuccess, "success");
    searchPO();
  } catch (err) { addToast(getApiErrorMessage(err, tx.value.returnFailed), "error"); }
};

const submitAdjustment = async () => {
  if (adjForm.value.Quantity <= 0) return addToast(tx.value.qtyRequired, "warning");
  try {
    await api.post("/inventory/adjust", adjForm.value);
    addToast(tx.value.adjustmentSuccess, "success");
    adjForm.value.Quantity = 0;
    fetchProducts();
  } catch (err) { addToast(tx.value.failed, "error"); }
};

const submitTransfer = async () => {
  const f = transferForm.value;
  if (!f.ToBranchId || !f.ProductId || !f.UnitId || f.Quantity <= 0)
    return addToast(tx.value.requiredFields, "warning");
  try {
    const payload = {
      FromBranchId: Number(f.FromBranchId),
      ToBranchId: Number(f.ToBranchId),
      UserId: Number(localStorage.getItem("UserId")),
      Note: f.Note || "Stock Transfer",
      Items: [{
        ProductId: Number(f.ProductId),
        Quantity: Number(f.Quantity),
        UnitId: Number(f.UnitId)
      }]
    };
    await api.post("/Inventory/transfer", payload);
    addToast(tx.value.success, "success");
    transferForm.value.Quantity = 1;
    fetchProducts();
  } catch (err) { addToast(tx.value.transferFailed, "error"); }
};

onMounted(() => {
  fetchProducts();
  fetchBranches();
  window.addEventListener("click", handleAdjClickOutside);
  window.addEventListener("click", handleTransferClickOutside);
  window.addEventListener("click", handlePoClickOutside);
});

watch(
  () => adjForm.value.ProductId,
  (id) => {
    if (!id) return;
    const p = products.value.find((x) => (x.ProductId || x.productId) == id);
    if (p) adjProductSearch.value = formatAdjProductLabel(p);
  },
);

watch(
  () => transferForm.value.ProductId,
  (id) => {
    if (!id) return;
    const p = products.value.find((x) => (x.ProductId || x.productId) == id);
    if (p) transferProductSearch.value = formatTransferProductLabel(p);
  },
);

watch(() => adjForm.value.ProductId, async (newId) => {
  if (!newId) { currentProductUnits.value = []; return; }
  try {
    const res = await api.get(`/Products/units/${newId}`);
    currentProductUnits.value = res.data;
    if (res.data.length > 0) adjForm.value.UnitId = res.data[0].UnitId || res.data[0].unitId;
  } catch (err) { console.error(err); }
}, { immediate: true });

watch(activeTab, (tab) => {
  if (tab === "adjustment" || tab === "transfer") fetchProducts();
});

onUnmounted(() => {
  window.removeEventListener("click", handleAdjClickOutside);
  window.removeEventListener("click", handleTransferClickOutside);
  window.removeEventListener("click", handlePoClickOutside);
});
</script>

<style scoped>
.tabular-nums {
  font-variant-numeric: tabular-nums;
}
</style>

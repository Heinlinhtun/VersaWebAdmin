<template>
  <div class="px-3 sm:px-5 lg:px-8 pt-2 pb-6 sm:pb-8 bg-[#f8fafc] min-h-screen font-sans">
    <!-- Header Section -->
    <div class="flex flex-col md:flex-row md:items-center justify-between mb-10 gap-4">
      <div>
        <h1 class="text-2xl font-normal text-slate-900 tracking-tight">{{ tx.title }}</h1>
        <p class="text-sm font-normal text-slate-500 mt-1 flex items-center">
          <span class="w-2 h-2 bg-emerald-500 rounded-full animate-pulse mr-2"></span>
          {{ tx.systemOperational }}
        </p>
      </div>

      <div class="bg-white px-5 py-2.5 rounded-2xl shadow-sm border border-slate-100 flex items-center space-x-3">
        <div class="p-2 bg-blue-50 rounded-lg text-blue-600">
          <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor"
            stroke-width="2">
            <rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect>
            <line x1="16" y1="2" x2="16" y2="6"></line>
            <line x1="8" y1="2" x2="8" y2="6"></line>
            <line x1="3" y1="10" x2="21" y2="10"></line>
          </svg>
        </div>
        <div>
          <p class="text-xs font-normal text-slate-400 uppercase tracking-widest leading-none mb-1">{{ tx.selectedDate }}</p>
          <input v-model="selectedDate" type="date"
            class="bg-transparent text-sm font-normal text-slate-800 leading-none outline-none cursor-pointer [color-scheme:light]">
          <p class="text-[10px] font-normal text-slate-400 mt-1">{{ currentDate }}</p>
        </div>
      </div>
    </div>

    <!-- Stats Grid -->
    <div class="grid grid-cols-2 sm:grid-cols-2 lg:grid-cols-4 xl:grid-cols-5 gap-4 sm:gap-6 mb-10">
      <div v-for="stat in stats" :key="stat.label" @click="handleStatClick(stat.key)" :class="['p-4 sm:p-6 rounded-[24px] shadow-lg transition-all duration-300 flex flex-col group cursor-pointer hover:-translate-y-1 hover:shadow-xl relative overflow-hidden',
        stat.cardBg, activeStat === stat.key ? 'ring-4 ring-offset-2 ring-indigo-500/30 shadow-indigo-500/20' : '']">

        <!-- Decorative background elements -->
        <div
          class="absolute -right-6 -top-6 w-24 h-24 rounded-full bg-white opacity-5 group-hover:scale-150 transition-transform duration-500">
        </div>
        <div
          class="absolute right-12 top-12 w-16 h-16 rounded-full bg-white opacity-5 group-hover:-translate-y-4 transition-transform duration-500">
        </div>

        <div class="flex justify-between items-start mb-6 relative z-10">
          <div
            :class="['w-12 h-12 rounded-2xl flex items-center justify-center p-2.5 transition-transform group-hover:scale-110 shadow-sm backdrop-blur-sm', stat.iconBg, stat.iconColor]">
            <svg v-if="stat.key === 'inventory'" class="w-full h-full" viewBox="0 0 24 24" fill="none"
              stroke="currentColor" stroke-width="2.5">
              <path d="M21 8V20a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8"></path>
              <path d="M21 3H3v5h18V3z"></path>
            </svg>
            <svg v-else-if="stat.key === 'customers'" class="w-full h-full" viewBox="0 0 24 24" fill="none"
              stroke="currentColor" stroke-width="2.5">
              <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path>
              <circle cx="9" cy="7" r="4"></circle>
              <path d="M23 21v-2a4 4 0 0 0-3-3.87"></path>
              <path d="M16 3.13a4 4 0 0 1 0 7.75"></path>
            </svg>
            <svg v-else-if="stat.key === 'sales'" class="w-full h-full" viewBox="0 0 24 24" fill="none"
              stroke="currentColor" stroke-width="2.5">
              <line x1="12" y1="1" x2="12" y2="23"></line>
              <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path>
            </svg>
            <svg v-else-if="stat.key === 'expenses'" class="w-full h-full" viewBox="0 0 24 24" fill="none"
              stroke="currentColor" stroke-width="2.5">
              <rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect>
              <line x1="16" y1="2" x2="16" y2="6"></line>
              <line x1="8" y1="2" x2="8" y2="6"></line>
              <line x1="3" y1="10" x2="21" y2="10"></line>
            </svg>
            <svg v-else class="w-full h-full" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
              <circle cx="12" cy="12" r="10"></circle>
              <line x1="12" y1="8" x2="12" y2="12"></line>
              <line x1="12" y1="16" x2="12.01" y2="16"></line>
            </svg>
          </div>
          <span
            :class="['text-[10px] font-normal px-2.5 py-1 rounded-lg backdrop-blur-md shadow-sm', stat.badgeBg, stat.badgeColor]">
            {{ stat.badge }}
          </span>
        </div>
        <div class="relative z-10">
          <p :class="['text-[11px] font-normal uppercase tracking-widest mb-1 opacity-90', stat.labelColor]">{{
            stat.label }}</p>
          <h3 :class="['text-3xl font-normal tracking-tight mb-2', stat.valueColor]">{{ stat.value }}</h3>
          <p :class="['text-[11px] font-normal opacity-80', stat.footerColor]">{{ stat.footer }}</p>
        </div>
      </div>
    </div>

    <!-- Main Content Section -->
    <div class="grid grid-cols-1 lg:grid-cols-3 gap-8">
      <!-- Dynamic Panel based on active stat -->
      <div class="lg:col-span-2 bg-white p-8 rounded-[10px] border border-slate-100 shadow-sm relative">
        <div class="flex flex-col sm:flex-row justify-between items-start sm:items-center mb-8 gap-3">
          <div>
            <h3 class="text-lg font-normal text-slate-800 tracking-tight">
              <span v-if="activeStat === 'sales'">{{ tx.salesAnalytics }}</span>
              <span v-else-if="activeStat === 'inventory'">Top 10 Sold Items</span>
              <span v-else-if="activeStat === 'customers'">Top 10 Customers</span>
              <span v-else-if="activeStat === 'today_sales'">Sold Items</span>
              <span v-else-if="activeStat === 'expenses'">Top 10 Expenses</span>
              <span v-else-if="activeStat === 'expired_soon'">Products Expiring Soon</span>
            </h3>
            <p class="text-xs text-slate-400 font-normal">
              <span v-if="activeStat === 'sales'">{{ tx.monthlyTracking }}</span>
              <span v-else>Click on the visualizer card to view different metrics</span>
            </p>
          </div>
          <div class="flex items-center space-x-2">
            <button v-if="activeStat !== 'sales'" @click="activeStat = 'sales'"
              class="bg-slate-100 text-slate-600 hover:text-slate-800 text-xs font-normal rounded-lg px-3 py-1.5 transition-colors">
              Reset View
            </button>
            <select v-if="activeStat === 'sales'" v-model="chartInterval"
              class="bg-slate-50 border-none text-xs font-normal text-slate-500 rounded-lg px-3 py-1.5 focus:ring-0 cursor-pointer">
              <option :value="7">{{ tx.last7Days }}</option>
              <option :value="30">{{ tx.last30Days }}</option>
            </select>
          </div>
        </div>

        <!-- 1. Default Sales Visualizer -->
        <div v-if="activeStat === 'sales'" class="h-64 flex items-end justify-between px-4 pb-4 gap-2 relative">
          <div v-if="!dashboardData.salesAnalytics.length"
            class="absolute inset-0 flex flex-col items-center justify-center bg-slate-50/30 rounded-[24px] border-2 border-dashed border-slate-100">
            <p class="text-sm font-normal text-slate-400 uppercase tracking-widest">{{ tx.awaitingVisuals }}</p>
          </div>
          <template v-else>
            <div v-for="(day, index) in dashboardData.salesAnalytics" :key="index"
              class="flex-1 h-full flex flex-col items-center group relative">
              <!-- Bar Wrapper -->
              <div class="flex-1 w-full flex items-end justify-center">
                <!-- Bar -->
                <div
                  class="w-full bg-blue-500 rounded-t-lg transition-all duration-500 hover:bg-blue-600 relative cursor-pointer"
                  :style="{ height: `${(day.total / maxSales) * 100}%` }">
                  <!-- Tooltip -->
                  <div
                    class="absolute -top-12 left-1/2 -translate-x-1/2 bg-slate-900 text-white text-[10px] py-1 px-2 rounded opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none whitespace-nowrap z-10 shadow-xl font-normal">
                    {{ day.total.toLocaleString() }} MMK
                  </div>
                </div>
              </div>
              <!-- Label -->
              <span class="text-[9px] font-normal text-slate-400 mt-2 uppercase tracking-tight">
                {{ new Date(day.date).toLocaleDateString(undefined, { weekday: 'short' }) }}
              </span>
            </div>
          </template>
        </div>

        <!-- 2. Top 10 Sold Items (Inventory Stat) -->
        <div v-else-if="activeStat === 'inventory'" class="table-responsive max-h-64 pr-1 sm:pr-2">
          <div v-if="isLoadingDrillDown" class="flex justify-center py-10"><span
              class="animate-spin h-5 w-5 border-2 border-blue-500 rounded-full border-t-transparent"></span></div>
          <table v-else class="w-full text-left text-xs sm:text-sm text-slate-600">
            <thead class="text-[10px] sm:text-xs uppercase bg-[#3f3f46] text-white sticky top-0">
              <tr>
                <th class="px-3 sm:px-4 py-3 font-normal rounded-tl-lg text-white">Item Name</th>
                <th class="px-3 sm:px-4 py-3 font-normal text-white">Quantity Sold</th>
                <th class="px-3 sm:px-4 py-3 font-normal text-right rounded-tr-lg text-white">Total Revenue</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(item, i) in activeDetailList" :key="i" class="border-b border-slate-50 hover:bg-slate-50/50">
                <td class="px-3 sm:px-4 py-2 sm:py-3 font-normal text-slate-800">{{ item.name }}</td>
                <td class="px-3 sm:px-4 py-2 sm:py-3">{{ item.totalQuantity }}</td>
                <td class="px-3 sm:px-4 py-2 sm:py-3 text-right font-normal text-blue-600">{{
                  item.totalAmount?.toLocaleString()
                  }} MMK</td>
              </tr>
              <tr v-if="!activeDetailList.length">
                <td colspan="3" class="text-center py-6 text-slate-400">No data available</td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- 3. Top Customers (Customers Stat) -->
        <div v-else-if="activeStat === 'customers'" class="table-responsive max-h-64 pr-1 sm:pr-2">
          <div v-if="isLoadingDrillDown" class="flex justify-center py-10"><span
              class="animate-spin h-5 w-5 border-2 border-indigo-500 rounded-full border-t-transparent"></span></div>
          <table v-else class="w-full text-left text-xs sm:text-sm text-slate-600">
            <thead class="text-[10px] sm:text-xs uppercase bg-[#3f3f46] text-white sticky top-0">
              <tr>
                <th class="px-3 sm:px-4 py-3 font-normal rounded-tl-lg text-white">Customer Name</th>
                <th class="px-3 sm:px-4 py-3 font-normal text-white">Visit Count</th>
                <th class="px-3 sm:px-4 py-3 font-normal text-right rounded-tr-lg text-white">Total Spent</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(cust, i) in activeDetailList" :key="i"
                class="border-b border-slate-50 hover:bg-indigo-50/30 transition-colors">
                <td class="px-3 sm:px-4 py-2 sm:py-3 font-normal text-slate-800 flex items-center">
                  <div
                    class="h-5 w-5 sm:h-6 sm:w-6 rounded-full bg-indigo-100 text-indigo-600 flex items-center justify-center text-[10px] font-normal mr-2 sm:mr-3">
                    {{ cust.name?.charAt(0).toUpperCase() }}</div>
                  {{ cust.name }}
                </td>
                <td class="px-3 sm:px-4 py-2 sm:py-3">{{ cust.visitCount }}</td>
                <td class="px-3 sm:px-4 py-2 sm:py-3 text-right font-normal text-indigo-600">{{
                  cust.totalSpent?.toLocaleString() }} MMK</td>
              </tr>
              <tr v-if="!activeDetailList.length">
                <td colspan="3" class="text-center py-6 text-slate-400">No data available</td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- 4. Today's Sold Items (Today's Sales Stat) -->
        <div v-else-if="activeStat === 'today_sales'" class="table-responsive max-h-64 pr-1 sm:pr-2">
          <div v-if="isLoadingDrillDown" class="flex justify-center py-10"><span
              class="animate-spin h-5 w-5 border-2 border-emerald-500 rounded-full border-t-transparent"></span></div>
          <table v-else class="w-full text-left text-xs sm:text-sm text-slate-600">
            <thead class="text-[10px] sm:text-xs uppercase bg-[#3f3f46] text-white sticky top-0">
              <tr>
                <th class="px-3 sm:px-4 py-3 font-normal rounded-tl-lg text-white">Item Name</th>
                <th class="px-3 sm:px-4 py-3 font-normal text-center text-white">Unit</th>
                <th class="px-3 sm:px-4 py-3 font-normal text-white">Qty Sold</th>
                <th class="px-3 sm:px-4 py-3 font-normal text-right rounded-tr-lg text-white">Amount</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(item, i) in activeDetailList" :key="i"
                class="border-b border-slate-50 hover:bg-emerald-50/30 transition-colors">
                <td class="px-3 sm:px-4 py-2 sm:py-3 font-normal text-slate-800">{{ item.name }}</td>
                <td class="px-3 sm:px-4 py-2 sm:py-3 text-center text-slate-500">{{ item.UnitName || item.unitName || item.unit || '-' }}</td>
                <td class="px-3 sm:px-4 py-2 sm:py-3"><span
                    class="px-1.5 py-0.5 sm:px-2 sm:py-1 bg-emerald-100 text-emerald-700 rounded-md text-[10px] sm:text-xs font-normal">{{
                      item.totalQuantity }}</span></td>
                <td class="px-3 sm:px-4 py-2 sm:py-3 text-right font-normal text-emerald-600">{{
                  item.totalAmount?.toLocaleString() }} MMK</td>
              </tr>
              <tr v-if="!activeDetailList.length">
                <td colspan="4" class="text-center py-6 text-slate-400">No sales recorded for this date.</td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- 5. Top Expenses (Expenses Stat) -->
        <div v-else-if="activeStat === 'expenses'" class="table-responsive max-h-64 pr-1 sm:pr-2">
          <div v-if="isLoadingDrillDown" class="flex justify-center py-10"><span
              class="animate-spin h-5 w-5 border-2 border-rose-500 rounded-full border-t-transparent"></span></div>
          <table v-else class="w-full text-left text-xs sm:text-sm text-slate-600">
            <thead class="text-[10px] sm:text-xs uppercase bg-[#3f3f46] text-white sticky top-0">
              <tr>
                <th class="px-3 sm:px-4 py-3 font-normal rounded-tl-lg text-white">Expense Description</th>
                <th class="px-3 sm:px-4 py-3 font-normal text-right rounded-tr-lg text-white">Total Amount Spent</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(exp, i) in activeDetailList" :key="i"
                class="border-b border-slate-50 hover:bg-rose-50/30 transition-colors">
                <td class="px-3 sm:px-4 py-2 sm:py-3 font-normal text-slate-800">{{ exp.name }}</td>
                <td class="px-3 sm:px-4 py-2 sm:py-3 text-right font-normal text-rose-600">{{
                  exp.totalAmount?.toLocaleString()
                  }} MMK</td>
              </tr>
              <tr v-if="!activeDetailList.length">
                <td colspan="2" class="text-center py-6 text-slate-400">No expenses recorded.</td>
              </tr>
            </tbody>
          </table>
        </div>
        <!-- 6. Expiring Soon Products (Expiry Stat) -->
        <div v-else-if="activeStat === 'expired_soon'" class="table-responsive max-h-64 pr-2">
          <div v-if="isLoadingDrillDown" class="flex justify-center py-10"><span
              class="animate-spin h-5 w-5 border-2 border-rose-500 rounded-full border-t-transparent"></span></div>
          <table v-else class="w-full text-left text-xs sm:text-sm text-slate-600">
            <thead class="text-[10px] sm:text-xs uppercase bg-[#3f3f46] text-white sticky top-0">
              <tr>
                <th class="px-3 sm:px-4 py-3 font-normal rounded-tl-lg text-white">Product Name</th>
                <th class="px-3 sm:px-4 py-3 font-normal text-white">Expiry Date</th>
                <th class="px-3 sm:px-4 py-3 font-normal text-center text-white">Expiring Qty</th>
                <th class="px-3 sm:px-4 py-3 font-normal text-center text-white">Total Stock</th>
                <th class="px-3 sm:px-4 py-3 font-normal text-center text-white">Alert Window</th>
                <th class="px-3 sm:px-4 py-3 font-normal text-right rounded-tr-lg text-white">Days Left</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(item, i) in activeDetailList" :key="i"
                class="border-b border-slate-50 hover:bg-rose-50/30 transition-colors">
                <td class="px-3 sm:px-4 py-3 font-normal text-slate-800">{{ item.name }}</td>
                <td class="px-3 sm:px-4 py-3 text-slate-500 whitespace-nowrap">{{ new
                  Date(item.expiryDate).toLocaleDateString()
                  }}</td>
                <td class="px-3 sm:px-4 py-3 text-center"><span
                    class="px-2 py-1 bg-rose-50 text-rose-600 rounded-md text-[10px] font-normal">{{ item.batchQuantity
                    }}</span></td>
                <td class="px-3 sm:px-4 py-3 text-center"><span
                    class="px-2 py-1 bg-slate-100 text-slate-600 rounded-md text-[10px] font-normal">{{ item.totalStock
                    }}</span></td>
                <td class="px-3 sm:px-4 py-3 text-center"><span class="text-[10px] text-slate-400 font-normal">{{
                    item.alertDays
                    }} d</span></td>
                <td class="px-3 sm:px-4 py-3 text-right">
                  <span :class="item.daysRemaining <= 7 ? 'text-rose-600 font-normal' : 'text-orange-600'"
                    class="text-[10px]">
                    {{ item.daysRemaining }} d
                  </span>
                </td>
              </tr>
              <tr v-if="!activeDetailList.length">
                <td colspan="6" class="text-center py-6 text-slate-400">No products expiring soon.</td>
              </tr>
            </tbody>
          </table>
        </div>

      </div>

      <!-- Recent Activity Section -->
      <div class="bg-white p-8 rounded-[10px] border border-slate-100 shadow-sm">
        <div class="flex flex-col sm:flex-row justify-between items-start sm:items-center mb-8 gap-3">
          <h3 class="text-lg font-normal text-slate-800 tracking-tight">{{ tx.recentActivity }}</h3>
          <button @click="navigateToHistory"
            class="text-xs font-normal text-blue-600 hover:text-blue-700 transition-colors">{{
              tx.viewAll
            }}</button>
        </div>

        <div class="space-y-6">
          <div v-for="activity in activities" :key="activity.actTitle"
            class="flex items-center gap-4 group cursor-pointer">
            <div
              :class="['w-10 h-10 rounded-xl flex items-center justify-center font-normal text-xs transition-transform group-hover:scale-110', activity.bg, activity.color]">
              {{ activity.initials }}
            </div>
            <div class="flex-1">
              <p class="text-sm font-normal text-slate-800 leading-snug group-hover:text-blue-600 transition-colors">{{
                activity.title }}</p>
              <p class="text-[11px] font-normal text-slate-400">{{ activity.time }} • {{ activity.actTitle }}</p>
            </div>
            <div class="text-right">
              <p :class="['text-sm font-normal', activity.amountColor]">{{ activity.amount }}</p>
            </div>
          </div>
          <div v-if="!activities.length" class="text-center py-10">
            <p class="text-sm font-normal text-slate-400 italic">No recent activity</p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useI18n } from '../composables/useI18n';
import api from '../services/api';

const { t } = useI18n();
const router = useRouter();
const branchId = localStorage.getItem('BranchId') || 0;
const chartInterval = ref(7);
const todayInputDate = () => {
  const date = new Date();
  const offset = date.getTimezoneOffset();
  const localDate = new Date(date.getTime() - offset * 60 * 1000);
  return localDate.toISOString().slice(0, 10);
};
const selectedDate = ref(todayInputDate());

const activeStat = ref('sales');
const activeDetailList = ref([]);
const isLoadingDrillDown = ref(false);

const dashboardData = ref({
  totalInventory: 0,
  activeCustomers: 0,
  todaySales: 0,
  todayExpenses: 0,
  expiredSoon: 0,
  expiredSoonQty: 0,
  salesAnalytics: [],
  recentActivities: []
});

const tx = computed(() => ({
  title: t('dashboard.title') || 'Dashboard',
  systemOperational: t('dashboard.systemOperational') || 'System Operational',
  today: t('dashboard.today') || 'Today',
  selectedDate: t('dashboard.selectedDate') || 'Date',
  salesAnalytics: t('dashboard.salesAnalytics') || 'Sales Analytics',
  monthlyTracking: t('dashboard.monthlyTracking') || 'Weekly performance metrics',
  last7Days: t('dashboard.last7Days') || 'Last 7 Days',
  last30Days: t('dashboard.last30Days') || 'Last 30 Days',
  awaitingVisuals: t('dashboard.awaitingVisuals') || 'Waiting for data',
  recentActivity: t('dashboard.recentActivity') || 'Recent Activity',
  viewAll: t('dashboard.viewAll') || 'View All'
}));

const currentDate = computed(() => {
  const date = new Date(`${selectedDate.value}T00:00:00`);
  if (Number.isNaN(date.getTime())) return '';
  return date.toLocaleDateString('en-GB', {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric'
  });
});

const formatNumber = (num) => {
  if (num >= 1000000) return (num / 1000000).toFixed(1) + 'M';
  if (num >= 1000) return (num / 1000).toFixed(1) + 'K';
  return num.toString();
};

const stats = computed(() => [
  {
    key: 'inventory',
    label: t('dashboard.totalInventory') || 'Total Inventory',
    value: dashboardData.value.totalInventory.toLocaleString(),
    badge: 'Live',
    iconBg: 'bg-white/20',
    iconColor: 'text-white',
    cardBg: 'bg-gradient-to-br from-blue-500 to-blue-600',
    badgeBg: 'bg-white/20',
    badgeColor: 'text-white',
    labelColor: 'text-blue-100',
    valueColor: 'text-white',
    footerColor: 'text-blue-100',
    footer: t('dashboard.inventoryFooter') || 'Products currently in stock'
  },
  {
    key: 'customers',
    label: t('dashboard.activeCustomers') || 'Active Customers',
    value: dashboardData.value.activeCustomers.toLocaleString(),
    badge: 'Total',
    iconBg: 'bg-white/20',
    iconColor: 'text-white',
    cardBg: 'bg-gradient-to-br from-indigo-500 to-indigo-600',
    badgeBg: 'bg-white/20',
    badgeColor: 'text-white',
    labelColor: 'text-indigo-100',
    valueColor: 'text-white',
    footerColor: 'text-indigo-100',
    footer: t('dashboard.customersFooter') || 'Total registered partners'
  },
  {
    key: 'today_sales',
    label: t('dashboard.todaysSales') || 'Today\'s Sales',
    value: formatNumber(dashboardData.value.todaySales),
    badge: 'Date',
    iconBg: 'bg-white/20',
    iconColor: 'text-white',
    cardBg: 'bg-gradient-to-br from-emerald-400 to-emerald-500',
    badgeBg: 'bg-white/20',
    badgeColor: 'text-white',
    labelColor: 'text-emerald-50',
    valueColor: 'text-white',
    footerColor: 'text-emerald-50',
    footer: t('dashboard.salesFooter') || 'Total sales for selected date'
  },
  {
    key: 'expenses',
    label: t('dashboard.expenses') || 'Expenses',
    value: formatNumber(dashboardData.value.todayExpenses),
    badge: 'Date',
    iconBg: 'bg-white/20',
    iconColor: 'text-white',
    cardBg: 'bg-gradient-to-br from-orange-400 to-orange-500',
    badgeBg: 'bg-white/20',
    badgeColor: 'text-white',
    labelColor: 'text-orange-50',
    valueColor: 'text-white',
    footerColor: 'text-orange-50',
    footer: t('dashboard.expensesFooter') || 'Total operational outflow for selected date'
  },
  {
    key: 'expired_soon',
    label: t('dashboard.expiringSoon') || 'Expiring Soon',
    value: `${dashboardData.value.expiredSoon} (${dashboardData.value.expiredSoonQty} Units)`,
    badge: 'Alert',
    iconBg: 'bg-white/20',
    iconColor: 'text-white',
    cardBg: 'bg-gradient-to-br from-rose-500 to-rose-600',
    badgeBg: 'bg-white/20',
    badgeColor: 'text-white',
    labelColor: 'text-rose-100',
    valueColor: 'text-white',
    footerColor: 'text-rose-100',
    footer: t('dashboard.expiringSoonFooter') || 'Items within alert window'
  },
]);

const maxSales = computed(() => {
  if (!dashboardData.value.salesAnalytics.length) return 1;
  return Math.max(...dashboardData.value.salesAnalytics.map(d => d.total), 1);
});

const fetchDashboardData = async () => {
  try {
    const currentBranchId = localStorage.getItem('BranchId') || 0;
    console.log('[Dashboard] Fetching data for Branch:', currentBranchId, 'Days:', chartInterval.value, 'Date:', selectedDate.value);

    const response = await api.get(`/dashboard/summary/${currentBranchId}`, {
      params: {
        days: chartInterval.value,
        date: selectedDate.value
      }
    });
    const data = response.data;
    console.log('[Dashboard] API Response:', data);

    // Case-insensitive helper
    const getVal = (obj, key) => {
      if (!obj) return 0;
      const lowerKey = key.toLowerCase();
      // Try exact, then try lowercase
      return obj[key] ?? obj[lowerKey] ?? 0;
    };

    const salesObj = data.sales || data.Sales || {};

    dashboardData.value = {
      totalInventory: getVal(data, 'totalInventory'),
      activeCustomers: getVal(data, 'activeCustomers'),
      todaySales: getVal(salesObj, 'todaySales'),
      todayExpenses: getVal(data, 'todayExpenses'),
      expiredSoon: getVal(data, 'expiredSoon'),
      expiredSoonQty: getVal(data, 'expiredSoonQty'),
      salesAnalytics: (data.salesAnalytics || data.SalesAnalytics || []).map(day => {
        const val = parseFloat(day.total ?? day.Total ?? 0);
        return {
          date: day.date || day.Date,
          total: isNaN(val) ? 0 : val
        };
      }),
      recentActivities: data.recentActivities || data.RecentActivities || []
    };

    console.log('[Dashboard] Normalized Chart Data:', dashboardData.value.salesAnalytics);
    console.log('[Dashboard] Calculated Max Sales:', maxSales.value);

    if (data.diagnostics) {
      console.log('[Dashboard] Diagnostics:', data.diagnostics);
    }
  } catch (error) {
    console.error('[Dashboard] Error:', error);
  }
};

const handleStatClick = async (statKey) => {
  // If clicking the same stat again, revert to default overall sales analytics
  if (activeStat.value === statKey) {
    activeStat.value = 'sales';
    return;
  }

  activeStat.value = statKey;
  await fetchDrillDownData(statKey);
};

const fetchDrillDownData = async (statKey) => {
  isLoadingDrillDown.value = true;
  activeDetailList.value = [];

  try {
    const currentBranchId = localStorage.getItem('BranchId') || 0;
    let endpoint = '';

    if (statKey === 'inventory') endpoint = `/dashboard/top-items/${currentBranchId}`;
    else if (statKey === 'customers') endpoint = `/dashboard/top-customers/${currentBranchId}`;
    else if (statKey === 'today_sales') endpoint = `/dashboard/today-sales-items/${currentBranchId}`;
    else if (statKey === 'expenses') endpoint = `/dashboard/top-expenses/${currentBranchId}`;
    else if (statKey === 'expired_soon') endpoint = `/dashboard/expired-soon/${currentBranchId}`;

    if (endpoint) {
      const params = ['today_sales', 'expenses'].includes(statKey)
        ? { date: selectedDate.value }
        : {};
      const response = await api.get(endpoint, { params });
      activeDetailList.value = response.data || [];
    }
  } catch (error) {
    console.error('[Dashboard] Error fetching drill down data:', error);
  } finally {
    isLoadingDrillDown.value = false;
  }
};

watch(chartInterval, () => {
  fetchDashboardData();
});

watch(selectedDate, async () => {
  await fetchDashboardData();
  if (['today_sales', 'expenses'].includes(activeStat.value)) {
    await fetchDrillDownData(activeStat.value);
  }
});

const navigateToHistory = () => {
  router.push('/HistoryCenter');
};

const activities = computed(() => {
  const raw = dashboardData.value.recentActivities || [];
  return raw.map(act => {
    // Handle both cases for activity properties
    const type = act.type || act.Type || 'Unknown';
    const amount = act.amount || act.Amount || 0;
    const date = act.date || act.Date;
    const title = act.title || act.Title || 'TX-000';
    const id = act.id || act.Id || 0;

    return {
      id: id,
      actTitle: title, // SA0000001
      title: type === 'Sale' ? t('dashboard.activitySaleRecorded') || 'Sale recorded' : type,
      initials: type.substring(0, 2).toUpperCase(),
      time: date ? new Date(date).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '--:--',
      amount: (amount > 0 ? '+' : '') + amount.toLocaleString(),
      bg: type === 'Sale' ? 'bg-emerald-50' : 'bg-blue-50',
      color: type === 'Sale' ? 'text-emerald-600' : 'text-blue-600',
      amountColor: type === 'Sale' ? 'text-emerald-600' : 'text-blue-600'
    };
  });
});

onMounted(() => {
  fetchDashboardData();
});
</script>

<style scoped>
::-webkit-scrollbar {
  width: 5px;
}

::-webkit-scrollbar-thumb {
  background: #e2e8f0;
  border-radius: 10px;
}
</style>

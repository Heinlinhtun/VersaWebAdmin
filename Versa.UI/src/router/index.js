import { createRouter, createWebHistory } from "vue-router";
import Login from "../views/Login.vue";
import DashboardLayout from "../layouts/DashboardLayout.vue";

const routes = [
  { path: "/login", name: "Login", component: Login },
  { path: "/privacy-policy", name: "PrivacyPolicy", component: () => import("../views/PrivacyPolicy.vue") },
  {
    path: "/",
    component: DashboardLayout,
    redirect: "/dashboard",
    meta: { requiresAuth: true },
    children: [
      { path: "dashboard",           component: () => import("../views/Dashboard.vue"),            meta: { roles: ['Admin','Manager','Staff'] } },
      { path: "Sale",                component: () => import("../views/Sale.vue"),                  meta: { roles: ['Admin','Manager','Staff','Cashier'] } },
      { path: "products",            component: () => import("../views/Products.vue"),              meta: { roles: ['Admin','Manager','Staff'] } },
      { path: "Purchases",           component: () => import("../views/Purchases.vue"),             meta: { roles: ['Admin','Manager'] } },
      { path: "Consignments",        component: () => import("../views/Consignments.vue"),          meta: { roles: ['Admin','Manager'] } },
      { path: "Inventory",           component: () => import("../views/Inventory.vue"),             meta: { roles: ['Admin','Manager','Staff'] } },
      { path: "inventory-adjustments", component: () => import("../views/InventoryAdjustments.vue"), meta: { roles: ['Admin','Manager','Staff'] } },
      { path: "historycenter",       component: () => import("../views/HistoryCenter.vue"),         meta: { roles: ['Admin','Manager','Staff'] } },
      { path: "Expenses",            component: () => import("../views/Expenses.vue"),              meta: { roles: ['Admin','Manager'] } },
      { path: "master-data",         component: () => import("../views/MasterData.vue"),            meta: { roles: ['Admin'] } },
      { path: "partners",            component: () => import("../views/Partners.vue"),              meta: { roles: ['Admin','Manager'] } },
      { path: "Reports",             component: () => import("../views/Reports.vue"),               meta: { roles: ['Admin'] } },
      { path: "settings",            component: () => import("../views/Settings.vue"),              meta: { roles: ['Admin'] } },
      // Legacy individual routes (backward compat)
      { path: "units",               component: () => import("../views/Units.vue"),                 meta: { roles: ['Admin'] } },
      { path: "categories",          component: () => import("../views/Categories.vue"),            meta: { roles: ['Admin'] } },
      { path: "customers",           component: () => import("../views/Customers.vue"),             meta: { roles: ['Admin','Manager'] } },
      { path: "suppliers",           component: () => import("../views/Suppliers.vue"),             meta: { roles: ['Admin'] } },
      { path: "Branches",            component: () => import("../views/Branches.vue"),              meta: { roles: ['Admin'], requiresBranchAccess: true } },
      { path: "Users",               component: () => import("../views/Users.vue"),                 meta: { roles: ['Admin'] } },
      { path: "StockAdjustment",     component: () => import("../views/StockAdjustment.vue"),       meta: { roles: ['Admin','Manager'] } },
      { path: "SaleReturn",          component: () => import("../views/SaleReturn.vue"),            meta: { roles: ['Admin','Manager','Staff'] } },
      { path: "PurchaseReturn",      component: () => import("../views/PurchaseReturn.vue"),        meta: { roles: ['Admin','Manager'] } },
    ],
  },
];

const router = createRouter({
  history: createWebHistory(),
  routes,
});

router.beforeEach((to, from, next) => {
  const isAuthenticated = !!localStorage.getItem("token");

  if (to.meta.requiresAuth && !isAuthenticated) {
    return next("/login");
  }
  if (to.path === "/login" && isAuthenticated) {
    return next("/dashboard");
  }

  // Role check
  if (to.meta.roles) {
    const userRole = localStorage.getItem("Role") || "Cashier";
    if (!to.meta.roles.includes(userRole)) {
      // Redirect to the highest page the role can access
      const fallback = ['Admin','Manager','Staff'].includes(userRole) ? "/dashboard" : "/Sale";
      return next(fallback);
    }
  }

  if (to.meta.requiresBranchAccess && localStorage.getItem('AllowBranch') !== 'true') {
    return next('/master-data');
  }

  next();
});

export default router;

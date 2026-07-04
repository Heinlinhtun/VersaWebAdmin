/**
 * usePermissions — Role-Based Access Control (Option A)
 *
 * Roles (highest to lowest):
 *   Admin    — full access to everything
 *   Manager  — operations (sales, purchases, inventory) — no master data / settings / reports
 *   Staff    — sale terminal + view-only for products/inventory/history
 *   Cashier  — sale terminal only
 *
 * Backward-compat: the old "Staff" DB value maps to the new "Staff" role.
 */
import { computed } from 'vue';

const ROLE_HIERARCHY = { Admin: 4, Manager: 3, Staff: 2, Cashier: 1 };

const PERMISSIONS = {
  // ── Pages ────────────────────────────────────────────────────────────
  canViewDashboard:       ['Admin', 'Manager', 'Staff'],
  canViewSale:            ['Admin', 'Manager', 'Staff', 'Cashier'],
  canViewProducts:        ['Admin', 'Manager', 'Staff'],
  canViewPurchases:       ['Admin', 'Manager'],
  canViewInventory:       ['Admin', 'Manager', 'Staff'],
  canViewAdjustments:     ['Admin', 'Manager'],
  canViewHistory:         ['Admin', 'Manager', 'Staff'],
  canViewExpenses:        ['Admin', 'Manager'],
  canViewMasterData:      ['Admin'],
  canViewPartners:        ['Admin', 'Manager'],
  canViewReports:         ['Admin'],
  canViewSettings:        ['Admin'],

  // ── Actions (CRUD) ───────────────────────────────────────────────────
  canEditProducts:        ['Admin', 'Manager'],     // add / edit / delete products
  canEditPurchases:       ['Admin', 'Manager'],
  canEditExpenses:        ['Admin', 'Manager'],
  canEditMasterData:      ['Admin'],
  canEditCustomers:       ['Admin', 'Manager'],
  canEditSuppliers:       ['Admin'],
  canManageUsers:         ['Admin'],
  canViewBusinessIntel:   ['Admin'],
};

export function usePermissions() {
  const userRole = computed(() => localStorage.getItem('Role') || 'Cashier');

  /**
   * can('canViewProducts') → true/false
   */
  function can(permissionKey) {
    const allowed = PERMISSIONS[permissionKey];
    if (!allowed) return false;
    return allowed.includes(userRole.value);
  }

  /**
   * hasRole('Admin', 'Manager') → true if current role is one of the listed roles
   */
  function hasRole(...roles) {
    return roles.includes(userRole.value);
  }

  /**
   * atLeast('Manager') → true if current role is Manager or higher
   */
  function atLeast(minRole) {
    const min = ROLE_HIERARCHY[minRole] ?? 0;
    const cur = ROLE_HIERARCHY[userRole.value] ?? 0;
    return cur >= min;
  }

  return { userRole, can, hasRole, atLeast, PERMISSIONS };
}

export const ALL_ROLES = ['Admin', 'Manager', 'Staff', 'Cashier'];

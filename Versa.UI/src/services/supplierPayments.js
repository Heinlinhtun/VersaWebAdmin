// Keep an unresolved request across modal closes/reloads. Never generate a fresh
// ID merely because a response timed out: the server may already have committed.
const inFlight = new Set();

function storageKey(supplierId) {
  const token = localStorage.getItem('token');
  const part = token?.split('.')[1];
  if (!part) throw new Error('Please sign in again before paying.');
  const claims = JSON.parse(atob(part.replace(/-/g, '+').replace(/_/g, '/')));
  if (!claims.TenantDb) throw new Error('Missing shop identity. Please sign in again.');
  return `supplier-payment:${claims.TenantDb}:${localStorage.getItem('UserId')}:${supplierId}`;
}

export function pendingSupplierPayment(supplierId) {
  const saved = localStorage.getItem(storageKey(supplierId));
  return saved ? JSON.parse(saved) : null;
}

export async function submitSupplierPayment(api, form) {
  const key = storageKey(form.SupplierId);
  if (inFlight.has(key)) throw new Error('This payment is still being processed.');
  inFlight.add(key);
  try {
    const saved = localStorage.getItem(key);
    let payload;
    if (saved) {
      payload = JSON.parse(saved);
      const { RequestId, ...original } = payload;
      const { RequestId: ignored, ...current } = form;
      if (JSON.stringify(original) !== JSON.stringify(current))
        throw new Error('An earlier payment is unresolved. Close and reopen this payment form to retry its original details.');
    } else {
      payload = { ...form, RequestId: crypto.randomUUID() };
      localStorage.setItem(key, JSON.stringify(payload));
    }
    try {
      const response = await api.post('/suppliers/pay-debt', payload);
      localStorage.removeItem(key);
      return response;
    } catch (error) {
      // Only explicit rejections are known not to have committed. Network/5xx
      // failures keep the original payload and request ID for recovery.
      if ([400, 403, 409].includes(error.response?.status)) localStorage.removeItem(key);
      throw error;
    }
  } finally {
    inFlight.delete(key);
  }
}

export const normalizeReceiptPayments = (payments = [], fallback = {}) => {
  const normalized = payments
    .map(payment => ({
      method: payment.method || payment.PaymentMethod || payment.paymentMethod || payment.Method || payment.methodName || fallback.method || 'Cash',
      amount: Number(payment.amount ?? payment.Amount ?? payment.paymentAmount ?? payment.PaymentAmount ?? 0)
    }))
    .filter(payment => payment.method && payment.amount > 0);

  if (normalized.length > 0) return normalized;

  const fallbackAmount = Number(fallback.amount || 0);
  if (fallbackAmount <= 0) return [];

  return [{
    method: fallback.method || 'Cash',
    amount: fallbackAmount
  }];
};

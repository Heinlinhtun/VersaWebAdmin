import assert from 'node:assert/strict';
import test from 'node:test';
import { pendingSupplierPayment, submitSupplierPayment } from '../src/services/supplierPayments.js';

const form = () => ({ SupplierId: 1, Amount: 601456, PaymentMethod: 'Cash', ReferenceNo: '', Note: '', PaymentDate: '2026-09-28' });
function storage(tenant = 'test_shop') {
  const data = new Map([
    ['token', `test.${Buffer.from(JSON.stringify({ TenantDb: tenant })).toString('base64url')}.test`],
    ['UserId', '1'],
  ]);
  globalThis.localStorage = { getItem: k => data.get(k) ?? null, setItem: (k, v) => data.set(k, v), removeItem: k => data.delete(k) };
  return data;
}

test('double click sends one HTTP request', async () => {
  storage(); let resolve; let requests = 0;
  const api = { post: () => { requests++; return new Promise(r => { resolve = r; }); } };
  const first = submitSupplierPayment(api, form());
  await assert.rejects(submitSupplierPayment(api, form()), /still being processed/);
  resolve({ data: { paymentId: 92 } }); await first;
  assert.equal(requests, 1);
  assert.equal(pendingSupplierPayment(1), null);
});

test('lost response preserves exact payload and request ID across a reopened form', async () => {
  storage(); const sent = [];
  await assert.rejects(submitSupplierPayment({ post: async (_, payload) => { sent.push(payload); throw new Error('Connection lost'); } }, form()));
  const pending = pendingSupplierPayment(1);
  assert.ok(pending.RequestId);
  await submitSupplierPayment({ post: async (_, payload) => { sent.push(payload); return { data: { paymentId: 92, replayed: true } }; } }, pending);
  assert.deepEqual(sent[0], sent[1]);
  assert.equal(pendingSupplierPayment(1), null);
});

test('changed details cannot silently replace an unresolved payment', async () => {
  storage();
  await assert.rejects(submitSupplierPayment({ post: async () => { throw new Error('timeout'); } }, form()));
  let called = false;
  await assert.rejects(submitSupplierPayment({ post: async () => { called = true; } }, { ...form(), Amount: 123 }), /unresolved/);
  assert.equal(called, false);
  assert.equal(pendingSupplierPayment(1).Amount, 601456);
});

test('server failures retain request, explicit validation rejection clears it', async () => {
  storage();
  await assert.rejects(submitSupplierPayment({ post: async () => { throw { response: { status: 500 } }; } }, form()));
  assert.ok(pendingSupplierPayment(1));
  await assert.rejects(submitSupplierPayment({ post: async () => { throw { response: { status: 409 } }; } }, pendingSupplierPayment(1)));
  assert.equal(pendingSupplierPayment(1), null);
});

test('pending requests cannot cross tenant boundaries', async () => {
  const data = storage('shop_one');
  await assert.rejects(submitSupplierPayment({ post: async () => { throw new Error('timeout'); } }, form()));
  data.set('token', `test.${Buffer.from(JSON.stringify({ TenantDb: 'shop_two' })).toString('base64url')}.test`);
  assert.equal(pendingSupplierPayment(1), null);
});

test('storage failure stops payment before any HTTP request', async () => {
  storage(); localStorage.setItem = () => { throw new Error('Storage unavailable'); };
  let called = false;
  await assert.rejects(submitSupplierPayment({ post: async () => { called = true; } }, form()), /Storage unavailable/);
  assert.equal(called, false);
});

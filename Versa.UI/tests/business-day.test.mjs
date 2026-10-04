import assert from 'node:assert/strict';
import test from 'node:test';

const cache = new Map();
globalThis.localStorage = {
  getItem: key => cache.get(key) ?? null,
  setItem: (key, value) => cache.set(key, value)
};

const { currentBusinessDateInput } = await import('../src/utils/businessDay.js');

test('business date moves to the following day at the configured cut-off', () => {
  localStorage.setItem('ShopSettings', JSON.stringify({ BusinessDayCutoffTime: '20:00:00' }));
  assert.equal(currentBusinessDateInput(new Date(2026, 9, 4, 19, 59)), '2026-10-04');
  assert.equal(currentBusinessDateInput(new Date(2026, 9, 4, 20, 0)), '2026-10-05');
  localStorage.setItem('ShopSettings', JSON.stringify({ BusinessDayCutoffTime: '00:00:00' }));
  assert.equal(currentBusinessDateInput(new Date(2026, 9, 4, 23, 30)), '2026-10-04');
});

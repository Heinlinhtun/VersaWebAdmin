import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { computed, ref } from 'vue';
import { parse } from '@vue/compiler-sfc';

globalThis.localStorage = { getItem: () => null, setItem() {} };
globalThis.document = { documentElement: { classList: { toggle() {} } } };
const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');
const report = parse(read('../src/views/reports/ExpenseReport.vue')).descriptor;
const { useI18n } = await import('../src/composables/useI18n.js');

test('expense analytics sends the selected date range with its branch filter', async () => {
  const requests = [];
  const filters = { fromDate: '2026-09-01', toDate: '2026-09-30', branchId: 4 };
  const source = report.scriptSetup.content.replace(/^import .*;\r?\n/gm, '');
  const deps = {
    ref,
    computed,
    onMounted() {},
    watch() {},
    defineProps: () => ({ filters }),
    api: { get: async (...args) => { requests.push(args); return { data: { categories: [], monthly: [] } }; } },
    useI18n
  };
  const vm = new Function(...Object.keys(deps), `${source}; return { fetchAnalytics };`)(...Object.values(deps));

  await vm.fetchAnalytics();
  assert.deepEqual(requests, [[
    '/Reports/expense-analytics',
    { params: { fromDate: '2026-09-01', toDate: '2026-09-30', branchId: 4 } }
  ]]);
});

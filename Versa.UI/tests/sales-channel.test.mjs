import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { parse } from '@vue/compiler-sfc';
import { ref, computed, reactive } from 'vue';

test('report channel selection scopes every report request and is included in voucher CSV', async () => {
  const source = parse(readFileSync(new URL('../src/views/reports/SaleReport.vue', import.meta.url), 'utf8'))
    .descriptor.scriptSetup.content.replace(/^import .*;\r?\n/gm, '');
  const requests = [];
  let csv;
  const filters = { fromDate: '2026-11-01', toDate: '2026-11-19', branchId: 2 };
  const deps = {
    ref, computed, reactive, onMounted() {}, watch() {},
    ChartJS: { register() {} }, ArcElement: {}, Tooltip: {}, Legend: {},
    defineProps: () => ({ filters }), useQuantitySettings: () => ({ formatQuantity: String }),
    useI18n: () => ({ t: key => key }), useToast: () => ({ addToast() {} }),
    useCsvExport: () => ({ exportToCsv: (...args) => { csv = args; } }),
    api: { get: async (url, config) => { requests.push([url, { ...config.params }]); return { data: url.endsWith('sales-summary') ? {} : [] }; } }
  };
  const vm = new Function(...Object.keys(deps), `${source}; return { selectedSalesChannel, fetchData, sales, handleExportCsv };`)(...Object.values(deps));
  for (const channel of ['Facebook', 'TikTok', 'Offline', 'Unspecified', '']) {
    requests.length = 0;
    vm.selectedSalesChannel.value = channel;
    await vm.fetchData();
    assert.equal(requests.length, 5);
    for (const [, params] of requests) assert.deepEqual(params, { ...filters, salesChannel: channel });
  }
  vm.sales.value = [{ SaleId: 827, SalesChannel: 'TikTok' }];
  vm.handleExportCsv();
  assert.equal(csv[1].at(-1), 'common.salesChannel');
  assert.equal(csv[2][0].at(-1), 'common.channelTikTok');
});

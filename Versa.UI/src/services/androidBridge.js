const bridge = () => window.VersaAndroid || window.VersaDesktop || null;
const bridgeKind = () => {
  if (window.VersaAndroid) return 'android';
  if (window.VersaDesktop) return 'desktop';
  return null;
};

const parseResult = (raw, fallback = null) => {
  if (raw == null || raw === '') return fallback;
  if (typeof raw === 'boolean') return raw;
  try {
    return JSON.parse(raw);
  } catch {
    return fallback;
  }
};

const callJson = (method, ...args) => {
  const native = bridge();
  if (!native || typeof native[method] !== 'function') {
    return { ok: false, message: 'Android bridge is not available.' };
  }

  try {
    return parseResult(native[method](...args), { ok: false, message: 'Invalid Android bridge response.' });
  } catch (err) {
    return { ok: false, message: err?.message || 'Android bridge call failed.' };
  }
};

export const androidBridge = {
  isAvailable() {
    return !!bridge();
  },

  isAndroid() {
    return bridgeKind() === 'android';
  },

  isDesktop() {
    return bridgeKind() === 'desktop';
  },

  supportsNativePrint() {
    return bridgeKind() === 'android';
  },

  isOnline() {
    const native = bridge();
    if (!native || typeof native.isOnline !== 'function') return navigator.onLine;
    return !!native.isOnline();
  },

  saveCatalog(catalog) {
    return callJson('saveCatalog', JSON.stringify(catalog || {}));
  },

  getCatalog() {
    const native = bridge();
    if (!native || typeof native.getCatalog !== 'function') return null;
    return parseResult(native.getCatalog(), null);
  },

  saveSale(payload) {
    return callJson('saveSale', JSON.stringify(payload || {}));
  },

  getPendingSales() {
    return callJson('getPendingSales');
  },

  markSaleSynced(localId, serverSaleId) {
    return callJson('markSaleSynced', localId, String(serverSaleId || ''));
  },

  getBondedPrinters() {
    return callJson('getBondedPrinters');
  },

  setPrinterAddress(address) {
    return callJson('setPrinterAddress', address);
  },

  getPrinterAddress() {
    return callJson('getPrinterAddress');
  },

  printText(text) {
    return callJson('printText', text || '');
  },

  printReceipt(text, logoData = '') {
    const native = bridge();
    if (native && typeof native.printReceipt === 'function') {
      return callJson('printReceipt', text || '', logoData || '');
    }

    return callJson('printText', text || '');
  },

  printHtml(html = '', title = 'Invoice') {
    return callJson('printHtml', html || '', title || 'Invoice');
  },

  printImage(imageData = '') {
    return callJson('printImage', imageData || '');
  },

  shareImage(imageData, fileName = 'invoice.png', title = 'Invoice', text = '') {
    return callJson('shareImage', imageData || '', fileName || 'invoice.png', title || 'Invoice', text || '');
  }
};

export default androidBridge;

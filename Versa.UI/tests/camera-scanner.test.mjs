import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { parse, compileScript, compileTemplate } from '@vue/compiler-sfc';
import { ref } from 'vue';
import { createCameraScanner, cameraErrorKey } from '../src/utils/cameraScanner.js';

function deferred() {
  let resolve, reject;
  const promise = new Promise((res, rej) => { resolve = res; reject = rej; });
  return { promise, resolve, reject };
}
function stream() {
  const track = { stops: 0, stop() { this.stops++; } };
  return { track, getTracks: () => [track] };
}
function harness(options = {}) {
  const media = stream();
  const video = { srcObject: null, play: async () => {}, pause() {} };
  const results = [], errors = [], constraints = [];
  const callbacks = [];
  const controls = { stops: 0, stop() { this.stops++; } };
  let scans = 0;
  const decoder = {
    reader: { scan(element, callback) { scans++; callbacks.push(callback); return controls; } },
    isExpectedError: error => error.name === 'NotFoundException',
  };
  const scanner = createCameraScanner({
    video, onResult: text => results.push(text), onError: key => errors.push(key),
    secureContext: true,
    mediaDevices: { getUserMedia: async value => { constraints.push(value); return media; } },
    loadDecoder: async () => decoder,
    ...options,
  });
  return { scanner, video, media, results, errors, constraints, callbacks, controls, get scans() { return scans; } };
}

test('rear camera scans once, tolerates unreadable frames and releases stream on result', async () => {
  const h = harness();
  await h.scanner.start();
  assert.deepEqual(h.constraints, [{ audio: false, video: { facingMode: { ideal: 'environment' } } }]);
  h.callbacks[0](undefined, { name: 'NotFoundException' }, h.controls);
  assert.deepEqual(h.errors, []);
  h.callbacks[0]({ getText: () => '  0012345  ' }, undefined, h.controls);
  h.callbacks[0]({ getText: () => 'duplicate' }, undefined, h.controls);
  assert.deepEqual(h.results, ['  0012345  ']);
  assert.ok(h.controls.stops > 0);
  assert.ok(h.media.track.stops > 0);
  assert.equal(h.video.srcObject, null);
});

test('closing before permission resolves stops the late stream without starting decoding', async () => {
  const permission = deferred();
  const media = stream();
  const h = harness({ mediaDevices: { getUserMedia: () => permission.promise } });
  const starting = h.scanner.start();
  await Promise.resolve();
  h.scanner.stop();
  permission.resolve(media);
  await starting;
  assert.equal(media.track.stops, 1);
  assert.equal(h.scans, 0);
  assert.equal(h.video.srcObject, null);
});

test('close during decoder loading never asks for camera permission', async () => {
  const loading = deferred();
  const h = harness({ loadDecoder: () => loading.promise });
  const starting = h.scanner.start();
  h.scanner.stop();
  loading.resolve({ reader: {}, isExpectedError: () => true });
  await starting;
  assert.equal(h.constraints.length, 0);
});

test('close during playback releases tracks and cannot start a stale decoder', async () => {
  const playback = deferred();
  const h = harness();
  h.video.play = () => playback.promise;
  const starting = h.scanner.start();
  await Promise.resolve();
  await Promise.resolve();
  assert.equal(h.video.srcObject, h.media);
  h.scanner.stop();
  assert.ok(h.media.track.stops > 0);
  playback.resolve();
  await starting;
  assert.equal(h.scans, 0);
});

test('old permission results and callbacks cannot affect a reopened scanner', async () => {
  const permission = deferred();
  const old = stream(), latest = stream();
  let requests = 0;
  const h = harness({ mediaDevices: { getUserMedia: () => ++requests === 1 ? permission.promise : Promise.resolve(latest) } });
  const first = h.scanner.start();
  await Promise.resolve();
  await h.scanner.start();
  permission.resolve(old);
  await first;
  assert.equal(old.track.stops, 1);
  assert.equal(latest.track.stops, 0);
  assert.equal(h.video.srcObject, latest);
  const previousCallback = h.callbacks[0];
  await h.scanner.start();
  previousCallback({ getText: () => 'stale' }, undefined, h.controls);
  assert.deepEqual(h.results, []);
  h.scanner.stop();
});

test('synchronous first-frame result also stops returned decoder controls', async () => {
  const controls = { stops: 0, stop() { this.stops++; } };
  const h = harness({ loadDecoder: async () => ({
    reader: { scan(video, callback) { callback({ getText: () => 'QR value' }, undefined, controls); return controls; } },
    isExpectedError: () => false,
  }) });
  await h.scanner.start();
  assert.deepEqual(h.results, ['QR value']);
  assert.ok(controls.stops > 0);
  assert.ok(h.media.track.stops > 0);
});

test('secure-context, WebView, permission, missing/busy camera and decoder errors are readable states', async () => {
  for (const [options, expected] of [
    [{ secureContext: false }, 'insecure'],
    [{ mediaDevices: {} }, 'unsupported'],
    ...['NotAllowedError', 'NotFoundError', 'NotReadableError', 'UnknownError'].map(name => [
      { mediaDevices: { getUserMedia: async () => { throw { name }; } } }, cameraErrorKey({ name }),
    ]),
  ]) {
    const h = harness(options);
    await h.scanner.start();
    assert.deepEqual(h.errors, [expected]);
    assert.equal(h.scans, 0);
  }
  const h = harness();
  await h.scanner.start();
  h.callbacks[0](undefined, new Error('decoder failed'), h.controls);
  assert.deepEqual(h.errors, ['failed']);
  assert.ok(h.media.track.stops > 0);
});

test('late permission rejection after close is silent', async () => {
  const permission = deferred();
  const h = harness({ mediaDevices: { getUserMedia: () => permission.promise } });
  const starting = h.scanner.start();
  await Promise.resolve();
  h.scanner.stop();
  permission.reject({ name: 'NotAllowedError' });
  await starting;
  assert.deepEqual(h.errors, []);
});

const modalSource = readFileSync(new URL('../src/components/BarcodeScannerModal.vue', import.meta.url), 'utf8');
test('modal compiles and closes/unmounts with camera cleanup and focus restoration', () => {
  const { descriptor } = parse(modalSource);
  const script = compileScript(descriptor, { id: 'scanner' });
  assert.deepEqual(compileTemplate({ id: 'scanner', source: descriptor.template.content,
    compilerOptions: { bindingMetadata: script.bindings } }).errors, []);
  let mounted, unmount, scannerOptions, stops = 0, starts = 0, restored = 0;
  const emitted = [], listeners = new Map();
  const doc = { hidden: false, activeElement: { isConnected: true, focus() { restored++; } },
    addEventListener: (name, fn) => listeners.set(name, fn), removeEventListener: name => listeners.delete(name) };
  const deps = {
    ref, defineEmits: () => (...args) => emitted.push(args), useI18n: () => ({ t: key => key }),
    onMounted: fn => { mounted = fn; }, onBeforeUnmount: fn => { unmount = fn; },
    document: doc, window: doc,
    createCameraScanner: options => { scannerOptions = options; return { start() { starts++; }, stop() { stops++; } }; },
  };
  const setup = descriptor.scriptSetup.content.replace(/^import .*;\r?\n/gm, '');
  const vm = new Function(...Object.keys(deps), `${setup}; return { video, closeButton, close, trapFocus };`)(...Object.values(deps));
  vm.video.value = {};
  vm.closeButton.value = { focus() {} };
  mounted();
  assert.equal(starts, 1);
  scannerOptions.onResult('123');
  assert.deepEqual(emitted[0], ['scan', '123']);
  vm.close();
  assert.equal(stops, 1);
  doc.hidden = true;
  listeners.get('visibilitychange')();
  assert.equal(stops, 2);
  unmount();
  assert.equal(stops, 3);
  assert.equal(restored, 1);
  assert.equal(listeners.size, 0);
});

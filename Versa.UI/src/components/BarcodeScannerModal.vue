<template>
  <Teleport to="body">
    <div ref="dialog" role="dialog" aria-modal="true" aria-labelledby="barcode-scanner-title"
      class="fixed inset-0 z-[190] flex items-center justify-center bg-slate-900/60 p-3"
      @click.self="close" @keydown.esc.stop.prevent="close" @keydown.tab="trapFocus" @keydown.stop>
      <section class="w-full max-w-lg max-h-full overflow-y-auto rounded-lg bg-white shadow-xl">
        <header class="flex items-center justify-between gap-3 border-b p-4">
          <h2 id="barcode-scanner-title" class="text-base">{{ t('scanner.title') }}</h2>
          <button ref="closeButton" type="button" @click="close" :title="t('common.close')" :aria-label="t('common.close')"
            class="flex h-11 w-11 shrink-0 items-center justify-center rounded-lg hover:bg-gray-100 focus-visible:ring-2 focus-visible:ring-blue-500">
            <X class="h-5 w-5" />
          </button>
        </header>
        <div class="space-y-3 p-4">
          <video ref="video" autoplay muted playsinline :aria-label="t('scanner.preview')"
            class="aspect-[4/3] w-full rounded-lg bg-black object-contain"></video>
          <p v-if="error" role="alert" class="break-words text-sm text-red-700">{{ t(`scanner.${error}`) }}</p>
          <p v-else role="status" class="text-sm text-gray-600">{{ t(ready ? 'scanner.scanning' : 'scanner.starting') }}</p>
        </div>
      </section>
    </div>
  </Teleport>
</template>

<script setup>
import { ref, onMounted, onBeforeUnmount } from 'vue';
import { X } from 'lucide-vue-next';
import { useI18n } from '../composables/useI18n';
import { createCameraScanner } from '../utils/cameraScanner';

const emit = defineEmits(['close', 'scan']);
const { t } = useI18n();
const video = ref(null);
const dialog = ref(null);
const closeButton = ref(null);
const error = ref('');
const ready = ref(false);
let scanner;
let previousFocus;
function close() { scanner?.stop(); emit('close'); }
function trapFocus(event) {
  event.preventDefault();
  closeButton.value?.focus();
}
function stopWhenHidden() { if (document.hidden) close(); }
onMounted(() => {
  previousFocus = document.activeElement;
  closeButton.value.focus();
  scanner = createCameraScanner({
    video: video.value,
    onResult: text => emit('scan', text),
    onError: key => { error.value = key; },
    onReady: () => { ready.value = true; },
  });
  scanner.start();
  document.addEventListener('visibilitychange', stopWhenHidden);
  window.addEventListener('pagehide', close);
});
onBeforeUnmount(() => {
  scanner?.stop();
  document.removeEventListener('visibilitychange', stopWhenHidden);
  window.removeEventListener('pagehide', close);
  if (previousFocus?.isConnected) previousFocus.focus();
});
</script>

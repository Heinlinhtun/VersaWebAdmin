import { watchEffect } from 'vue';
import { useQuantitySettings } from '../composables/useQuantitySettings.js';

// Only quantity inputs use this directive; prices, dates and IDs are unaffected.
export const quantityInput = {
  mounted(element, binding = {}) {
    const { quantityStep, roundQuantity, floorQuantity } = useQuantitySettings();
    const positiveMinimum = element.min === '1';
    const stop = watchEffect(() => {
      element.step = String(quantityStep.value);
      if (positiveMinimum) element.min = String(quantityStep.value);
    });
    const normalize = () => {
      // Bundle composition validates on Add so a small recipe amount is never
      // silently replaced with zero before the button handler can explain it.
      if (binding.modifiers?.preserve) return;
      if (element.value === '' || !Number.isFinite(Number(element.value))) return;
      let value = roundQuantity(element.value);
      if (element.min !== '') value = Math.max(value, Number(element.min));
      if (element.max !== '') value = Math.min(value, floorQuantity(Number(element.max)));
      if (Number(element.value) !== value) {
        element.value = String(value);
        element.dispatchEvent(new Event('input', { bubbles: true }));
      }
    };
    element.addEventListener('change', normalize);
    element.__quantityCleanup = () => { stop(); element.removeEventListener('change', normalize); };
  },
  unmounted(element) { element.__quantityCleanup?.(); },
};

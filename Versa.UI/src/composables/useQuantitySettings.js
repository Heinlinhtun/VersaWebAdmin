import { computed, ref } from 'vue';

export const normalizeQuantityDecimals = value => {
  const number = Number(value);
  return Number.isInteger(number) && number >= 1 && number <= 5 ? number : 2;
};
const enabled = ref(false);
const places = ref(2);
export function applyQuantitySettings(settings = {}) {
  const value = settings.UseDecimalQuantity ?? settings.useDecimalQuantity;
  enabled.value = [true, 1, '1', 'true', 'True'].includes(value);
  places.value = normalizeQuantityDecimals(settings.QuantityDecimalPlaces ?? settings.quantityDecimalPlaces);
}
try { applyQuantitySettings(JSON.parse(localStorage.getItem('ShopSettings') || '{}')); } catch { /* Default for a missing or invalid cache. */ }
const quantityDecimals = computed(() => enabled.value ? places.value : 0);
const quantityStep = computed(() => enabled.value ? 10 ** -places.value : 1);
const roundQuantity = value => Number((Number(value) || 0).toFixed(quantityDecimals.value));
// Stock limits must round down, never offer more stock than is available.
const floorQuantity = value => Math.floor((Number(value) + Number.EPSILON) * 10 ** quantityDecimals.value) / 10 ** quantityDecimals.value;
const formatQuantity = (value, { fixed = false } = {}) => (Number(value) || 0).toLocaleString(undefined, {
  minimumFractionDigits: fixed ? quantityDecimals.value : 0,
  maximumFractionDigits: quantityDecimals.value,
});
export function useQuantitySettings() {
  return { useDecimalQuantity: enabled, quantityDecimals, quantityStep, roundQuantity, floorQuantity, formatQuantity };
}

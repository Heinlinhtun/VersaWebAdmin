import { ref } from 'vue';

const toasts = ref([]);

export function useToast() {
  const addToast = (message, type = 'success') => {
    const id = Date.now();
    toasts.value.push({ id, message, type });
    
    // ၄ စက္ကန့်ကြာရင် ပျောက်သွားအောင် သတ်မှတ်ခြင်း
    setTimeout(() => {
      removeToast(id);
    }, 4000);
  };

  const removeToast = (id) => {
    toasts.value = toasts.value.filter(t => t.id !== id);
  };

  return { toasts, addToast, removeToast };
}

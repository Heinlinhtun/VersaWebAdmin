<template>
  <router-view></router-view>
</template>

<script setup>
import { watch } from 'vue';
import { useRoute } from 'vue-router';
import api from './services/api';
import { applyQuantitySettings } from './composables/useQuantitySettings';

const route = useRoute();
let loadedToken;
watch(() => route.fullPath, async () => {
  const token = localStorage.getItem('token');
  if (!token) { loadedToken = null; applyQuantitySettings(); return; }
  if (token === loadedToken) return;
  loadedToken = token;
  try {
    const { data } = await api.get('/Settings');
    if (token !== localStorage.getItem('token')) return;
    applyQuantitySettings(data || {});
    localStorage.setItem('ShopSettings', JSON.stringify(data || {}));
  } catch { loadedToken = null; }
}, { immediate: true });
</script>

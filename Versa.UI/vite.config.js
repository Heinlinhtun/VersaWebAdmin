import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  build: {
    // Production build မှာ .map ဖိုင်တွေ မထုတ်အောင် လုပ်ခြင်း
    sourcemap: false,
    // (Option) Code တွေကို ပိုပြီးကျစ်လစ်စေပြီး ဖတ်ရခက်အောင်လုပ်ပေးတဲ့ tool
    minify: 'terser', 
    terserOptions: {
      compress: {
        // console.log တွေကိုပါ တစ်ခါတည်း ဖြုတ်ပစ်ချင်ရင် သုံးနိုင်ပါတယ်
        drop_console: true,
        drop_debugger: true,
      },
    },
  },
})
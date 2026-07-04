import { createApp } from 'vue'
import './style.css' // Tailwind သုံးထားရင် ဒါလေး ထည့်ပါ
import App from './App.vue'
import router from './router' // router ကို import လုပ်ပါ

const theme = localStorage.getItem('app_theme') === 'dark' ? 'dark' : 'light'
document.documentElement.classList.add(theme === 'dark' ? 'theme-dark' : 'theme-light')
document.documentElement.setAttribute('data-theme', theme)

const app = createApp(App)
app.use(router) // router ကို အသုံးပြုရန် ပြောခြင်း
app.mount('#app')

const MYANMAR_TEXT_RE = /[\u1000-\u109F\uAA60-\uAA7F\uA9E0-\uA9FF]/;
const MYANMAR_ATTRS = ['placeholder', 'title', 'aria-label', 'value'];
const SKIP_MYANMAR_SCAN = new Set(['SCRIPT', 'STYLE', 'NOSCRIPT', 'SVG', 'PATH']);

const markMyanmarTextElement = (el) => {
  if (!el || el.nodeType !== 1 || SKIP_MYANMAR_SCAN.has(el.tagName)) return;

  let hasMyanmar = false;
  for (const attr of MYANMAR_ATTRS) {
    const value = el.getAttribute?.(attr);
    if (value && MYANMAR_TEXT_RE.test(value)) {
      hasMyanmar = true;
      break;
    }
  }

  if (!hasMyanmar && ['INPUT', 'TEXTAREA', 'SELECT'].includes(el.tagName)) {
    hasMyanmar = MYANMAR_TEXT_RE.test(el.value || '');
  }

  el.classList.toggle('myanmar-text', hasMyanmar);
};

const scanMyanmarText = (root = document.body) => {
  if (!root) return;

  if (root.nodeType === Node.TEXT_NODE) {
    if (MYANMAR_TEXT_RE.test(root.nodeValue || '')) {
      markMyanmarTextElement(root.parentElement);
    }
    return;
  }

  if (root.nodeType !== Node.ELEMENT_NODE || SKIP_MYANMAR_SCAN.has(root.tagName)) return;

  markMyanmarTextElement(root);

  const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
    acceptNode(node) {
      const parent = node.parentElement;
      if (!parent || SKIP_MYANMAR_SCAN.has(parent.tagName)) return NodeFilter.FILTER_REJECT;
      return MYANMAR_TEXT_RE.test(node.nodeValue || '') ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_SKIP;
    }
  });

  while (walker.nextNode()) {
    markMyanmarTextElement(walker.currentNode.parentElement);
  }
};

let myanmarScanTimer = 0;
const scheduleMyanmarScan = (root = document.body) => {
  window.clearTimeout(myanmarScanTimer);
  myanmarScanTimer = window.setTimeout(() => scanMyanmarText(root), 50);
};

scanMyanmarText();

document.addEventListener('input', (e) => {
  markMyanmarTextElement(e.target);
});

// Prevent invalid characters in all number inputs globally
document.addEventListener('keydown', (e) => {
  if (e.target && e.target.tagName === 'INPUT' && e.target.type === 'number') {
    if (['e', 'E', '+', '-'].includes(e.key)) {
      e.preventDefault();
    }
  }
});

// Force numeric keypad on mobile for all number inputs
const observer = new MutationObserver((mutations) => {
  mutations.forEach((mutation) => {
    if (mutation.addedNodes.length) {
      mutation.addedNodes.forEach((node) => {
        if (node.nodeType === 1) { // ELEMENT_NODE
          if (node.tagName === 'INPUT' && node.type === 'number') {
            if (!node.hasAttribute('inputmode')) {
                node.setAttribute('inputmode', 'decimal');
            }
          }
          if (node.querySelectorAll) {
            node.querySelectorAll('input[type="number"]').forEach(input => {
              if (!input.hasAttribute('inputmode')) {
                  input.setAttribute('inputmode', 'decimal');
              }
            });
          }
        }
      });
      scheduleMyanmarScan(document.body);
    }
  });
});
observer.observe(document.body, { childList: true, subtree: true });

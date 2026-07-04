<template>
  <div class="privacy-container">
    <div class="privacy-content">
      <div class="header">
        <h1>Privacy Policy</h1>
        <p>Last updated: {{ currentDate }}</p>
      </div>

      <div class="section">
        <h2>1. Introduction</h2>
        <p>Welcome to Versa Web. We respect your privacy and are committed to protecting your personal data. This privacy policy will inform you as to how we look after your personal data when you visit our website and tell you about your privacy rights and how the law protects you.</p>
      </div>

      <div class="section">
        <h2>2. Data We Collect</h2>
        <p>We may collect, use, store and transfer different kinds of personal data about you which we have grouped together as follows:</p>
        <ul>
          <li><strong>Identity Data:</strong> includes first name, last name, username or similar identifier.</li>
          <li><strong>Contact Data:</strong> includes billing address, delivery address, email address and telephone numbers.</li>
          <li><strong>Technical Data:</strong> includes internet protocol (IP) address, your login data, browser type and version, time zone setting and location.</li>
          <li><strong>Usage Data:</strong> includes information about how you use our website, products and services.</li>
        </ul>
      </div>

      <div class="section">
        <h2>3. How We Use Your Data</h2>
        <p>We will only use your personal data when the law allows us to. Most commonly, we will use your personal data in the following circumstances:</p>
        <ul>
          <li>Where we need to perform the contract we are about to enter into or have entered into with you.</li>
          <li>Where it is necessary for our legitimate interests (or those of a third party) and your interests and fundamental rights do not override those interests.</li>
          <li>Where we need to comply with a legal obligation.</li>
        </ul>
      </div>

      <div class="section">
        <h2>4. Data Security</h2>
        <p>We have put in place appropriate security measures to prevent your personal data from being accidentally lost, used or accessed in an unauthorized way, altered or disclosed. In addition, we limit access to your personal data to those employees, agents, contractors and other third parties who have a business need to know.</p>
      </div>

      <div class="section">
        <h2>5. Your Legal Rights</h2>
        <p>Under certain circumstances, you have rights under data protection laws in relation to your personal data, including the right to:</p>
        <ul>
          <li>Request access to your personal data.</li>
          <li>Request correction of your personal data.</li>
          <li>Request erasure of your personal data.</li>
          <li>Object to processing of your personal data.</li>
          <li>Request restriction of processing your personal data.</li>
        </ul>
      </div>

      <div class="footer-actions">
        <button @click="goBack" class="btn btn-primary">Return</button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue';
import { useRouter } from 'vue-router';

const router = useRouter();

const currentDate = computed(() => {
  return new Date().toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'long',
    day: 'numeric'
  });
});

const goBack = () => {
  // If there's history, go back, otherwise go to login
  if (window.history.length > 2) {
    router.back();
  } else {
    router.push('/login');
  }
};
</script>

<style scoped>
.privacy-container {
  min-height: 100vh;
  background-color: #f8fafc;
  padding: 3rem 1rem;
  display: flex;
  justify-content: center;
}

.privacy-content {
  background: white;
  max-width: 800px;
  width: 100%;
  padding: 3rem;
  border-radius: 12px;
  box-shadow: 0 4px 6px -1px rgb(0 0 0 / 0.1), 0 2px 4px -2px rgb(0 0 0 / 0.1);
  color: #334155;
  font-family: ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
  text-align: left;
}

.header {
  margin-bottom: 2.5rem;
  border-bottom: 1px solid #e2e8f0;
  padding-bottom: 1.5rem;
  text-align: center;
}

.header h1 {
  font-size: 2.25rem;
  font-weight: 700;
  color: #0f172a;
  margin: 0 0 0.5rem 0;
}

.header p {
  color: #64748b;
  margin: 0;
  font-style: italic;
}

.section {
  margin-bottom: 2rem;
}

.section h2 {
  font-size: 1.25rem;
  font-weight: 600;
  color: #1e293b;
  margin-bottom: 1rem;
}

.section p {
  line-height: 1.6;
  margin-bottom: 1rem;
  font-size: 1rem;
}

.section ul {
  padding-left: 1.5rem;
  margin-bottom: 1rem;
}

.section li {
  line-height: 1.6;
  margin-bottom: 0.5rem;
  font-size: 1rem;
}

.section strong {
  color: #0f172a;
}

.footer-actions {
  margin-top: 3rem;
  padding-top: 1.5rem;
  border-top: 1px solid #e2e8f0;
  display: flex;
  justify-content: center;
}

.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0.75rem 1.5rem;
  font-size: 1rem;
  font-weight: 500;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
  border: none;
}

.btn-primary {
  background-color: #3b82f6;
  color: white;
}

.btn-primary:hover {
  background-color: #2563eb;
}

@media (max-width: 640px) {
  .privacy-container {
    padding: 1rem;
  }
  
  .privacy-content {
    padding: 1.5rem;
  }
  
  .header h1 {
    font-size: 1.75rem;
  }
}
</style>

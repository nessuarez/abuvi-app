<script setup lang="ts">
import { ref } from 'vue'
import TabView from 'primevue/tabview'
import TabPanel from 'primevue/tabpanel'
import LoginForm from './LoginForm.vue'
import RegisterForm from './RegisterForm.vue'

const activeTab = ref(0)
</script>

<template>
  <div class="w-full max-w-md rounded-lg bg-white/95 p-8 shadow-2xl backdrop-blur-sm">
    <div class="mb-6 text-center">
      <h1 class="mb-2 text-3xl font-bold text-gray-900">Bienvenidos a ABUVI</h1>
      <p class="text-sm text-gray-600">
        Plataforma exclusiva para socios/as
      </p>
    </div>

    <TabView v-model:activeIndex="activeTab" class="auth-tabs">
      <TabPanel header="Iniciar Sesión">
        <LoginForm />
      </TabPanel>
      <TabPanel header="Registrarse">
        <RegisterForm />
      </TabPanel>
    </TabView>
  </div>
</template>

<style>
.auth-tabs .p-tabview-nav {
  background: transparent;
  border: none;
}

.auth-tabs .p-tabview-panels {
  background: transparent;
  padding: 1.5rem 0 0 0;
}
</style>

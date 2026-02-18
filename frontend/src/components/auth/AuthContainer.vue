<script setup lang="ts">
import { ref } from 'vue'
import Tabs from 'primevue/tabs'
import TabList from 'primevue/tablist'
import Tab from 'primevue/tab'
import TabPanels from 'primevue/tabpanels'
import TabPanel from 'primevue/tabpanel'
import LoginForm from './LoginForm.vue'
import RegisterForm from './RegisterForm.vue'

const activeTab = ref('login')
</script>

<template>
  <div class="w-full max-w-md rounded-lg bg-white/95 p-8 shadow-2xl backdrop-blur-sm">
    <div class="mb-6 text-center">
      <h1 class="mb-2 text-3xl font-bold text-gray-900">Bienvenidos a ABUVI</h1>
      <p class="text-sm text-gray-600">
        Plataforma exclusiva para socios/as
      </p>
    </div>

    <Tabs v-model:value="activeTab" class="auth-tabs">
      <TabList>
        <Tab value="login">Iniciar Sesión</Tab>
        <Tab value="register">Registrarse</Tab>
      </TabList>
      <TabPanels>
        <TabPanel value="login">
          <LoginForm />
        </TabPanel>
        <TabPanel value="register">
          <RegisterForm />
        </TabPanel>
      </TabPanels>
    </Tabs>
  </div>
</template>

<style>
.auth-tabs .p-tablist {
  background: transparent;
  border: none;
}

.auth-tabs .p-tabpanels {
  background: transparent;
  padding: 1.5rem 0 0 0;
}
</style>

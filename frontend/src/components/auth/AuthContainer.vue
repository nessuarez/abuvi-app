<script setup lang="ts">
import { ref } from 'vue'
import Tabs from 'primevue/tabs'
import TabList from 'primevue/tablist'
import Tab from 'primevue/tab'
import TabPanels from 'primevue/tabpanels'
import TabPanel from 'primevue/tabpanel'
import LoginForm from './LoginForm.vue'
import RegisterForm from './RegisterForm.vue'
import logo from '@/assets/images/logo.svg'

const activeTab = ref('login')

function switchToLogin() {
  activeTab.value = 'login'
}
</script>

<template>
  <div class="w-full max-w-md rounded-lg bg-white/95 p-4 shadow-2xl backdrop-blur-sm sm:p-8">
    <div class="mb-4 text-center sm:mb-6">
      <img
        :src="logo"
        alt="ABUVI"
        class="mx-auto mb-3 max-w-[80px] sm:max-w-[120px]"
      />
      <h1 class="mb-2 text-2xl font-bold text-gray-900 sm:text-3xl">Te damos la bienvenida a ABUVI</h1>
      <p class="text-sm font-medium text-gray-700">
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
          <RegisterForm @go-to-login="switchToLogin" />
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

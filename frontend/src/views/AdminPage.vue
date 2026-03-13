<script setup lang="ts">
import { ref } from 'vue'
import Container from '@/components/ui/Container.vue'
import AdminSidebar from '@/components/admin/AdminSidebar.vue'
import Button from 'primevue/button'
import Drawer from 'primevue/drawer'

const drawerVisible = ref(false)
</script>

<template>
  <Container maxWidth="full">
    <div class="py-8">
      <div class="mb-6 flex items-center justify-between">
        <h1 class="text-3xl font-bold text-gray-900">Panel de Administración</h1>
        <!-- Mobile menu toggle -->
        <Button icon="pi pi-bars" text rounded class="md:hidden" data-testid="admin-menu-toggle"
          @click="drawerVisible = true" />
      </div>

      <div class="flex gap-8">
        <!-- Desktop sidebar -->
        <AdminSidebar class="hidden md:block" />

        <!-- Mobile drawer -->
        <Drawer v-model:visible="drawerVisible" header="Menú de Administración" position="left" class="md:hidden"
          data-testid="admin-drawer">
          <AdminSidebar @click="drawerVisible = false" />
        </Drawer>

        <!-- Main content area -->
        <main class="min-w-0 flex-1">
          <div class="py-4">
            <router-view />
          </div>
        </main>
      </div>
    </div>
  </Container>
</template>
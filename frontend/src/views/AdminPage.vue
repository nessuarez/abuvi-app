<script setup lang="ts">
import Container from '@/components/ui/Container.vue'
import Tabs from 'primevue/tabs'
import TabList from 'primevue/tablist'
import Tab from 'primevue/tab'
import TabPanels from 'primevue/tabpanels'
import TabPanel from 'primevue/tabpanel'
import CampsAdminPanel from '@/components/admin/CampsAdminPanel.vue'
import FamilyUnitsAdminPanel from '@/components/admin/FamilyUnitsAdminPanel.vue'
import UsersAdminPanel from '@/components/admin/UsersAdminPanel.vue'
</script>

<template>
  <Container>
    <div class="py-8">
      <h1 class="mb-6 text-3xl font-bold text-gray-900">Panel de Administración</h1>

      <Tabs value="0" data-testid="admin-tabs">
        <TabList>
          <Tab value="0" data-testid="tab-camps">
            <i class="pi pi-map mr-2" />
            Campamentos
          </Tab>
          <Tab value="1" data-testid="tab-family-units">
            <i class="pi pi-users mr-2" />
            Unidades Familiares
          </Tab>
          <Tab value="2" data-testid="tab-users">
            <i class="pi pi-user-edit mr-2" />
            Usuarios
          </Tab>
        </TabList>

        <TabPanels>
          <TabPanel value="0" data-testid="panel-camps">
            <div class="py-4">
              <CampsAdminPanel />
            </div>
          </TabPanel>

          <TabPanel value="1" data-testid="panel-family-units">
            <div class="py-4">
              <FamilyUnitsAdminPanel />
            </div>
          </TabPanel>

          <TabPanel value="2" data-testid="panel-users">
            <div class="py-4">
              <UsersAdminPanel />
            </div>
          </TabPanel>
        </TabPanels>
      </Tabs>
    </div>
  </Container>
</template>

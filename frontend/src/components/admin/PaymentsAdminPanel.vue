<script setup lang="ts">
import Tabs from 'primevue/tabs'
import TabList from 'primevue/tablist'
import Tab from 'primevue/tab'
import TabPanels from 'primevue/tabpanels'
import TabPanel from 'primevue/tabpanel'
import PaymentsReviewQueue from './PaymentsReviewQueue.vue'
import PaymentsAllList from './PaymentsAllList.vue'
import PaymentSettingsForm from './PaymentSettingsForm.vue'
</script>

<template>
  <Tabs value="0">
    <TabList>
      <Tab value="0">
        <i class="pi pi-clock mr-2" />
        Pendientes de revisión
      </Tab>
      <Tab value="1">
        <i class="pi pi-list mr-2" />
        Todos los pagos
      </Tab>
      <Tab value="2">
        <i class="pi pi-cog mr-2" />
        Configuración
      </Tab>
    </TabList>

    <TabPanels>
      <TabPanel value="0">
        <div class="py-4">
          <PaymentsReviewQueue />
        </div>
      </TabPanel>
      <TabPanel value="1">
        <div class="py-4">
          <PaymentsAllList />
        </div>
      </TabPanel>
      <TabPanel value="2">
        <div class="py-4">
          <PaymentSettingsForm />
        </div>
      </TabPanel>
    </TabPanels>
  </Tabs>
</template>

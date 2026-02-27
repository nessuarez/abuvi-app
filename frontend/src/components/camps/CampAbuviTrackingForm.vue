<script setup lang="ts">
import Panel from 'primevue/panel'
import InputText from 'primevue/inputtext'
import ToggleSwitch from 'primevue/toggleswitch'

defineProps<{
  abuviManagedByUserId: string | null
  abuviContactedAt: string | null
  abuviPossibility: string | null
  abuviLastVisited: string | null
  abuviHasDataErrors: boolean | null
  externalSourceId: number | null
}>()

const emit = defineEmits<{
  'update:abuviManagedByUserId': [value: string | null]
  'update:abuviContactedAt': [value: string | null]
  'update:abuviPossibility': [value: string | null]
  'update:abuviLastVisited': [value: string | null]
  'update:abuviHasDataErrors': [value: boolean | null]
}>()
</script>

<template>
  <Panel header="Seguimiento interno ABUVI" :toggleable="true" :collapsed="true">
    <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
      <div v-if="externalSourceId != null">
        <label class="mb-1 block text-sm font-medium text-gray-700">
          ID externo (N° hoja de cálculo)
        </label>
        <InputText
          :model-value="String(externalSourceId)"
          class="w-full"
          disabled
        />
      </div>

      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">
          Contactado (texto libre)
        </label>
        <InputText
          :model-value="abuviContactedAt ?? ''"
          class="w-full"
          placeholder="Ej: Febrero 2025"
          @update:model-value="emit('update:abuviContactedAt', ($event as string) || null)"
        />
      </div>

      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Posibilidad</label>
        <InputText
          :model-value="abuviPossibility ?? ''"
          class="w-full"
          placeholder="Ej: Alta, Media, Baja"
          @update:model-value="emit('update:abuviPossibility', ($event as string) || null)"
        />
      </div>

      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Última visita ABUVI</label>
        <InputText
          :model-value="abuviLastVisited ?? ''"
          class="w-full"
          placeholder="Ej: Verano 2024"
          @update:model-value="emit('update:abuviLastVisited', ($event as string) || null)"
        />
      </div>

      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Responsable ABUVI (ID)</label>
        <InputText
          :model-value="abuviManagedByUserId ?? ''"
          class="w-full"
          placeholder="UUID del usuario responsable"
          @update:model-value="emit('update:abuviManagedByUserId', ($event as string) || null)"
        />
      </div>

      <div class="flex items-center gap-3 pt-5">
        <ToggleSwitch
          :model-value="abuviHasDataErrors ?? false"
          @update:model-value="emit('update:abuviHasDataErrors', $event as boolean)"
        />
        <label class="text-sm font-medium" :class="abuviHasDataErrors ? 'text-red-600' : 'text-gray-700'">
          ¿Datos erróneos?
        </label>
      </div>
    </div>
  </Panel>
</template>

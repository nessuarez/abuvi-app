<script setup lang="ts">
import { ref } from 'vue'
import { useToast } from 'primevue/usetoast'
import Button from 'primevue/button'
import Panel from 'primevue/panel'

const props = withDefaults(
  defineProps<{
    iban: string
    bankName: string
    accountHolder: string
    amount?: number
    transferConcept?: string
    collapsible?: boolean
  }>(),
  { collapsible: false }
)

const toast = useToast()
const collapsed = ref(true)

const formatIban = (iban: string): string => {
  return iban.replace(/(.{4})/g, '$1 ').trim()
}

const formatCurrency = (amount: number): string =>
  new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(amount)

const copyToClipboard = async (text: string, label: string) => {
  try {
    await navigator.clipboard.writeText(text)
    toast.add({
      severity: 'success',
      summary: 'Copiado',
      detail: `${label} copiado al portapapeles`,
      life: 2000
    })
  } catch {
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: 'No se pudo copiar al portapapeles',
      life: 3000
    })
  }
}
</script>

<template>
  <Panel
    v-if="collapsible"
    :collapsed="collapsed"
    toggleable
    @update:collapsed="collapsed = $event"
  >
    <template #header>
      <div class="flex items-center gap-2">
        <i class="pi pi-building-columns text-blue-600" />
        <span class="font-semibold text-gray-900">Datos de transferencia bancaria</span>
      </div>
    </template>

    <div class="grid gap-3 sm:grid-cols-2">
      <div>
        <span class="text-xs font-medium text-gray-500">IBAN</span>
        <div class="flex items-center gap-2">
          <span class="font-mono text-sm text-gray-900">{{ formatIban(iban) }}</span>
          <Button
            icon="pi pi-copy"
            text
            rounded
            size="small"
            aria-label="Copiar IBAN"
            @click="copyToClipboard(iban, 'IBAN')"
          />
        </div>
      </div>
      <div>
        <span class="text-xs font-medium text-gray-500">Titular</span>
        <p class="text-sm text-gray-900">{{ accountHolder }}</p>
      </div>
      <div>
        <span class="text-xs font-medium text-gray-500">Banco</span>
        <p class="text-sm text-gray-900">{{ bankName }}</p>
      </div>
      <div v-if="amount != null">
        <span class="text-xs font-medium text-gray-500">Importe</span>
        <p class="text-sm font-semibold text-gray-900">{{ formatCurrency(amount) }}</p>
      </div>
      <div v-if="transferConcept" class="sm:col-span-2">
        <span class="text-xs font-medium text-gray-500">Concepto</span>
        <div class="flex items-center gap-2">
          <span class="font-mono text-sm font-semibold text-blue-700">{{ transferConcept }}</span>
          <Button
            icon="pi pi-copy"
            text
            rounded
            size="small"
            aria-label="Copiar concepto"
            @click="copyToClipboard(transferConcept, 'Concepto')"
          />
        </div>
      </div>
    </div>
  </Panel>

  <div
    v-else
    class="rounded-lg border border-blue-200 bg-blue-50 p-4"
  >
    <div class="mb-3 flex items-center gap-2">
      <i class="pi pi-building-columns text-blue-600" />
      <span class="font-semibold text-blue-900">Datos de transferencia bancaria</span>
    </div>

    <div class="grid gap-3 sm:grid-cols-2">
      <div>
        <span class="text-xs font-medium text-blue-600">IBAN</span>
        <div class="flex items-center gap-2">
          <span class="font-mono text-sm text-gray-900">{{ formatIban(iban) }}</span>
          <Button
            icon="pi pi-copy"
            text
            rounded
            size="small"
            aria-label="Copiar IBAN"
            @click="copyToClipboard(iban, 'IBAN')"
          />
        </div>
      </div>
      <div>
        <span class="text-xs font-medium text-blue-600">Titular</span>
        <p class="text-sm text-gray-900">{{ accountHolder }}</p>
      </div>
      <div>
        <span class="text-xs font-medium text-blue-600">Banco</span>
        <p class="text-sm text-gray-900">{{ bankName }}</p>
      </div>
      <div v-if="amount != null">
        <span class="text-xs font-medium text-blue-600">Importe</span>
        <p class="text-sm font-semibold text-gray-900">{{ formatCurrency(amount) }}</p>
      </div>
      <div v-if="transferConcept" class="sm:col-span-2">
        <span class="text-xs font-medium text-blue-600">Concepto</span>
        <div class="flex items-center gap-2">
          <span class="font-mono text-sm font-semibold text-blue-700">{{ transferConcept }}</span>
          <Button
            icon="pi pi-copy"
            text
            rounded
            size="small"
            aria-label="Copiar concepto"
            @click="copyToClipboard(transferConcept, 'Concepto')"
          />
        </div>
      </div>
    </div>
  </div>
</template>

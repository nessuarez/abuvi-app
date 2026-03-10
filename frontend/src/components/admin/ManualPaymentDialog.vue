<script setup lang="ts">
import { ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import InputNumber from 'primevue/inputnumber'
import Textarea from 'primevue/textarea'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { usePayments } from '@/composables/usePayments'
import type { AdminPaymentResponse } from '@/types/payment'

const props = defineProps<{
  visible: boolean
  registrationId: string
  familyUnitName: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void
  (e: 'created', payment: AdminPaymentResponse): void
}>()

const { createManualPayment, loading, error } = usePayments()

const amount = ref<number | null>(null)
const description = ref('')
const adminNotes = ref('')

const resetForm = () => {
  amount.value = null
  description.value = ''
  adminNotes.value = ''
  error.value = null
}

watch(
  () => props.visible,
  (val) => {
    if (val) resetForm()
  }
)

const handleCreate = async () => {
  if (!amount.value || !description.value) return
  const result = await createManualPayment(props.registrationId, {
    amount: amount.value,
    description: description.value,
    adminNotes: adminNotes.value || null
  })
  if (result) {
    emit('update:visible', false)
    emit('created', result)
  }
}

const close = () => emit('update:visible', false)
</script>

<template>
  <Dialog
    :visible="visible"
    header="Crear pago manual"
    :modal="true"
    :style="{ width: '28rem' }"
    @update:visible="emit('update:visible', $event)"
  >
    <div class="space-y-4">
      <p class="text-sm text-gray-600">
        Crear un pago adicional para <strong>{{ familyUnitName }}</strong>.
      </p>

      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">
          Importe <span class="text-red-500">*</span>
        </label>
        <InputNumber
          v-model="amount"
          mode="currency"
          currency="EUR"
          locale="es-ES"
          :min="0.01"
          class="w-full"
          placeholder="0,00"
        />
      </div>

      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">
          Descripcion <span class="text-red-500">*</span>
        </label>
        <InputText
          v-model="description"
          class="w-full"
          placeholder="Ej: Cargo adicional por actividad especial"
          maxlength="500"
        />
      </div>

      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Notas (opcional)</label>
        <Textarea
          v-model="adminNotes"
          :rows="2"
          class="w-full"
          placeholder="Notas internas..."
          maxlength="2000"
        />
      </div>

      <Message v-if="error" severity="error" :closable="false" class="text-sm">
        {{ error }}
      </Message>
    </div>

    <template #footer>
      <Button label="Cancelar" severity="secondary" text @click="close" />
      <Button
        label="Crear pago"
        icon="pi pi-plus"
        :loading="loading"
        :disabled="!amount || !description"
        @click="handleCreate"
      />
    </template>
  </Dialog>
</template>

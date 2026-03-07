<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useToast } from 'primevue/usetoast'
import InputText from 'primevue/inputtext'
import InputNumber from 'primevue/inputnumber'
import Button from 'primevue/button'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import { usePayments } from '@/composables/usePayments'

const toast = useToast()
const { getPaymentSettings, updatePaymentSettings, loading, error } = usePayments()

const iban = ref('')
const bankName = ref('')
const accountHolder = ref('')
const secondInstallmentDaysBefore = ref(15)
const transferConceptPrefix = ref('CAMP')
const initialLoading = ref(true)
const saving = ref(false)

const ibanError = ref('')
const validate = (): boolean => {
  ibanError.value = ''
  const raw = iban.value.replace(/\s/g, '')
  if (!/^ES\d{22}$/.test(raw)) {
    ibanError.value = 'El IBAN debe tener el formato ES seguido de 22 dígitos.'
    return false
  }
  if (!bankName.value.trim()) return false
  if (!accountHolder.value.trim()) return false
  return true
}

const handleSave = async () => {
  if (!validate()) return
  saving.value = true
  const result = await updatePaymentSettings({
    iban: iban.value.replace(/\s/g, ''),
    bankName: bankName.value.trim(),
    accountHolder: accountHolder.value.trim(),
    secondInstallmentDaysBefore: secondInstallmentDaysBefore.value,
    transferConceptPrefix: transferConceptPrefix.value.trim().toUpperCase()
  })
  saving.value = false
  if (result) {
    toast.add({
      severity: 'success',
      summary: 'Configuración guardada',
      detail: 'Los datos de pago se han actualizado correctamente.',
      life: 4000
    })
  }
}

onMounted(async () => {
  const settings = await getPaymentSettings()
  if (settings) {
    iban.value = settings.iban
    bankName.value = settings.bankName
    accountHolder.value = settings.accountHolder
    secondInstallmentDaysBefore.value = settings.secondInstallmentDaysBefore
    transferConceptPrefix.value = settings.transferConceptPrefix
  }
  initialLoading.value = false
})
</script>

<template>
  <div class="mx-auto max-w-lg">
    <div v-if="initialLoading" class="flex justify-center py-8">
      <ProgressSpinner />
    </div>

    <div v-else class="space-y-4">
      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">IBAN</label>
        <InputText v-model="iban" class="w-full font-mono" placeholder="ES0000000000000000000000" />
        <p v-if="ibanError" class="mt-1 text-xs text-red-500">{{ ibanError }}</p>
      </div>

      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Nombre del banco</label>
        <InputText v-model="bankName" class="w-full" placeholder="Banco Example" />
      </div>

      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Titular de la cuenta</label>
        <InputText v-model="accountHolder" class="w-full" placeholder="Asociación ABUVI" />
      </div>

      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">
          Días de antelación para el 2º plazo
        </label>
        <InputNumber v-model="secondInstallmentDaysBefore" :min="1" :max="90" class="w-full" />
        <p class="mt-1 text-xs text-gray-500">
          El 2º plazo vencerá estos días antes del inicio del campamento.
        </p>
      </div>

      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">
          Prefijo del concepto de transferencia
        </label>
        <InputText
          v-model="transferConceptPrefix"
          class="w-full font-mono uppercase"
          placeholder="CAMP"
          :maxlength="20"
        />
        <p class="mt-1 text-xs text-gray-500">
          Solo letras mayúsculas, números y guiones. Ejemplo: CAMP
        </p>
      </div>

      <Message v-if="error" severity="error" :closable="false">
        {{ error }}
      </Message>

      <div class="flex justify-end pt-2">
        <Button
          label="Guardar configuración"
          icon="pi pi-save"
          :loading="saving || loading"
          :disabled="!iban || !bankName || !accountHolder"
          @click="handleSave"
        />
      </div>
    </div>
  </div>
</template>

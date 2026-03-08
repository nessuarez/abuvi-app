<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useToast } from 'primevue/usetoast'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import Button from 'primevue/button'
import Container from '@/components/ui/Container.vue'
import RegistrationStatusBadge from '@/components/registrations/RegistrationStatusBadge.vue'
import RegistrationPricingBreakdown from '@/components/registrations/RegistrationPricingBreakdown.vue'
import RegistrationCancelDialog from '@/components/registrations/RegistrationCancelDialog.vue'
import RegistrationDeleteDialog from '@/components/registrations/RegistrationDeleteDialog.vue'
import BankTransferInstructions from '@/components/payments/BankTransferInstructions.vue'
import PaymentInstallmentCard from '@/components/payments/PaymentInstallmentCard.vue'
import { useRegistrations } from '@/composables/useRegistrations'
import { usePayments } from '@/composables/usePayments'
import { useAuthStore } from '@/stores/auth'
import type { PaymentResponse, PaymentSettings } from '@/types/payment'

const route = useRoute()
const router = useRouter()
const toast = useToast()
const auth = useAuthStore()

const {
  registration,
  loading,
  error,
  getRegistrationById,
  cancelRegistration,
  deleteRegistration,
  getAccommodationPreferences
} = useRegistrations()
const { getRegistrationPayments, getPaymentSettings } = usePayments()
const showCancelDialog = ref(false)
const cancelling = ref(false)
const showDeleteDialog = ref(false)
const deleting = ref(false)
const installments = ref<PaymentResponse[]>([])
const paymentSettingsData = ref<PaymentSettings | null>(null)

import type { AccommodationPreferenceResponse } from '@/types/registration'
import type { AccommodationType } from '@/types/camp-edition'

const accommodationPrefs = ref<AccommodationPreferenceResponse[]>([])

const ACCOMMODATION_TYPE_LABELS: Record<AccommodationType, string> = {
  Lodge: 'Refugio',
  Caravan: 'Caravana',
  Tent: 'Tienda de campaña',
  Bungalow: 'Bungalow',
  Motorhome: 'Autocaravana'
}

const registrationId = computed(() => route.params.id as string)

const isRepresentative = computed(
  () => registration.value?.familyUnit.representativeUserId === auth.user?.id
)

const isAdminOrBoard = computed(() => auth.isAdmin || auth.isBoard)

const isDraft = computed(() => registration.value?.status === 'Draft')

const canCancel = computed(
  () =>
    registration.value?.status === 'Pending' ||
    registration.value?.status === 'Confirmed' ||
    registration.value?.status === 'Draft'
)

const canDelete = computed(() => {
  if (!registration.value) return false
  const status = registration.value.status
  if (isAdminOrBoard.value) return status !== 'Confirmed'
  return (status === 'Pending' || status === 'Draft') && isRepresentative.value
})

const formatDate = (dateStr: string): string =>
  new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'long', year: 'numeric' }).format(
    new Date(dateStr)
  )

const formatCurrency = (amount: number): string =>
  new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(amount)

const formatPaymentDate = (dateStr: string): string =>
  new Intl.DateTimeFormat('es-ES', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  }).format(new Date(dateStr))

const PAYMENT_METHOD_LABELS: Record<string, string> = {
  Card: 'Tarjeta',
  Transfer: 'Transferencia',
  Cash: 'Efectivo'
}

const PAYMENT_STATUS_LABELS: Record<string, string> = {
  Pending: 'Pendiente',
  Completed: 'Completado',
  Failed: 'Fallido',
  Refunded: 'Reembolsado'
}

const handleCancel = async () => {
  cancelling.value = true
  const success = await cancelRegistration(registrationId.value)
  cancelling.value = false
  showCancelDialog.value = false
  if (success) {
    toast.add({
      severity: 'info',
      summary: 'Inscripción cancelada',
      detail: 'Tu inscripción ha sido cancelada.',
      life: 4000
    })
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
  }
}

const handleDelete = async () => {
  deleting.value = true
  const success = await deleteRegistration(registrationId.value)
  deleting.value = false
  showDeleteDialog.value = false
  if (success) {
    toast.add({
      severity: 'success',
      summary: 'Registration deleted',
      detail: 'Your registration has been deleted. You can register again for this camp edition.',
      life: 4000
    })
    router.push('/registrations')
  } else {
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: error.value || 'Could not delete the registration.',
      life: 5000
    })
  }
}

const handleInstallmentUpdated = (updated: PaymentResponse) => {
  const index = installments.value.findIndex((p) => p.id === updated.id)
  if (index !== -1) installments.value[index] = updated
}

onMounted(async () => {
  await getRegistrationById(registrationId.value)
  const [prefs, paymentsResult, settingsResult] = await Promise.all([
    getAccommodationPreferences(registrationId.value),
    getRegistrationPayments(registrationId.value),
    getPaymentSettings()
  ])
  if (prefs) {
    accommodationPrefs.value = prefs.sort((a, b) => a.preferenceOrder - b.preferenceOrder)
  }
  installments.value = paymentsResult
  paymentSettingsData.value = settingsResult
})
</script>

<template>
  <Container>
    <div class="py-8">
      <!-- Loading -->
      <div v-if="loading" class="flex justify-center py-12">
        <ProgressSpinner />
      </div>

      <!-- Error -->
      <Message v-else-if="error && !registration" severity="error" :closable="false" class="mb-4">
        {{ error }}
      </Message>

      <!-- Content -->
      <template v-else-if="registration">
        <!-- Header -->
        <div class="mb-6 flex items-start gap-3">
          <Button icon="pi pi-arrow-left" severity="secondary" text @click="router.push({ name: 'registrations' })"
            aria-label="Volver a mis inscripciones" />
          <div class="flex-1">
            <div class="flex flex-wrap items-center gap-3">
              <h1 class="text-2xl font-bold text-gray-900">
                {{ registration.campEdition.campName }} {{ registration.campEdition.year }}
              </h1>
              <RegistrationStatusBadge :status="registration.status" />
            </div>
            <p class="mt-1 text-sm text-gray-500">
              {{ formatDate(registration.campEdition.startDate) }} —
              {{ formatDate(registration.campEdition.endDate) }}
            </p>
            <p v-if="registration.campEdition.location" class="mt-0.5 text-sm text-gray-400">
              <i class="pi pi-map-marker mr-1" />{{ registration.campEdition.location }}
            </p>
          </div>
        </div>

        <!-- Draft banner -->
        <Message v-if="isDraft" severity="warn" :closable="false" class="mb-6" data-testid="draft-banner">
          Esta inscripción fue modificada por un administrador. El representante familiar debe volver a confirmarla.
        </Message>

        <!-- Notes -->
        <div v-if="registration.notes" class="mb-6 rounded-lg border border-gray-200 bg-gray-50 p-4">
          <h2 class="mb-1 text-sm font-semibold text-gray-700">Notas</h2>
          <p class="text-sm text-gray-600">{{ registration.notes }}</p>
        </div>

        <!-- Preference fields -->
        <div v-if="registration.specialNeeds || registration.campatesPreference"
          class="mb-6 rounded-lg border border-gray-200 bg-gray-50 p-4">
          <h2 class="mb-3 text-sm font-semibold text-gray-700">Informacion adicional</h2>
          <dl class="space-y-2 text-sm">
            <div v-if="registration.specialNeeds" class="flex flex-col gap-0.5">
              <dt class="font-medium text-gray-600">Necesidades especiales</dt>
              <dd class="whitespace-pre-line text-gray-800">{{ registration.specialNeeds }}</dd>
            </div>
            <div v-if="registration.campatesPreference" class="flex flex-col gap-0.5">
              <dt class="font-medium text-gray-600">Preferencia de acampantes</dt>
              <dd class="text-gray-800">{{ registration.campatesPreference }}</dd>
            </div>
          </dl>
        </div>

        <!-- Accommodation preferences -->
        <div v-if="accommodationPrefs.length > 0" class="mb-6 rounded-lg border border-gray-200 bg-gray-50 p-4">
          <h2 class="mb-2 text-sm font-semibold text-gray-700">Preferencias de alojamiento</h2>
          <ol class="list-inside list-decimal space-y-1 text-sm text-gray-800">
            <li v-for="pref in accommodationPrefs" :key="pref.campEditionAccommodationId">
              {{ pref.accommodationName }}
              <span class="text-xs text-gray-500">
                · {{ ACCOMMODATION_TYPE_LABELS[pref.accommodationType] }}
              </span>
            </li>
          </ol>
        </div>

        <!-- Pricing breakdown -->
        <div class="mb-6">
          <h2 class="mb-3 text-base font-semibold text-gray-900">Desglose de precio</h2>
          <RegistrationPricingBreakdown :pricing="registration.pricing" />
        </div>

        <!-- Payments -->
        <div class="mb-6">
          <h2 class="mb-3 text-base font-semibold text-gray-900">Pagos</h2>

          <!-- Bank transfer instructions (collapsible) -->
          <div v-if="paymentSettingsData" class="mb-4">
            <BankTransferInstructions
              :iban="paymentSettingsData.iban"
              :bank-name="paymentSettingsData.bankName"
              :account-holder="paymentSettingsData.accountHolder"
              collapsible
            />
          </div>

          <!-- Installment cards -->
          <div v-if="installments.length > 0" class="space-y-4">
            <PaymentInstallmentCard
              v-for="payment in installments"
              :key="payment.id"
              :payment="payment"
              @updated="handleInstallmentUpdated"
            />
          </div>

          <div v-else
            class="rounded-lg border border-dashed border-gray-200 px-4 py-6 text-center text-sm text-gray-400">
            Sin pagos registrados
          </div>

          <div class="mt-3 flex justify-between rounded-lg bg-gray-50 px-4 py-3 text-sm">
            <span class="text-gray-600">Pagado</span>
            <span class="font-medium text-green-700">{{ formatCurrency(registration.amountPaid) }}</span>
          </div>
          <div class="mt-1 flex justify-between rounded-lg bg-gray-50 px-4 py-3 text-sm">
            <span class="text-gray-600">Pendiente de pago</span>
            <span class="font-medium" :class="registration.amountRemaining > 0 ? 'text-red-600' : 'text-gray-600'">
              {{ formatCurrency(registration.amountRemaining) }}
            </span>
          </div>
        </div>

        <!-- Actions -->
        <div v-if="(isRepresentative && canCancel) || canDelete" class="flex justify-end gap-2">
          <Button v-if="isRepresentative && canCancel" label="Cancelar inscripción" severity="danger" outlined
            icon="pi pi-times" @click="showCancelDialog = true" data-testid="cancel-registration-btn" />
          <Button v-if="canDelete" label="Delete registration" severity="danger" icon="pi pi-trash"
            @click="showDeleteDialog = true" data-testid="delete-registration-btn" />
        </div>

        <RegistrationCancelDialog v-model:visible="showCancelDialog" :registration-id="registrationId"
          :loading="cancelling" @confirm="handleCancel" />
        <RegistrationDeleteDialog v-model:visible="showDeleteDialog" :loading="deleting"
          @confirm="handleDelete" />
      </template>
    </div>
  </Container>
</template>

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
import { useRegistrations } from '@/composables/useRegistrations'
import { useAuthStore } from '@/stores/auth'

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
  getAccommodationPreferences
} = useRegistrations()
const showCancelDialog = ref(false)
const cancelling = ref(false)

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

const canCancel = computed(
  () => registration.value?.status === 'Pending' || registration.value?.status === 'Confirmed'
)

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

onMounted(async () => {
  await getRegistrationById(registrationId.value)
  const prefs = await getAccommodationPreferences(registrationId.value)
  if (prefs) {
    accommodationPrefs.value = prefs.sort((a, b) => a.preferenceOrder - b.preferenceOrder)
  }
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

          <div v-if="registration.payments.length > 0" class="space-y-2">
            <div v-for="payment in registration.payments" :key="payment.id"
              class="flex items-center justify-between rounded-lg border border-gray-200 bg-white px-4 py-3">
              <div>
                <span class="text-sm font-medium text-gray-900">
                  {{ PAYMENT_METHOD_LABELS[payment.method] ?? payment.method }}
                </span>
                <span class="ml-2 text-xs text-gray-400">{{ formatPaymentDate(payment.paymentDate) }}</span>
              </div>
              <div class="flex items-center gap-3">
                <span class="text-xs text-gray-500">{{ PAYMENT_STATUS_LABELS[payment.status] ?? payment.status }}</span>
                <span class="font-semibold text-gray-900">{{ formatCurrency(payment.amount) }}</span>
              </div>
            </div>
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

        <!-- Cancel action -->
        <div v-if="isRepresentative && canCancel" class="flex justify-end">
          <Button label="Cancelar inscripción" severity="danger" outlined icon="pi pi-times"
            @click="showCancelDialog = true" data-testid="cancel-registration-btn" />
        </div>

        <RegistrationCancelDialog v-model:visible="showCancelDialog" :registration-id="registrationId"
          :loading="cancelling" @confirm="handleCancel" />
      </template>
    </div>
  </Container>
</template>

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
import RegistrationMemberSelector from '@/components/registrations/RegistrationMemberSelector.vue'
import RegistrationExtrasSelector from '@/components/registrations/RegistrationExtrasSelector.vue'
import RegistrationCancelDialog from '@/components/registrations/RegistrationCancelDialog.vue'
import RegistrationDeleteDialog from '@/components/registrations/RegistrationDeleteDialog.vue'
import BankTransferInstructions from '@/components/payments/BankTransferInstructions.vue'
import PaymentInstallmentCard from '@/components/payments/PaymentInstallmentCard.vue'
import ManualPaymentDialog from '@/components/admin/ManualPaymentDialog.vue'
import { useRegistrations } from '@/composables/useRegistrations'
import { usePayments } from '@/composables/usePayments'
import { useFamilyUnits } from '@/composables/useFamilyUnits'
import { useCampEditions } from '@/composables/useCampEditions'
import { useAuthStore } from '@/stores/auth'
import { api } from '@/utils/api'
import type { PaymentResponse, PaymentSettings } from '@/types/payment'
import type {
  AccommodationPreferenceResponse,
  WizardMemberSelection,
  WizardExtrasSelection
} from '@/types/registration'
import type { AccommodationType, CampEdition, CampEditionExtra } from '@/types/camp-edition'
import type { FamilyMemberResponse } from '@/types/family-unit'
import type { ApiResponse } from '@/types/api'

const route = useRoute()
const router = useRouter()
const toast = useToast()
const auth = useAuthStore()

const {
  registration,
  loading,
  error,
  getRegistrationById,
  updateMembers,
  setExtras,
  cancelRegistration,
  deleteRegistration,
  getAccommodationPreferences
} = useRegistrations()
const { getRegistrationPayments, getPaymentSettings } = usePayments()
const { getFamilyMembers } = useFamilyUnits()
const { getEditionById } = useCampEditions()

const showCancelDialog = ref(false)
const cancelling = ref(false)
const showDeleteDialog = ref(false)
const deleting = ref(false)
const showManualPaymentDialog = ref(false)
const installments = ref<PaymentResponse[]>([])
const paymentSettingsData = ref<PaymentSettings | null>(null)
const accommodationPrefs = ref<AccommodationPreferenceResponse[]>([])

// Edit mode state
const isEditingMembers = ref(false)
const isEditingExtras = ref(false)
const savingMembers = ref(false)
const savingExtras = ref(false)
const loadingEditData = ref(false)

// Data for edit mode (loaded on demand)
const familyMembersData = ref<FamilyMemberResponse[]>([])
const campEditionData = ref<CampEdition | null>(null)
const campExtrasData = ref<CampEditionExtra[]>([])
const memberSelections = ref<WizardMemberSelection[]>([])
const extrasSelections = ref<WizardExtrasSelection[]>([])

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

const canEdit = computed(() => {
  if (!registration.value) return false
  const status = registration.value.status
  if (status !== 'Pending' && status !== 'Draft') return false
  if (!isRepresentative.value) return false
  return !installments.value.some((p) => p.proofFileUrl != null)
})

const canCancel = computed(
  () =>
    registration.value?.status === 'Pending' ||
    registration.value?.status === 'Confirmed' ||
    registration.value?.status === 'Draft'
)

const canDelete = computed(() => {
  if (!registration.value) return false
  const status = registration.value.status
  if (status !== 'Pending' && status !== 'Draft') return false
  return isRepresentative.value || isAdminOrBoard.value
})

const sortedInstallments = computed(() =>
  [...installments.value].sort((a, b) => a.installmentNumber - b.installmentNumber)
)

const isPaymentLocked = (payment: PaymentResponse, index: number): boolean => {
  // If backend provides isActionable, defer to it
  if (payment.isActionable !== undefined) return false
  // First payment is never locked
  if (index === 0) return false
  // Locked if any previous payment is not Completed
  const previous = sortedInstallments.value.slice(0, index)
  return previous.some((p) => p.status !== 'Completed')
}

const installmentLabel = (payment: PaymentResponse): string => {
  if (payment.isManual && payment.manualConceptLine) {
    return payment.manualConceptLine.description
  }
  switch (payment.installmentNumber) {
    case 1:
      return 'Primer pago'
    case 2:
      return 'Segundo pago'
    case 3:
      return 'Pago de extras'
    default:
      return `Plazo ${payment.installmentNumber}`
  }
}

const formatDate = (dateStr: string): string =>
  new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'long', year: 'numeric' }).format(
    new Date(dateStr)
  )

const formatCurrency = (amount: number): string =>
  new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(amount)

// --- Edit mode helpers ---

const loadEditData = async () => {
  if (!registration.value || familyMembersData.value.length > 0) return
  loadingEditData.value = true
  try {
    const familyUnitId = registration.value.familyUnit.id
    const campEditionId = registration.value.campEdition.id

    const [members, edition, extrasRes] = await Promise.all([
      getFamilyMembers(familyUnitId),
      getEditionById(campEditionId),
      api.get<ApiResponse<CampEditionExtra[]>>(`/camps/editions/${campEditionId}/extras`, {
        params: { activeOnly: true }
      })
    ])

    familyMembersData.value = members
    campEditionData.value = edition
    campExtrasData.value = extrasRes.data.success ? (extrasRes.data.data ?? []) : []
  } finally {
    loadingEditData.value = false
  }
}

const startEditingMembers = async () => {
  await loadEditData()
  if (!registration.value || !campEditionData.value) return
  memberSelections.value = registration.value.pricing.members.map((m) => ({
    memberId: m.familyMemberId,
    attendancePeriod: m.attendancePeriod,
    visitStartDate: m.visitStartDate ?? null,
    visitEndDate: m.visitEndDate ?? null,
    guardianName: m.guardianName ?? null,
    guardianDocumentNumber: m.guardianDocumentNumber ?? null
  }))
  isEditingMembers.value = true
}

const startEditingExtras = async () => {
  await loadEditData()
  if (!registration.value) return
  extrasSelections.value = registration.value.pricing.extras.map((e) => ({
    campEditionExtraId: e.campEditionExtraId,
    name: e.name,
    quantity: e.quantity,
    unitPrice: e.unitPrice,
    userInput: e.userInput
  }))
  isEditingExtras.value = true
}

const refreshInstallments = async () => {
  installments.value = await getRegistrationPayments(registrationId.value)
}

const handleSaveMembers = async () => {
  savingMembers.value = true
  const result = await updateMembers(registrationId.value, {
    members: memberSelections.value.map((s) => ({
      memberId: s.memberId,
      attendancePeriod: s.attendancePeriod,
      visitStartDate: s.visitStartDate,
      visitEndDate: s.visitEndDate,
      guardianName: s.guardianName,
      guardianDocumentNumber: s.guardianDocumentNumber
    }))
  })
  savingMembers.value = false
  if (result) {
    await refreshInstallments()
    isEditingMembers.value = false
    toast.add({
      severity: 'success',
      summary: 'Éxito',
      detail: 'Miembros actualizados',
      life: 3000
    })
  } else {
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: error.value ?? 'Error al actualizar miembros',
      life: 5000
    })
  }
}

const handleSaveExtras = async () => {
  savingExtras.value = true
  const result = await setExtras(registrationId.value, {
    extras: extrasSelections.value
      .filter((e) => e.quantity > 0)
      .map((e) => ({
        campEditionExtraId: e.campEditionExtraId,
        quantity: e.quantity,
        userInput: e.userInput
      }))
  })
  savingExtras.value = false
  if (result) {
    await refreshInstallments()
    isEditingExtras.value = false
    toast.add({
      severity: 'success',
      summary: 'Éxito',
      detail: 'Extras actualizados',
      life: 3000
    })
  } else {
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: error.value ?? 'Error al actualizar extras',
      life: 5000
    })
  }
}

// --- Cancel / Delete ---

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
      summary: 'Inscripción eliminada',
      detail: 'Tu inscripción ha sido eliminada. Puedes volver a inscribirte.',
      life: 4000
    })
    router.push('/registrations')
  } else {
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: error.value || 'No se pudo eliminar la inscripción.',
      life: 5000
    })
  }
}

const handleInstallmentUpdated = (updated: PaymentResponse) => {
  const index = installments.value.findIndex((p) => p.id === updated.id)
  if (index !== -1) installments.value[index] = updated
}

const handleManualPaymentCreated = async () => {
  await refreshInstallments()
  await getRegistrationById(registrationId.value)
  toast.add({
    severity: 'success',
    summary: 'Pago manual creado',
    detail: 'Se ha añadido un nuevo pago a la inscripción.',
    life: 3000
  })
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
          <Button
            icon="pi pi-arrow-left"
            severity="secondary"
            text
            @click="router.push({ name: 'registrations' })"
            aria-label="Volver a mis inscripciones"
          />
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
          Esta inscripción fue modificada por un administrador. El representante familiar debe
          volver a confirmarla.
        </Message>

        <!-- Draft info for representative -->
        <Message
          v-if="isDraft && isRepresentative"
          severity="info"
          :closable="false"
          class="mb-6"
          data-testid="draft-edit-hint"
        >
          Puedes revisar y editar los miembros o extras antes de confirmar.
        </Message>

        <!-- Notes -->
        <div v-if="registration.notes" class="mb-6 rounded-lg border border-gray-200 bg-gray-50 p-4">
          <h2 class="mb-1 text-sm font-semibold text-gray-700">Notas</h2>
          <p class="text-sm text-gray-600">{{ registration.notes }}</p>
        </div>

        <!-- Preference fields -->
        <div
          v-if="registration.specialNeeds || registration.campatesPreference"
          class="mb-6 rounded-lg border border-gray-200 bg-gray-50 p-4"
        >
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
          <div class="mb-3 flex items-center justify-between">
            <h2 class="text-base font-semibold text-gray-900">Desglose de precio</h2>
          </div>
          <RegistrationPricingBreakdown :pricing="registration.pricing" />
        </div>

        <!-- Edit members -->
        <div v-if="canEdit" class="mb-6">
          <template v-if="!isEditingMembers">
            <Button
              label="Editar miembros"
              icon="pi pi-users"
              severity="secondary"
              outlined
              :loading="loadingEditData"
              data-testid="edit-members-btn"
              @click="startEditingMembers"
            />
          </template>
          <template v-else>
            <div class="rounded-lg border border-blue-200 bg-blue-50/30 p-4">
              <h3 class="mb-3 text-sm font-semibold text-gray-900">Editar miembros</h3>
              <RegistrationMemberSelector
                v-if="campEditionData"
                v-model="memberSelections"
                :members="familyMembersData"
                :edition="campEditionData"
              />
              <div class="mt-4 flex gap-2">
                <Button
                  label="Guardar"
                  icon="pi pi-check"
                  :loading="savingMembers"
                  @click="handleSaveMembers"
                />
                <Button
                  label="Cancelar"
                  severity="secondary"
                  text
                  :disabled="savingMembers"
                  @click="isEditingMembers = false"
                />
              </div>
            </div>
          </template>
        </div>

        <!-- Edit extras -->
        <div v-if="canEdit" class="mb-6">
          <template v-if="!isEditingExtras">
            <Button
              label="Editar extras"
              icon="pi pi-box"
              severity="secondary"
              outlined
              :loading="loadingEditData"
              data-testid="edit-extras-btn"
              @click="startEditingExtras"
            />
          </template>
          <template v-else>
            <div class="rounded-lg border border-blue-200 bg-blue-50/30 p-4">
              <h3 class="mb-3 text-sm font-semibold text-gray-900">Editar extras</h3>
              <RegistrationExtrasSelector
                v-model="extrasSelections"
                :extras="campExtrasData"
              />
              <div class="mt-4 flex gap-2">
                <Button
                  label="Guardar"
                  icon="pi pi-check"
                  :loading="savingExtras"
                  @click="handleSaveExtras"
                />
                <Button
                  label="Cancelar"
                  severity="secondary"
                  text
                  :disabled="savingExtras"
                  @click="isEditingExtras = false"
                />
              </div>
            </div>
          </template>
        </div>

        <!-- Payments -->
        <div class="mb-6">
          <div class="mb-3 flex items-center justify-between">
            <h2 class="text-base font-semibold text-gray-900">Pagos</h2>
            <Button
              v-if="isAdminOrBoard"
              label="Añadir pago manual"
              icon="pi pi-plus"
              size="small"
              severity="secondary"
              outlined
              @click="showManualPaymentDialog = true"
            />
          </div>

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
          <div v-if="sortedInstallments.length > 0" class="space-y-4">
            <div v-for="(payment, idx) in sortedInstallments" :key="payment.id">
              <p
                v-if="payment.installmentNumber === 3 || payment.isManual"
                class="mb-1 text-xs font-medium"
                :class="payment.isManual ? 'text-purple-600' : 'text-purple-600'"
              >
                {{ installmentLabel(payment) }}
              </p>
              <PaymentInstallmentCard
                :payment="payment"
                :locked="isPaymentLocked(payment, idx)"
                @updated="handleInstallmentUpdated"
              />
            </div>
          </div>

          <div
            v-else
            class="rounded-lg border border-dashed border-gray-200 px-4 py-6 text-center text-sm text-gray-400"
          >
            Sin pagos registrados
          </div>

          <div class="mt-3 flex justify-between rounded-lg bg-gray-50 px-4 py-3 text-sm">
            <span class="text-gray-600">Pagado</span>
            <span class="font-medium text-green-700">{{ formatCurrency(registration.amountPaid) }}</span>
          </div>
          <div class="mt-1 flex justify-between rounded-lg bg-gray-50 px-4 py-3 text-sm">
            <span class="text-gray-600">Pendiente de pago</span>
            <span
              class="font-medium"
              :class="registration.amountRemaining > 0 ? 'text-red-600' : 'text-gray-600'"
            >
              {{ formatCurrency(registration.amountRemaining) }}
            </span>
          </div>
        </div>

        <!-- Actions -->
        <div v-if="(isRepresentative && canCancel) || canDelete" class="flex justify-end gap-2">
          <Button
            v-if="isRepresentative && canCancel"
            label="Cancelar inscripción"
            severity="danger"
            outlined
            icon="pi pi-times"
            data-testid="cancel-registration-btn"
            @click="showCancelDialog = true"
          />
          <Button
            v-if="canDelete"
            label="Eliminar inscripción"
            severity="danger"
            icon="pi pi-trash"
            data-testid="delete-registration-btn"
            @click="showDeleteDialog = true"
          />
        </div>

        <RegistrationCancelDialog
          v-model:visible="showCancelDialog"
          :registration-id="registrationId"
          :loading="cancelling"
          @confirm="handleCancel"
        />
        <RegistrationDeleteDialog
          v-model:visible="showDeleteDialog"
          :loading="deleting"
          @confirm="handleDelete"
        />

        <ManualPaymentDialog
          v-if="registration"
          v-model:visible="showManualPaymentDialog"
          :registration-id="registrationId"
          :family-unit-name="registration.familyUnit.name"
          @created="handleManualPaymentCreated"
        />
      </template>
    </div>
  </Container>
</template>

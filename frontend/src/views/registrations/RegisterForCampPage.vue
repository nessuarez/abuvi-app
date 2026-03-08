<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useToast } from 'primevue/usetoast'
import Stepper from 'primevue/stepper'
import StepList from 'primevue/steplist'
import Step from 'primevue/step'
import StepPanels from 'primevue/steppanels'
import StepPanel from 'primevue/steppanel'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import Textarea from 'primevue/textarea'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import Container from '@/components/ui/Container.vue'
import RegistrationMemberSelector from '@/components/registrations/RegistrationMemberSelector.vue'
import RegistrationExtrasSelector from '@/components/registrations/RegistrationExtrasSelector.vue'
import RegistrationAccommodationSelector from '@/components/registrations/RegistrationAccommodationSelector.vue'
import BankTransferInstructions from '@/components/payments/BankTransferInstructions.vue'
import PaymentInstallmentCard from '@/components/payments/PaymentInstallmentCard.vue'
import { useCampEditions } from '@/composables/useCampEditions'
import { useCampExtras } from '@/composables/useCampExtras'
import { useCampAccommodations } from '@/composables/useCampAccommodations'
import { useFamilyUnits } from '@/composables/useFamilyUnits'
import { useRegistrations } from '@/composables/useRegistrations'
import { usePayments } from '@/composables/usePayments'
import { useAuthStore } from '@/stores/auth'
import type { CampEdition } from '@/types/camp-edition'
import type { WizardMemberSelection, WizardExtrasSelection, WizardAccommodationPreference } from '@/types/registration'
import type { PaymentResponse, PaymentSettings } from '@/types/payment'
import { ATTENDANCE_PERIOD_LABELS, computePeriodDays } from '@/utils/registration'
import { parseDateSafe } from '@/utils/date'

const route = useRoute()
const router = useRouter()
const toast = useToast()
const auth = useAuthStore()

const editionId = computed(() => route.params.editionId as string)

const { getEditionById } = useCampEditions()
const { familyUnit, familyMembers, getCurrentUserFamilyUnit, getFamilyMembers } = useFamilyUnits()
const { extras: campExtras, fetchExtras } = useCampExtras(editionId.value)
const { accommodations: campAccommodations, fetchAccommodations } = useCampAccommodations(editionId.value)
const { createRegistration, setExtras, setAccommodationPreferences, loading, error } = useRegistrations()
const { getRegistrationPayments, getPaymentSettings } = usePayments()

const currentStep = ref(1)
const selectedMembers = ref<WizardMemberSelection[]>([])
const extrasSelections = ref<WizardExtrasSelection[]>([])
const accommodationPreferences = ref<WizardAccommodationPreference[]>([])
const notes = ref<string>('')
const specialNeeds = ref<string>('')
const campatesPreference = ref<string>('')
const edition = ref<CampEdition | null>(null)
const acceptTerms = ref(false)
const pageLoading = ref(true)
const createdRegistrationId = ref<string | null>(null)
const installments = ref<PaymentResponse[]>([])
const paymentSettings = ref<PaymentSettings | null>(null)

const isRepresentative = computed(
  () => !!familyUnit.value && familyUnit.value.representativeUserId === auth.user?.id
)

// FamilyMemberResponse objects for the selected wizard members
const selectedMemberDetails = computed(() =>
  familyMembers.value.filter((m) => selectedMembers.value.some((s) => s.memberId === m.id))
)

const hasExtrasSelected = computed(() => extrasSelections.value.some((e) => e.quantity > 0))

const hasActiveAccommodations = computed(() =>
  campAccommodations.value.some((a) => a.isActive)
)
const accommodationStepValue = 3
const confirmStepValue = computed(() => (hasActiveAccommodations.value ? 4 : 3))
const paymentStepValue = computed(() => confirmStepValue.value + 1)
const stepAfterExtras = computed(() => (hasActiveAccommodations.value ? accommodationStepValue : confirmStepValue.value))

const weekendVisitIsValid = computed(() =>
  selectedMembers.value
    .filter((s) => s.attendancePeriod === 'WeekendVisit')
    .every((s) => s.visitStartDate != null && s.visitEndDate != null)
)

const canProceedFromStep1 = computed(
  () => selectedMembers.value.length > 0 && isRepresentative.value && weekendVisitIsValid.value
)

const formatCurrency = (amount: number): string =>
  new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(amount)

const formatDate = (dateStr: string): string =>
  new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'long', year: 'numeric' }).format(
    parseDateSafe(dateStr)
  )

const handleConfirm = async () => {
  if (!familyUnit.value) return

  const created = await createRegistration({
    campEditionId: editionId.value,
    familyUnitId: familyUnit.value.id,
    members: selectedMembers.value.map((s) => ({
      memberId: s.memberId,
      attendancePeriod: s.attendancePeriod,
      visitStartDate: s.visitStartDate ?? null,
      visitEndDate: s.visitEndDate ?? null,
      guardianName: s.guardianName || null,
      guardianDocumentNumber: s.guardianDocumentNumber || null
    })),
    notes: notes.value || null,
    specialNeeds: specialNeeds.value || null,
    campatesPreference: campatesPreference.value || null
  })

  if (!created) {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
    return
  }

  if (hasExtrasSelected.value) {
    const extrasResult = await setExtras(created.id, {
      extras: extrasSelections.value
        .filter((e) => e.quantity > 0)
        .map((e) => ({ campEditionExtraId: e.campEditionExtraId, quantity: e.quantity, userInput: e.userInput || undefined }))
    })
    if (!extrasResult) {
      toast.add({
        severity: 'warn',
        summary: 'Inscripción creada',
        detail: 'No se pudieron guardar los extras. Puedes editarlos desde el detalle.',
        life: 6000
      })
      router.push({ name: 'registration-detail', params: { id: created.id } })
      return
    }
  }

  if (accommodationPreferences.value.length > 0) {
    const prefsResult = await setAccommodationPreferences(created.id, {
      preferences: accommodationPreferences.value.map((p) => ({
        campEditionAccommodationId: p.campEditionAccommodationId,
        preferenceOrder: p.preferenceOrder
      }))
    })
    if (!prefsResult) {
      toast.add({
        severity: 'warn',
        summary: 'Inscripción creada',
        detail: 'No se pudieron guardar las preferencias de alojamiento.',
        life: 6000
      })
      router.push({ name: 'registration-detail', params: { id: created.id } })
      return
    }
  }

  // Fetch payment data and advance to payment step
  createdRegistrationId.value = created.id
  const [paymentsResult, settingsResult] = await Promise.all([
    getRegistrationPayments(created.id),
    getPaymentSettings()
  ])
  installments.value = paymentsResult
  paymentSettings.value = settingsResult

  toast.add({
    severity: 'success',
    summary: '¡Inscripción realizada!',
    detail: 'Tu inscripción ha sido creada. A continuación, las instrucciones de pago.',
    life: 4000
  })
  currentStep.value = paymentStepValue.value
}

const handleInstallmentUpdated = (updated: PaymentResponse) => {
  const index = installments.value.findIndex((p) => p.id === updated.id)
  if (index !== -1) installments.value[index] = updated
}

onMounted(async () => {
  pageLoading.value = true
  edition.value = await getEditionById(editionId.value)

  if (!edition.value || edition.value.status !== 'Open') {
    toast.add({
      severity: 'warn',
      summary: 'No disponible',
      detail: 'Esta edición no está abierta para inscripciones.',
      life: 4000
    })
    router.push({ name: 'camp' })
    return
  }

  await getCurrentUserFamilyUnit()
  if (familyUnit.value) {
    await getFamilyMembers(familyUnit.value.id)
  }
  await Promise.all([fetchExtras(), fetchAccommodations()])
  pageLoading.value = false
})
</script>

<template>
  <Container>
    <div class="py-8">
      <div v-if="pageLoading" class="flex justify-center py-12">
        <ProgressSpinner />
      </div>

      <template v-else>
        <div class="mb-6 flex items-center gap-3">
          <Button icon="pi pi-arrow-left" severity="secondary" text @click="router.push({ name: 'camp' })"
            aria-label="Volver" />
          <div>
            <h1 class="text-2xl font-bold text-gray-900">Nueva inscripción</h1>
            <p v-if="edition" class="text-sm text-gray-500">
              {{ edition.name ?? 'Campamento' }} {{ edition.year }} ·
              {{ formatDate(edition.startDate) }} — {{ formatDate(edition.endDate) }}
            </p>
          </div>
        </div>

        <!-- No family unit message -->
        <Message v-if="!familyUnit" severity="info" :closable="false" class="mb-6">
          En primer lugar, define tu unidad familiar para poder inscribirte.
          <RouterLink to="/family-unit" class="ml-1 font-semibold text-blue-600 underline">
            Crear unidad familiar
          </RouterLink>
        </Message>

        <!-- Non-representative warning -->
        <Message v-else-if="!isRepresentative" severity="warn" :closable="false" class="mb-6">
          Solo el representante de la unidad familiar puede inscribirse. Si quieres registrar a tu
          familia, contacta con el representante.
        </Message>

        <div class="mx-auto max-w-2xl">
          <Stepper v-model:value="currentStep" linear data-onboarding="registration-stepper">
            <StepList>
              <Step :value="1">Participantes</Step>
              <Step :value="2">Extras</Step>
              <Step v-if="hasActiveAccommodations" :value="3">Alojamiento</Step>
              <Step :value="confirmStepValue">Confirmar</Step>
              <Step v-if="createdRegistrationId" :value="paymentStepValue">Pago</Step>
            </StepList>

            <StepPanels>
              <!-- Step 1: Member selection -->
              <StepPanel :value="1">
                <div class="flex flex-col gap-6 py-4">
                  <div>
                    <h2 class="mb-1 text-base font-semibold text-gray-900">
                      Selecciona los participantes
                    </h2>
                    <p class="mb-4 text-sm text-gray-500">
                      Elige qué miembros de tu unidad familiar se inscriben al campamento.
                    </p>

                    <div v-if="familyMembers.length === 0" class="py-4 text-center text-sm text-gray-400">
                      No hay miembros en tu unidad familiar. Añádelos desde
                      <RouterLink to="/family-unit" class="text-blue-600 underline">Mi Unidad Familiar</RouterLink>.
                    </div>

                    <RegistrationMemberSelector v-else v-model="selectedMembers" :members="familyMembers"
                      :edition="edition!" />
                  </div>

                  <div class="flex justify-end">
                    <Button label="Siguiente" icon="pi pi-arrow-right" icon-pos="right" :disabled="!canProceedFromStep1"
                      @click="currentStep = 2" data-testid="next-step-btn" />
                  </div>
                </div>
              </StepPanel>

              <!-- Step 2: Extras -->
              <StepPanel :value="2">
                <div class="flex flex-col gap-6 py-4">
                  <div>
                    <h2 class="mb-1 text-base font-semibold text-gray-900">
                      Selecciona los extras (opcional)
                    </h2>
                    <p class="mb-4 text-sm text-gray-500">
                      Añade servicios o artículos adicionales para tu familia.
                    </p>

                    <RegistrationExtrasSelector v-model="extrasSelections" :extras="campExtras" />

                    <!-- Special needs -->
                    <div class="mb-5 mt-6">
                      <label class="mb-1 block text-sm font-medium text-gray-700">
                        Necesidades especiales
                      </label>
                      <Textarea v-model="specialNeeds" :rows="2" :maxlength="2000"
                        placeholder="Dietas especiales, necesidades de movilidad, etc." class="w-full"
                        data-testid="special-needs" />
                    </div>

                    <!-- Campmates preference -->
                    <div class="mb-5">
                      <label class="mb-1 block text-sm font-medium text-gray-700">
                        Preferencia de acampantes
                      </label>
                      <Textarea v-model="campatesPreference" :rows="2" :maxlength="500"
                        placeholder="Con quien te gustaria acampar cerca..." class="w-full"
                        data-testid="campates-preference" />
                    </div>
                  </div>

                  <div class="flex flex-col gap-2 sm:flex-row sm:justify-between">
                    <Button label="Atrás" icon="pi pi-arrow-left" severity="secondary" @click="currentStep = 1" />
                    <div class="flex gap-2">
                      <Button label="Saltar este paso" severity="secondary" text @click="currentStep = stepAfterExtras"
                        data-testid="skip-extras-btn" />
                      <Button label="Siguiente" icon="pi pi-arrow-right" icon-pos="right"
                        @click="currentStep = stepAfterExtras" data-testid="next-step-btn" />
                    </div>
                  </div>
                </div>
              </StepPanel>

              <!-- Step 3: Accommodation preferences (conditional) -->
              <StepPanel v-if="hasActiveAccommodations" :value="3">
                <div class="flex flex-col gap-6 py-4">
                  <div>
                    <h2 class="mb-1 text-base font-semibold text-gray-900">
                      Preferencia de alojamiento (opcional)
                    </h2>
                    <p class="mb-4 text-sm text-gray-500">
                      Selecciona hasta 3 opciones de alojamiento ordenadas por preferencia.
                    </p>

                    <RegistrationAccommodationSelector v-model="accommodationPreferences"
                      :accommodations="campAccommodations" />
                  </div>

                  <div class="flex flex-col gap-2 sm:flex-row sm:justify-between">
                    <Button label="Atrás" icon="pi pi-arrow-left" severity="secondary" @click="currentStep = 2" />
                    <div class="flex gap-2">
                      <Button label="Saltar este paso" severity="secondary" text @click="currentStep = confirmStepValue"
                        data-testid="skip-accommodation-btn" />
                      <Button label="Siguiente" icon="pi pi-arrow-right" icon-pos="right"
                        @click="currentStep = confirmStepValue" data-testid="next-step-btn" />
                    </div>
                  </div>
                </div>
              </StepPanel>

              <!-- Confirm step -->
              <StepPanel :value="confirmStepValue">
                <div class="flex flex-col gap-6 py-4">
                  <div>
                    <h2 class="mb-1 text-base font-semibold text-gray-900">
                      Revisa y confirma
                    </h2>
                    <p class="mb-4 text-sm text-gray-500">
                      Comprueba los datos antes de confirmar tu inscripción.
                    </p>

                    <!-- Selected members with period -->
                    <div class="mb-4 rounded-lg border border-gray-200 p-4">
                      <h3 class="mb-2 text-sm font-semibold text-gray-700">
                        Participantes seleccionados
                      </h3>
                      <ul class="space-y-1">
                        <li v-for="member in selectedMemberDetails" :key="member.id" class="text-sm text-gray-800">
                          {{ member.firstName }} {{ member.lastName }}
                          <span class="ml-1 text-xs text-gray-500">
                            ·
                            {{
                              ATTENDANCE_PERIOD_LABELS[
                              selectedMembers.find((s) => s.memberId === member.id)!
                                .attendancePeriod
                              ]
                            }}
                          </span>
                        </li>
                      </ul>
                    </div>

                    <!-- Extras summary -->
                    <div v-if="hasExtrasSelected" class="mb-4 rounded-lg border border-gray-200 p-4">
                      <h3 class="mb-2 text-sm font-semibold text-gray-700">Extras seleccionados</h3>
                      <ul class="space-y-1">
                        <li v-for="extra in extrasSelections.filter((e) => e.quantity > 0)"
                          :key="extra.campEditionExtraId" class="text-sm text-gray-800">
                          {{ extra.name }} × {{ extra.quantity }}
                          <p v-if="extra.userInput" class="mt-0.5 text-xs text-gray-500 italic">
                            {{ extra.userInput }}
                          </p>
                        </li>
                      </ul>
                    </div>

                    <!-- Accommodation preferences summary -->
                    <div v-if="accommodationPreferences.length > 0" class="mb-4 rounded-lg border border-gray-200 p-4">
                      <h3 class="mb-2 text-sm font-semibold text-gray-700">
                        Preferencias de alojamiento
                      </h3>
                      <ol class="list-inside list-decimal space-y-1">
                        <li v-for="pref in [...accommodationPreferences].sort(
                          (a, b) => a.preferenceOrder - b.preferenceOrder
                        )" :key="pref.campEditionAccommodationId" class="text-sm text-gray-800">
                          {{ pref.accommodationName }}
                        </li>
                      </ol>
                    </div>

                    <!-- Notes -->
                    <div class="mb-4">
                      <label class="mb-1 block text-sm font-medium text-gray-700">
                        Notas adicionales (opcional)
                      </label>
                      <Textarea v-model="notes" :rows="3" :maxlength="1000"
                        placeholder="Cualquier información adicional que quieras comunicar..." class="w-full"
                        data-testid="notes-textarea" />
                    </div>

                    <!-- Preference fields summary -->
                    <div v-if="specialNeeds || campatesPreference" class="mb-4 rounded-lg border border-gray-200 p-4">
                      <h3 class="mb-2 text-sm font-semibold text-gray-700">Informacion adicional</h3>
                      <dl class="space-y-1 text-sm">
                        <div v-if="specialNeeds" class="flex gap-2">
                          <dt class="text-gray-500">Necesidades:</dt>
                          <dd class="text-gray-800">{{ specialNeeds }}</dd>
                        </div>
                        <div v-if="campatesPreference" class="flex gap-2">
                          <dt class="text-gray-500">Acampantes:</dt>
                          <dd class="text-gray-800">{{ campatesPreference }}</dd>
                        </div>
                      </dl>
                    </div>

                    <!-- Legal notice -->
                    <div class="mb-4 rounded-lg border border-amber-200 bg-amber-50 p-4">
                      <h3 class="mb-2 text-sm font-semibold text-amber-800">
                        <i class="pi pi-exclamation-triangle mr-1"></i>
                        Aviso legal
                      </h3>
                      <p class="mb-2 text-sm text-amber-700">
                        Al confirmar esta inscripción, declaro que:
                      </p>
                      <ul class="mb-4 list-inside list-disc space-y-1 text-sm text-amber-700">
                        <li>He leído y acepto las normas del campamento.</li>
                        <li>
                          Autorizo el tratamiento de los datos personales según la
                          <a href="/legal/privacy" target="_blank" rel="noopener noreferrer"
                            class="font-medium text-amber-900 underline">política de privacidad</a>.
                        </li>
                        <li>
                          Acepto las condiciones de pago y cancelación establecidas en el
                          <a href="/legal/notice" target="_blank" rel="noopener noreferrer"
                            class="font-medium text-amber-900 underline">aviso legal</a>.
                        </li>
                      </ul>
                      <div class="flex items-start gap-2">
                        <Checkbox v-model="acceptTerms" :binary="true" input-id="accept-terms"
                          data-testid="accept-terms-checkbox" />
                        <label for="accept-terms" class="cursor-pointer text-sm text-amber-800">
                          He leído y acepto los términos y condiciones del campamento
                        </label>
                      </div>
                    </div>
                  </div>

                  <div class="flex flex-col gap-2 sm:flex-row sm:justify-between">
                    <Button label="Atrás" icon="pi pi-arrow-left" severity="secondary"
                      @click="currentStep = hasActiveAccommodations ? accommodationStepValue : 2" />
                    <Button label="Confirmar inscripción" icon="pi pi-check" :loading="loading"
                      :disabled="selectedMembers.length === 0 || !acceptTerms" @click="handleConfirm"
                      data-testid="confirm-registration-btn" />
                  </div>
                </div>
              </StepPanel>

              <!-- Step: Payment Instructions -->
              <StepPanel v-if="createdRegistrationId" :value="paymentStepValue">
                <div class="flex flex-col gap-6 py-4">
                  <div>
                    <h2 class="mb-1 text-base font-semibold text-gray-900">
                      Instrucciones de pago
                    </h2>
                    <p class="mb-4 text-sm text-gray-500">
                      Realiza una transferencia bancaria con los datos indicados y sube el justificante.
                    </p>
                  </div>

                  <BankTransferInstructions v-if="paymentSettings" :iban="paymentSettings.iban"
                    :bank-name="paymentSettings.bankName" :account-holder="paymentSettings.accountHolder" />

                  <div class="space-y-4">
                    <PaymentInstallmentCard v-for="payment in installments" :key="payment.id" :payment="payment"
                      @updated="handleInstallmentUpdated" />
                  </div>

                  <Message v-if="installments.length > 1 && installments[1].dueDate" severity="info" :closable="false">
                    El segundo plazo vence el {{ formatDate(installments[1].dueDate!) }}.
                    Puedes subir el justificante ahora o más tarde desde el detalle de tu inscripción.
                  </Message>

                  <div class="flex justify-end">
                    <Button label="Ir a mi inscripción" icon="pi pi-arrow-right" icon-pos="right"
                      @click="router.push({ name: 'registration-detail', params: { id: createdRegistrationId! } })"
                      data-testid="go-to-registration-btn" />
                  </div>
                </div>
              </StepPanel>

            </StepPanels>
          </Stepper>
        </div>
      </template>
    </div>
  </Container>
</template>

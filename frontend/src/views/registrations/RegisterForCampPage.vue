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
import Textarea from 'primevue/textarea'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import Container from '@/components/ui/Container.vue'
import RegistrationMemberSelector from '@/components/registrations/RegistrationMemberSelector.vue'
import RegistrationExtrasSelector from '@/components/registrations/RegistrationExtrasSelector.vue'
import { useCampEditions } from '@/composables/useCampEditions'
import { useCampExtras } from '@/composables/useCampExtras'
import { useFamilyUnits } from '@/composables/useFamilyUnits'
import { useRegistrations } from '@/composables/useRegistrations'
import { useAuthStore } from '@/stores/auth'
import type { CampEdition } from '@/types/camp-edition'
import type { WizardExtrasSelection } from '@/types/registration'

const route = useRoute()
const router = useRouter()
const toast = useToast()
const auth = useAuthStore()

const editionId = computed(() => route.params.editionId as string)

const { getEditionById } = useCampEditions()
const { familyUnit, familyMembers, getCurrentUserFamilyUnit, getFamilyMembers } = useFamilyUnits()
const { extras: campExtras, fetchExtras } = useCampExtras(editionId.value)
const { createRegistration, setExtras, loading, error } = useRegistrations()

const currentStep = ref(1)
const selectedMemberIds = ref<string[]>([])
const extrasSelections = ref<WizardExtrasSelection[]>([])
const notes = ref<string>('')
const edition = ref<CampEdition | null>(null)
const pageLoading = ref(true)

const isRepresentative = computed(
  () => !!familyUnit.value && familyUnit.value.representativeUserId === auth.user?.id
)

const selectedMembers = computed(() =>
  familyMembers.value.filter((m) => selectedMemberIds.value.includes(m.id))
)

const hasExtrasSelected = computed(() =>
  extrasSelections.value.some((e) => e.quantity > 0)
)

const formatCurrency = (amount: number): string =>
  new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(amount)

const formatDate = (dateStr: string): string =>
  new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'long', year: 'numeric' }).format(
    new Date(dateStr)
  )

const handleConfirm = async () => {
  if (!familyUnit.value) return

  const created = await createRegistration({
    campEditionId: editionId.value,
    familyUnitId: familyUnit.value.id,
    memberIds: selectedMemberIds.value,
    notes: notes.value || null
  })

  if (!created) {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
    return
  }

  if (hasExtrasSelected.value) {
    const extrasResult = await setExtras(created.id, {
      extras: extrasSelections.value
        .filter((e) => e.quantity > 0)
        .map((e) => ({ campEditionExtraId: e.campEditionExtraId, quantity: e.quantity }))
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

  toast.add({
    severity: 'success',
    summary: '¡Inscripción realizada!',
    detail: 'Tu inscripción ha sido creada correctamente.',
    life: 4000
  })
  router.push({ name: 'registration-detail', params: { id: created.id } })
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
  await fetchExtras()
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
          <Button
            icon="pi pi-arrow-left"
            severity="secondary"
            text
            @click="router.push({ name: 'camp' })"
            aria-label="Volver"
          />
          <div>
            <h1 class="text-2xl font-bold text-gray-900">Nueva inscripción</h1>
            <p v-if="edition" class="text-sm text-gray-500">
              {{ edition.name ?? 'Campamento' }} {{ edition.year }} ·
              {{ formatDate(edition.startDate) }} — {{ formatDate(edition.endDate) }}
            </p>
          </div>
        </div>

        <!-- Non-representative warning -->
        <Message
          v-if="!isRepresentative && familyUnit"
          severity="warn"
          :closable="false"
          class="mb-6"
        >
          Solo el representante de la unidad familiar puede inscribirse. Si quieres registrar a tu
          familia, contacta con el representante.
        </Message>

        <div class="mx-auto max-w-2xl">
          <Stepper v-model:value="currentStep" linear>
            <StepList>
              <Step :value="1">Participantes</Step>
              <Step :value="2">Extras</Step>
              <Step :value="3">Confirmar</Step>
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

                    <RegistrationMemberSelector
                      v-else
                      v-model="selectedMemberIds"
                      :members="familyMembers"
                    />
                  </div>

                  <div class="flex justify-end">
                    <Button
                      label="Siguiente"
                      icon="pi pi-arrow-right"
                      icon-pos="right"
                      :disabled="selectedMemberIds.length === 0 || !isRepresentative"
                      @click="currentStep = 2"
                      data-testid="next-step-btn"
                    />
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

                    <RegistrationExtrasSelector
                      v-model="extrasSelections"
                      :extras="campExtras"
                    />
                  </div>

                  <div class="flex flex-col gap-2 sm:flex-row sm:justify-between">
                    <Button
                      label="Atrás"
                      icon="pi pi-arrow-left"
                      severity="secondary"
                      @click="currentStep = 1"
                    />
                    <div class="flex gap-2">
                      <Button
                        label="Saltar este paso"
                        severity="secondary"
                        text
                        @click="currentStep = 3"
                        data-testid="skip-extras-btn"
                      />
                      <Button
                        label="Siguiente"
                        icon="pi pi-arrow-right"
                        icon-pos="right"
                        @click="currentStep = 3"
                        data-testid="next-step-btn"
                      />
                    </div>
                  </div>
                </div>
              </StepPanel>

              <!-- Step 3: Confirm -->
              <StepPanel :value="3">
                <div class="flex flex-col gap-6 py-4">
                  <div>
                    <h2 class="mb-1 text-base font-semibold text-gray-900">
                      Revisa y confirma
                    </h2>
                    <p class="mb-4 text-sm text-gray-500">
                      Comprueba los datos antes de confirmar tu inscripción.
                    </p>

                    <!-- Selected members -->
                    <div class="mb-4 rounded-lg border border-gray-200 p-4">
                      <h3 class="mb-2 text-sm font-semibold text-gray-700">Participantes seleccionados</h3>
                      <ul class="space-y-1">
                        <li
                          v-for="member in selectedMembers"
                          :key="member.id"
                          class="text-sm text-gray-800"
                        >
                          {{ member.firstName }} {{ member.lastName }}
                        </li>
                      </ul>
                    </div>

                    <!-- Price reference -->
                    <div v-if="edition" class="mb-4 rounded-lg border border-blue-100 bg-blue-50 p-4">
                      <h3 class="mb-2 text-sm font-semibold text-blue-800">Precios de referencia</h3>
                      <ul class="space-y-1 text-sm text-blue-700">
                        <li>Adulto: {{ formatCurrency(edition.pricePerAdult) }}</li>
                        <li>Niño/Niña: {{ formatCurrency(edition.pricePerChild) }}</li>
                        <li>Bebé: {{ formatCurrency(edition.pricePerBaby) }}</li>
                      </ul>
                      <p class="mt-2 text-xs text-blue-600">
                        El precio final se calculará al confirmar según las categorías de edad de cada persona.
                      </p>
                    </div>

                    <!-- Extras summary -->
                    <div v-if="hasExtrasSelected" class="mb-4 rounded-lg border border-gray-200 p-4">
                      <h3 class="mb-2 text-sm font-semibold text-gray-700">Extras seleccionados</h3>
                      <ul class="space-y-1">
                        <li
                          v-for="extra in extrasSelections.filter(e => e.quantity > 0)"
                          :key="extra.campEditionExtraId"
                          class="text-sm text-gray-800"
                        >
                          {{ extra.name }} × {{ extra.quantity }}
                        </li>
                      </ul>
                    </div>

                    <!-- Notes -->
                    <div class="mb-4">
                      <label class="mb-1 block text-sm font-medium text-gray-700">
                        Notas adicionales (opcional)
                      </label>
                      <Textarea
                        v-model="notes"
                        :rows="3"
                        :maxlength="1000"
                        placeholder="Cualquier información adicional que quieras comunicar..."
                        class="w-full"
                        data-testid="notes-textarea"
                      />
                    </div>
                  </div>

                  <div class="flex flex-col gap-2 sm:flex-row sm:justify-between">
                    <Button
                      label="Atrás"
                      icon="pi pi-arrow-left"
                      severity="secondary"
                      @click="currentStep = 2"
                    />
                    <Button
                      label="Confirmar inscripción"
                      icon="pi pi-check"
                      :loading="loading"
                      :disabled="selectedMemberIds.length === 0"
                      @click="handleConfirm"
                      data-testid="confirm-registration-btn"
                    />
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

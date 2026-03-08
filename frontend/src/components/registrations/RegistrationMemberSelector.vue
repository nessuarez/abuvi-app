<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import Checkbox from 'primevue/checkbox'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import DateInput from '@/components/shared/DateInput.vue'

import type { FamilyMemberResponse, FamilyRelationship } from '@/types/family-unit'
import { FamilyRelationshipLabels } from '@/types/family-unit'
import type { CampEdition } from '@/types/camp-edition'
import type { WizardMemberSelection, AttendancePeriod } from '@/types/registration'
import { ATTENDANCE_PERIOD_LABELS, getAllowedPeriods } from '@/utils/registration'
import { formatDateLocal, parseDateLocal } from '@/utils/date'

const props = defineProps<{
  members: FamilyMemberResponse[]
  modelValue: WizardMemberSelection[]
  edition: CampEdition
}>()

const emit = defineEmits<{
  'update:modelValue': [selections: WizardMemberSelection[]]
}>()

const globalPeriod = ref<AttendancePeriod>('Complete')
const globalVisitStartDate = ref<string | null>(null)
const globalVisitEndDate = ref<string | null>(null)
const hasDifferentPeriods = ref(false)

const allowedPeriods = computed(() => getAllowedPeriods(props.edition))

const showPeriodSelector = computed(() => allowedPeriods.value.length > 1)

const hasSelectedMembers = computed(() => props.modelValue.length > 0)

const periodOptions = computed(() =>
  allowedPeriods.value.map((p) => ({
    label: ATTENDANCE_PERIOD_LABELS[p],
    value: p
  }))
)

const weekendMinDate = computed(() =>
  props.edition.weekendStartDate ? parseDateLocal(props.edition.weekendStartDate) : undefined
)
const weekendMaxDate = computed(() =>
  props.edition.weekendEndDate ? parseDateLocal(props.edition.weekendEndDate) : undefined
)

const isSelected = (memberId: string): boolean =>
  props.modelValue.some((s) => s.memberId === memberId)

const getSelection = (memberId: string): WizardMemberSelection | undefined =>
  props.modelValue.find((s) => s.memberId === memberId)

const getMemberName = (memberId: string): string => {
  const m = props.members.find((fm) => fm.id === memberId)
  return m ? `${m.firstName} ${m.lastName}` : ''
}

const toggleMember = (memberId: string) => {
  if (isSelected(memberId)) {
    emit(
      'update:modelValue',
      props.modelValue.filter((s) => s.memberId !== memberId)
    )
  } else {
    const member = props.members.find((m) => m.id === memberId)
    const adult = firstSelectedAdult.value
    const isNewMemberAdult = member && !isMinor(member)

    const guardianName = member && isMinor(member) && adult
      ? `${adult.firstName} ${adult.lastName}`
      : null
    const guardianDocumentNumber = member && isMinor(member) && adult
      ? adult.documentNumber
      : null

    const useGlobalDates = globalPeriod.value === 'WeekendVisit' && !hasDifferentPeriods.value

    let updatedSelections: WizardMemberSelection[] = [
      ...props.modelValue,
      {
        memberId,
        attendancePeriod: globalPeriod.value,
        visitStartDate: useGlobalDates ? globalVisitStartDate.value : null,
        visitEndDate: useGlobalDates ? globalVisitEndDate.value : null,
        guardianName,
        guardianDocumentNumber
      }
    ]

    // Backfill empty guardian fields on existing minors when first adult is selected
    if (isNewMemberAdult && !adult) {
      const newAdultName = `${member!.firstName} ${member!.lastName}`
      const newAdultDoc = member!.documentNumber
      updatedSelections = updatedSelections.map((s) => {
        if (s.memberId === memberId) return s
        const m = props.members.find((fm) => fm.id === s.memberId)
        if (m && isMinor(m) && !s.guardianName && !s.guardianDocumentNumber) {
          return { ...s, guardianName: newAdultName, guardianDocumentNumber: newAdultDoc }
        }
        return s
      })
    }

    emit('update:modelValue', updatedSelections)
  }
}

const updateAllMembersPeriod = (period: AttendancePeriod) => {
  if (props.modelValue.length === 0) return
  emit(
    'update:modelValue',
    props.modelValue.map((s) => ({
      ...s,
      attendancePeriod: period,
      visitStartDate: null,
      visitEndDate: null
    }))
  )
}

const updateAllMembersVisitDate = (field: 'visitStartDate' | 'visitEndDate', dateStr: string | null) => {
  if (props.modelValue.length === 0) return
  emit(
    'update:modelValue',
    props.modelValue.map((s) =>
      s.attendancePeriod === 'WeekendVisit' ? { ...s, [field]: dateStr } : s
    )
  )
}

// Sync global period to all members when not in individual mode
watch(globalPeriod, (period) => {
  if (!hasDifferentPeriods.value) {
    globalVisitStartDate.value = null
    globalVisitEndDate.value = null
    updateAllMembersPeriod(period)
  }
})

// When toggling off individual mode, overwrite all members with global values
watch(hasDifferentPeriods, (isDifferent) => {
  if (!isDifferent && props.modelValue.length > 0) {
    const useGlobalDates = globalPeriod.value === 'WeekendVisit'
    emit(
      'update:modelValue',
      props.modelValue.map((s) => ({
        ...s,
        attendancePeriod: globalPeriod.value,
        visitStartDate: useGlobalDates ? globalVisitStartDate.value : null,
        visitEndDate: useGlobalDates ? globalVisitEndDate.value : null
      }))
    )
  }
})

const updateGlobalVisitDate = (field: 'visitStartDate' | 'visitEndDate', value: Date | null) => {
  const dateStr = value ? formatDateLocal(value) : null
  if (field === 'visitStartDate') {
    globalVisitStartDate.value = dateStr
  } else {
    globalVisitEndDate.value = dateStr
  }
  if (!hasDifferentPeriods.value) {
    updateAllMembersVisitDate(field, dateStr)
  }
}

const updatePeriod = (memberId: string, period: AttendancePeriod) => {
  emit(
    'update:modelValue',
    props.modelValue.map((s) =>
      s.memberId === memberId
        ? { ...s, attendancePeriod: period, visitStartDate: null, visitEndDate: null }
        : s
    )
  )
}

const updateVisitDate = (
  memberId: string,
  field: 'visitStartDate' | 'visitEndDate',
  value: Date | null
) => {
  const dateStr = value ? formatDateLocal(value) : null
  emit(
    'update:modelValue',
    props.modelValue.map((s) => (s.memberId === memberId ? { ...s, [field]: dateStr } : s))
  )
}

const formatDate = (dateStr: string): string =>
  new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'long', year: 'numeric' }).format(
    parseDateLocal(dateStr)
  )

const isMinor = (member: FamilyMemberResponse): boolean => {
  const dob = parseDateLocal(member.dateOfBirth)
  const today = new Date()
  let age = today.getFullYear() - dob.getFullYear()
  const monthDiff = today.getMonth() - dob.getMonth()
  if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < dob.getDate())) {
    age--
  }
  return age < 18
}

const updateGuardianField = (
  memberId: string,
  field: 'guardianName' | 'guardianDocumentNumber',
  value: string
) => {
  emit(
    'update:modelValue',
    props.modelValue.map((s) =>
      s.memberId === memberId ? { ...s, [field]: value || null } : s
    )
  )
}

const firstSelectedAdult = computed(() => {
  for (const member of props.members) {
    if (isSelected(member.id) && !isMinor(member)) {
      return member
    }
  }
  return null
})

const relationshipLabel = (rel: FamilyRelationship): string =>
  FamilyRelationshipLabels[rel] ?? rel
</script>

<template>
  <div>
    <!-- Member cards grid -->
    <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
      <div v-for="member in members" :key="member.id"
        class="flex cursor-pointer items-start gap-3 rounded-lg border border-gray-200 bg-white p-3 transition hover:border-blue-300 hover:bg-blue-50"
        :class="{ 'border-blue-400 bg-blue-50': isSelected(member.id) }" :data-testid="`member-label-${member.id}`"
        @click="toggleMember(member.id)">
        <Checkbox :model-value="isSelected(member.id)" :binary="true"
          :input-id="`member-${member.id}`" data-testid="member-checkbox" @click.stop />
        <div class="flex-1 min-w-0">
          <div class="flex items-center gap-2">
            <span class="font-medium text-gray-900">
              {{ member.firstName }} {{ member.lastName }}
            </span>
          </div>
          <p class="mt-0.5 text-xs text-gray-500">
            {{ relationshipLabel(member.relationship) }} · {{ formatDate(member.dateOfBirth) }}
          </p>

          <!-- Guardian info for minors -->
          <div v-if="isSelected(member.id) && isMinor(member)" class="mt-2 space-y-2 border-t border-gray-100 pt-2"
            @click.stop>
            <p class="text-xs font-medium text-gray-500">Datos del tutor/a legal</p>
            <InputText :model-value="getSelection(member.id)?.guardianName ?? ''"
              placeholder="Nombre completo del tutor/a" class="w-full text-sm" :maxlength="200"
              :data-testid="`guardian-name-${member.id}`"
              @update:model-value="(v: string) => updateGuardianField(member.id, 'guardianName', v)" />
            <InputText :model-value="getSelection(member.id)?.guardianDocumentNumber ?? ''"
              placeholder="DNI / Documento del tutor/a" class="w-full text-sm" :maxlength="50"
              :data-testid="`guardian-doc-${member.id}`"
              @update:model-value="(v: string) => updateGuardianField(member.id, 'guardianDocumentNumber', v)" />
          </div>
        </div>
      </div>
    </div>

    <!-- Global period selection section -->
    <div v-if="hasSelectedMembers && showPeriodSelector"
      class="mt-4 space-y-3 rounded-lg border border-gray-200 bg-white p-4" data-testid="global-period-section">
      <h3 class="text-sm font-semibold text-gray-700">Estancia</h3>

      <!-- Global period selector -->
      <Select v-model="globalPeriod" :options="periodOptions" option-label="label"
        option-value="value" placeholder="Periodo" class="w-full text-sm"
        data-testid="global-period-select" />

      <!-- Global WeekendVisit date pickers -->
      <template v-if="globalPeriod === 'WeekendVisit' && !hasDifferentPeriods">
        <div class="flex gap-2">
          <div class="flex-1">
            <label class="mb-1 block text-xs text-gray-500">Llegada</label>
            <DateInput :model-value="globalVisitStartDate ? parseDateLocal(globalVisitStartDate) : null"
              :min-date="weekendMinDate" :max-date="weekendMaxDate"
              data-testid="global-visit-start"
              @update:model-value="(d: Date | null) => updateGlobalVisitDate('visitStartDate', d)" />
          </div>
          <div class="flex-1">
            <label class="mb-1 block text-xs text-gray-500">Salida</label>
            <DateInput :model-value="globalVisitEndDate ? parseDateLocal(globalVisitEndDate) : null"
              :min-date="globalVisitStartDate ? parseDateLocal(globalVisitStartDate) : weekendMinDate"
              :max-date="weekendMaxDate"
              data-testid="global-visit-end"
              @update:model-value="(d: Date | null) => updateGlobalVisitDate('visitEndDate', d)" />
          </div>
        </div>
        <p v-if="edition.weekendStartDate && edition.weekendEndDate" class="text-xs text-orange-600">
          Maximo 3 dias. Dentro del periodo {{ formatDate(edition.weekendStartDate) }} —
          {{ formatDate(edition.weekendEndDate) }}
        </p>
      </template>

      <!-- Different periods checkbox -->
      <div class="flex items-center gap-2 pt-1">
        <Checkbox v-model="hasDifferentPeriods" :binary="true"
          input-id="different-periods" data-testid="different-periods-checkbox" />
        <label for="different-periods" class="cursor-pointer text-sm text-gray-600">
          No todos los asistentes tienen la misma estancia
        </label>
      </div>

      <!-- Individual period selectors -->
      <div v-if="hasDifferentPeriods" class="space-y-3 border-t border-gray-100 pt-3"
        data-testid="individual-periods-section">
        <div v-for="sel in modelValue" :key="sel.memberId" class="space-y-2">
          <div class="flex items-center gap-3">
            <span class="min-w-[120px] text-sm font-medium text-gray-700">{{ getMemberName(sel.memberId) }}</span>
            <Select :model-value="sel.attendancePeriod" :options="periodOptions"
              option-label="label" option-value="value" class="flex-1 text-sm"
              :data-testid="`period-select-${sel.memberId}`"
              @update:model-value="(p: AttendancePeriod) => updatePeriod(sel.memberId, p)" />
          </div>

          <!-- Individual WeekendVisit date pickers -->
          <template v-if="sel.attendancePeriod === 'WeekendVisit'">
            <div class="ml-[132px] flex gap-2">
              <div class="flex-1">
                <label class="mb-1 block text-xs text-gray-500">Llegada</label>
                <DateInput :model-value="sel.visitStartDate ? parseDateLocal(sel.visitStartDate) : null"
                  :min-date="weekendMinDate" :max-date="weekendMaxDate"
                  :data-testid="`visit-start-${sel.memberId}`"
                  @update:model-value="(d: Date | null) => updateVisitDate(sel.memberId, 'visitStartDate', d)" />
              </div>
              <div class="flex-1">
                <label class="mb-1 block text-xs text-gray-500">Salida</label>
                <DateInput :model-value="sel.visitEndDate ? parseDateLocal(sel.visitEndDate) : null"
                  :min-date="sel.visitStartDate ? parseDateLocal(sel.visitStartDate) : weekendMinDate"
                  :max-date="weekendMaxDate"
                  :data-testid="`visit-end-${sel.memberId}`"
                  @update:model-value="(d: Date | null) => updateVisitDate(sel.memberId, 'visitEndDate', d)" />
              </div>
            </div>
            <p v-if="edition.weekendStartDate && edition.weekendEndDate"
              class="ml-[132px] text-xs text-orange-600">
              Maximo 3 dias. Dentro del periodo {{ formatDate(edition.weekendStartDate) }} —
              {{ formatDate(edition.weekendEndDate) }}
            </p>
          </template>
        </div>
      </div>
    </div>
  </div>
</template>

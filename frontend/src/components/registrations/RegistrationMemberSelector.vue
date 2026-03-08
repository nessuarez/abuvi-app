<script setup lang="ts">
import { computed } from 'vue'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import DatePicker from 'primevue/datepicker'

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

const allowedPeriods = computed(() => getAllowedPeriods(props.edition))

const showPeriodSelector = computed(() => allowedPeriods.value.length > 1)

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

    let updatedSelections: WizardMemberSelection[] = [
      ...props.modelValue,
      {
        memberId,
        attendancePeriod: 'Complete',
        visitStartDate: null,
        visitEndDate: null,
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
  <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
    <div v-for="member in members" :key="member.id"
      class="flex cursor-pointer items-start gap-3 rounded-lg border border-gray-200 bg-white p-3 transition hover:border-blue-300 hover:bg-blue-50"
      :class="{ 'border-blue-400 bg-blue-50': isSelected(member.id) }" :data-testid="`member-label-${member.id}`"
      @click="toggleMember(member.id)">
      <div
        class="mt-0.5 flex h-5 w-5 flex-shrink-0 items-center justify-center rounded border-2 transition-colors"
        :class="isSelected(member.id) ? 'border-blue-500 bg-blue-500' : 'border-gray-300 bg-white'"
        data-testid="member-checkbox"
        aria-hidden="true"
      >
        <i v-if="isSelected(member.id)" class="pi pi-check text-[10px] text-white" />
      </div>
      <div class="flex-1 min-w-0">
        <div class="flex items-center gap-2">
          <span class="font-medium text-gray-900">
            {{ member.firstName }} {{ member.lastName }}
          </span>
        </div>
        <p class="mt-0.5 text-xs text-gray-500">
          {{ relationshipLabel(member.relationship) }} · {{ formatDate(member.dateOfBirth) }}
        </p>

        <!-- Period selector: only shown when member is selected and edition allows multiple periods -->
        <div v-if="isSelected(member.id) && showPeriodSelector" class="mt-2 space-y-2" @click.stop>
          <Select :model-value="getSelection(member.id)?.attendancePeriod" :options="periodOptions" option-label="label"
            option-value="value" placeholder="Periodo" class="w-full text-sm"
            :data-testid="`period-select-${member.id}`"
            @update:model-value="(p: AttendancePeriod) => updatePeriod(member.id, p)" />

          <!-- Weekend visit date pickers -->
          <template v-if="getSelection(member.id)?.attendancePeriod === 'WeekendVisit'">
            <div class="flex gap-2">
              <div class="flex-1">
                <label class="mb-1 block text-xs text-gray-500">Llegada</label>
                <DatePicker :model-value="getSelection(member.id)?.visitStartDate
                  ? parseDateLocal(getSelection(member.id)!.visitStartDate!)
                  : null
                  " :min-date="weekendMinDate" :max-date="weekendMaxDate" date-format="dd/mm/yy" show-icon
                  class="w-full text-sm" :data-testid="`visit-start-${member.id}`"
                  @update:model-value="(d: Date | null) => updateVisitDate(member.id, 'visitStartDate', d)" />
              </div>
              <div class="flex-1">
                <label class="mb-1 block text-xs text-gray-500">Salida</label>
                <DatePicker :model-value="getSelection(member.id)?.visitEndDate
                  ? parseDateLocal(getSelection(member.id)!.visitEndDate!)
                  : null
                  " :min-date="getSelection(member.id)?.visitStartDate
                    ? parseDateLocal(getSelection(member.id)!.visitStartDate!)
                    : weekendMinDate
                    " :max-date="weekendMaxDate" date-format="dd/mm/yy" show-icon class="w-full text-sm"
                  :data-testid="`visit-end-${member.id}`"
                  @update:model-value="(d: Date | null) => updateVisitDate(member.id, 'visitEndDate', d)" />
              </div>
            </div>
            <p v-if="edition.weekendStartDate && edition.weekendEndDate" class="text-xs text-orange-600">
              Máximo 3 días. Dentro del periodo {{ formatDate(edition.weekendStartDate) }} —
              {{ formatDate(edition.weekendEndDate) }}
            </p>
          </template>
        </div>

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
</template>

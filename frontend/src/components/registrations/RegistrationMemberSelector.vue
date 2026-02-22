<script setup lang="ts">
import { computed } from 'vue'
import Checkbox from 'primevue/checkbox'
import type { FamilyMemberResponse, FamilyRelationship } from '@/types/family-unit'
import { FamilyRelationshipLabels } from '@/types/family-unit'

const props = defineProps<{
  members: FamilyMemberResponse[]
  modelValue: string[]
}>()

const emit = defineEmits<{
  'update:modelValue': [ids: string[]]
}>()

const selectedIds = computed({
  get: () => props.modelValue,
  set: (ids: string[]) => emit('update:modelValue', ids)
})

const formatDate = (dateStr: string): string =>
  new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'long', year: 'numeric' }).format(
    new Date(dateStr)
  )

const relationshipLabel = (rel: FamilyRelationship): string =>
  FamilyRelationshipLabels[rel] ?? rel

const toggleMember = (id: string) => {
  const current = props.modelValue
  if (current.includes(id)) {
    emit('update:modelValue', current.filter((i) => i !== id))
  } else {
    emit('update:modelValue', [...current, id])
  }
}
</script>

<template>
  <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
    <label
      v-for="member in members"
      :key="member.id"
      class="flex cursor-pointer items-start gap-3 rounded-lg border border-gray-200 bg-white p-3 transition hover:border-blue-300 hover:bg-blue-50"
      :class="{ 'border-blue-400 bg-blue-50': modelValue.includes(member.id) }"
      :data-testid="`member-label-${member.id}`"
    >
      <Checkbox
        :model-value="modelValue.includes(member.id)"
        :binary="true"
        @update:model-value="toggleMember(member.id)"
        :input-id="`member-${member.id}`"
        data-testid="member-checkbox"
      />
      <div class="flex-1 min-w-0">
        <div class="flex items-center gap-2">
          <span class="font-medium text-gray-900">
            {{ member.firstName }} {{ member.lastName }}
          </span>
          <span
            v-if="member.hasMedicalNotes"
            class="inline-flex items-center text-amber-500"
            aria-label="Tiene notas médicas"
            title="Tiene notas médicas"
            data-testid="medical-notes-icon"
          >
            <i class="pi pi-exclamation-triangle text-xs" />
          </span>
          <span
            v-if="member.hasAllergies"
            class="inline-flex items-center text-orange-500"
            aria-label="Tiene alergias"
            title="Tiene alergias"
            data-testid="allergies-icon"
          >
            <i class="pi pi-exclamation-circle text-xs" />
          </span>
        </div>
        <p class="mt-0.5 text-xs text-gray-500">
          {{ relationshipLabel(member.relationship) }} · {{ formatDate(member.dateOfBirth) }}
        </p>
      </div>
    </label>
  </div>
</template>

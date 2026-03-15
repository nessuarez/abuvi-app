<script setup lang="ts">
import { computed } from 'vue'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import Message from 'primevue/message'
import ProfilePhotoAvatar from '@/components/family-units/ProfilePhotoAvatar.vue'
import { FamilyRelationshipLabels } from '@/types/family-unit'
import type { FamilyMemberResponse } from '@/types/family-unit'
import { parseDateLocal } from '@/utils/date'
import { getMemberDataWarnings, getWarningMessage } from '@/utils/member-validation'

const props = defineProps<{
  members: FamilyMemberResponse[]
  loading?: boolean
  canManageMemberships?: boolean
  readOnly?: boolean
  uploadingMemberId?: string | null
  isAdminOrBoard?: boolean
  representativeUserId?: string
}>()

const emit = defineEmits<{
  edit: [member: FamilyMemberResponse]
  delete: [member: FamilyMemberResponse]
  manageMembership: [member: FamilyMemberResponse]
  uploadPhoto: [memberId: string, file: File]
  removePhoto: [memberId: string]
}>()

const membersWithAge = computed(() => {
  return props.members.map((member) => {
    const age = calculateAge(member.dateOfBirth)
    const warnings = getMemberDataWarnings(member, age >= 18)
    return { ...member, age, warnings }
  })
})

const hasWarnings = computed(() =>
  !props.readOnly && membersWithAge.value.some((m) => m.warnings !== null)
)

const calculateAge = (dateOfBirth: string): number => {
  const birthDate = parseDateLocal(dateOfBirth)
  const today = new Date()
  let age = today.getFullYear() - birthDate.getFullYear()
  const monthDiff = today.getMonth() - birthDate.getMonth()

  if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < birthDate.getDate())) {
    age--
  }

  return age
}

const formatDate = (dateString: string): string => {
  const date = parseDateLocal(dateString)
  return new Intl.DateTimeFormat('es-ES', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric'
  }).format(date)
}

const getRelationshipLabel = (relationship: string): string => {
  return FamilyRelationshipLabels[relationship as keyof typeof FamilyRelationshipLabels] || relationship
}

const handleEdit = (member: FamilyMemberResponse) => {
  emit('edit', member)
}

const handleDelete = (member: FamilyMemberResponse) => {
  emit('delete', member)
}

const isRepresentative = (member: FamilyMemberResponse) => {
  return props.representativeUserId != null && member.userId === props.representativeUserId
}
</script>

<template>
  <div class="family-member-list">
    <DataTable
      :value="membersWithAge"
      :loading="loading"
      stripedRows
      responsiveLayout="scroll"
      :paginator="members.length > 10"
      :rows="10"
      class="p-datatable-sm"
    >
      <template #empty>
        <div class="text-center py-8 text-gray-500">
          No hay miembros familiares registrados
        </div>
      </template>

      <Column field="firstName" header="Nombre" :sortable="true">
        <template #body="{ data }">
          <div class="flex items-center gap-2">
            <ProfilePhotoAvatar
              :photo-url="data.profilePhotoUrl"
              :initials="(data.firstName?.[0] ?? '') + (data.lastName?.[0] ?? '')"
              size="sm"
              :editable="!props.readOnly"
              :loading="props.uploadingMemberId === data.id"
              @upload="(file: File) => emit('uploadPhoto', data.id, file)"
              @remove="() => emit('removePhoto', data.id)"
            />
            <div>
              <div class="font-medium">
                {{ data.firstName }} {{ data.lastName }}
                <i
                  v-if="data.warnings"
                  class="pi pi-exclamation-triangle text-orange-500 ml-1 text-xs"
                  v-tooltip.top="getWarningMessage(data.warnings)"
                  data-testid="member-warning-icon"
                />
              </div>
              <div v-if="data.userId" class="text-xs text-gray-500">
                <i class="pi pi-user text-xs"></i> Usuario vinculado
              </div>
            </div>
          </div>
        </template>
      </Column>

      <Column field="dateOfBirth" header="Fecha Nacimiento" :sortable="true">
        <template #body="{ data }">
          <div>{{ formatDate(data.dateOfBirth) }}</div>
          <div class="text-xs text-gray-500">{{ data.age }} años</div>
        </template>
      </Column>

      <Column field="relationship" header="Relación" :sortable="true">
        <template #body="{ data }">
          <Tag :value="getRelationshipLabel(data.relationship)" severity="info" />
        </template>
      </Column>

      <Column header="Contacto">
        <template #body="{ data }">
          <div class="text-sm space-y-1">
            <div v-if="data.email" class="flex items-center gap-1">
              <i class="pi pi-envelope text-xs text-gray-500"></i>
              <span>{{ data.email }}</span>
            </div>
            <div v-if="data.phone" class="flex items-center gap-1">
              <i class="pi pi-phone text-xs text-gray-500"></i>
              <span>{{ data.phone }}</span>
            </div>
            <div v-if="!data.email && !data.phone" class="text-gray-400 italic">
              Sin contacto
            </div>
          </div>
        </template>
      </Column>

      <Column header="Acciones" :exportable="false" class="text-right">
        <template #body="{ data }">
          <div class="flex justify-end gap-2">
            <Button
              v-if="props.canManageMemberships"
              icon="pi pi-id-card"
              severity="secondary"
              text
              rounded
              :data-testid="`manage-membership-btn-${data.id}`"
              v-tooltip.top="'Gestionar membresía'"
              @click="emit('manageMembership', data)"
            />
            <Button
              v-if="!props.readOnly"
              icon="pi pi-pencil"
              severity="info"
              text
              rounded
              @click="handleEdit(data)"
              v-tooltip.top="'Editar'"
            />
            <Button
              v-if="!props.readOnly || props.isAdminOrBoard"
              :disabled="isRepresentative(data)"
              icon="pi pi-trash"
              severity="danger"
              text
              rounded
              @click="handleDelete(data)"
              v-tooltip.top="isRepresentative(data) ? 'No se puede eliminar al representante' : 'Eliminar'"
            />
          </div>
        </template>
      </Column>
    </DataTable>
    <Message v-if="hasWarnings" severity="warn" :closable="false" class="mt-3" data-testid="member-warnings-banner">
      Algunos miembros adultos tienen datos incompletos (DNI, email o fecha de nacimiento).
      Estos datos son necesarios para la inscripción oficial en el campamento
      por motivos legales y de seguro. Asegúrate de que cada nombre, apellido,
      DNI y email sea correcto y único.
    </Message>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import Message from 'primevue/message'
import Drawer from 'primevue/drawer'
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
  anonymisePii: [member: FamilyMemberResponse]
  manageMembership: [member: FamilyMemberResponse]
  uploadPhoto: [memberId: string, file: File]
  removePhoto: [memberId: string]
}>()

const selectedMemberId = ref<string | null>(null)
const drawerVisible = ref(false)

const membersWithAge = computed(() =>
  props.members.map((member) => {
    const age = calculateAge(member.dateOfBirth)
    const warnings = getMemberDataWarnings(member, age >= 18)
    return { ...member, age, warnings }
  })
)

const selectedMember = computed(() =>
  membersWithAge.value.find((m) => m.id === selectedMemberId.value) ?? null
)

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
    year: 'numeric',
  }).format(date)
}

const getRelationshipLabel = (relationship: string): string =>
  FamilyRelationshipLabels[relationship as keyof typeof FamilyRelationshipLabels] || relationship

const openMemberDetail = (memberId: string) => {
  selectedMemberId.value = memberId
  drawerVisible.value = true
}

const isRepresentative = (member: FamilyMemberResponse) =>
  props.representativeUserId != null && member.userId === props.representativeUserId

const handleEdit = () => {
  if (!selectedMember.value) return
  drawerVisible.value = false
  emit('edit', selectedMember.value)
}

const handleDelete = () => {
  if (!selectedMember.value) return
  drawerVisible.value = false
  emit('delete', selectedMember.value)
}

const handleAnonymise = () => {
  if (!selectedMember.value) return
  drawerVisible.value = false
  emit('anonymisePii', selectedMember.value)
}

const handleManageMembership = () => {
  if (!selectedMember.value) return
  drawerVisible.value = false
  emit('manageMembership', selectedMember.value)
}
</script>

<template>
  <div class="family-member-list">
    <!-- Empty state -->
    <div v-if="!loading && members.length === 0" class="text-center py-8 text-gray-500">
      No hay miembros familiares registrados
    </div>

    <!-- Loading state -->
    <div v-else-if="loading" class="flex justify-center py-8">
      <i class="pi pi-spin pi-spinner text-2xl text-primary-500"></i>
    </div>

    <!-- Card list -->
    <div v-else class="flex flex-col gap-3">
      <button
        v-for="member in membersWithAge"
        :key="member.id"
        type="button"
        :data-testid="`member-card-${member.id}`"
        class="w-full text-left bg-white border border-gray-200 rounded-xl p-4 flex items-center gap-3 hover:bg-gray-50 active:bg-gray-100 transition-colors cursor-pointer shadow-sm"
        @click="openMemberDetail(member.id)"
      >
        <ProfilePhotoAvatar
          :photo-url="member.profilePhotoUrl"
          :initials="(member.firstName?.[0] ?? '') + (member.lastName?.[0] ?? '')"
          size="sm"
          :editable="false"
        />
        <div class="flex-1 min-w-0">
          <div class="font-medium truncate flex items-center gap-1">
            {{ member.firstName }} {{ member.lastName }}
            <i
              v-if="member.warnings"
              class="pi pi-exclamation-triangle text-orange-500 text-xs"
              data-testid="member-warning-icon"
            />
          </div>
          <div class="flex items-center gap-2 mt-0.5 flex-wrap">
            <Tag :value="getRelationshipLabel(member.relationship)" severity="info" />
            <span class="text-sm text-gray-500">{{ member.age }} años</span>
          </div>
          <div v-if="member.userId" class="text-xs text-gray-400 mt-0.5">
            <i class="pi pi-user text-xs"></i> Usuario vinculado
          </div>
        </div>
        <i class="pi pi-chevron-right text-gray-400 flex-shrink-0"></i>
      </button>
    </div>

    <!-- Warnings banner -->
    <Message
      v-if="hasWarnings"
      severity="warn"
      :closable="false"
      class="mt-3"
      data-testid="member-warnings-banner"
    >
      Algunos miembros adultos tienen datos incompletos (DNI, email o fecha de nacimiento).
      Estos datos son necesarios para la inscripción oficial en el campamento por motivos legales y
      de seguro. Asegúrate de que cada nombre, apellido, DNI y email sea correcto y único.
    </Message>

    <!-- Member detail drawer -->
    <Drawer v-model:visible="drawerVisible" position="right" class="!w-full sm:!w-96" appendTo="self">
      <template #header>
        <span class="font-semibold text-lg">Detalle del miembro</span>
      </template>

      <div v-if="selectedMember" class="flex flex-col gap-5 h-full">
        <!-- Avatar + name -->
        <div class="flex flex-col items-center gap-3 pb-4 border-b border-gray-200">
          <ProfilePhotoAvatar
            :photo-url="selectedMember.profilePhotoUrl"
            :initials="(selectedMember.firstName?.[0] ?? '') + (selectedMember.lastName?.[0] ?? '')"
            size="lg"
            :editable="!props.readOnly"
            :loading="props.uploadingMemberId === selectedMember.id"
            @upload="(file: File) => emit('uploadPhoto', selectedMember!.id, file)"
            @remove="() => emit('removePhoto', selectedMember!.id)"
          />
          <div class="text-center">
            <p class="text-xl font-semibold">
              {{ selectedMember.firstName }} {{ selectedMember.lastName }}
            </p>
            <Tag
              :value="getRelationshipLabel(selectedMember.relationship)"
              severity="info"
              class="mt-1"
            />
          </div>
        </div>

        <!-- Fields -->
        <div class="flex flex-col gap-4 text-sm">
          <div class="flex items-start gap-3">
            <i class="pi pi-calendar text-gray-400 mt-0.5"></i>
            <div>
              <p class="text-xs text-gray-400 uppercase font-medium tracking-wide">
                Fecha de nacimiento
              </p>
              <p>{{ formatDate(selectedMember.dateOfBirth) }} · {{ selectedMember.age }} años</p>
            </div>
          </div>

          <div v-if="selectedMember.documentNumber" class="flex items-start gap-3">
            <i class="pi pi-id-card text-gray-400 mt-0.5"></i>
            <div>
              <p class="text-xs text-gray-400 uppercase font-medium tracking-wide">Documento</p>
              <p>{{ selectedMember.documentNumber }}</p>
            </div>
          </div>

          <div v-if="selectedMember.email" class="flex items-start gap-3">
            <i class="pi pi-envelope text-gray-400 mt-0.5"></i>
            <div>
              <p class="text-xs text-gray-400 uppercase font-medium tracking-wide">Email</p>
              <p>{{ selectedMember.email }}</p>
            </div>
          </div>

          <div v-if="selectedMember.phone" class="flex items-start gap-3">
            <i class="pi pi-phone text-gray-400 mt-0.5"></i>
            <div>
              <p class="text-xs text-gray-400 uppercase font-medium tracking-wide">Teléfono</p>
              <p>{{ selectedMember.phone }}</p>
            </div>
          </div>

          <div v-if="selectedMember.userId" class="flex items-start gap-3">
            <i class="pi pi-user text-gray-400 mt-0.5"></i>
            <div>
              <p class="text-xs text-gray-400 uppercase font-medium tracking-wide">Cuenta</p>
              <p>Usuario vinculado</p>
            </div>
          </div>
        </div>

        <!-- Warning for incomplete data -->
        <Message
          v-if="selectedMember.warnings && !props.readOnly"
          severity="warn"
          :closable="false"
        >
          {{ getWarningMessage(selectedMember.warnings) }}
        </Message>

        <!-- Actions -->
        <div class="flex flex-col gap-2 pt-4 border-t border-gray-200 mt-auto">
          <Button
            v-if="props.canManageMemberships"
            icon="pi pi-id-card"
            label="Gestionar membresía"
            severity="secondary"
            outlined
            class="w-full justify-start"
            :data-testid="`manage-membership-btn-${selectedMember.id}`"
            @click="handleManageMembership"
          />
          <Button
            v-if="!props.readOnly"
            icon="pi pi-pencil"
            label="Editar"
            severity="info"
            class="w-full justify-start"
            @click="handleEdit"
          />
          <Button
            v-if="!props.readOnly || props.isAdminOrBoard"
            icon="pi pi-trash"
            label="Eliminar"
            severity="danger"
            outlined
            class="w-full justify-start"
            :disabled="isRepresentative(selectedMember)"
            v-tooltip.top="
              isRepresentative(selectedMember)
                ? 'No se puede eliminar al representante'
                : undefined
            "
            @click="handleDelete"
          />
          <Button
            v-if="props.isAdminOrBoard"
            icon="pi pi-eraser"
            label="Anonimizar datos (RGPD)"
            severity="warning"
            outlined
            class="w-full justify-start"
            :disabled="isRepresentative(selectedMember)"
            v-tooltip.top="
              isRepresentative(selectedMember)
                ? 'No se puede anonimizar al representante'
                : undefined
            "
            @click="handleAnonymise"
          />
        </div>
      </div>
    </Drawer>
  </div>
</template>

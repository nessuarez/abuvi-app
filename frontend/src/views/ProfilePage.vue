<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useToast } from 'primevue/usetoast'
import Card from 'primevue/card'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import PhoneInput from '@/components/shared/PhoneInput.vue'
import Tag from 'primevue/tag'
import Skeleton from 'primevue/skeleton'
import Container from '@/components/ui/Container.vue'
import ProfilePhotoAvatar from '@/components/family-units/ProfilePhotoAvatar.vue'
import PayFeeDialog from '@/components/memberships/PayFeeDialog.vue'
import MembershipDialog from '@/components/memberships/MembershipDialog.vue'
import BulkMembershipDialog from '@/components/memberships/BulkMembershipDialog.vue'
import { useAuthStore } from '@/stores/auth'
import { useProfile } from '@/composables/useProfile'
import { useFamilyUnits } from '@/composables/useFamilyUnits'
import { useMemberships } from '@/composables/useMemberships'
import { getRoleLabel } from '@/utils/user'
import { FamilyRelationshipLabels, type FamilyMemberResponse } from '@/types/family-unit'
import { parseDateSafe } from '@/utils/date'
import { FeeStatus, FeeStatusLabels, FeeStatusSeverity, type MembershipFeeResponse, type PayFeeRequest, type MemberMembershipData, type MembershipStatus } from '@/types/membership'

const auth = useAuthStore()
const router = useRouter()
const toast = useToast()

const { fullUser, loading: profileLoading, error: profileError, loadProfile, updateProfile } = useProfile()
const {
  familyUnit, familyMembers, getCurrentUserFamilyUnit, getFamilyMembers,
  uploadMemberProfilePhoto, removeMemberProfilePhoto,
  uploadUnitProfilePhoto, removeUnitProfilePhoto
} = useFamilyUnits()

// --- Edit profile state ---
const isEditing = ref(false)
const editForm = ref({ firstName: '', lastName: '', phone: '' })
const editErrors = ref({ firstName: '', lastName: '', phone: '' })
const submitting = ref(false)

// --- Members with membership/fee data ---
const memberData = ref<MemberMembershipData[]>([])
const memberDataLoading = ref(false)

// --- Pay fee dialog ---
const payFeeVisible = ref(false)
const selectedMemberData = ref<MemberMembershipData | null>(null)
const payFeeLoading = ref(false)

// --- Membership dialog ---
const showMembershipDialog = ref(false)
const selectedMemberForMembership = ref<MemberMembershipData | null>(null)
const showBulkMembershipDialog = ref(false)

// Helpers
const formatDate = (iso: string) =>
  new Intl.DateTimeFormat('es-ES', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(parseDateSafe(iso))

const calculateAge = (dateOfBirth: string): number => {
  const today = new Date()
  const birth = parseDateSafe(dateOfBirth)
  let age = today.getFullYear() - birth.getFullYear()
  const m = today.getMonth() - birth.getMonth()
  if (m < 0 || (m === 0 && today.getDate() < birth.getDate())) age--
  return age
}

const currentYear = new Date().getFullYear()

const getFeeForCurrentYear = (fees: MembershipFeeResponse[]): MembershipFeeResponse | null =>
  fees.find((f) => f.year === currentYear) ?? null

const getMembershipBadge = (data: MemberMembershipData): { label: string; severity: 'success' | 'warn' | 'danger' | 'secondary' } => {
  if (!data.membershipId) return { label: 'Sin alta de socio/a', severity: 'warn' }
  if (!data.isActiveMembership) return { label: 'Membresía inactiva', severity: 'danger' }
  return { label: 'Socio/a activo/a', severity: 'success' }
}

const getFeeBadge = (data: MemberMembershipData): { label: string; severity: 'success' | 'warn' | 'danger' | 'secondary' } | null => {
  if (!data.membershipId || !data.isActiveMembership) return null
  if (!data.currentFee) return { label: 'Sin cuota', severity: 'secondary' }
  return { label: FeeStatusLabels[data.currentFee.status], severity: FeeStatusSeverity[data.currentFee.status] }
}

// Load all member membership data in parallel
const loadMemberMembershipData = async () => {
  if (!familyUnit.value || familyMembers.value.length === 0) {
    memberData.value = []
    return
  }

  memberDataLoading.value = true

  const results = await Promise.all(
    familyMembers.value.map(async (member): Promise<MemberMembershipData> => {
      try {
        const { getMembership } = useMemberships()
        const membership = await getMembership(familyUnit.value!.id, member.id)
        const currentFee = membership ? getFeeForCurrentYear(membership.fees) : null
        const membershipStatus: MembershipStatus = !membership
          ? 'none'
          : !membership.isActive
            ? 'inactive'
            : currentFee?.status === FeeStatus.Paid
              ? 'active'
              : 'activeFeePending'
        return {
          member,
          membershipId: membership?.id ?? null,
          isActiveMembership: membership?.isActive ?? false,
          currentFee,
          feeLoading: false,
          membershipStatus,
        }
      } catch {
        return { member, membershipId: null, isActiveMembership: false, currentFee: null, feeLoading: false, membershipStatus: 'none' as const }
      }
    }),
  )

  memberData.value = results
  memberDataLoading.value = false
}

// Page initialisation
onMounted(async () => {
  await Promise.all([loadProfile(), getCurrentUserFamilyUnit()])
  if (familyUnit.value) {
    await getFamilyMembers(familyUnit.value.id)
    await loadMemberMembershipData()
  }
})

// Edit profile
const startEditing = () => {
  editForm.value = {
    firstName: auth.user?.firstName ?? '',
    lastName: auth.user?.lastName ?? '',
    phone: fullUser.value?.phone ?? '',
  }
  editErrors.value = { firstName: '', lastName: '', phone: '' }
  isEditing.value = true
}

const cancelEditing = () => {
  isEditing.value = false
}

const E164_PATTERN = /^\+[1-9]\d{6,14}$/

const validateEditForm = (): boolean => {
  editErrors.value = { firstName: '', lastName: '', phone: '' }
  let valid = true

  if (!editForm.value.firstName.trim()) {
    editErrors.value.firstName = 'El nombre es obligatorio'
    valid = false
  } else if (editForm.value.firstName.length > 100) {
    editErrors.value.firstName = 'El nombre no puede exceder 100 caracteres'
    valid = false
  }

  if (!editForm.value.lastName.trim()) {
    editErrors.value.lastName = 'Los apellidos son obligatorios'
    valid = false
  } else if (editForm.value.lastName.length > 100) {
    editErrors.value.lastName = 'Los apellidos no pueden exceder 100 caracteres'
    valid = false
  }

  const phone = editForm.value.phone.trim()
  if (phone && !E164_PATTERN.test(phone)) {
    editErrors.value.phone = 'El teléfono debe estar en formato internacional (ej. +34612345678)'
    valid = false
  }

  return valid
}

const handleSaveProfile = async () => {
  if (!validateEditForm()) return

  submitting.value = true
  const success = await updateProfile({
    firstName: editForm.value.firstName.trim(),
    lastName: editForm.value.lastName.trim(),
    phone: editForm.value.phone.trim() || null,
  })
  submitting.value = false

  if (success) {
    isEditing.value = false
    toast.add({ severity: 'success', summary: 'Éxito', detail: 'Perfil actualizado correctamente', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: profileError.value || 'Error al actualizar el perfil', life: 5000 })
  }
}

// Pay fee dialog
const openPayFeeDialog = (data: MemberMembershipData) => {
  selectedMemberData.value = data
  payFeeVisible.value = true
}

const handlePayFee = async (request: PayFeeRequest) => {
  if (!selectedMemberData.value?.membershipId || !selectedMemberData.value?.currentFee) return

  payFeeLoading.value = true
  const { payFee } = useMemberships()
  const result = await payFee(
    selectedMemberData.value.membershipId,
    selectedMemberData.value.currentFee.id,
    request,
  )
  payFeeLoading.value = false

  if (result) {
    payFeeVisible.value = false
    toast.add({ severity: 'success', summary: 'Éxito', detail: 'Pago registrado correctamente', life: 3000 })
    await loadMemberMembershipData()
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: 'Error al registrar el pago', life: 5000 })
  }
}

const openMembershipDialog = (data: MemberMembershipData) => {
  selectedMemberForMembership.value = data
  showMembershipDialog.value = true
}

const handleMembershipDialogClose = async () => {
  showMembershipDialog.value = false
  selectedMemberForMembership.value = null
  await loadMemberMembershipData()
}

const goToFamilyManagement = () => router.push('/family-unit/me')
const goToForgotPassword = () => router.push('/forgot-password')

const displayPhone = computed(() => fullUser.value?.phone ?? auth.user?.phone ?? null)

// --- Profile photo state ---
const uploadingUnitPhoto = ref(false)
const uploadingMemberPhotoId = ref<string | null>(null)

const isRepresentative = computed(() =>
  familyUnit.value?.representativeUserId === auth.user?.id
)

async function onUploadUnitPhoto(file: File) {
  if (!familyUnit.value) return
  uploadingUnitPhoto.value = true
  const result = await uploadUnitProfilePhoto(familyUnit.value.id, file)
  uploadingUnitPhoto.value = false
  if (result) {
    toast.add({ severity: 'success', summary: 'Foto actualizada', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error al subir la foto', life: 5000 })
  }
}

async function onRemoveUnitPhoto() {
  if (!familyUnit.value) return
  uploadingUnitPhoto.value = true
  const ok = await removeUnitProfilePhoto(familyUnit.value.id)
  uploadingUnitPhoto.value = false
  if (ok) {
    toast.add({ severity: 'success', summary: 'Foto eliminada', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error al eliminar la foto', life: 5000 })
  }
}

async function onUploadMemberPhoto(memberId: string, file: File) {
  if (!familyUnit.value) return
  uploadingMemberPhotoId.value = memberId
  const result = await uploadMemberProfilePhoto(familyUnit.value.id, memberId, file)
  uploadingMemberPhotoId.value = null
  if (result) {
    // Update memberData local state
    const idx = memberData.value.findIndex(d => d.member.id === memberId)
    if (idx !== -1) {
      memberData.value[idx] = { ...memberData.value[idx], member: result }
    }
    toast.add({ severity: 'success', summary: 'Foto actualizada', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error al subir la foto', life: 5000 })
  }
}

async function onRemoveMemberPhoto(memberId: string) {
  if (!familyUnit.value) return
  uploadingMemberPhotoId.value = memberId
  const ok = await removeMemberProfilePhoto(familyUnit.value.id, memberId)
  uploadingMemberPhotoId.value = null
  if (ok) {
    const idx = memberData.value.findIndex(d => d.member.id === memberId)
    if (idx !== -1) {
      memberData.value[idx] = {
        ...memberData.value[idx],
        member: { ...memberData.value[idx].member, profilePhotoUrl: null }
      }
    }
    toast.add({ severity: 'success', summary: 'Foto eliminada', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error al eliminar la foto', life: 5000 })
  }
}
</script>

<template>
  <Container>
    <div class="py-8 space-y-6 max-w-5xl">
      <h1 class="text-4xl font-bold text-gray-900">Mi Perfil</h1>

      <div class="grid grid-cols-1 gap-6 lg:grid-cols-2">
      <!-- Section 1: Personal Information -->
      <Card data-testid="personal-info-card">
        <template #content>
          <!-- Read mode -->
          <div v-if="!isEditing">
            <div class="flex items-start justify-between gap-4">
              <div class="flex items-center gap-4">
                <ProfilePhotoAvatar
                  :photo-url="familyUnit?.profilePhotoUrl ?? null"
                  :initials="(auth.user?.firstName?.[0] ?? '') + (auth.user?.lastName?.[0] ?? '')"
                  size="md"
                  :editable="isRepresentative"
                  :loading="uploadingUnitPhoto"
                  @upload="onUploadUnitPhoto"
                  @remove="onRemoveUnitPhoto"
                />
                <div>
                  <h2 class="text-xl font-semibold text-gray-900" data-testid="user-full-name">
                    {{ auth.fullName }}
                  </h2>
                  <p class="text-sm text-gray-500" data-testid="user-email">{{ auth.user?.email }}</p>
                </div>
              </div>
              <Button
                label="Editar perfil"
                icon="pi pi-pencil"
                outlined
                size="small"
                aria-label="Editar perfil"
                data-testid="edit-profile-btn"
                @click="startEditing"
              />
            </div>

            <div class="mt-4 grid grid-cols-1 gap-2 text-sm sm:grid-cols-2">
              <div class="flex items-center gap-2">
                <i class="pi pi-phone text-gray-400" aria-hidden="true" />
                <span v-if="profileLoading" class="text-gray-400">Cargando...</span>
                <span v-else-if="displayPhone" data-testid="user-phone">{{ displayPhone }}</span>
                <span v-else class="text-gray-400" data-testid="user-phone-empty">—</span>
              </div>
              <div class="flex items-center gap-2">
                <i class="pi pi-id-card text-gray-400" aria-hidden="true" />
                <span data-testid="user-role">Rol: <strong>{{ getRoleLabel(auth.user?.role ?? '') }}</strong></span>
              </div>
              <div v-if="fullUser" class="flex items-center gap-2">
                <i class="pi pi-calendar text-gray-400" aria-hidden="true" />
                <span>Miembro desde: <strong>{{ formatDate(fullUser.createdAt) }}</strong></span>
              </div>
            </div>
          </div>

          <!-- Edit mode -->
          <div v-else>
            <h2 class="mb-4 text-lg font-semibold text-gray-800">Editar perfil</h2>
            <form class="space-y-4" @submit.prevent="handleSaveProfile">
              <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div class="flex flex-col gap-1">
                  <label for="edit-firstName" class="text-sm font-medium">
                    Nombre <span class="text-red-500">*</span>
                  </label>
                  <InputText
                    id="edit-firstName"
                    v-model="editForm.firstName"
                    :invalid="!!editErrors.firstName"
                    :maxlength="100"
                    data-testid="edit-firstName"
                  />
                  <small v-if="editErrors.firstName" class="text-red-500">{{ editErrors.firstName }}</small>
                </div>

                <div class="flex flex-col gap-1">
                  <label for="edit-lastName" class="text-sm font-medium">
                    Apellidos <span class="text-red-500">*</span>
                  </label>
                  <InputText
                    id="edit-lastName"
                    v-model="editForm.lastName"
                    :invalid="!!editErrors.lastName"
                    :maxlength="100"
                    data-testid="edit-lastName"
                  />
                  <small v-if="editErrors.lastName" class="text-red-500">{{ editErrors.lastName }}</small>
                </div>
              </div>

              <div class="flex flex-col gap-1">
                <label for="edit-phone" class="text-sm font-medium">Teléfono</label>
                <PhoneInput
                  id="edit-phone"
                  v-model="editForm.phone"
                  :invalid="!!editErrors.phone"
                  data-testid="edit-phone"
                />
                <small v-if="editErrors.phone" class="text-red-500">{{ editErrors.phone }}</small>
              </div>

              <div class="flex justify-end gap-2">
                <Button
                  type="button"
                  label="Cancelar"
                  severity="secondary"
                  :disabled="submitting"
                  data-testid="cancel-edit-btn"
                  @click="cancelEditing"
                />
                <Button
                  type="submit"
                  label="Guardar cambios"
                  :loading="submitting"
                  :disabled="submitting"
                  data-testid="save-profile-btn"
                />
              </div>
            </form>
          </div>
        </template>
      </Card>

      <!-- Section 4: Account Security -->
      <Card data-testid="security-card">
        <template #title>
          <div class="flex items-center gap-2">
            <i class="pi pi-lock" aria-hidden="true" />
            <span>Seguridad</span>
          </div>
        </template>
        <template #content>
          <div class="space-y-3 text-sm">
            <div class="flex items-center gap-2">
              <i class="pi pi-envelope text-gray-400" aria-hidden="true" />
              <span class="text-gray-700">{{ auth.user?.email }}</span>
              <span class="text-xs text-gray-400">(no editable)</span>
            </div>
            <div>
              <Button
                label="Cambiar contraseña"
                icon="pi pi-key"
                text
                size="small"
                data-testid="change-password-btn"
                @click="goToForgotPassword"
              />
            </div>
          </div>
        </template>
      </Card>
      </div>

      <!-- Section 2 & 3: Family Unit & Members -->
      <Card data-testid="family-unit-card">
        <template #title>
          <div class="flex items-center justify-between gap-2">
            <div class="flex items-center gap-2">
              <i class="pi pi-users" aria-hidden="true" />
              <span>Mi Unidad Familiar</span>
            </div>
            <div class="flex gap-2">
              <Button
                v-if="auth.isBoard && familyUnit"
                label="Activar membresía familiar"
                icon="pi pi-users"
                severity="secondary"
                outlined
                size="small"
                data-testid="bulk-membership-btn"
                @click="showBulkMembershipDialog = true"
              />
              <Button
                v-if="familyUnit"
                label="Gestionar"
                icon="pi pi-arrow-right"
                icon-pos="right"
                outlined
                size="small"
                data-testid="manage-family-unit-btn"
                @click="goToFamilyManagement"
              />
            </div>
          </div>
        </template>

        <template #content>
          <div v-if="!familyUnit" class="space-y-3 py-2 text-center">
            <p class="text-sm text-gray-600">
              Aún no has creado tu unidad familiar. Crea una para poder inscribirte en campamentos.
            </p>
            <Button
              label="Crear Unidad Familiar"
              icon="pi pi-plus"
              data-testid="create-family-unit-btn"
              @click="goToFamilyManagement"
            />
          </div>

          <div v-else>
            <p class="mb-4 text-sm text-gray-500" data-testid="family-unit-name">
              {{ familyUnit.name }} · {{ familyMembers.length }} miembro{{ familyMembers.length !== 1 ? 's' : '' }}
            </p>

            <div v-if="memberDataLoading" class="space-y-3" data-testid="members-loading">
              <div v-for="i in 2" :key="i" class="flex items-center gap-3 rounded-lg border border-gray-100 p-3">
                <Skeleton shape="circle" size="2.5rem" />
                <div class="flex-1 space-y-2">
                  <Skeleton width="40%" height="1rem" />
                  <Skeleton width="60%" height="0.75rem" />
                </div>
              </div>
            </div>

            <div v-else class="space-y-3" data-testid="members-list">
              <div
                v-for="data in memberData"
                :key="data.member.id"
                class="flex flex-col gap-2 rounded-lg border border-gray-100 p-3 sm:flex-row sm:items-center sm:justify-between"
                :data-testid="`member-row-${data.member.id}`"
              >
                <div class="flex items-center gap-3">
                  <ProfilePhotoAvatar
                    :photo-url="data.member.profilePhotoUrl"
                    :initials="(data.member.firstName?.[0] ?? '') + (data.member.lastName?.[0] ?? '')"
                    size="sm"
                    :editable="isRepresentative"
                    :loading="uploadingMemberPhotoId === data.member.id"
                    @upload="(file: File) => onUploadMemberPhoto(data.member.id, file)"
                    @remove="() => onRemoveMemberPhoto(data.member.id)"
                  />
                  <div>
                    <p class="font-medium text-gray-900">
                      {{ data.member.firstName }} {{ data.member.lastName }}
                    </p>
                    <p class="text-xs text-gray-500">
                      {{ FamilyRelationshipLabels[data.member.relationship] }} · {{ calculateAge(data.member.dateOfBirth) }} años
                    </p>
                  </div>
                </div>

                <div class="flex flex-wrap items-center gap-2">
                  <Tag
                    :value="getMembershipBadge(data).label"
                    :severity="getMembershipBadge(data).severity"
                    :data-testid="`membership-badge-${data.member.id}`"
                  />

                  <span
                    v-if="getMembershipBadge(data).severity === 'warn'"
                    class="text-xs text-gray-500 italic"
                  >
                    La Junta actualizará la situación a medida que se comprueben las cuotas.
                  </span>

                  <template v-if="getFeeBadge(data)">
                    <span class="text-xs text-gray-400" aria-hidden="true">|</span>
                    <Tag
                      :value="`Cuota ${currentYear}: ${getFeeBadge(data)!.label}`"
                      :severity="getFeeBadge(data)!.severity"
                      :data-testid="`fee-badge-${data.member.id}`"
                    />
                    <span
                      v-if="data.currentFee?.status === FeeStatus.Paid && data.currentFee.paidDate"
                      class="text-xs text-gray-500"
                    >
                      {{ formatDate(data.currentFee.paidDate) }}
                    </span>
                    <span
                      v-else-if="data.currentFee && data.currentFee.status !== FeeStatus.Paid"
                      class="text-xs text-gray-500"
                    >
                      ({{ data.currentFee.amount.toFixed(2) }}€)
                    </span>
                  </template>

                  <Button
                    v-if="auth.isBoard"
                    label="Gestionar membresía"
                    icon="pi pi-id-card"
                    size="small"
                    severity="secondary"
                    outlined
                    :data-testid="`manage-membership-btn-${data.member.id}`"
                    @click="openMembershipDialog(data)"
                  />

                  <Button
                    v-if="auth.isBoard && data.membershipId && data.isActiveMembership && data.currentFee && data.currentFee.status !== FeeStatus.Paid"
                    label="Pagar cuota"
                    icon="pi pi-credit-card"
                    size="small"
                    severity="secondary"
                    outlined
                    :data-testid="`pay-fee-btn-${data.member.id}`"
                    @click="openPayFeeDialog(data)"
                  />
                </div>
              </div>
            </div>
          </div>
        </template>
      </Card>
    </div>
  </Container>

  <PayFeeDialog
    v-model:visible="payFeeVisible"
    :fee="selectedMemberData?.currentFee ?? null"
    :loading="payFeeLoading"
    @submit="handlePayFee"
  />

  <MembershipDialog
    v-if="selectedMemberForMembership"
    v-model:visible="showMembershipDialog"
    :family-unit-id="familyUnit?.id ?? ''"
    :member-id="selectedMemberForMembership.member.id"
    :member-name="`${selectedMemberForMembership.member.firstName} ${selectedMemberForMembership.member.lastName}`"
    @update:visible="(val) => { if (!val) handleMembershipDialogClose() }"
  />

  <BulkMembershipDialog
    v-if="showBulkMembershipDialog"
    v-model:visible="showBulkMembershipDialog"
    :family-unit-id="familyUnit?.id ?? ''"
    :members="familyMembers"
    :member-data="memberData"
    @done="loadMemberMembershipData"
  />
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useToast } from 'primevue/usetoast'
import Dialog from 'primevue/dialog'
import Button from 'primevue/button'
import InputNumber from 'primevue/inputnumber'
import Message from 'primevue/message'
import Tag from 'primevue/tag'
import { useMemberships } from '@/composables/useMemberships'
import type { BulkActivateMembershipResponse, MemberMembershipData, MembershipStatus } from '@/types/membership'
import { FeeStatus } from '@/types/membership'
import type { FamilyMemberResponse } from '@/types/family-unit'

const props = defineProps<{
  visible: boolean
  familyUnitId: string
  members: FamilyMemberResponse[]
  memberData: MemberMembershipData[]
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  done: []
}>()

const toast = useToast()

const currentYear = new Date().getFullYear()
const selectedYear = ref<number>(currentYear)
const result = ref<BulkActivateMembershipResponse | null>(null)

const internalMemberData = ref<MemberMembershipData[]>([])
const loadingMemberData = ref(false)

const { loading, error, bulkActivateMemberships, getMembership } = useMemberships()

watch(
  () => props.visible,
  async (val) => {
    if (!val) return
    // If parent already provides data, use it directly
    if (props.memberData.length > 0) {
      internalMemberData.value = props.memberData
      return
    }
    // Otherwise self-load membership data for each member
    loadingMemberData.value = true
    try {
      const results = await Promise.all(
        props.members.map(async (member): Promise<MemberMembershipData> => {
          const ms = await getMembership(props.familyUnitId, member.id)
          const currentFee = ms?.fees.find((f) => f.year === currentYear) ?? null
          const membershipStatus: MembershipStatus = !ms
            ? 'none'
            : !ms.isActive
              ? 'inactive'
              : currentFee?.status === FeeStatus.Paid
                ? 'active'
                : 'activeFeePending'
          return {
            member,
            membershipId: ms?.id ?? null,
            isActiveMembership: ms?.isActive ?? false,
            currentFee,
            feeLoading: false,
            membershipStatus,
          }
        }),
      )
      internalMemberData.value = results
    } finally {
      loadingMemberData.value = false
    }
  },
  { immediate: true },
)

const membersWithoutMembership = computed(() =>
  internalMemberData.value.filter((d) => !d.membershipId),
)

const membersAlreadyActive = computed(() =>
  internalMemberData.value.filter((d) => d.membershipId && d.isActiveMembership),
)

const handleBulkActivate = async () => {
  result.value = null
  const res = await bulkActivateMemberships(props.familyUnitId, { year: selectedYear.value })
  if (res) {
    result.value = res
    if (res.activated > 0) {
      toast.add({
        severity: 'success',
        summary: 'Éxito',
        detail: `${res.activated} membresía(s) activada(s)`,
        life: 3000,
      })
    }
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
  }
}

const handleClose = () => {
  if ((result.value?.activated ?? 0) > 0) {
    emit('done')
  }
  emit('update:visible', false)
  result.value = null
  selectedYear.value = currentYear
  internalMemberData.value = []
}
</script>

<template>
  <Dialog
    :visible="visible"
    header="Activar membresía familiar"
    :modal="true"
    :closable="true"
    :dismissable-mask="!loading"
    class="w-full max-w-xl"
    @update:visible="handleClose"
  >
    <!-- Loading state while fetching membership data -->
    <div v-if="loadingMemberData" class="flex items-center justify-center py-6 gap-2 text-gray-500">
      <i class="pi pi-spin pi-spinner" />
      <span>Cargando datos de membresía...</span>
    </div>

    <template v-else>
      <!-- Summary of current state (before activation) -->
      <div v-if="!result" class="mb-4 space-y-1 text-sm text-gray-600">
        <p>
          <strong>{{ membersWithoutMembership.length }}</strong>
          miembro(s) sin alta de socio/a.
        </p>
        <p v-if="membersAlreadyActive.length > 0">
          <strong>{{ membersAlreadyActive.length }}</strong>
          miembro(s) ya con membresía activa (se omitirán).
        </p>
      </div>

      <!-- Empty state: all members already have membership -->
      <Message
        v-if="membersWithoutMembership.length === 0 && !result"
        severity="info"
        class="mb-4"
      >
        Todos los miembros de esta familia ya tienen una membresía activa.
      </Message>

      <!-- Year picker (only shown before activation and when there are members to activate) -->
      <div
        v-if="!result && membersWithoutMembership.length > 0"
        class="mb-6 flex flex-col gap-2"
      >
        <label for="bulk-start-year" class="text-sm font-medium">
          Año de inicio <span class="text-red-500">*</span>
        </label>
        <InputNumber
          id="bulk-start-year"
          v-model="selectedYear"
          :min="2001"
          :max="currentYear"
          :use-grouping="false"
          class="w-full"
          data-testid="bulk-year-input"
        />
        <small class="text-gray-500">
          Año en que estos miembros se hacen socios. Se aplicará a quienes no tengan membresía.
        </small>
      </div>

      <!-- Result summary (after activation) -->
      <div v-if="result" class="mb-4 space-y-3">
        <Message :severity="result.activated > 0 ? 'success' : 'info'">
          {{ result.activated }} membresía(s) activada(s), {{ result.skipped }} omitida(s).
        </Message>
        <div class="space-y-2">
          <div
            v-for="r in result.results"
            :key="r.memberId"
            class="flex items-center justify-between rounded-lg border border-gray-100 px-3 py-2 text-sm"
          >
            <span>{{ r.memberName }}</span>
            <Tag
              :value="r.status === 'Activated' ? 'Activada' : r.status === 'Skipped' ? 'Omitida' : 'Error'"
              :severity="r.status === 'Activated' ? 'success' : r.status === 'Skipped' ? 'secondary' : 'danger'"
            />
          </div>
        </div>
      </div>

      <!-- Actions -->
      <div class="flex justify-end gap-2">
        <Button
          label="Cancelar"
          severity="secondary"
          :disabled="loading"
          data-testid="cancel-btn"
          @click="handleClose"
        />
        <Button
          v-if="!result && membersWithoutMembership.length > 0"
          :label="`Activar ${membersWithoutMembership.length} membresía(s)`"
          icon="pi pi-check"
          :loading="loading"
          data-testid="activate-btn"
          @click="handleBulkActivate"
        />
        <Button
          v-if="result"
          label="Cerrar"
          data-testid="close-btn"
          @click="handleClose"
        />
      </div>
    </template>
  </Dialog>
</template>

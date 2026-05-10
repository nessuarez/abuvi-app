<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useToast } from 'primevue/usetoast'
import Button from 'primevue/button'
import Message from 'primevue/message'
import MultiSelect from 'primevue/multiselect'
import { useAdminRegistrations } from '@/composables/useAdminRegistrations'
import { useRegistrationAccommodationTagging } from '@/composables/useRegistrationAccommodationTagging'
import type { FriendLinkResponse } from '@/types/registration'

const props = defineProps<{
  registrationId: string
  campEditionId: string
  initialFriendLinks: FriendLinkResponse[]
}>()

const emit = defineEmits<{
  (e: 'updated', links: FriendLinkResponse[]): void
}>()

const toast = useToast()
const { registrations: editionRegistrations, fetchAdminRegistrations } = useAdminRegistrations()
const { friendLinks, saving, saveError, updateFriendLinks } = useRegistrationAccommodationTagging()

const isEditing = ref(false)
const selectedLinkedIds = ref<string[]>([])
const loadingEditionRegs = ref(false)

const availableRegistrations = computed(() =>
  editionRegistrations.value
    .filter((r) => r.id !== props.registrationId)
    .map((r) => ({ id: r.id, familyName: r.familyUnit.name })),
)

onMounted(() => {
  friendLinks.value = [...props.initialFriendLinks]
})

watch(
  () => props.initialFriendLinks,
  (val) => {
    if (!isEditing.value) friendLinks.value = [...val]
  },
)

function formatDate(dateStr: string): string {
  return new Intl.DateTimeFormat('es-ES', {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  }).format(new Date(dateStr))
}

async function startEditing() {
  loadingEditionRegs.value = true
  await fetchAdminRegistrations(props.campEditionId, { pageSize: 200 })
  loadingEditionRegs.value = false
  selectedLinkedIds.value = friendLinks.value.map((l) => l.linkedRegistrationId)
  isEditing.value = true
}

function cancelEditing() {
  isEditing.value = false
  selectedLinkedIds.value = []
}

async function save() {
  if (selectedLinkedIds.value.includes(props.registrationId)) {
    toast.add({ severity: 'warn', summary: 'Error', detail: 'No puedes vincular la inscripción consigo misma', life: 4000 })
    return
  }
  if (selectedLinkedIds.value.length > 10) {
    toast.add({ severity: 'warn', summary: 'Límite', detail: 'Máximo 10 familias amigas', life: 3000 })
    return
  }
  const result = await updateFriendLinks(props.registrationId, selectedLinkedIds.value)
  if (result) {
    isEditing.value = false
    emit('updated', result.friendLinks)
    toast.add({ severity: 'success', summary: 'Guardado', detail: 'Familias amigas actualizadas', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: saveError.value ?? 'Error al guardar', life: 5000 })
  }
}
</script>

<template>
  <div class="rounded-lg border border-teal-100 bg-teal-50/30 p-4">
    <div class="mb-3 flex items-center justify-between">
      <h2 class="text-sm font-semibold text-teal-900">Familias amigas</h2>
      <Button
        v-if="!isEditing"
        icon="pi pi-pencil"
        label="Editar"
        size="small"
        severity="secondary"
        outlined
        :loading="loadingEditionRegs"
        data-testid="edit-friend-links-btn"
        @click="startEditing"
      />
    </div>

    <!-- Read view -->
    <template v-if="!isEditing">
      <ul v-if="friendLinks.length > 0" class="space-y-1">
        <li
          v-for="link in friendLinks"
          :key="link.linkedRegistrationId"
          class="flex items-center gap-2 text-sm text-gray-800"
        >
          <i class="pi pi-users text-teal-500" />
          <span>{{ link.linkedFamilyName }}</span>
          <span class="text-xs text-gray-400">· {{ formatDate(link.createdAt) }}</span>
        </li>
      </ul>
      <p v-else class="text-sm italic text-gray-400">Sin vínculos</p>
    </template>

    <!-- Edit view -->
    <template v-else>
      <MultiSelect
        v-model="selectedLinkedIds"
        :options="availableRegistrations"
        option-label="familyName"
        option-value="id"
        filter
        :max-selected-labels="3"
        placeholder="Buscar familia..."
        class="w-full"
        aria-label="Familias amigas"
        data-testid="friend-links-multiselect"
      />
      <p class="mt-1 text-xs text-gray-500">Máx. 10 familias</p>
      <p v-if="selectedLinkedIds.length >= 10" class="text-xs text-amber-600">
        Límite de 10 familias alcanzado
      </p>
      <Message v-if="saveError && !saving" severity="error" :closable="false" class="mt-2 text-sm">
        {{ saveError }}
      </Message>
      <div class="mt-3 flex gap-2">
        <Button
          label="Guardar"
          icon="pi pi-check"
          size="small"
          :loading="saving"
          data-testid="save-friend-links-btn"
          @click="save"
        />
        <Button
          label="Cancelar"
          severity="secondary"
          text
          size="small"
          :disabled="saving"
          @click="cancelEditing"
        />
      </div>
    </template>
  </div>
</template>

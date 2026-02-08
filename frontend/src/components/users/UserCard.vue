<script setup lang="ts">
import Card from 'primevue/card'
import Tag from 'primevue/tag'
import type { User } from '@/types/user'

interface Props {
  user: User
  selected?: boolean
}

defineProps<Props>()

const emit = defineEmits<{
  select: [user: User]
}>()

const getRoleSeverity = (role: string): 'success' | 'info' | 'warning' => {
  switch (role) {
    case 'Admin':
      return 'success'
    case 'Board':
      return 'info'
    case 'Member':
      return 'warning'
    default:
      return 'info'
  }
}

const handleCardClick = (user: User) => {
  emit('select', user)
}
</script>

<template>
  <Card
    class="cursor-pointer transition-shadow hover:shadow-md"
    :class="{
      'ring-2 ring-primary-500': selected
    }"
    @click="handleCardClick(user)"
  >
    <template #title>
      <div class="flex items-center justify-between">
        <span>{{ user.firstName }} {{ user.lastName }}</span>
        <Tag :value="user.role" :severity="getRoleSeverity(user.role)" />
      </div>
    </template>
    <template #content>
      <div class="space-y-2 text-sm">
        <div class="flex items-center gap-2">
          <i class="pi pi-envelope text-gray-500" />
          <span class="text-gray-700">{{ user.email }}</span>
        </div>
        <div v-if="user.phone" class="flex items-center gap-2">
          <i class="pi pi-phone text-gray-500" />
          <span class="text-gray-700">{{ user.phone }}</span>
        </div>
        <div class="flex items-center gap-2">
          <i
            class="pi text-gray-500"
            :class="user.isActive ? 'pi-check-circle' : 'pi-times-circle'"
          />
          <span class="text-gray-700">
            {{ user.isActive ? 'Active' : 'Inactive' }}
          </span>
        </div>
      </div>
    </template>
  </Card>
</template>

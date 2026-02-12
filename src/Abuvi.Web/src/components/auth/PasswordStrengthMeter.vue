<script setup lang="ts">
import { computed } from 'vue'
import ProgressBar from 'primevue/progressbar'

interface Props {
  password: string
}

const props = defineProps<Props>()

const passwordCriteria = computed(() => ({
  hasMinLength: props.password.length >= 8,
  hasUppercase: /[A-Z]/.test(props.password),
  hasLowercase: /[a-z]/.test(props.password),
  hasDigit: /\d/.test(props.password),
  hasSpecialChar: /[@$!%*?&#]/.test(props.password)
}))

const strengthScore = computed(() => {
  const criteria = passwordCriteria.value
  return Object.values(criteria).filter(Boolean).length
})

const strengthLabel = computed(() => {
  const score = strengthScore.value
  if (score <= 2) return 'Weak'
  if (score === 3) return 'Fair'
  if (score === 4) return 'Good'
  return 'Strong'
})

const strengthColor = computed(() => {
  const score = strengthScore.value
  if (score <= 2) return 'danger'
  if (score === 3) return 'warn'
  if (score === 4) return 'info'
  return 'success'
})

const strengthPercentage = computed(() => {
  return (strengthScore.value / 5) * 100
})

const missingCriteria = computed(() => {
  const criteria = passwordCriteria.value
  const missing: string[] = []

  if (!criteria.hasMinLength) missing.push('At least 8 characters')
  if (!criteria.hasUppercase) missing.push('One uppercase letter')
  if (!criteria.hasLowercase) missing.push('One lowercase letter')
  if (!criteria.hasDigit) missing.push('One digit')
  if (!criteria.hasSpecialChar) missing.push('One special character (@$!%*?&#)')

  return missing
})
</script>

<template>
  <div v-if="password" class="mt-2">
    <div class="mb-2 flex items-center justify-between">
      <span class="text-sm font-medium">Password Strength:</span>
      <span
        class="text-sm font-semibold"
        :class="{
          'text-red-600': strengthScore <= 2,
          'text-yellow-600': strengthScore === 3,
          'text-blue-600': strengthScore === 4,
          'text-green-600': strengthScore === 5
        }"
      >
        {{ strengthLabel }}
      </span>
    </div>

    <ProgressBar
      :value="strengthPercentage"
      :severity="strengthColor"
      :show-value="false"
      class="h-2"
    />

    <div v-if="missingCriteria.length > 0" class="mt-2">
      <p class="mb-1 text-xs text-gray-600">Missing requirements:</p>
      <ul class="list-disc space-y-0.5 pl-5 text-xs text-gray-600">
        <li v-for="criterion in missingCriteria" :key="criterion">
          {{ criterion }}
        </li>
      </ul>
    </div>
  </div>
</template>

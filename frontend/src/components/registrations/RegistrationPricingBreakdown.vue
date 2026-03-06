<script setup lang="ts">
import { computed } from 'vue'
import type { PricingBreakdown, AgeCategory } from '@/types/registration'
import { ATTENDANCE_PERIOD_LABELS } from '@/utils/registration'

const props = defineProps<{
  pricing: PricingBreakdown
}>()

const AGE_CATEGORY_LABELS: Record<AgeCategory, string> = {
  Baby: 'Bebé',
  Child: 'Niño/Niña',
  Adult: 'Adulto/Adulta'
}

const showPeriodColumn = computed(() =>
  props.pricing.members.some((m) => m.attendancePeriod && m.attendancePeriod !== 'Complete')
)

const paidExtras = computed(() => props.pricing.extras.filter((e) => e.totalAmount > 0))

const formatCurrency = (amount: number): string =>
  new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(amount)
</script>

<template>
  <div class="space-y-4">
    <!-- Members breakdown -->
    <div>
      <h3 class="mb-2 text-sm font-semibold text-gray-700">Participantes</h3>
      <div class="overflow-hidden rounded-lg border border-gray-200">
        <table class="w-full text-sm">
          <thead class="bg-gray-50">
            <tr>
              <th class="px-4 py-2 text-left font-medium text-gray-600">Nombre</th>
              <th class="px-4 py-2 text-left font-medium text-gray-600">Categoría</th>
              <th v-if="showPeriodColumn" class="px-4 py-2 text-left font-medium text-gray-600">
                Periodo
              </th>
              <th class="px-4 py-2 text-right font-medium text-gray-600">Importe</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="member in pricing.members"
              :key="member.familyMemberId"
              class="border-t border-gray-100"
            >
              <td class="px-4 py-2">
                <span class="text-gray-900">{{ member.fullName }}</span>
                <span
                  v-if="member.guardianName"
                  class="block text-xs text-gray-400"
                  :data-testid="`guardian-info-${member.familyMemberId}`"
                >
                  Tutor/a: {{ member.guardianName }}
                  <span v-if="member.guardianDocumentNumber"> · {{ member.guardianDocumentNumber }}</span>
                </span>
              </td>
              <td class="px-4 py-2 text-gray-600">
                {{ AGE_CATEGORY_LABELS[member.ageCategory] }}
                <span class="text-xs text-gray-400">({{ member.ageAtCamp }} años)</span>
              </td>
              <td
                v-if="showPeriodColumn"
                class="px-4 py-2 text-gray-500 text-sm"
                :data-testid="`member-period-${member.familyMemberId}`"
              >
                {{ ATTENDANCE_PERIOD_LABELS[member.attendancePeriod] }}
                <span class="text-xs text-gray-400">({{ member.attendanceDays }}d)</span>
                <span
                  v-if="member.attendancePeriod === 'WeekendVisit' && member.visitStartDate"
                  class="block text-xs text-gray-400"
                >
                  {{ member.visitStartDate }} — {{ member.visitEndDate }}
                </span>
              </td>
              <td class="px-4 py-2 text-right text-gray-900">
                {{ formatCurrency(member.individualAmount) }}
              </td>
            </tr>
            <tr class="border-t border-gray-200 bg-gray-50">
              <td
                :colspan="showPeriodColumn ? 3 : 2"
                class="px-4 py-2 font-medium text-gray-700"
              >
                Subtotal participantes
              </td>
              <td class="px-4 py-2 text-right font-medium text-gray-900">
                {{ formatCurrency(pricing.baseTotalAmount) }}
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Extras breakdown -->
    <div v-if="paidExtras.length > 0">
      <h3 class="mb-2 text-sm font-semibold text-gray-700">Extras</h3>
      <div class="overflow-hidden rounded-lg border border-gray-200">
        <table class="w-full text-sm">
          <thead class="bg-gray-50">
            <tr>
              <th class="px-4 py-2 text-left font-medium text-gray-600">Concepto</th>
              <th class="px-4 py-2 text-left font-medium text-gray-600">Cálculo</th>
              <th class="px-4 py-2 text-right font-medium text-gray-600">Importe</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="extra in paidExtras"
              :key="extra.campEditionExtraId"
              class="border-t border-gray-100"
            >
              <td class="px-4 py-2 text-gray-900">
                {{ extra.name }}
                <p v-if="extra.userInput" class="mt-0.5 text-xs text-gray-500 italic">{{ extra.userInput }}</p>
              </td>
              <td class="px-4 py-2 text-gray-500 text-xs">{{ extra.calculation }}</td>
              <td class="px-4 py-2 text-right text-gray-900">
                {{ formatCurrency(extra.totalAmount) }}
              </td>
            </tr>
            <tr class="border-t border-gray-200 bg-gray-50">
              <td colspan="2" class="px-4 py-2 font-medium text-gray-700">Subtotal extras</td>
              <td class="px-4 py-2 text-right font-medium text-gray-900">
                {{ formatCurrency(pricing.extrasAmount) }}
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- No extras placeholder -->
    <div
      v-else
      class="rounded-lg border border-dashed border-gray-200 px-4 py-3 text-sm text-gray-400"
    >
      Sin extras seleccionados — importe de extras: {{ formatCurrency(0) }}
    </div>

    <!-- Total -->
    <div class="flex items-center justify-between rounded-lg bg-gray-900 px-4 py-3 text-white">
      <span class="text-base font-semibold">Total inscripción</span>
      <span class="text-xl font-bold">{{ formatCurrency(pricing.totalAmount) }}</span>
    </div>
  </div>
</template>

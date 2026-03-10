<script setup lang="ts">
import { ref, computed } from 'vue'
import type { PaymentConceptLine, PaymentExtraConceptLine, ManualPaymentConceptLine } from '@/types/payment'

const props = defineProps<{
  conceptLines: PaymentConceptLine[] | null
  extraConceptLines: PaymentExtraConceptLine[] | null
  manualConceptLine?: ManualPaymentConceptLine | null
}>()

const expanded = ref(false)

const hasContent = computed(
  () =>
    (props.conceptLines && props.conceptLines.length > 0) ||
    (props.extraConceptLines && props.extraConceptLines.length > 0) ||
    !!props.manualConceptLine
)

const memberTotal = computed(() =>
  props.conceptLines?.reduce((sum, l) => sum + l.amountInPayment, 0) ?? 0
)

const extrasTotal = computed(() =>
  props.extraConceptLines?.reduce((sum, l) => sum + l.totalAmount, 0) ?? 0
)

const formatCurrency = (amount: number): string =>
  new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(amount)

const pricingTypeLabel = (type: string): string =>
  type === 'PerFamily' ? 'por familia' : 'por persona'
</script>

<template>
  <div v-if="hasContent" class="border-t border-gray-100 pt-2">
    <button
      type="button"
      class="flex w-full items-center gap-1 text-xs font-medium text-blue-600 hover:text-blue-800"
      @click="expanded = !expanded"
    >
      <i :class="expanded ? 'pi pi-chevron-down' : 'pi pi-chevron-right'" class="text-[0.6rem]" />
      Detalle del pago
    </button>

    <div v-if="expanded" class="mt-2 space-y-3">
      <!-- Member lines (P1/P2) -->
      <div v-if="conceptLines && conceptLines.length > 0">
        <div class="divide-y divide-gray-50">
          <div
            v-for="(line, idx) in conceptLines"
            :key="idx"
            class="flex flex-col gap-0.5 py-1.5 text-xs sm:flex-row sm:items-center sm:justify-between"
          >
            <div class="flex items-center gap-2">
              <span class="font-medium text-gray-900">{{ line.personFullName }}</span>
              <span class="text-gray-400">{{ line.ageCategory }} · {{ line.attendancePeriod }}</span>
            </div>
            <div class="text-gray-600">
              <span>{{ formatCurrency(line.individualAmount) }}</span>
              <span class="text-gray-400"> ({{ line.percentage }}%)</span>
              <span class="ml-1 font-medium text-gray-900">→ {{ formatCurrency(line.amountInPayment) }}</span>
            </div>
          </div>
        </div>
        <div class="mt-1 flex justify-end border-t border-gray-100 pt-1 text-xs font-semibold text-gray-900">
          Total: {{ formatCurrency(memberTotal) }}
        </div>
      </div>

      <!-- Extra lines (P3) -->
      <div v-if="extraConceptLines && extraConceptLines.length > 0">
        <div class="divide-y divide-gray-50">
          <div
            v-for="(line, idx) in extraConceptLines"
            :key="idx"
            class="py-1.5 text-xs"
          >
            <div class="flex flex-col gap-0.5 sm:flex-row sm:items-center sm:justify-between">
              <div class="flex items-center gap-2">
                <span class="font-medium text-gray-900">{{ line.extraName }}</span>
                <span class="text-gray-400">({{ pricingTypeLabel(line.pricingType) }})</span>
              </div>
              <div class="text-gray-600">
                <span>x{{ line.quantity }} @ {{ formatCurrency(line.unitPrice) }}</span>
                <span class="ml-1 font-medium text-gray-900">= {{ formatCurrency(line.totalAmount) }}</span>
              </div>
            </div>
            <p v-if="line.userInput" class="mt-0.5 text-gray-400 sm:pl-4">{{ line.userInput }}</p>
          </div>
        </div>
        <div class="mt-1 flex justify-end border-t border-gray-100 pt-1 text-xs font-semibold text-gray-900">
          Total: {{ formatCurrency(extrasTotal) }}
        </div>
      </div>

      <!-- Manual concept line -->
      <div v-if="manualConceptLine" class="py-1.5 text-xs">
        <div class="flex flex-col gap-0.5 sm:flex-row sm:items-center sm:justify-between">
          <div class="flex items-center gap-2">
            <span class="font-medium text-gray-900">{{ manualConceptLine.description }}</span>
            <span class="inline-flex items-center rounded-full bg-purple-100 px-1.5 py-0.5 text-[0.6rem] font-medium text-purple-700">
              Manual
            </span>
          </div>
          <div class="font-medium text-gray-900">
            {{ formatCurrency(manualConceptLine.amount) }}
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

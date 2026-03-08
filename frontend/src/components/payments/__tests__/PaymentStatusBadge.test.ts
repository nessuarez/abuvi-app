import { describe, it, expect } from 'vitest'
import { shallowMount } from '@vue/test-utils'
import PaymentStatusBadge from '../PaymentStatusBadge.vue'
import type { PaymentStatus } from '@/types/registration'

const mount = (status: PaymentStatus) =>
  shallowMount(PaymentStatusBadge, { props: { status } })

describe('PaymentStatusBadge', () => {
  it.each([
    ['Pending', 'Pendiente', 'bg-yellow-100'],
    ['PendingReview', 'En revisión', 'bg-blue-100'],
    ['Completed', 'Completado', 'bg-green-100'],
    ['Failed', 'Fallido', 'bg-red-100'],
    ['Refunded', 'Reembolsado', 'bg-gray-100']
  ] as [PaymentStatus, string, string][])(
    'renders "%s" status with label "%s" and class "%s"',
    (status, label, colorClass) => {
      const wrapper = mount(status)
      expect(wrapper.text()).toBe(label)
      expect(wrapper.find('[data-testid="payment-status"]').classes()).toContain(colorClass)
    }
  )
})

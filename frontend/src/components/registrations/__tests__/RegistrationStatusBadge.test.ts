import { describe, it, expect } from 'vitest'
import { shallowMount } from '@vue/test-utils'
import RegistrationStatusBadge from '../RegistrationStatusBadge.vue'
import type { RegistrationStatus } from '@/types/registration'

const mount = (status: RegistrationStatus) =>
  shallowMount(RegistrationStatusBadge, { props: { status } })

describe('RegistrationStatusBadge', () => {
  it.each([
    ['Pending',       'Pendiente',     'bg-yellow-100'],
    ['PartiallyPaid', 'Al corriente',  'bg-blue-100'],
    ['FullyPaid',     'Pago completo', 'bg-teal-100'],
    ['Confirmed',     'Confirmada',    'bg-green-100'],
    ['Draft',         'En revisión',   'bg-orange-100'],
    ['Cancelled',     'Cancelada',     'bg-gray-100'],
  ] as [RegistrationStatus, string, string][])(
    'renders "%s" with label "%s" and class "%s"',
    (status, label, colorClass) => {
      const wrapper = mount(status)
      expect(wrapper.text()).toBe(label)
      expect(wrapper.find('[data-testid="registration-status"]').classes()).toContain(colorClass)
    }
  )

  it('Draft renders "En revisión" not "Borrador" (regression)', () => {
    const wrapper = mount('Draft')
    expect(wrapper.text()).toBe('En revisión')
    expect(wrapper.text()).not.toBe('Borrador')
  })
})

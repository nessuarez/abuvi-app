import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import RegistrationStatusTimeline from '../RegistrationStatusTimeline.vue'
import type { RegistrationStatusHistoryEntry } from '@/types/registration'

const makeEntry = (
  overrides: Partial<RegistrationStatusHistoryEntry> = {}
): RegistrationStatusHistoryEntry => ({
  id: 'entry-1',
  previousStatus: 'Pending',
  newStatus: 'PartiallyPaid',
  changedAt: '2026-03-01T10:00:00Z',
  changedByUserName: 'Admin User',
  trigger: 'AdminAction',
  notes: null,
  ...overrides
})

const mountComponent = (history: RegistrationStatusHistoryEntry[]) =>
  mount(RegistrationStatusTimeline, {
    props: { history },
    global: { plugins: [PrimeVue] }
  })

describe('RegistrationStatusTimeline', () => {
  it('renders nothing when history is empty', () => {
    const wrapper = mountComponent([])
    expect(wrapper.find('section').exists()).toBe(false)
  })

  it('renders the section heading when history has entries', () => {
    const wrapper = mountComponent([makeEntry()])
    expect(wrapper.find('section').exists()).toBe(true)
    expect(wrapper.text()).toContain('Historial de cambios')
  })

  it('shows the description for PartiallyPaid status', () => {
    const wrapper = mountComponent([makeEntry({ newStatus: 'PartiallyPaid' })])
    expect(wrapper.text()).toContain('Junta confirmó primer pago')
  })

  it('shows the description for Draft status', () => {
    const wrapper = mountComponent([makeEntry({ newStatus: 'Draft' })])
    expect(wrapper.text()).toContain('Junta realizó cambios')
  })

  it('shows the description for FullyPaid status', () => {
    const wrapper = mountComponent([makeEntry({ newStatus: 'FullyPaid' })])
    expect(wrapper.text()).toContain('Todos los pagos recibidos')
  })

  it('shows "Sistema" for Automatic trigger entries', () => {
    const wrapper = mountComponent([makeEntry({ trigger: 'Automatic', changedByUserName: 'System' })])
    expect(wrapper.text()).toContain('Sistema')
    expect(wrapper.text()).not.toContain('System')
  })

  it('shows changedByUserName for AdminAction trigger entries', () => {
    const wrapper = mountComponent([makeEntry({ trigger: 'AdminAction', changedByUserName: 'Nestor Suarez' })])
    expect(wrapper.text()).toContain('Nestor Suarez')
  })

  it('shows notes when present', () => {
    const wrapper = mountComponent([makeEntry({ notes: 'Pago confirmado por transferencia' })])
    expect(wrapper.text()).toContain('Pago confirmado por transferencia')
  })

  it('does not render notes paragraph when notes is null', () => {
    const wrapper = mountComponent([makeEntry({ notes: null })])
    expect(wrapper.text()).not.toContain('null')
  })
})
